#!/usr/bin/env python3
"""Renders the hand-authored level sprites in scripts/level_art.py.

One pixel is one cell. Every distinct opaque colour becomes a sphere colour, and
transparent pixels are holes in the picture. Run from the project root:

    python3 scripts/make_level_sprites.py                   # all of them, into levels/
    python3 scripts/make_level_sprites.py --out /tmp/art elephant giraffe

Rendering into levels/ replaces the art a level is made of but leaves its .tres and its place
in campaign.tres alone, so a new picture is not a level until scripts/import_level.py has
played it through. Draw somewhere else with --out, import from there, and levels/ is only
touched once the checks pass.

Keep tests/SortPaint.Tests/LevelSprites.cs in step with the art here; it is the
same pictures in lowercase, and it is what proves each one can still be finished.
"""

import argparse
import sys
from pathlib import Path

from PIL import Image

from level_art import PALETTE, SPRITES

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
    """Prints the shape of a sprite and whether it can be dealt. True when it can."""
    counts = {}
    for row in rows:
        for key in row:
            if key != HOLE:
                counts[key] = counts.get(key, 0) + 1

    playable = sum(counts.values())
    largest = max(counts.values())
    # A fixed-point-free opening only exists when no colour covers more than half the sprite.
    ok = largest * 2 <= playable
    verdict = "ok" if ok else "SOME CELLS WILL START PAINTED"
    breakdown = ", ".join(f"{key}={n}" for key, n in sorted(counts.items()))
    print(f"{name}: {playable} cells, {len(counts)} colours ({breakdown}), scramble {verdict}")
    return ok


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("names", nargs="*", help="sprites to draw. Default: all of them")
    parser.add_argument("--out", type=Path, help="where to write the PNGs. Default: levels/")
    args = parser.parse_args(argv)

    out_dir = args.out or Path(__file__).resolve().parent.parent / "levels"
    out_dir.mkdir(parents=True, exist_ok=True)

    unknown = [name for name in args.names if name not in SPRITES]
    if unknown:
        parser.error(f"no sprite named {', '.join(unknown)}")

    wanted = args.names or list(SPRITES)
    dealt = True

    for name in wanted:
        rows = SPRITES[name]
        render(rows).save(out_dir / f"{name}.png")
        dealt &= summarise(name, rows)

    print(f"{len(wanted)} sprite(s) into {out_dir}")
    return 0 if dealt else 1


if __name__ == "__main__":
    sys.exit(main())
