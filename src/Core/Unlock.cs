using System;

namespace SortPaint.Core;

/// <summary>
/// Which levels are open to play. A level may ask for a number of green checks first, a check
/// being a level painted in par, so the later pictures arrive as the player gets good at the
/// game rather than all at once on the first run.
/// </summary>
/// <remarks>
/// The requirement is a plain number on each level (<c>RequiredChecks</c>), not a position in the
/// list, so the menu can be reordered without moving any lock. Zero, which every level shipped
/// before locks existed carries, means open from the start.
/// </remarks>
public static class Unlock
{
    /// <summary>Whether a level asking for <paramref name="required"/> checks is open.</summary>
    public static bool IsOpen(int checks, int required) => required <= 0 || checks >= required;

    /// <summary>How many more checks the level wants, or 0 once it is open.</summary>
    public static int Shortfall(int checks, int required) =>
        Math.Max(0, required - Math.Max(0, checks));

    /// <summary>What a locked level says when it is tapped.</summary>
    public static string LockedMessage(int required) =>
        $"This level requires {required} par completions to unlock. Get more green checks!";
}
