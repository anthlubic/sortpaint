"""How few moves a level can be finished in, which is what par is measured against.

Finding the true minimum means searching every order the runs could be moved in, which is far
too large a space for a picture of a few hundred cells. What this does instead is play the level
out once, and at every turn try each move worth trying and keep the one that leaves the board in
the best shape. The number it comes back with is the shortest solution it found, so it is an
upper bound on the minimum, and on the shipped levels a close one.

The shape of the board is what the choice is really about. Two things decide what a move is
worth:

* A move carries min(run, pocket) spheres, so the big wins come from a long run of one colour
  landing in a wide pocket of cells waiting for it.
* Runs only ever break apart. A sphere is never dropped anywhere but its own colour, so two runs
  never join, and every sphere left stranded on its own is a move that can only carry one.
  Keeping runs whole is worth more than painting a few extra cells now.

There are exactly as many bare cells as there are spheres in the tray, so the tray is also the
size of the pockets to aim at, which is why holding spheres back is rewarded rather than
punished. The weights below were fitted against the shipped levels.

This runs at authoring time, not in the game: scripts/update_par.py writes the answer into each
level's .tres as OptimalMoves, and the game only ever reads that number. `verify` replays a plan
through rules.BoardState, the port the parity fixture pins to the C#, so a number that ships has
been played out under the real rules.
"""

import math

from .rules import BARE, EMPTY, NEIGHBOURS, BoardState

TO_TRAY = "to_tray"
"""A run lifted off the picture and dropped in the tray."""

FROM_TRAY = "from_tray"
"""Spheres taken back out of the tray and dropped on the picture."""

ACROSS = "across"
"""A run carried straight from where it sat to where it belongs."""

PAR_ALLOWANCE = 0.40
"""The golf part: how much room over the best known solution a round is given."""

# How many places on a run or a pocket to try tapping when only part of it moves.
TAPS_PER_GROUP = 3

# Runs of one colour worth offering as a source, longest first.
RUNS_PER_COLOR = 2

# Pockets worth offering as a destination, widest first.
POCKETS_CONSIDERED = 10

# Positions a layer of the search carries forward. A width of one is a plain greedy playthrough,
# and every width is tried: see `solve`.
BEAM_WIDTHS = (1, 4, 16)

# What a move is judged on, beyond the cells it paints. The run count dominates, which is the
# search saying that breaking a run up costs about three and a half painted cells.
NEXT_PAINT_WEIGHT = 0.5
TRAY_WEIGHT = 0.8
RUN_COUNT_WEIGHT = -3.5
STRANDED_WEIGHT = -0.3


def par_from(best_moves, allowance=PAR_ALLOWANCE):
    """Par: the best known solution plus its allowance, rounded up."""
    if best_moves <= 0:
        return 0
    return math.ceil(best_moves * (1.0 + allowance))


class Move:
    """One move of a plan, as the two taps that play it: lift, then drop."""

    __slots__ = ("kind", "color", "lift", "drop")

    def __init__(self, kind, color, lift, drop):
        self.kind = kind
        self.color = color
        self.lift = lift  # cell tapped to lift, or None out of the tray
        self.drop = drop  # cell tapped to drop on, or None into the tray

    def __repr__(self):
        return f"Move({self.kind}, colour {self.color}, {self.lift} -> {self.drop})"


def solve(grid, spheres, tray_capacity, widths=BEAM_WIDTHS):
    """Plays the level out at each search width. Returns the shortest plan, or None.

    A wider search usually finds a shorter solution, but not always. Which boards a layer
    carries forward is decided by a guess at what a board is worth, and a wide layer can crowd
    out the line a narrow one would have stayed on. Running every width costs little next to
    getting a level's par wrong, so the shortest of them wins.
    """
    best = None
    for width in widths:
        plan = _Search(grid, spheres, tray_capacity, width).run()
        if plan is not None and (best is None or len(plan) < len(best)):
            best = plan
    return best


