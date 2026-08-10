using SortPaint.Core;
using Xunit;

namespace SortPaint.Tests;

public class ParTests
{
    [Theory]
    [InlineData(1, 2)]      // 1.15 rounds up, so a one-move level still gives a spare
    [InlineData(20, 23)]
    [InlineData(62, 72)]    // toadstool
    [InlineData(100, 115)]
    public void ParIsTheBestKnownSolutionPlusItsAllowance(int optimal, int expected)
    {
        Assert.Equal(expected, Par.From(optimal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void ALevelNobodyHasSolvedHasNoPar(int optimal)
    {
        Assert.Equal(0, Par.From(optimal));
    }

    [Fact]
    public void ComingInOnParCountsAndGoingOverDoesNot()
    {
        Assert.True(Par.IsMet(71, 72));
        Assert.True(Par.IsMet(72, 72));
        Assert.False(Par.IsMet(73, 72));
    }

    [Fact]
    public void ARoundWithNothingToMeasureItAgainstAlwaysCounts()
    {
        Assert.True(Par.IsMet(500, 0));
        Assert.True(Par.IsMet(0, 72));
    }
}
