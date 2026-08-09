#!/usr/bin/env python3
"""Turns an image into a level, or refuses to.

    python3 scripts/import_level.py art/dog.png --size 16 --colors 6

Writes levels/dog.png, levels/dog.tres, and adds the level to levels/campaign.tres, but only
after playing the thing through. A picture that cannot be scrambled clean, or that a stand-in
player cannot finish with the tray it is given, is reported and nothing is written.

The rules used to decide that are ports, in scripts/sortpaint/. They agree with the C# down to
the shuffle: see scripts/scramble_parity.json and tests/SortPaint.Tests/ScrambleParityTests.cs.

Run with --write-parity on its own to regenerate that fixture after changing a level.

This does not touch tests/SortPaint.Tests/LevelSprites.cs. It prints the block to paste there,
and until you do, the level is not covered by dotnet test.
"""

import argparse
import hashlib
import json
import re
import shutil
import subprocess
import sys
from pathlib import Path

from sortpaint.level import (
    BASE_SEED,
    DEFAULT_ALPHA_THRESHOLD,
    DEFAULT_TRAY_CAPACITY,
    as_ascii,
    campaign_with,
    grid_from_pixels,
    grid_from_png,
    level_tres,
    pixels_of,
    read_campaign,
    read_level_tres,
)
from sortpaint.rules import BoardState, can_fully_scramble, greedy_solve, scramble

ROOT = Path(__file__).resolve().parent.parent
LEVELS = ROOT / "levels"
CAMPAIGN = LEVELS / "campaign.tres"
PARITY = ROOT / "scripts" / "scramble_parity.json"

# ShippedLevelsTests: a picture worth showing is bigger than the tray and has 2 to 8 colours.
MIN_COLORS = 2
MAX_COLORS = 8


class Refused(Exception):
    """A check failed. The message says which, and what would help."""


# --------------------------------------------------------------------------- image


def prepare(source, size, colors, alpha_threshold):
    """The source image as a level-sized, level-paletted RGBA image. Deterministic throughout."""
    from PIL import Image

    with Image.open(source) as handle:
        image = handle.convert("RGBA")

    alpha = image.getchannel("A").point(lambda a: 255 if a / 255.0 >= alpha_threshold else 0)
    rgb = image.convert("RGB")

    if size is not None:
        width, height = size
        # A box filter averages the block, so a cell takes after the area it covers rather than
        # whichever single pixel happened to land under the sample point.
        rgb = _neutralise_holes(rgb, alpha).resize((width, height), Image.Resampling.BOX)
        # Same filter on the mask, then back to hard edges: a cell that was mostly solid is solid.
        alpha = alpha.resize((width, height), Image.Resampling.BOX).point(lambda a: 255 if a >= 128 else 0)

    if colors is not None:
        rgb = _quantise(rgb, alpha, colors)

    out = rgb.convert("RGBA")
    out.putalpha(alpha)
    return out