def best_moves(grid, spheres, tray_capacity, widths=BEAM_WIDTHS):
    """The move count of the best solution found, or None."""
    plan = solve(grid, spheres, tray_capacity, widths)
    return None if plan is None else len(plan)


def verify(grid, spheres, tray_capacity, plan):
    """Replays a plan through the rules themselves. Returns the moves it took, or raises.

    Every step is resolved the way the game resolves it, from the cell the plan says to tap, so
    a plan that only works under the search's own idea of the rules is caught here.
    """
    state = BoardState(grid, spheres, tray_capacity)

    for number, move in enumerate(plan, start=1):
        if move.kind == TO_TRAY:
            # Interaction: a tap lifts the run, a tap on the rail drops it in, and the tray's own
            # rules cut the run down to the slots that are left.
            tap = state.resolve(move.lift)
            if tap.kind is None or tap.color != move.color:
                raise ValueError(f"move {number} lifts nothing from cell {move.lift}")
            state.apply(tap)

        elif move.kind == FROM_TRAY:
            tap = state.resolve(move.drop)
            if tap.kind is None or tap.color != move.color:
                raise ValueError(f"move {number} places nothing on cell {move.drop}")
            state.apply(tap)

        elif move.kind == ACROSS:
            run = state.lift_region(move.lift)
            landing = state.bare_region(move.drop)
            carried = min(len(run), len(landing))
            if carried == 0 or state.spheres[move.lift] != move.color:
                raise ValueError(f"move {number} carries nothing from {move.lift} to {move.drop}")
            state.move_on_board(move.color, run[:carried], landing[:carried])

        else:
            raise ValueError(f"move {number} has unknown kind {move.kind!r}")

    if not state.is_solved:
        raise ValueError(f"plan ends at {state.painted_count}/{grid.playable_count} painted")
    if not state.tray.is_empty:
        raise ValueError(f"plan ends with {state.tray.count} spheres left in the tray")

    return len(plan)


class _Position:
    """A board partway through a solution, and the move that got there.

    The plan is held as a chain back to the opening position rather than a list per board, so
    widening the search costs no more copying.
    """

    __slots__ = ("spheres", "tray", "painted", "parent", "move", "board")

    def __init__(self, spheres, tray, painted, parent, move):
        self.spheres = spheres
        self.tray = tray
        self.painted = painted
        self.parent = parent
        self.move = move
        self.board = None

    def plan(self):
        moves = []
        position = self
        while position.move is not None:
            moves.append(position.move)
            position = position.parent
        moves.reverse()
        return moves


