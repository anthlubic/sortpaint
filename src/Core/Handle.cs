using System;
using System.Collections.Generic;

namespace SortPaint.Core;

/// <summary>
/// The name a player wears on the leaderboard: an adjective, a noun, and three digits, as in
/// BriskAxolotl042. Handed out once and kept, so nobody types anything.
/// </summary>
/// <remarks>
/// Both lists are closed, and the shape is fixed, which is the point. Because a handle can only
/// ever be two words from these fifty apiece, the server can reject anything else outright, and
/// there is no way to put a word of your own on a stranger's screen. That removes the need for a
/// profanity filter and a moderation queue rather than trying to build one that works.
/// <para>
/// The same two lists live in <c>worker/words.json</c>, which is the copy the server validates
/// against. <c>HandleWordsTests</c> fails the build if the two ever drift apart.
/// </para>
/// </remarks>
public static class Handle
{
    /// <summary>How many digits close a handle. Enough that two players rarely collide.</summary>
    public const int DigitCount = 3;

    public static readonly IReadOnlyList<string> Adjectives =
    [
        "Amber", "Azure", "Brave", "Brisk", "Calm",
        "Cheery", "Clever", "Copper", "Cosmic", "Crimson",
        "Dapper", "Dawn", "Deft", "Eager", "Emerald",
        "Fleet", "Fond", "Gentle", "Golden", "Grand",
        "Happy", "Hazel", "Humble", "Indigo", "Jolly",
        "Keen", "Lively", "Lucky", "Mellow", "Merry",
        "Minty", "Nimble", "Noble", "Olive", "Peppy",
        "Plucky", "Quick", "Quiet", "Rapid", "Royal",
        "Ruby", "Rustic", "Sage", "Scarlet", "Sunny",
        "Swift", "Tidy", "Violet", "Witty", "Zesty",
    ];

    public static readonly IReadOnlyList<string> Nouns =
    [
        "Acorn", "Anchor", "Apple", "Axolotl", "Badger",
        "Balloon", "Beacon", "Bramble", "Cactus", "Canvas",
        "Cedar", "Cherry", "Comet", "Compass", "Cricket",
        "Dahlia", "Eagle", "Ember", "Falcon", "Fennel",
        "Ferry", "Finch", "Ginger", "Harbour", "Heron",
        "Ivy", "Juniper", "Kettle", "Lantern", "Lemon",
        "Lichen", "Magpie", "Maple", "Marble", "Meadow",
        "Mulberry", "Nutmeg", "Otter", "Pebble", "Pelican",
        "Pomelo", "Quartz", "Raven", "Ribbon", "Saffron",
        "Sparrow", "Thistle", "Toadstool", "Walnut", "Willow",
    ];

    private static readonly HashSet<string> NounSet = new(Nouns, StringComparer.Ordinal);

    /// <summary>How many different handles there are to go round.</summary>
    public static int Combinations => Adjectives.Count * Nouns.Count * 1000;

    /// <summary>Draws a handle. Called once per player, the first time the game runs.</summary>
    public static string Generate(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        string adjective = Adjectives[random.Next(Adjectives.Count)];
        string noun = Nouns[random.Next(Nouns.Count)];
        return $"{adjective}{noun}{random.Next(1000):D3}";
    }

    /// <summary>
    /// Whether a handle is one this game could have handed out. The server asks exactly this
    /// question of every submission, so a handle that passes here is one that will be accepted.
    /// </summary>
    public static bool IsWellFormed(string handle)
    {
        if (string.IsNullOrEmpty(handle)) return false;

        int split = handle.Length - DigitCount;
        if (split <= 0) return false;

        for (int i = split; i < handle.Length; i++)
            if (!char.IsAsciiDigit(handle[i])) return false;

        // Adjectives are not prefix-free (Dawn and Dapper both start with D), so every adjective
        // that fits is tried rather than the first one that matches.
        foreach (string adjective in Adjectives)
        {
            if (adjective.Length >= split) continue;
            if (string.CompareOrdinal(handle, 0, adjective, 0, adjective.Length) != 0) continue;

            if (NounSet.Contains(handle[adjective.Length..split])) return true;
        }

        return false;
    }
}
