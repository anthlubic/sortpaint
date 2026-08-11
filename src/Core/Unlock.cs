using System;

namespace SortPaint.Core;

/// <summary>
/// Which levels are open to play. A level may ask for a number of gold trophies first, gold being
/// what a level painted in par awards, so the later pictures arrive as the player gets good at the
/// game rather than all at once on the first run.
/// </summary>
/// <remarks>
/// The requirement is a plain number on each level (<c>RequiredChecks</c>), not a position in the
/// list, so the menu can be reordered without moving any lock. Zero, which every level shipped
/// before locks existed carries, means open from the start. A silver trophy, which is what going
/// over par earns, buys nothing: only gold counts here.
/// </remarks>
public static class Unlock
{
    /// <summary>Whether a level asking for <paramref name="required"/> gold trophies is open.</summary>
    public static bool IsOpen(int trophies, int required) => required <= 0 || trophies >= required;

    /// <summary>How many more gold trophies the level wants, or 0 once it is open.</summary>
    public static int Shortfall(int trophies, int required) =>
        Math.Max(0, required - Math.Max(0, trophies));

    /// <summary>A count of gold trophies in words, so nothing ends up saying "1 gold trophies".</summary>
    public static string GoldTrophies(int trophies) =>
        trophies == 1 ? "1 gold trophy" : $"{trophies} gold trophies";

    /// <summary>What a locked level says when it is tapped.</summary>
    public static string LockedMessage(int required) =>
        $"This level needs {GoldTrophies(required)} to unlock. Paint a level in par to win one!";
}
