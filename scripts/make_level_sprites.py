#!/usr/bin/env python3
"""Renders the hand-authored level sprites in scripts/level_art.py.

One pixel is one cell. Every distinct opaque colour becomes a sphere colour, and
transparent pixels are holes in the picture. Run from the project root:

    python3 scripts/make_level_sprites.py                   # all of them, into levels/
    python3 scripts/make_level_sprites.py --out /tmp/art elephant giraffe
    python3 scripts/make_level_sprites.py --check           # report only, write nothing

Rendering into levels/ replaces the art a level is made of but leaves its .tres and its place
in campaign.tres alone, so a new picture is not a level until scripts/import_level.py has
played it through. Draw somewhere else with --out, import from there, and levels/ is only
touched once the checks pass.

Every sprite is reported with the closest pair of colours it uses, measured the way a player sees
them rather than the way a colour picker does (scripts/sortpaint/contrast.py). PALETTE is only as
safe as the combinations in use: two entries that no picture currently pairs are free to sit close
together, and drawing the first picture that pairs them is how that goes unnoticed. A sprite whose
closest pair falls under the threshold fails the run, same as one that cannot be dealt.

Keep tests/SortPaint.Tests/LevelSprites.cs in step with the art here; it is the
same pictures in lowercase, and it is what proves each one can still be finished.
"""

import argparse
import sys
from pathlib import Path

from PIL import Image

from level_art import PALETTE, SPRITES
from sortpaint import contrast

# A level whose largest colour covers more than half its cells cannot be dealt without leaving
# some cell already on its target, so the importer will refuse it. Reported here to save the trip.
HOLE = "."


def render(rows):
    width = len(rows[0])
    for y, row in enumerate(rows):
        if len(row) != width:
            raise ValueError(f"row {y} is {len(row)} wide, expected {width}")

    image = Image.new("RGBA", (width, len(rows)), (0, 0, 0, 0))
    pixels = image.load()

    for y, row in enumerate(rows):
        for x, key in enumerate(row):
            if key == HOLE:
                continue
            if key not in PALETTE:
                raise ValueError(f"unknown palette key {key!r} at ({x}, {y})")
            pixels[x, y] = PALETTE[key] + (255,)

    return image


def summarise(name, rows):
    """Prints the shape of a sprite, whether it can be dealt, and how close its colours sit.

    True when both hold. The contrast half is the one that catches a picture pairing two palette
    entries no other picture pairs: PALETTE is only as safe as the combinations in use.
    """
    counts = {}
    for row in rows:
        for key in row:
            if key != HOLE:
                counts[key] = counts.get(key, 0) + 1

    playable = sum(counts.values())
    largest = max(counts.values())
    # A fixed-point-free opening only exists when no colour covers more than half the sprite.
    dealt = largest * 2 <= playable
    verdict = "ok" if dealt else "SOME CELLS WILL START PAINTED"
    breakdown = ", ".join(f"{key}={n}" for key, n in sorted(counts.items()))

    keys = sorted(counts)
    closest = contrast.tightest([PALETTE[key] for key in keys])
    telling = ""
    readable = True
    if closest is not None:
        gap, first, second = closest
        pair = f"{keys[first]}/{keys[second]}"
        if gap < contrast.REFUSE:
            telling = f", {pair} TOO CLOSE TO TELL APART ({gap:.1f})"
            readable = False
        elif gap < contrast.CLOSE:
            telling = f", {pair} close ({gap:.1f})"
        else:
            telling = f", closest {pair} {gap:.1f}"

    print(
        f"{name}: {playable} cells, {len(counts)} colours ({breakdown}), "
        f"scramble {verdict}{telling}"
    )
    return dealt and readable


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("names", nargs="*", help="sprites to draw. Default: all of them")
    parser.add_argument("--out", type=Path, help="where to write the PNGs. Default: levels/")
    parser.add_argument(
        "--check",
        action="store_true",
        help="report every sprite and write nothing. Exits non-zero if any of them fails",
    )
    args = parser.parse_args(argv)

    if args.check and args.out:
        parser.error("--check writes nothing, so --out has nothing to do")

    out_dir = args.out or Path(__file__).resolve().parent.parent / "levels"
    if not args.check:
        out_dir.mkdir(parents=True, exist_ok=True)

    unknown = [name for name in args.names if name not in SPRITES]
    if unknown:
        parser.error(f"no sprite named {', '.join(unknown)}")

    wanted = args.names or list(SPRITES)
    ok = True

    for name in wanted:
        rows = SPRITES[name]
        if not args.check:
            render(rows).save(out_dir / f"{name}.png")
        ok &= summarise(name, rows)

    if args.check:
        print(f"{len(wanted)} sprite(s) checked, nothing written")
    else:
        print(f"{len(wanted)} sprite(s) into {out_dir}")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
