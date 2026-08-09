"""The game's rules, in Python, so tooling can play a level before it ships.

A line-by-line port of src/Core/{LevelGrid,Flood,Tray,BoardState,Scrambler}.cs, plus
src/LevelLoader.cs and the stand-in player from tests/SortPaint.Tests/GreedyPlayer.cs.
The C# is the original; this exists only so scripts/import_level.py can prove a picture is
finishable without a Godot runtime or a build step.

Two details decide whether this port agrees with the game, and both are load-bearing:

* Colours are numbered in the order they are first met, reading left to right and top to
  bottom, keyed on RGB alone. LevelLoader does that, and LevelSprites.Grid in the test
  project copies it for the same reason.
* Scramble draws from DotNetRandom in the exact order the LINQ in Scrambler.Scramble does.
  See the comments in `scramble`: the order is a consequence of how OrderBy evaluates its
  keys, not of anything the C# says out loud.
"""

from collections import OrderedDict, deque

from .dotnet_random import DotNetRandom

EMPTY = -1
"""LevelGrid.Empty, a hole in the picture."""

BARE = -1
"""BoardState.Bare, a playable cell with no sphere on it."""


class LevelGrid:
    """The target picture: the colour index every cell has to end up showing."""

    def __init__(self, width, height, targets):
        if width <= 0 or height <= 0:
            raise ValueError(f"grid is {width}x{height}")
        if len(targets) != width * height:
            raise ValueError(f"expected {width * height} targets, got {len(targets)}")

        self.width = width
        self.height = height
        self.targets = list(targets)
        self.playable_count = sum(1 for t in self.targets if t != EMPTY)

    @property
    def cell_count(self):
        return self.width * self.height

    def index(self, x, y):
        return y * self.width + x

    def x_of(self, index):
        return index % self.width

    def y_of(self, index):
        return index // self.width

    def in_bounds(self, x, y):
        return 0 <= x < self.width and 0 <= y < self.height

    def target_at(self, index):
        return self.targets[index]

    def is_playable(self, index):
        return self.targets[index] != EMPTY

    def playable_cells(self):
        """Indices of every cell that is part of the sprite, in reading order."""
        return [i for i, t in enumerate(self.targets) if t != EMPTY]

    def color_counts(self):
        """Cells per colour, keyed by palette index."""
        counts = {}
        for target in self.targets:
            if target != EMPTY:
                counts[target] = counts.get(target, 0) + 1
        return counts


