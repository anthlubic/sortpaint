using System.Collections.Generic;
using SortPaint.Core;
using Xunit;

namespace SortPaint.Tests;

public class FloodTests
{
    [Fact]
    public void RegionComesBackNearestTheTapFirst()
    {
        LevelGrid grid = Boards.Grid("aaaa");

        List<int> region = Flood.Region(grid, 2, _ => true);

        // Breadth first from the tap, so a trimmed region keeps the cells under the finger.
        Assert.Equal(new[] { 2, 1, 3, 0 }, region);
    }

    [Fact]
    public void DiagonalNeighboursAreConnected()
    {
        LevelGrid grid = Boards.Grid("ab", "ba");

        List<int> region = Flood.Region(grid, 0, cell => grid.TargetAt(cell) == Boards.Color('a'));

        Assert.Equal(new[] { 0, 3 }, region);
    }

    [Fact]
    public void ARunSlipsThroughASingleDiagonalGap()
    {
        // The two 'a' patches touch only at the corner between (1,0) and (2,1).
        LevelGrid grid = Boards.Grid(
            "aab.",
            "bbaa");

        List<int> region = Flood.Region(grid, 0, cell => grid.TargetAt(cell) == Boards.Color('a'));

        Assert.Equal(new[] { 0, 1, 6, 7 }, region);
    }

    [Fact]
    public void ADiagonalWallTwoCellsThickStillSeparates()
    {
        LevelGrid grid = Boards.Grid(
            "abb",
            "bba",
            "bba");

        List<int> region = Flood.Region(grid, 0, cell => grid.TargetAt(cell) == Boards.Color('a'));

        Assert.Equal(new[] { 0 }, region);
    }

    [Fact]
    public void AFillCrossesACornerEvenWithHolesEitherSideOfIt()
    {
        LevelGrid grid = Boards.Grid(
            "a.",
            ".a");

        List<int> region = Flood.Region(grid, 0, cell => grid.TargetAt(cell) == Boards.Color('a'));

        Assert.Equal(new[] { 0, 3 }, region);
    }

    [Fact]
    public void AStartCellThatFailsThePredicateYieldsNothing()
    {
        LevelGrid grid = Boards.Grid("aa");

        Assert.Empty(Flood.Region(grid, 0, _ => false));
    }

    [Fact]
    public void CellsThatFailThePredicateActAsWalls()
    {
        LevelGrid grid = Boards.Grid("aabaa");

        List<int> region = Flood.Region(grid, 0, cell => grid.TargetAt(cell) == Boards.Color('a'));

        Assert.Equal(new[] { 0, 1 }, region);
    }

    [Fact]
    public void FillWrapsAroundObstaclesRatherThanThroughThem()
    {
        LevelGrid grid = Boards.Grid(
            "aaa",
            "aba",
            "aaa");

        List<int> region = Flood.Region(grid, 0, cell => grid.TargetAt(cell) == Boards.Color('a'));

        Assert.Equal(8, region.Count);
        Assert.DoesNotContain(4, region);
    }
}
