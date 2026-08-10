using System;
using SortPaint.Core;
using Xunit;

namespace SortPaint.Tests;

public class ProgressTests
{
    [Fact]
    public void ANewRecordHasNothingInIt()
    {
        var progress = new Progress();

        Assert.Equal(0, progress.CompletedCount);
        Assert.False(progress.IsCompleted("res://levels/toadstool.tres"));
        Assert.Empty(progress.CompletedIds());
    }

    [Fact]
    public void FinishingALevelIsRemembered()
    {
        var progress = new Progress();

        Assert.True(progress.MarkCompleted("toadstool"));
        Assert.True(progress.IsCompleted("toadstool"));
        Assert.False(progress.IsCompleted("cactus"));
    }

    [Fact]
    public void ReplayingAFinishedLevelChangesNothing()
    {
        var progress = new Progress();
        progress.MarkCompleted("toadstool");

        Assert.False(progress.MarkCompleted("toadstool"));
        Assert.Equal(1, progress.CompletedCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ALevelWithNoIdIsNotRecorded(string id)
    {
        var progress = new Progress();

        Assert.False(progress.MarkCompleted(id));
        Assert.False(progress.IsCompleted(id));
        Assert.Equal(0, progress.CompletedCount);
    }

    [Fact]
    public void SavedIdsComeBackAsTheSameRecord()
    {
        var saved = new Progress(["cactus", "toadstool", "cactus", ""]);

        Assert.Equal(2, saved.CompletedCount);
        Assert.Equal(["cactus", "toadstool"], saved.CompletedIds());
    }

    [Fact]
    public void TheIdsComeOutSortedSoASaveFileDoesNotChurn()
    {
        var progress = new Progress();
        progress.MarkCompleted("rocket");
        progress.MarkCompleted("balloon");
        progress.MarkCompleted("cherries");

        Assert.Equal(["balloon", "cherries", "rocket"], progress.CompletedIds());
    }

    [Fact]
    public void CountingIsOverTheLevelsAskedAbout()
    {
        var progress = new Progress(["toadstool", "cactus", "somewhere-else"]);

        Assert.Equal(2, progress.CountCompleted(["toadstool", "cactus", "rocket"]));
        Assert.Equal(0, progress.CountCompleted([]));
    }

    [Fact]
    public void ALevelCanBeForgotten()
    {
        var progress = new Progress(["toadstool"]);

        Assert.True(progress.Forget("toadstool"));
        Assert.False(progress.Forget("toadstool"));
        Assert.Equal(0, progress.CompletedCount);
    }

    [Fact]
    public void EverythingCanBeForgottenAtOnce()
    {
        var progress = new Progress(["toadstool", "cactus"]);

        progress.ForgetAll();

        Assert.Equal(0, progress.CompletedCount);
    }

    [Fact]
    public void RebuildingFromNothingIsARefusal()
    {
        Assert.Throws<ArgumentNullException>(() => new Progress(null));
    }

    [Fact]
    public void ARoundIsRememberedWithHowManyMovesItTook()
    {
        var progress = new Progress();

        Assert.True(progress.Record("toadstool", 74));

        Assert.True(progress.IsCompleted("toadstool"));
        Assert.Equal(74, progress.BestMoves("toadstool"));
    }

    [Fact]
    public void OnlyAShorterRoundReplacesTheOneOnFile()
    {
        var progress = new Progress();
        progress.Record("toadstool", 74);

        Assert.False(progress.Record("toadstool", 90));
        Assert.Equal(74, progress.BestMoves("toadstool"));

        Assert.True(progress.Record("toadstool", 66));
        Assert.Equal(66, progress.BestMoves("toadstool"));
    }

    [Fact]
    public void AFinishWithNoCountStillCountsAsAFinish()
    {
        var progress = new Progress();

        Assert.True(progress.MarkCompleted("toadstool"));
        Assert.Equal(0, progress.BestMoves("toadstool"));

        // A round with a count is news even though the level was already finished.
        Assert.True(progress.Record("toadstool", 74));
        Assert.Equal(74, progress.BestMoves("toadstool"));

        // And one without never throws a count away.
        Assert.False(progress.MarkCompleted("toadstool"));
        Assert.Equal(74, progress.BestMoves("toadstool"));
    }

    [Fact]
    public void AnUnfinishedLevelHasNoBestRound()
    {
        var progress = new Progress(["cactus"]);

        Assert.Equal(0, progress.BestMoves("toadstool"));
        Assert.Equal(0, progress.BestMoves(null));
    }
}
