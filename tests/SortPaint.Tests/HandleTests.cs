using System;
using System.Collections.Generic;
using SortPaint.Core;
using Xunit;

namespace SortPaint.Tests;

public class HandleTests
{
    [Fact]
    public void BothListsAreFiftyWordsLong()
    {
        Assert.Equal(50, Handle.Adjectives.Count);
        Assert.Equal(50, Handle.Nouns.Count);
        Assert.Equal(2_500_000, Handle.Combinations);
    }

    [Fact]
    public void NoWordIsListedTwice()
    {
        Assert.Equal(Handle.Adjectives.Count, new HashSet<string>(Handle.Adjectives).Count);
        Assert.Equal(Handle.Nouns.Count, new HashSet<string>(Handle.Nouns).Count);
    }

    [Fact]
    public void AHandleIsTwoWordsAndThreeDigits()
    {
        string handle = Handle.Generate(new Random(1));

        Assert.Matches("^[A-Z][a-z]+[A-Z][a-z]+[0-9]{3}$", handle);
    }

    [Fact]
    public void EveryHandleTheGameHandsOutIsOneItWouldAccept()
    {
        var random = new Random(20260810);

        for (int i = 0; i < 2000; i++)
            Assert.True(Handle.IsWellFormed(Handle.Generate(random)));
    }

    [Fact]
    public void EveryPairingIsAccepted()
    {
        // Adjectives are not prefix-free, so this checks the parse rather than trusting one sample.
        foreach (string adjective in Handle.Adjectives)
            foreach (string noun in Handle.Nouns)
                Assert.True(Handle.IsWellFormed($"{adjective}{noun}000"), $"{adjective}{noun}000");
    }

    [Fact]
    public void AHandleDrawnTwiceIsNotTheSameOne()
    {
        // Two players opening the game are not handed the same name by construction.
        var seen = new HashSet<string>();
        var random = new Random(7);

        for (int i = 0; i < 100; i++) seen.Add(Handle.Generate(random));

        Assert.True(seen.Count > 90, $"only {seen.Count} distinct handles in 100 draws");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("042")]
    [InlineData("BriskAxolotl")]           // no digits
    [InlineData("BriskAxolotl42")]         // two digits, not three
    [InlineData("BriskAxolotl0424")]       // four digits, so the noun no longer matches
    [InlineData("briskaxolotl042")]        // wrong case
    [InlineData("Brisk Axolotl042")]       // a space is not in the grammar
    [InlineData("BriskWombat042")]         // adjective is listed, noun is not
    [InlineData("SneakyAxolotl042")]       // noun is listed, adjective is not
    [InlineData("Axolotl042")]             // a noun on its own
    [InlineData("BriskAxolotlBrisk042")]   // three words
    public void AnythingElseIsRefused(string handle)
    {
        Assert.False(Handle.IsWellFormed(handle));
    }

    [Fact]
    public void AHandleNobodyCouldBeHandedIsRefusedWhateverItSays()
    {
        // The point of the closed lists: there is no way to get an arbitrary word onto a board.
        Assert.False(Handle.IsWellFormed("DropTableScores042"));
        Assert.False(Handle.IsWellFormed("<script>alert(1)</script>"));
    }

    [Fact]
    public void DrawingWithoutARandomIsARefusal()
    {
        Assert.Throws<ArgumentNullException>(() => Handle.Generate(null));
    }
}
