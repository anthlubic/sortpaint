using System.Linq;
using SortPaint.Core;
using Xunit;

namespace SortPaint.Tests;

public class PendingScoresTests
{
    [Fact]
    public void AFinishThatDidNotSendWaitsItsTurn()
    {
        var pending = new PendingScores();

        Assert.True(pending.Add(new Submission("apple", 94, 200_000)));
        Assert.Equal(1, pending.Count);
        Assert.Equal("apple", pending.Waiting[0].Level);
    }

    [Fact]
    public void ALevelOnlyWaitsOnce()
    {
        var pending = new PendingScores();
        pending.Add(new Submission("apple", 94, 200_000));

        Assert.True(pending.Add(new Submission("apple", 90, 300_000)));

        Assert.Equal(1, pending.Count);
        Assert.Equal(90, pending.Waiting[0].Moves);
    }

    [Fact]
    public void OnlyTheBestWaitingRoundIsKept()
    {
        var pending = new PendingScores();
        pending.Add(new Submission("apple", 90, 200_000));

        Assert.False(pending.Add(new Submission("apple", 94, 100_000)));
        Assert.Equal(90, pending.Waiting[0].Moves);

        // The same length, quicker, is worth replacing it with.
        Assert.True(pending.Add(new Submission("apple", 90, 150_000)));
        Assert.Equal(150_000, pending.Waiting[0].Millis);
    }

    [Fact]
    public void ABetterRoundKeepsTheLevelsPlaceInTheQueue()
    {
        var pending = new PendingScores();
        pending.Add(new Submission("apple", 94, 200_000));
        pending.Add(new Submission("cactus", 60, 100_000));

        pending.Add(new Submission("apple", 88, 190_000));

        Assert.Equal(["apple", "cactus"], pending.Waiting.Select(waiting => waiting.Level));
    }

    [Fact]
    public void SendingALevelTakesItOutOfTheQueue()
    {
        var pending = new PendingScores();
        pending.Add(new Submission("apple", 94, 200_000));
        pending.Add(new Submission("cactus", 60, 100_000));

        Assert.True(pending.Sent("apple"));
        Assert.False(pending.Sent("apple"));

        Assert.Equal(["cactus"], pending.Waiting.Select(waiting => waiting.Level));
    }

    [Fact]
    public void ThereIsNothingToSendForARoundWithNoCount()
    {
        var pending = new PendingScores();

        Assert.False(pending.Add(new Submission("apple", 0, 200_000)));
        Assert.False(pending.Add(new Submission("", 94, 200_000)));
        Assert.False(pending.Add(null));

        Assert.Equal(0, pending.Count);
    }

    [Fact]
    public void TheQueueDoesNotGrowWithoutBound()
    {
        var pending = new PendingScores();

        for (int i = 0; i < PendingScores.Capacity + 20; i++)
            pending.Add(new Submission($"level-{i}", 50, 100_000));

        Assert.Equal(PendingScores.Capacity, pending.Count);

        // The oldest go first, so the most recent rounds are the ones that survive.
        Assert.Equal("level-20", pending.Waiting[0].Level);
    }

    [Fact]
    public void TheQueueCanBeEmptied()
    {
        var pending = new PendingScores();
        pending.Add(new Submission("apple", 94, 200_000));

        pending.Clear();

        Assert.Equal(0, pending.Count);
    }
}