# Offsets of the eight surrounding cells, orthogonals first then diagonals. The order sets the
# breadth-first order a region comes back in, which decides which cells survive a trim.
NEIGHBOURS = ((-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (1, -1), (-1, 1), (1, 1))


def flood_region(grid, start, include):
    """The connected run reachable from `start`, breadth-first, cells failing `include` as walls."""
    region = []
    if start < 0 or start >= grid.cell_count or not include(start):
        return region

    visited = [False] * grid.cell_count
    visited[start] = True
    queue = deque([start])

    while queue:
        index = queue.popleft()
        region.append(index)

        x = grid.x_of(index)
        y = grid.y_of(index)

        for dx, dy in NEIGHBOURS:
            nx, ny = x + dx, y + dy
            if not grid.in_bounds(nx, ny):
                continue
            neighbour = grid.index(nx, ny)
            if visited[neighbour]:
                continue
            visited[neighbour] = True
            if include(neighbour):
                queue.append(neighbour)

    return region


class Tray:
    """The holding area: a fixed number of spheres in total, of any mix of colours."""

    def __init__(self, capacity):
        if capacity <= 0:
            raise ValueError(f"tray capacity {capacity} must be positive")
        self.capacity = capacity
        self._counts = {}
        self.count = 0

    @property
    def free_slots(self):
        return self.capacity - self.count

    @property
    def is_full(self):
        return self.count >= self.capacity

    @property
    def is_empty(self):
        return self.count == 0

    def count_of(self, color):
        return self._counts.get(color, 0)

    def add(self, color, amount):
        if amount > self.free_slots:
            raise ValueError(f"adding {amount} would overflow the tray ({self.count}/{self.capacity})")
        self._counts[color] = self.count_of(color) + amount
        self.count += amount

    def remove(self, color, amount):
        held = self.count_of(color)
        if amount > held:
            raise ValueError(f"tray holds {held} of colour {color}, cannot remove {amount}")
        self._counts[color] = held - amount
        self.count -= amount


PICKUP = "pickup"
PLACE = "place"


class Tap:
    """What a tap resolves to, before it is applied. `kind` is None when nothing happens."""

    __slots__ = ("kind", "color", "cells")

    def __init__(self, kind, color, cells):
        self.kind = kind
        self.color = color
        self.cells = cells

    @property
    def count(self):
        return len(self.cells)


NO_TAP = Tap(None, EMPTY, ())


class BoardState:
    """Which sphere sits on each cell, plus the tray. The whole playable state of one level."""

    def __init__(self, grid, spheres, tray_capacity):
        if len(spheres) != grid.cell_count:
            raise ValueError(f"expected {grid.cell_count} sphere entries, got {len(spheres)}")

        self.grid = grid
        self.tray = Tray(tray_capacity)
        self.spheres = list(spheres)
        self.painted_count = 0

        for i, sphere in enumerate(self.spheres):
            if not grid.is_playable(i) and sphere != BARE:
                raise ValueError(f"cell {i} is not part of the sprite but carries a sphere")
            if sphere != BARE and sphere == grid.target_at(i):
                self.painted_count += 1

    @property
    def is_solved(self):
        return self.painted_count == self.grid.playable_count

    def is_painted(self, index):
        return self.spheres[index] != BARE and self.spheres[index] == self.grid.target_at(index)

    def resolve(self, index):
        if index < 0 or index >= self.grid.cell_count:
            return NO_TAP

        target = self.grid.target_at(index)
        if target == EMPTY:
            return NO_TAP

        sphere = self.spheres[index]
        if sphere == BARE:
            return self._resolve_place(index, target)
        return self._resolve_pickup(index, sphere, target)

    def _resolve_pickup(self, index, sphere, target):
        if sphere == target or self.tray.is_full:
            return NO_TAP

        # Painted spheres are excluded, which also makes them walls the fill cannot cross.
        region = flood_region(
            self.grid, index, lambda cell: self.spheres[cell] == sphere and not self.is_painted(cell)
        )
        return Tap(PICKUP, sphere, region[: self.tray.free_slots])

    def _resolve_place(self, index, target):
        held = self.tray.count_of(target)
        if held == 0:
            return NO_TAP

        region = flood_region(
            self.grid, index, lambda cell: self.spheres[cell] == BARE and self.grid.target_at(cell) == target
        )
        return Tap(PLACE, target, region[:held])

    def apply(self, tap):
        if tap.kind == PICKUP:
            for cell in tap.cells:
                self.spheres[cell] = BARE
            self.tray.add(tap.color, tap.count)
        elif tap.kind == PLACE:
            for cell in tap.cells:
                self.spheres[cell] = tap.color
            self.tray.remove(tap.color, tap.count)
            self.painted_count += tap.count


def can_fully_scramble(grid):
    """True when the sprite admits an opening with no cell already painted."""
    if grid.playable_count == 0:
        return True
    return max(grid.color_counts().values()) * 2 <= grid.playable_count


def scramble(grid, seed):
    """Scrambler.Scramble: every sphere the picture needs, arranged so none starts on its colour."""
    spheres = [BARE] * grid.cell_count
    random = DotNetRandom(seed)

    # GroupBy(grid.TargetAt): colours in first-encountered order, source order kept inside each.
    groups = OrderedDict()
    for cell in grid.playable_cells():
        groups.setdefault(grid.target_at(cell), []).append(cell)

    if not groups:
        return spheres

    colors = list(groups)

    # .ThenBy(_ => random.Next()): OrderBy computes every key up front, in the order above, before
    # it sorts anything. So all the tiebreak draws happen here, ahead of any shuffling.
    keys = [random.next() for _ in colors]

    # OrderByDescending(count).ThenBy(key). LINQ's sort is stable and so is Python's, so equal
    # (count, key) pairs keep their GroupBy order in both.
    order = sorted(range(len(colors)), key=lambda i: (-len(groups[colors[i]]), keys[i]))

    cells = []
    for i in order:
        cells.extend(_shuffled(list(groups[colors[i]]), random))

    n = len(cells)
    shift = max(len(group) for group in groups.values())

    for i in range(n):
        spheres[cells[i]] = grid.target_at(cells[(i + shift) % n])

    return spheres


def _shuffled(items, random):
    """Fisher-Yates, walking down exactly as Scrambler.Shuffled does."""
    for i in range(len(items) - 1, 0, -1):
        j = random.next(i + 1)
        items[i], items[j] = items[j], items[i]
    return items


def greedy_solve(state, move_limit=5000):
    """GreedyPlayer.Solve, a stand-in player, used to check a level can actually be finished."""
    moves = 0
    while not state.is_solved and moves < move_limit:
        moves += 1
        if not _greedy_step(state):
            break
    return state.is_solved


def _greedy_step(state):
    # Placing paints cells and frees tray slots, so it always comes first.
    best = _best(state, PLACE, prefer_larger=True, wanted=None)
    if best is None:
        # Otherwise lift a colour some bare cell is already waiting for, so the very next step
        # can put it down again.
        best = _best(state, PICKUP, prefer_larger=False, wanted=_colours_wanted(state))
        if best is None:
            best = _best(state, PICKUP, prefer_larger=False, wanted=None)

    if best is None:
        return False

    state.apply(best)
    return True


def _colours_wanted(state):
    return {
        state.grid.target_at(cell)
        for cell in range(state.grid.cell_count)
        if state.grid.is_playable(cell) and state.spheres[cell] == BARE
    }


def _best(state, kind, prefer_larger, wanted):
    best = None
    for cell in range(state.grid.cell_count):
        tap = state.resolve(cell)
        if tap.kind != kind:
            continue
        if wanted is not None and tap.color not in wanted:
            continue
        if best is None or (tap.count > best.count if prefer_larger else tap.count < best.count):
            best = tap
    return best