def _neutralise_holes(rgb, alpha):
    """Paint transparent pixels the average of the solid ones, so downscaling has nothing to bleed.

    Without this a box filter drags whatever happened to sit behind the transparency (usually
    black) into every cell along the silhouette, and the level gains a dark fringe nobody drew.
    """
    from PIL import Image

    solid = [px for px, a in zip(pixels_of(rgb), pixels_of(alpha)) if a]
    if not solid or len(solid) == rgb.width * rgb.height:
        return rgb

    n = len(solid)
    mean = tuple(sum(channel) // n for channel in zip(*solid))
    filled = Image.new("RGB", rgb.size, mean)
    filled.paste(rgb, mask=alpha)
    return filled


def _quantise(rgb, alpha, colors):
    """Median-cut down to `colors`, choosing the palette from the solid pixels alone.

    Quantising the whole image would let a large transparent area, which the game never draws,
    win a palette slot from the picture. So the palette is picked from a strip holding only the
    pixels that will be cells, then applied to everything.
    """
    from PIL import Image

    solid = [px for px, a in zip(pixels_of(rgb), pixels_of(alpha)) if a]
    if not solid:
        raise Refused("every pixel is transparent, so there is no picture to play")

    strip = Image.new("RGB", (len(solid), 1))
    strip.putdata(solid)
    palette = strip.quantize(colors=colors, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)

    # Dithering would scatter single-pixel colour noise, which at this scale is just noise.
    return rgb.quantize(palette=palette, dither=Image.Dither.NONE).convert("RGB")


# --------------------------------------------------------------------------- checks


def check(grid, palette):
    """The seed-independent half of ShippedLevelsTests. Raises Refused with a way forward."""
    if grid.playable_count <= DEFAULT_TRAY_CAPACITY:
        raise Refused(
            f"only {grid.playable_count} playable cells, which is not more than the tray holds "
            f"({DEFAULT_TRAY_CAPACITY}). Use a larger --size, or an image with less transparency."
        )

    if len(palette) < MIN_COLORS:
        raise Refused(
            f"{len(palette)} colour(s), so there is nothing to sort. Raise --colors, or pick an "
            "image with more contrast."
        )

    if len(palette) > MAX_COLORS:
        raise Refused(
            f"{len(palette)} colours, more than the {MAX_COLORS} a level may have. "
            f"Pass --colors {MAX_COLORS} or fewer."
        )

    if not can_fully_scramble(grid):
        counts = grid.color_counts()
        worst = max(counts, key=counts.get)
        share = counts[worst] / grid.playable_count
        raise Refused(
            f"colour {worst} covers {counts[worst]} of {grid.playable_count} cells ({share:.0%}); "
            "no colour may cover more than half, or some cells start already painted. Try a "
            "higher --colors to split the dominant area, or crop the image tighter to the subject."
        )


def playable(grid, seed, tray_capacity):
    """Deals the level at `seed` and plays it through. True when a stand-in player finishes it."""
    spheres = scramble(grid, seed)
    if any(spheres[cell] == grid.target_at(cell) for cell in grid.playable_cells()):
        return False

    state = BoardState(grid, spheres, tray_capacity)
    return greedy_solve(state) and state.tray.is_empty


def find_seed(grid, first, tries, tray_capacity):
    """The first seed at or after `first` that yields a finishable level, or None."""
    for seed in range(first, first + tries):
        if playable(grid, seed, tray_capacity):
            return seed
    return None


# --------------------------------------------------------------------------- parity fixture


def write_parity():
    """Regenerates scripts/scramble_parity.json from whatever campaign.tres currently offers."""
    entries = []
    for name in read_campaign(CAMPAIGN):
        meta = read_level_tres(LEVELS / f"{name}.tres")
        grid, _ = grid_from_png(LEVELS / f"{meta['sprite']}.png", meta["alpha_threshold"])
        spheres = scramble(grid, meta["seed"])
        entries.append(
            {
                "name": name,
                "seed": meta["seed"],
                "rows": as_ascii(grid),
                "scramble": hashlib.sha256(",".join(map(str, spheres)).encode()).hexdigest(),
            }
        )

    PARITY.write_text(
        "// Written by scripts/import_level.py --write-parity. Do not edit by hand.\n"
        + json.dumps(entries, indent=2)
        + "\n"
    )
    return entries


# --------------------------------------------------------------------------- reporting


def report_fixture(name, grid, seed):
    """The block to paste into tests/SortPaint.Tests/LevelSprites.cs."""
    symbol = "".join(part.capitalize() for part in re.split(r"[^a-z0-9]+", name) if part)
    rows = "\n".join(f'        "{row}",' for row in as_ascii(grid))

    print()
    print(f"{name} is not covered by dotnet test until it is in LevelSprites.cs. Paste this in:")
    print()
    print(f"    public static readonly string[] {symbol} =")
    print("    [")
    print(rows)
    print("    ];")
    print()
    print("and add to Shipped():")
    print()
    print(f'        yield return ["{name}", {symbol}, {seed}];')
    print()
    print("then re-run: python3 scripts/import_level.py --write-parity")


# --------------------------------------------------------------------------- entry point


def slug(text):
    return re.sub(r"[^a-z0-9_]+", "_", text.lower()).strip("_")


def parse_size(text):
    match = re.fullmatch(r"(\d+)(?:x(\d+))?", text)
    if not match:
        raise argparse.ArgumentTypeError("size must be N or WxH, for example 16 or 20x16")
    width = int(match.group(1))
    return width, int(match.group(2) or width)


def build_parser():
    parser = argparse.ArgumentParser(
        description="Turn an image into a Sort Painting level, if it makes a playable one.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="Run --write-parity on its own to refresh scripts/scramble_parity.json.",
    )
    parser.add_argument("image", nargs="?", type=Path, help="source image; any format Pillow reads")
    parser.add_argument("--name", help="level name, used for the filenames. Default: the image's")
    parser.add_argument("--display-name", help="name shown on the menu. Default: the level name, titled")
    parser.add_argument("--size", type=parse_size, help="resize to N or WxH cells, for example 16")
    parser.add_argument("--colors", type=int, metavar="N", help=f"quantise to N colours, {MIN_COLORS}-{MAX_COLORS}")
    parser.add_argument("--alpha-threshold", type=float, default=DEFAULT_ALPHA_THRESHOLD,
                        help="pixels fainter than this are holes (default: %(default)s)")
    parser.add_argument("--seed", type=int, help="pin the shuffle seed instead of searching for one")
    parser.add_argument("--seed-tries", type=int, default=32, metavar="N",
                        help="seeds to try before giving up (default: %(default)s)")
    parser.add_argument("--tray", type=int, default=DEFAULT_TRAY_CAPACITY, metavar="N",
                        help="tray capacity (default: %(default)s)")
    parser.add_argument("--no-register", action="store_true", help="do not add the level to campaign.tres")
    parser.add_argument("--godot-import", action="store_true", help="run godot --headless --import afterwards")
    parser.add_argument("--dry-run", action="store_true", help="run every check but write nothing")
    parser.add_argument("--write-parity", action="store_true", help="refresh scripts/scramble_parity.json and exit")
    return parser


def main(argv=None):
    parser = build_parser()
    args = parser.parse_args(argv)

    if args.write_parity and args.image is None:
        entries = write_parity()
        print(f"wrote {PARITY.relative_to(ROOT)} for {len(entries)} level(s)")
        return 0

    if args.image is None:
        parser.error("an image is required (or --write-parity on its own)")

    if args.colors is not None and not MIN_COLORS <= args.colors <= MAX_COLORS:
        parser.error(f"--colors must be between {MIN_COLORS} and {MAX_COLORS}")

    name = slug(args.name or args.image.stem)
    if not name:
        parser.error(f"cannot make a level name out of {args.image.name}; pass --name")

    display_name = args.display_name or name.replace("_", " ").title()
    existing = LEVELS / f"{name}.tres"
    registered = read_campaign(CAMPAIGN)

    try:
        image = prepare(args.image, args.size, args.colors, args.alpha_threshold)
        grid, palette = grid_from_pixels(
            image.width, image.height, pixels_of(image), args.alpha_threshold
        )
        check(grid, palette)

        if args.seed is not None:
            seed = args.seed if playable(grid, args.seed, args.tray) else None
            if seed is None:
                raise Refused(
                    f"seed {args.seed} does not give a finishable level. Drop --seed to search, "
                    "or change the picture."
                )
        else:
            # Re-importing keeps the seed it already had, so a repeat run changes nothing.
            first = read_level_tres(existing)["seed"] if existing.exists() else BASE_SEED + len(registered)
            seed = find_seed(grid, first, args.seed_tries, args.tray)
            if seed is None:
                raise Refused(
                    f"no finishable level from {args.seed_tries} seeds starting at {first}. The "
                    "picture is probably too fragmented; try a lower --colors, or a bolder image."
                )
    except Refused as refusal:
        print(f"{args.image}: {refusal}", file=sys.stderr)
        return 1

    print(f"{name}: {grid.width}x{grid.height}, {grid.playable_count} cells, {len(palette)} colours")
    print(f"  scrambles clean and solves with a {args.tray}-sphere tray at seed {seed}")

    if args.dry_run:
        print("  --dry-run, so nothing written")
        report_fixture(name, grid, seed)
        return 0

    png = LEVELS / f"{name}.png"
    image.save(png)
    existing.write_text(
        level_tres(name, display_name, seed, args.tray, args.alpha_threshold)
    )
    print(f"  wrote {png.relative_to(ROOT)} and {existing.relative_to(ROOT)}")

    if not args.no_register:
        updated = campaign_with(CAMPAIGN, name)
        if updated == CAMPAIGN.read_text():
            print(f"  already listed in {CAMPAIGN.relative_to(ROOT)}")
        else:
            CAMPAIGN.write_text(updated)
            print(f"  added to {CAMPAIGN.relative_to(ROOT)}")

    if args.godot_import:
        godot = shutil.which("godot")
        if godot is None:
            print("  ! no godot on PATH, so the PNG is not imported yet", file=sys.stderr)
        else:
            subprocess.run([godot, "--headless", "--path", str(ROOT), "--import"], check=False)
    else:
        print("  next: godot --headless --path . --import  (Godot writes the .import file)")

    report_fixture(name, grid, seed)
    return 0


if __name__ == "__main__":
    sys.exit(main())
