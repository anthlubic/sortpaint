using SortPaint.Core;
using Xunit;

namespace SortPaint.Tests;

public class ClockTests
{
    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(1, "0:01")]
    [InlineData(1_000, "0:01")]
    [InlineData(1_500, "0:02")]
    [InlineData(59_000, "0:59")]
    [InlineData(60_000, "1:00")]
    [InlineData(134_000, "2:14")]
    [InlineData(3_600_000, "60:00")]
    public void ARoundReadsAsMinutesAndSeconds(int millis, string expected)
    {
        Assert.Equal(expected, Clock.Format(millis));
    }

    [Fact]
    public void ANonsenseClockReadsAsNothingRatherThanGoingBackwards()
    {
        Assert.Equal("0:00", Clock.Format(-5));
    }
}
