using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SortPaint.Core;
using Xunit;

namespace SortPaint.Tests;

/// <summary>
/// Holds <c>scripts/import_level.py</c> to the game.
/// </summary>
/// <remarks>
/// The importer decides whether a picture makes a playable level by scrambling and solving it in
/// Python, using ports of <see cref="Scrambler"/> and the rules, including a port of .NET's
/// seeded <c>System.Random</c>. If a port drifts, the importer still passes levels, but it is
/// passing a level the game never deals. That failure is silent everywhere else, so it is caught
/// here: <c>scripts/scramble_parity.json</c> records what Python produced, and these tests replay
/// it through the real thing.
///
/// Regenerate the fixture with <c>python3 scripts/import_level.py --write-parity</c>, but only
/// once you know why it changed. A fixture refreshed to make a red test go green proves nothing.
///
/// The fixture is built from the level PNGs, so it doubles as a check that the hand-mirrored art
/// in <see cref="LevelSprites"/> still matches the pictures the game actually loads.
/// </remarks>
public class ScrambleParityTests
{
    private sealed record ParityEntry(string Name, int Seed, string[] Rows, string Scramble);

    private static readonly Dictionary<string, ParityEntry> Fixture = LoadFixture();

    [Theory]
    [MemberData(nameof(LevelSprites.Shipped), MemberType = typeof(LevelSprites))]
    public void ThePythonPortDealsTheSameOpeningAsTheGame(string name, string[] sprite, int seed)
    {
        ParityEntry entry = Entry(name);

        int[] spheres = Scrambler.Scramble(LevelSprites.Grid(sprite), seed);

        Assert.Equal(entry.Scramble, Sha256(string.Join(",", spheres)));
    }

    [Theory]
    [MemberData(nameof(LevelSprites.Shipped), MemberType = typeof(LevelSprites))]
    public void TheMirroredArtStillMatchesTheLevelPng(string name, string[] sprite, int seed)
    {
        _ = seed;
        ParityEntry entry = Entry(name);

        Assert.Equal(entry.Rows, Canonicalise(sprite));
    }

    [Theory]
    [MemberData(nameof(LevelSprites.Shipped), MemberType = typeof(LevelSprites))]
    public void TheSeedHereMatchesTheOneInTheLevelResource(string name, string[] sprite, int seed)
    {
        _ = sprite;

        Assert.Equal(Entry(name).Seed, seed);
    }

    private static ParityEntry Entry(string name)
    {
        Assert.True(
            Fixture.TryGetValue(name, out ParityEntry entry),
            $"{name} is missing from scripts/scramble_parity.json. Is it listed in levels/campaign.tres? " +
            "Refresh with: python3 scripts/import_level.py --write-parity");

        return entry;
    }

    /// <summary>
    /// Re-letters a sprite by palette index, the way <c>sortpaint/level.py</c> writes the fixture.
    /// The hand-authored blocks name their colours ('o' outline, 'r' red); the fixture cannot know
    /// those names, so both sides are reduced to first-encountered order before they are compared.
    /// </summary>
    private static string[] Canonicalise(string[] rows)
    {
        const string letters = "orwsdgnbycakepl";
        var indices = new Dictionary<char, int>();

        return rows
            .Select(row => new string(row.Select(symbol =>
            {
                if (symbol == Boards.Hole) return Boards.Hole;
                if (!indices.TryGetValue(symbol, out int index))
                {
                    index = indices.Count;
                    indices[symbol] = index;
                }
                return letters[index];
            }).ToArray()))
            .ToArray();
    }

    private static string Sha256(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static Dictionary<string, ParityEntry> LoadFixture()
    {
        string path = Path.Combine(RepoRoot(), "scripts", "scramble_parity.json");
        Assert.True(File.Exists(path), $"{path} is missing. Run: python3 scripts/import_level.py --write-parity");

        // The file opens with a "do not edit" comment, which is not JSON.
        string json = File.ReadAllText(path);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        return JsonSerializer.Deserialize<ParityEntry[]>(json, options)!
            .ToDictionary(entry => entry.Name);
    }

    /// <summary>Walks up from the test assembly until the project root turns up.</summary>
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "project.godot")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
