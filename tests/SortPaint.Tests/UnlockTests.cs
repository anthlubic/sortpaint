using SortPaint.Core;
using Xunit;

namespace SortPaint.Tests;

public class UnlockTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 0)]
    [InlineData(5, 5)]
    [InlineData(9, 5)]
    public void ALevelIsOpenOnceTheChecksAreIn(int checks, int required)
    {
        Assert.True(Unlock.IsOpen(checks, required));
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(4, 5)]
    [InlineData(39, 40)]
    public void ALevelStaysShutUntilThen(int checks, int required)
    {
        Assert.False(Unlock.IsOpen(checks, required));
    }

    [Fact]
    public void EveryLevelOfTheOriginalCampaignIsOpenFromTheStart()
    {
        Assert.True(Unlock.IsOpen(0, 0));
    }

    [Theory]
    [InlineData(0, 5, 5)]
    [InlineData(2, 5, 3)]
    [InlineData(5, 5, 0)]
    [InlineData(8, 5, 0)]
    public void TheShortfallIsWhatIsStillMissing(int checks, int required, int expected)
    {
        Assert.Equal(expected, Unlock.Shortfall(checks, required));
    }

    [Fact]
    public void TheLockedMessageNamesTheNumberItWants()
    {
        Assert.Equal(
            "This level requires 15 par completions to unlock. Get more green checks!",
            Unlock.LockedMessage(15));
    }
}