class _Search:
    """A beam search over positions.

    Every board in a layer is the same number of moves in, so the first solved board found is the
    shortest solution the search will see. What a layer carries forward is the `width` boards left
    in the best shape, by `_score`.
    """

    def __init__(self, grid, spheres, tray_capacity, width):
        self.grid = grid
        self.cells = grid.cell_count
        self.capacity = tray_capacity
        self.width = max(1, width)
        self.targets = list(grid.targets)
        self.neighbours = _neighbour_table(grid)
        self.colors = max(self.targets) + 1 if self.targets else 1

        opening = list(spheres)
        painted = sum(
            1 for cell, sphere in enumerate(opening)
            if sphere != BARE and sphere == self.targets[cell]
        )
        self.root = _Position(opening, [0] * self.colors, painted, None, None)

    def run(self):
        # Every move either paints a cell or fills a tray slot, and the tray is small, so a
        # solution is never anywhere near this long. The cap is only here so a level the search
        # cannot finish gives up rather than spinning.
        limit = self.grid.playable_count * 2 + 64
        goal = self.grid.playable_count
        layer = [self.root]

        for _ in range(limit):
            scored = []
            seen = set()

            for position in layer:
                board = self._board(position)
                moves = self._candidates(position, board, POCKETS_CONSIDERED)
                if not moves:
                    # The cap on pockets can hide the only move there is, so widen and look again.
                    moves = self._candidates(position, board, None)

                for move in moves:
                    child = self._step(position, board, move)
                    if child.painted == goal:
                        return child.plan()

                    # The spheres on the board say what is in the tray as well, since between
                    # them they hold every sphere the picture needs.
                    key = tuple(child.spheres)
                    if key in seen:
                        continue
                    seen.add(key)
                    scored.append((self._score(child), child))

            if not scored:
                return None

            scored.sort(key=lambda entry: entry[0], reverse=True)
            layer = [child for _, child in scored[: self.width]]

        return None

    # ----------------------------------------------------------------- the board, in groups

    def _board(self, position):
        """The runs and pockets of a position, worked out once and kept with it."""
        if position.board is None:
            run_classes, runs = self._scan(position.spheres, sources=True)
            pocket_classes, pockets = self._scan(position.spheres, sources=False)
            position.board = (run_classes, runs, pocket_classes, pockets)
        return position.board

    def _scan(self, spheres, sources):
        """Connected groups: the runs of unpainted spheres, or the pockets of bare cells.

        Returns (classes, groups), where classes[cell] is what the cell counts as for this scan
        (None to sit it out) and each group is (class, members) with members in the order a
        flood from the group's first cell would find them.
        """
        targets = self.targets
        if sources:
            classes = [
                sphere if sphere != BARE and sphere != target else None
                for sphere, target in zip(spheres, targets)
            ]
        else:
            classes = [
                target if target != EMPTY and sphere == BARE else None
                for sphere, target in zip(spheres, targets)
            ]

        neighbours = self.neighbours
        seen = bytearray(self.cells)
        groups = []

        for seed in range(self.cells):
            wanted = classes[seed]
            if wanted is None or seen[seed]:
                continue

            seen[seed] = 1
            members = [seed]

            # Appending while iterating is the queue: the list comes out breadth-first.
            for cell in members:
                for neighbour in neighbours[cell]:
                    if seen[neighbour] or classes[neighbour] != wanted:
                        continue
                    seen[neighbour] = 1
                    members.append(neighbour)

            groups.append((wanted, members))

        return classes, groups

    def _walk(self, classes, tap):
        """Flood.Region: the group around `tap`, in the order it comes back to the finger."""
        wanted = classes[tap]
        neighbours = self.neighbours
        seen = {tap}
        order = [tap]

        for cell in order:
            for neighbour in neighbours[cell]:
                if neighbour in seen:
                    continue
                seen.add(neighbour)
                if classes[neighbour] == wanted:
                    order.append(neighbour)

        return order

    # ----------------------------------------------------------------- the moves worth trying

    def _candidates(self, position, board, pocket_limit):
        """Every move worth weighing up from a position.

        Only the side of a move that gets cut short is worth trying different taps on: when a
        whole run fits the pocket it lands in, where the finger goes makes no difference.
        """
        _, runs, _, pockets = board
        moves = []

        longest = self._longest_runs(runs)
        held_total = sum(position.tray)

        if held_total < self.capacity:
            free = self.capacity - held_total
            for color, color_runs in longest.items():
                for _, members in color_runs:
                    for tap in self._taps(members, trimmed=len(members) > free):
                        moves.append(Move(TO_TRAY, color, tap, None))

        widest = sorted(pockets, key=lambda group: -len(group[1]))
        if pocket_limit is not None:
            widest = widest[:pocket_limit]

        for color, cells in widest:
            width = len(cells)

            held = position.tray[color]
            if held > 0:
                for tap in self._taps(cells, trimmed=held < width):
                    moves.append(Move(FROM_TRAY, color, None, tap))

            for _, members in longest.get(color, ()):
                length = len(members)
                if length > width:
                    # The pocket fills either way, so what matters is which end of the run goes.
                    for tap in self._taps(members, trimmed=True):
                        moves.append(Move(ACROSS, color, tap, cells[0]))
                else:
                    # The whole run goes, so what matters is which corner of the pocket it lands in.
                    for tap in self._taps(cells, trimmed=length < width):
                        moves.append(Move(ACROSS, color, members[0], tap))

        return moves

    def _longest_runs(self, runs):
        """The longest few runs of each colour, which is as many as are worth offering."""
        by_color = {}
        for color, members in runs:
            by_color.setdefault(color, []).append((len(members), members))

        for color, found in by_color.items():
            found.sort(key=lambda entry: -entry[0])
            by_color[color] = found[:RUNS_PER_COLOR]

        return by_color

    def _taps(self, members, trimmed):
        """Where to tap a group.

        Cells furthest from the middle come first, because taking cells from an edge leaves what
        is left in one piece, where taking them from the middle would cut the group in two. A
        group that moves whole only needs one tap, and any of them will do.
        """
        if not trimmed or len(members) == 1:
            return members[:1]

        grid = self.grid
        center_x = sum(grid.x_of(cell) for cell in members) / len(members)
        center_y = sum(grid.y_of(cell) for cell in members) / len(members)

        def reach(cell):
            return (grid.x_of(cell) - center_x) ** 2 + (grid.y_of(cell) - center_y) ** 2

        return sorted(members, key=reach, reverse=True)[:TAPS_PER_GROUP]

    # ----------------------------------------------------------------- weighing one up

    def _step(self, position, board, move):
        """The position a move leads to."""
        spheres = list(position.spheres)
        tray = list(position.tray)
        painted = position.painted + self._play(board, move, spheres, tray)
        return _Position(spheres, tray, painted, position, move)

    def _score(self, position):
        """Marks a position on the shape it is in, not just the cells painted so far."""
        _, runs, _, pockets = self._board(position)
        tray = position.tray

        longest = {}
        for color, members in runs:
            if len(members) > longest.get(color, 0):
                longest[color] = len(members)

        following = 0
        stranded = 0
        for color, cells in pockets:
            width = len(cells)
            following = max(following, min(longest.get(color, 0), width), min(tray[color], width))
            if width == 1:
                stranded += 1

        return (
            position.painted
            + NEXT_PAINT_WEIGHT * following
            + TRAY_WEIGHT * sum(tray)
            + RUN_COUNT_WEIGHT * len(runs)
            + STRANDED_WEIGHT * stranded
        )

    def _play(self, board, move, spheres, tray):
        """The rules themselves, played onto the arrays handed in. Returns the cells painted.

        Runs and pockets are walked from the tapped cell, in the same order a flood would, so a
        move that only part of a group fits takes exactly the cells the game would take.
        """
        run_classes, _, pocket_classes, _ = board

        if move.kind == TO_TRAY:
            run = self._walk(run_classes, move.lift)
            carried = min(len(run), self.capacity - sum(tray))
            for cell in run[:carried]:
                spheres[cell] = BARE
            tray[move.color] += carried
            return 0

        if move.kind == FROM_TRAY:
            landing = self._walk(pocket_classes, move.drop)
            carried = min(tray[move.color], len(landing))
            for cell in landing[:carried]:
                spheres[cell] = move.color
            tray[move.color] -= carried
            return carried

        run = self._walk(run_classes, move.lift)
        landing = self._walk(pocket_classes, move.drop)
        carried = min(len(run), len(landing))
        for cell in run[:carried]:
            spheres[cell] = BARE
        for cell in landing[:carried]:
            spheres[cell] = move.color
        return carried


def _neighbour_table(grid):
    """The eight surrounding cells of every cell, worked out once."""
    table = []
    for cell in range(grid.cell_count):
        x, y = grid.x_of(cell), grid.y_of(cell)
        table.append(
            tuple(
                grid.index(x + dx, y + dy)
                for dx, dy in NEIGHBOURS
                if grid.in_bounds(x + dx, y + dy)
            )
        )
    return table
