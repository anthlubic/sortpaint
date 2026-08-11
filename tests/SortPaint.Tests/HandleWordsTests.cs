using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using SortPaint.Core;
using Xunit;

namespace SortPaint.Tests;

/// <summary>
/// The game draws handles from one copy of the word lists and the server validates them against
/// another. If they ever drift, every submission from an updated game is rejected by an older
/// server, or the other way round, and nothing says so except an empty leaderboard. So the drift
/// fails here instead.
/// </summary>
public class HandleWordsTests
{
    private static JsonElement Read(string file)
    {
        string path = Path.Combine(AppContext.BaseDirectory, file);
        Assert.True(File.Exists(path), $"worker/{file} was not copied next to the tests: {path}");

        return JsonDocument.Parse(File.ReadAllText(path)).RootElement;
    }

    private static string[] ListOf(string file, string name) =>
        [.. Read(file).GetProperty(name).EnumerateArray().Select(word => word.GetString())];

    public static TheoryData<string> Accepted() => Cases("valid");

    public static TheoryData<string> Refused() => Cases("invalid");

    private static TheoryData<string> Cases(string name)
    {
        var data = new TheoryData<string>();
        foreach (string handle in ListOf("handle-cases.json", name)) data.Add(handle);
        return data;
    }

    [Fact]
    public void TheServerAdjectivesAreTheGameAdjectives()
    {
        Assert.Equal(Handle.Adjectives, ListOf("words.json", "adjectives"));
    }

    [Fact]
    public void TheServerNounsAreTheGameNouns()
    {
        Assert.Equal(Handle.Nouns, ListOf("words.json", "nouns"));
    }

    // Matching word lists are not enough on their own: the game and the server each parse a handle
    // with their own code, and the two could disagree about an odd string while holding identical
    // lists. These are the cases both must answer the same way, and the worker's suite runs them
    // too, so a handle the game hands out is always one the server will take.

    [Theory]
    [MemberData(nameof(Accepted))]
    public void TheServerTakesEveryHandleTheGameWouldHandOut(string handle)
    {
        Assert.True(Handle.IsWellFormed(handle), handle);
    }

    [Theory]
    [MemberData(nameof(Refused))]
    public void NeitherSideTakesAHandleTheGameCouldNotHaveMade(string handle)
    {
        Assert.False(Handle.IsWellFormed(handle), handle);
    }
}
