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
    public void ALevelIsOpenOnceTheTrophiesAreIn(int trophies, int required)
    {
        Assert.True(Unlock.IsOpen(trophies, required));
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(4, 5)]
    [InlineData(39, 40)]
    public void ALevelStaysShutUntilThen(int trophies, int required)
    {
        Assert.False(Unlock.IsOpen(trophies, required));
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
    public void TheShortfallIsWhatIsStillMissing(int trophies, int required, int expected)
    {
        Assert.Equal(expected, Unlock.Shortfall(trophies, required));
    }

    [Fact]
    public void TheLockedMessageNamesTheNumberItWants()
    {
        Assert.Equal(
            "This level needs 15 gold trophies to unlock. Paint a level in par to win one!",
            Unlock.LockedMessage(15));
    }

    [Fact]
    public void ALevelWantingOneTrophyAsksForItInTheSingular()
    {
        Assert.Equal(
            "This level needs 1 gold trophy to unlock. Paint a level in par to win one!",
            Unlock.LockedMessage(1));
    }

    [Theory]
    [InlineData(0, "0 gold trophies")]
    [InlineData(1, "1 gold trophy")]
    [InlineData(2, "2 gold trophies")]
    public void TheCountReadsAsEnglish(int trophies, string expected)
    {
        Assert.Equal(expected, Unlock.GoldTrophies(trophies));
    }
}
