#!/usr/bin/env python3
"""Works out par for the levels and writes it into levels/*.tres.

    python3 scripts/update_par.py               # every level campaign.tres offers
    python3 scripts/update_par.py apple bat     # just these
    python3 scripts/update_par.py --check       # say what would change, write nothing

Par is the golf target the move counter is measured against: the shortest solution
scripts/sortpaint/par.py can find, plus its allowance. The number that lands in the .tres is
the solution length (OptimalMoves); the game adds the allowance itself, so the two ideas stay
in one place each.

Every plan is replayed through the rules before its number is written, so a level ships a
target that has actually been played out. Re-run this after changing a level's picture, its
seed or its tray, since all three change what the shortest solution is.
"""

import argparse
import sys
from concurrent.futures import ProcessPoolExecutor
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from sortpaint import par
from sortpaint.level import (
    grid_from_png,
    read_campaign,
    read_level_tres,
    with_optimal_moves,
)
from sortpaint.rules import scramble

ROOT = Path(__file__).resolve().parent.parent
LEVELS = ROOT / "levels"
CAMPAIGN = LEVELS / "campaign.tres"


def solve_level(name):
    """The shortest solution found for a level, as (name, cells, moves). Raises if it is wrong."""
    meta = read_level_tres(LEVELS / f"{name}.tres")
    grid, _ = grid_from_png(LEVELS / f"{meta['sprite']}.png", meta["alpha_threshold"])
    spheres = scramble(grid, meta["seed"])

    plan = par.solve(grid, spheres, meta["tray_capacity"])
    if plan is None:
        raise ValueError(f"{name}: the search could not finish this level")

    moves = par.verify(grid, spheres, meta["tray_capacity"], plan)
    return name, grid.playable_count, moves, meta["optimal_moves"]


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("levels", nargs="*", help="level names; default is all of campaign.tres")
    parser.add_argument("--check", action="store_true", help="report differences and write nothing")
    args = parser.parse_args(argv)

    names = args.levels or read_campaign(CAMPAIGN)
    missing = [name for name in names if not (LEVELS / f"{name}.tres").exists()]
    if missing:
        parser.error(f"no such level(s): {', '.join(missing)}")

    changed = 0
    with ProcessPoolExecutor() as pool:
        for name, cells, moves, was in pool.map(solve_level, names):
            target = par.par_from(moves)
            note = "" if was == moves else f"  (was {was})" if was else "  (new)"
            print(f"{name:18}{cells:5} cells  {moves:4} moves, par {target:4}{note}")

            if was == moves:
                continue

            changed += 1
            if args.check:
                continue

            path = LEVELS / f"{name}.tres"
            path.write_text(with_optimal_moves(path.read_text(), moves))

    if args.check:
        print(f"{changed} of {len(names)} level(s) would change")
        return 1 if changed else 0

    print(f"wrote {changed} of {len(names)} level(s)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
