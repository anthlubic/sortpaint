using System;
using System.Collections.Generic;
using System.Linq;
using SortPaint.Core;
using Xunit;

namespace SortPaint.Tests;

public class LeaderboardTests
{
    private static BoardEntry Entry(string handle, int moves, int millis, bool isYou = false) =>
        new(handle, moves, millis, isYou);

    [Fact]
    public void FewestMovesLeadsTheBoard()
    {
        IReadOnlyList<BoardRow> rows = Leaderboard.Rank(
        [
            Entry("MintyBadger042", 94, 200_000),
            Entry("QuietPomelo117", 88, 134_000),
            Entry("RusticKettle300", 89, 118_000),
        ]);

        Assert.Equal(["QuietPomelo117", "RusticKettle300", "MintyBadger042"], rows.Select(row => row.Handle));
        Assert.Equal([1, 2, 3], rows.Select(row => row.Rank));
    }

    [Fact]
    public void TheQuickerRoundBreaksATieOnMoves()
    {
        IReadOnlyList<BoardRow> rows = Leaderboard.Rank(
        [
            Entry("BriskAxolotl042", 88, 171_000),
            Entry("QuietPomelo117", 88, 134_000),
            Entry("MintyBadger042", 88, 187_000),
        ]);

        Assert.Equal(["QuietPomelo117", "BriskAxolotl042", "MintyBadger042"], rows.Select(row => row.Handle));
    }

    [Fact]
    public void ASlowerRoundStillWinsOnFewerMoves()
    {
        // A leaderboard on a golf game ranks on strokes. The clock only separates equal rounds.
        IReadOnlyList<BoardRow> rows = Leaderboard.Rank(
        [
            Entry("RusticKettle300", 89, 60_000),
            Entry("QuietPomelo117", 88, 600_000),
        ]);

        Assert.Equal("QuietPomelo117", rows[0].Handle);
    }

    [Fact]
    public void RankingNothingIsAnEmptyBoard()
    {
        Assert.Empty(Leaderboard.Rank([]));
        Assert.Throws<ArgumentNullException>(() => Leaderboard.Rank(null));
    }

    [Fact]
    public void ARowKnowsHowLongTheRoundTook()
    {
        BoardRow row = Leaderboard.Rank([Entry("QuietPomelo117", 88, 134_000)])[0];

        Assert.Equal("2:14", row.Elapsed);
    }

    [Fact]
    public void YourOwnRowIsAddedWhenYouPlacedOutsideTheLeaders()
    {
        IReadOnlyList<BoardRow> leaders = Leaderboard.Rank(
        [
            Entry("QuietPomelo117", 88, 134_000),
            Entry("RusticKettle300", 89, 118_000),
        ]);
        var you = new BoardRow(47, "MintyBadger042", 190, 400_000, true);

        IReadOnlyList<BoardRow> board = Leaderboard.Merge(leaders, you);

        Assert.Equal(3, board.Count);
        Assert.Same(you, board[2]);
    }

    [Fact]
    public void YouAreNotShownTwiceWhenYouAreAlreadyALeader()
    {
        IReadOnlyList<BoardRow> leaders = Leaderboard.Rank(
        [
            Entry("QuietPomelo117", 88, 134_000, isYou: true),
            Entry("RusticKettle300", 89, 118_000),
        ]);

        IReadOnlyList<BoardRow> board = Leaderboard.Merge(leaders, leaders[0]);

        Assert.Equal(2, board.Count);
    }

    [Fact]
    public void SomebodyWhoIsNotOnTheBoardGetsTheLeadersAlone()
    {
        IReadOnlyList<BoardRow> leaders = Leaderboard.Rank([Entry("QuietPomelo117", 88, 134_000)]);

        Assert.Same(leaders, Leaderboard.Merge(leaders, null));
        Assert.Throws<ArgumentNullException>(() => Leaderboard.Merge(null, null));
    }
}
