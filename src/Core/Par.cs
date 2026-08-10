using System;

namespace SortPaint.Core;

/// <summary>
/// The golf part: par is the fewest moves a level is known to be finishable in, plus a little
/// room. Beat it and the round counts as a good one.
/// </summary>
/// <remarks>
/// The fewest moves is not worked out by the game. Searching for it takes far longer than a
/// level is allowed to spend opening, so it is worked out once at authoring time by
/// scripts/update_par.py and written into each level as <c>OptimalMoves</c>. The allowance is
/// applied here, so the shipped number stays a plain fact about the level and how generous par
/// is stays a decision the game makes.
/// </remarks>
public static class Par
{
    /// <summary>Fifteen percent, rounded up, so even a short level gets at least one spare move.</summary>
    public const double Allowance = 0.15;

    /// <summary>Par for a level, or zero when nobody has worked out how short it can be.</summary>
    public static int From(int optimalMoves) =>
        optimalMoves <= 0 ? 0 : (int)Math.Ceiling(optimalMoves * (1.0 + Allowance));

    /// <summary>
    /// Whether a finish counts as a good round. A level with no par to measure against always
    /// does, since the player cannot be asked to beat a number that was never worked out, and so
    /// does a finish with no count, which is one banked before the game counted moves.
    /// </summary>
    public static bool IsMet(int moves, int par) => par <= 0 || moves <= 0 || moves <= par;
}
