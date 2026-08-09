using System.Linq;
using SortPaint.Core;
using Xunit;

namespace SortPaint.Tests;

/// <summary>
/// Every level offered by the level select, checked at the exact seed its resource carries.
/// A level that cannot be finished has no business being on the menu.
/// </summary>
public class ShippedLevelsTests
{
    [Theory]
    [MemberData(nameof(LevelSprites.Shipped), MemberType = typeof(LevelSprites))]
    public void EveryLevelOpensWithNothingAlreadyPainted(string name, string[] sprite, int seed)
    {
        LevelGrid grid = LevelSprites.Grid(sprite);
        Assert.True(Scrambler.CanFullyScramble(grid), $"{name}: one colour covers more than half the picture");

        int[] spheres = Scrambler.Scramble(grid, seed);
        int painted = grid.PlayableCells().Count(cell => spheres[cell] == grid.TargetAt(cell));

        Assert.True(painted == 0, $"{name}: {painted} cells start painted");
    }

    [Theory]
    [MemberData(nameof(LevelSprites.Shipped), MemberType = typeof(LevelSprites))]
    public void EveryLevelCanBePaintedWithA24SphereTray(string name, string[] sprite, int seed)
    {
        LevelGrid grid = LevelSprites.Grid(sprite);
        var state = new BoardState(grid, Scrambler.Scramble(grid, seed), 24);

        bool solved = GreedyPlayer.Solve(state);

        Assert.True(solved, $"{name}: stuck at {state.PaintedCount}/{grid.PlayableCount}");
        Assert.True(state.Tray.IsEmpty, $"{name}: finished with {state.Tray.Count} spheres left in the tray");
    }

    /// <summary>The menu leans on this: a picture with no colours would draw an empty preview.</summary>
    [Theory]
    [MemberData(nameof(LevelSprites.Shipped), MemberType = typeof(LevelSprites))]
    public void EveryLevelIsAPictureWorthShowing(string name, string[] sprite, int seed)
    {
        _ = seed;
        LevelGrid grid = LevelSprites.Grid(sprite);

        Assert.True(grid.PlayableCount > 24, $"{name}: only {grid.PlayableCount} cells, smaller than the tray");
        Assert.InRange(grid.ColorCounts().Count, 2, 8);
    }
}
