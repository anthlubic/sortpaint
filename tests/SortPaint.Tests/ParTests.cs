using SortPaint.Core;
using Xunit;

namespace SortPaint.Tests;

public class ParTests
{
    [Theory]
    [InlineData(1, 2)]      // 1.4 rounds up, so a one-move level still gives a spare
    [InlineData(20, 28)]
    [InlineData(62, 87)]    // toadstool
    [InlineData(100, 140)]
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
        Assert.True(Par.IsMet(86, 87));
        Assert.True(Par.IsMet(87, 87));
        Assert.False(Par.IsMet(88, 87));
    }

    [Fact]
    public void ARoundWithNothingToMeasureItAgainstAlwaysCounts()
    {
        Assert.True(Par.IsMet(500, 0));
        Assert.True(Par.IsMet(0, 72));
    }
}
