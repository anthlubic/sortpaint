using System.Linq;
using SortPaint.Core;
using Xunit;

namespace SortPaint.Tests;

public class ScramblerTests
{
    private static readonly string[] Sprite =
    [
        ".aabb.",
        "abccba",
        "abccba",
        ".aabb.",
    ];

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(99)]
    [InlineData(20260809)]
    public void EveryCellStartsOnTheWrongColour(int seed)
    {
        LevelGrid grid = Boards.Grid(Sprite);
        Assert.True(Scrambler.CanFullyScramble(grid));

        int[] spheres = Scrambler.Scramble(grid, seed);

        foreach (int cell in grid.PlayableCells())
            Assert.NotEqual(grid.TargetAt(cell), spheres[cell]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(20260809)]
    public void ScrambleDealsExactlyTheColoursThePictureNeeds(int seed)
    {
        LevelGrid grid = Boards.Grid(Sprite);

        int[] spheres = Scrambler.Scramble(grid, seed);

        // Same multiset as the target, so the board always holds precisely enough to finish.
        Assert.Equal(grid.ColorCounts(), Boards.Tally(spheres));
    }

    [Fact]
    public void HolesInTheSpriteStayBare()
    {
        LevelGrid grid = Boards.Grid(Sprite);

        int[] spheres = Scrambler.Scramble(grid, 5);

        for (int i = 0; i < grid.CellCount; i++)
            if (!grid.IsPlayable(i)) Assert.Equal(BoardState.Bare, spheres[i]);
    }

    [Fact]
    public void TheSameSeedAlwaysDealsTheSameOpening()
    {
        LevelGrid grid = Boards.Grid(Sprite);

        Assert.Equal(Scrambler.Scramble(grid, 42), Scrambler.Scramble(grid, 42));
    }

    [Fact]
    public void DifferentSeedsDealDifferentOpenings()
    {
        LevelGrid grid = Boards.Grid(Sprite);

        Assert.NotEqual(Scrambler.Scramble(grid, 1), Scrambler.Scramble(grid, 2));
    }

    [Fact]
    public void AColourCoveringMoreThanHalfLeavesTheFewestPossiblePaintedCells()
    {
        // 'a' takes four of five cells, so at least three of them must start already painted.
        LevelGrid grid = Boards.Grid("aaaab");
        Assert.False(Scrambler.CanFullyScramble(grid));

        int[] spheres = Scrambler.Scramble(grid, 1);
        int painted = grid.PlayableCells().Count(cell => spheres[cell] == grid.TargetAt(cell));

        Assert.Equal(3, painted);
    }
}
