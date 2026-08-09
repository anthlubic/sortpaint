"""A faithful port of .NET's seeded System.Random.

Scrambler.Scramble deals a level's opening layout out of `new Random(seed)`, so anything
outside the game that wants to reason about a level has to draw the same numbers in the same
order. .NET keeps the legacy subtractive generator for the seeded constructor precisely so
that seeded output stays stable across releases, which is what makes this port possible.

Ported from Random.Net5CompatSeedImpl.CompatPrng in dotnet/runtime. Only the members
Scrambler actually uses are here: Next() and Next(maxValue).

scripts/scramble_parity.json plus tests/SortPaint.Tests/ScrambleParityTests.cs are the
tripwire on this file: if it drifts from the real thing, `dotnet test` says so.
"""

INT_MAX = 2147483647
INT_MIN = -2147483648
MSEED = 161803398


class DotNetRandom:
    """System.Random(int seed). Not the parameterless constructor, which is a different algorithm."""

    def __init__(self, seed):
        if not INT_MIN <= seed <= INT_MAX:
            raise ValueError(f"seed {seed} does not fit in a C# int")

        subtraction = INT_MAX if seed == INT_MIN else abs(seed)
        mj = MSEED - subtraction

        self._seed_array = [0] * 56
        self._seed_array[55] = mj

        mk = 1
        ii = 0
        for _ in range(1, 55):
            ii += 21
            if ii >= 55:
                ii -= 55
            self._seed_array[ii] = mk
            mk = mj - mk
            if mk < 0:
                mk += INT_MAX
            mj = self._seed_array[ii]

        for _ in range(1, 5):
            for i in range(1, 56):
                n = i + 30
                if n >= 55:
                    n -= 55
                self._seed_array[i] -= self._seed_array[1 + n]
                if self._seed_array[i] < 0:
                    self._seed_array[i] += INT_MAX

        self._inext = 0
        self._inextp = 21

    def _internal_sample(self):
        loc_inext = self._inext + 1
        if loc_inext >= 56:
            loc_inext = 1
        loc_inextp = self._inextp + 1
        if loc_inextp >= 56:
            loc_inextp = 1

        value = self._seed_array[loc_inext] - self._seed_array[loc_inextp]
        if value == INT_MAX:
            value -= 1
        if value < 0:
            value += INT_MAX

        self._seed_array[loc_inext] = value
        self._inext = loc_inext
        self._inextp = loc_inextp

        return value

    def sample(self):
        return self._internal_sample() * (1.0 / INT_MAX)

    def next(self, max_value=None):
        """Next() with no argument, or Next(maxValue) for a half-open [0, maxValue) draw."""
        if max_value is None:
            return self._internal_sample()
        if max_value < 0:
            raise ValueError(f"maxValue {max_value} must not be negative")
        return int(self.sample() * max_value)
