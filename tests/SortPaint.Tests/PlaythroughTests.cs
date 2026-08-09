using System.Linq;
using SortPaint.Core;
using Xunit;

namespace SortPaint.Tests;

/// <summary>
/// End-to-end checks that a scrambled board can actually be painted back under the rules.
/// </summary>
public class PlaythroughTests
{
    private static readonly string[] Sprite =
    [
        ".aabb.",
        "abccba",
        "abccba",
        ".aabb.",
    ];

    [Theory]
    [InlineData(0, 24)]
    [InlineData(7, 24)]
    [InlineData(20260809, 24)]
    [InlineData(3, 8)]
    public void AScrambledBoardCanBePaintedBackToThePicture(int seed, int trayCapacity)
    {
        LevelGrid grid = Boards.Grid(Sprite);
        var state = new BoardState(grid, Scrambler.Scramble(grid, seed), trayCapacity);

        bool solved = GreedyPlayer.Solve(state);

        Assert.True(solved, $"stuck at {state.PaintedCount}/{grid.PlayableCount}");
        Assert.True(state.Tray.IsEmpty);
        Assert.Equal(Boards.Tally(grid.PlayableCells().Select(cell => grid.TargetAt(cell))), Boards.Tally(state.Spheres));
    }

    [Fact]
    public void ABoardWithMovesLeftReportsSo()
    {
        var state = new BoardState(Boards.Grid(Sprite), Scrambler.Scramble(Boards.Grid(Sprite), 4), 24);

        Assert.True(state.HasLegalMove);
    }

    [Fact]
    public void AFullTrayOfUnwantedColoursIsADeadEnd()
    {
        // Both beads are lifted off cells that want 'a', filling the tray with 'b'. Nothing on the
        // board is left to lift, and no bare cell wants what the tray holds.
        var state = Boards.State(["aa"], ["bb"], capacity: 2);

        state.Tap(0, 0);

        Assert.True(state.Tray.IsFull);
        Assert.False(state.IsSolved);
        Assert.False(state.HasLegalMove);
    }

    [Fact]
    public void ASolvedBoardHasNoMovesLeft()
    {
        var state = Boards.State(["ab"], ["ab"]);

        Assert.True(state.IsSolved);
        Assert.False(state.HasLegalMove);
    }
}
