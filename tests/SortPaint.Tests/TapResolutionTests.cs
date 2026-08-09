using SortPaint.Core;
using Xunit;

namespace SortPaint.Tests;

public class TapResolutionTests
{
    [Fact]
    public void PickupLiftsTheWholeConnectedRunOfThatSphereColour()
    {
        var state = Boards.State(
            target: ["aaaa", "aaaa"],
            spheres: ["bbcb", "bbcb"]);

        TapResult result = state.Resolve(0, 0);

        Assert.Equal(TapKind.Pickup, result.Kind);
        Assert.Equal(Boards.Color('b'), result.Color);
        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void RunsOfTheSameColourSplitByAnotherNeedSeparateTaps()
    {
        var state = Boards.State(
            target: ["aaaa", "aaaa"],
            spheres: ["bbcb", "bbcb"]);

        state.Tap(0, 0);

        // The right-hand column of b is a separate region and stays put.
        Assert.Equal(new[] { "..cb", "..cb" }, Boards.Spheres(state));
        Assert.Equal(4, state.Tray.Count);

        TapResult second = state.Tap(3, 0);

        Assert.Equal(2, second.Count);
        Assert.Equal(6, state.Tray.Count);
    }

    [Fact]
    public void PickupStopsWhenTheTrayFillsAndTakesTheNearestFirst()
    {
        var state = Boards.State(["aaaa"], ["bbbb"], capacity: 2);

        TapResult result = state.Tap(0, 0);

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { 0, 1 }, result.Cells);
        Assert.Equal(new[] { "..bb" }, Boards.Spheres(state));
        Assert.True(state.Tray.IsFull);
    }

    [Fact]
    public void AFullTrayBlocksFurtherPickups()
    {
        var state = Boards.State(["aa"], ["bb"], capacity: 1);

        state.Tap(0, 0);

        Assert.Equal(TapRejection.TrayFull, state.Resolve(1, 0).Rejection);
    }

    [Fact]
    public void TappingAPaintedSphereDoesNothing()
    {
        var state = Boards.State(["ab"], ["ab"]);

        TapResult result = state.Resolve(0, 0);

        Assert.Equal(TapKind.None, result.Kind);
        Assert.Equal(TapRejection.AlreadyPainted, result.Rejection);
    }

    [Fact]
    public void PaintedSpheresWallOffAPickupRun()
    {
        // The middle bead is already on its own colour, so it neither leaves nor lets the
        // fill through to the two beads beyond it.
        var state = Boards.State(
            target: ["ccbcc"],
            spheres: ["bbbbb"]);

        TapResult result = state.Tap(0, 0);

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { "..bbb" }, Boards.Spheres(state));
    }

    [Fact]
    public void PlacingFillsTheConnectedRunOfCellsNeedingThatColour()
    {
        var state = Boards.State(["aabaa"], ["....."]);
        state.Tray.Add(Boards.Color('a'), 10);

        TapResult result = state.Tap(0, 0);

        Assert.Equal(TapKind.Place, result.Kind);
        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { "aa..." }, Boards.Spheres(state));
    }

    [Fact]
    public void PlacingIsLimitedByWhatTheTrayHolds()
    {
        var state = Boards.State(["aaaa"], ["...."]);
        state.Tray.Add(Boards.Color('a'), 2);

        TapResult result = state.Tap(0, 0);

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { 0, 1 }, result.Cells);
        Assert.True(state.Tray.IsEmpty);
    }

    [Fact]
    public void ABareCellWithNothingMatchingInTheTrayIsRejected()
    {
        var state = Boards.State(["ab"], [".."]);
        state.Tray.Add(Boards.Color('b'), 1);

        Assert.Equal(TapRejection.NoMatchingSpheres, state.Resolve(0, 0).Rejection);
        Assert.Equal(TapKind.Place, state.Resolve(1, 0).Kind);
    }

    [Fact]
    public void HolesAndOffBoardTapsAreRejected()
    {
        var state = Boards.State(["a.a"], ["b.b"]);

        Assert.Equal(TapRejection.NotPlayable, state.Resolve(1, 0).Rejection);
        Assert.Equal(TapRejection.OutOfBounds, state.Resolve(5, 0).Rejection);
        Assert.Equal(TapRejection.OutOfBounds, state.Resolve(-1, 0).Rejection);
    }

    [Fact]
    public void PlacedSpheresAlwaysMatchSoTheyStayPut()
    {
        var state = Boards.State(["ab"], ["ba"]);

        state.Tap(0, 0);
        state.Tap(1, 0);
        Assert.False(state.IsSolved);

        state.Tap(0, 0);
        state.Tap(1, 0);

        Assert.True(state.IsSolved);
        Assert.Equal(2, state.PaintedCount);
        Assert.Equal(TapRejection.AlreadyPainted, state.Resolve(0, 0).Rejection);
    }
}
