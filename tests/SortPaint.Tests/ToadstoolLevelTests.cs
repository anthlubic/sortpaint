using System.Linq;
using SortPaint.Core;
using Xunit;

namespace SortPaint.Tests;

/// <summary>
/// The first level, in close-up: 'o' outline, 'r' cap, 'd' cap shadow, 'w' spots, 's' stem,
/// 't' stem shadow. The picture itself lives in <see cref="LevelSprites"/> alongside the others.
/// </summary>
public class ToadstoolLevelTests
{
    private static LevelGrid Grid() => Boards.Grid(LevelSprites.Toadstool);

    [Fact]
    public void TheSpriteHasTheShapeTheLevelExpects()
    {
        LevelGrid grid = Grid();

        Assert.Equal(16, grid.Width);
        Assert.Equal(16, grid.Height);
        Assert.Equal(178, grid.PlayableCount);
        Assert.Equal(6, grid.ColorCounts().Count);
    }

    [Theory]
    [InlineData(20260809)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1234)]
    public void TheShippedLevelOpensWithNothingAlreadyPainted(int seed)
    {
        LevelGrid grid = Grid();
        Assert.True(Scrambler.CanFullyScramble(grid));

        int[] spheres = Scrambler.Scramble(grid, seed);
        var state = new BoardState(grid, spheres, 24);

        int painted = grid.PlayableCells().Count(cell => spheres[cell] == grid.TargetAt(cell));
        Assert.Equal(0, painted);
        Assert.Equal(0, state.PaintedCount);
    }

    [Theory]
    [InlineData(20260809)]
    [InlineData(0)]
    [InlineData(1234)]
    public void TheShippedLevelCanBePaintedWithA24SphereTray(int seed)
    {
        LevelGrid grid = Grid();
        var state = new BoardState(grid, Scrambler.Scramble(grid, seed), 24);

        bool solved = GreedyPlayer.Solve(state);

        Assert.True(solved, $"stuck at {state.PaintedCount}/{grid.PlayableCount}");
        Assert.True(state.Tray.IsEmpty);
    }
}
