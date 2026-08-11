using SortPaint.Core;
using Xunit;

namespace SortPaint.Tests;

public class RoundOrderTests
{
    [Fact]
    public void FewerMovesIsAlwaysBetter()
    {
        Assert.True(RoundOrder.IsBetter(88, 300_000, 94, 100_000));
        Assert.False(RoundOrder.IsBetter(94, 100_000, 88, 300_000));
    }

    [Fact]
    public void TheClockOnlySeparatesRoundsOfEqualLength()
    {
        Assert.True(RoundOrder.IsBetter(88, 100_000, 88, 134_000));
        Assert.False(RoundOrder.IsBetter(88, 134_000, 88, 100_000));
        Assert.False(RoundOrder.IsBetter(88, 100_000, 88, 100_000));
    }

    [Fact]
    public void ARoundWithNoClockCannotWinATie()
    {
        Assert.False(RoundOrder.IsBetter(88, 0, 88, 134_000));
    }

    [Fact]
    public void ATimedRoundBeatsAnUntimedOneOfTheSameLength()
    {
        // The record with a clock on it is the more useful of the two, so it is the one kept.
        Assert.True(RoundOrder.IsBetter(88, 134_000, 88, 0));
    }

    [Fact]
    public void ComparingSortsShortestFirstThenQuickest()
    {
        Assert.True(RoundOrder.Compare(88, 134_000, 89, 10_000) < 0);
        Assert.True(RoundOrder.Compare(88, 134_000, 88, 100_000) > 0);
        Assert.Equal(0, RoundOrder.Compare(88, 134_000, 88, 134_000));
    }
}
