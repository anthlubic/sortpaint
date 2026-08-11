namespace SortPaint.Core;

/// <summary>
/// Which of two finished rounds is the better one. Fewest moves is the whole game, so it decides
/// first; the clock only separates two rounds of the same length.
/// </summary>
/// <remarks>
/// Named once and used everywhere the same question is asked: what the save file keeps, which
/// unsent result is worth holding on to, and the order the leaderboard is drawn in. If these ever
/// disagreed, a player's own row would sort differently from the record behind it.
/// </remarks>
public static class RoundOrder
{
    /// <summary>
    /// Whether a round beats the one already on file. A round with no clock can still win on
    /// moves, it just cannot win a tie; and a round on file with no clock loses a tie to any
    /// timed round, since a record with a clock on it is the more useful of the two.
    /// </summary>
    public static bool IsBetter(int moves, int millis, int bestMoves, int bestMillis)
    {
        if (moves < bestMoves) return true;
        if (moves > bestMoves) return false;

        return millis > 0 && (bestMillis == 0 || millis < bestMillis);
    }

    /// <summary>
    /// Sort order for two rounds, negative when the first is better. The comparison the board is
    /// drawn in, and the same one <see cref="IsBetter"/> answers, minus its handling of a missing
    /// clock: rows on a board have all been timed.
    /// </summary>
    public static int Compare(int moves, int millis, int otherMoves, int otherMillis)
    {
        int byMoves = moves.CompareTo(otherMoves);
        return byMoves != 0 ? byMoves : millis.CompareTo(otherMillis);
    }
}
