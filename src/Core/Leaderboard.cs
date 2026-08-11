using System;
using System.Collections.Generic;
using System.Linq;

namespace SortPaint.Core;

/// <summary>One player's best round on a level, as the board keeps it.</summary>
/// <param name="IsYou">Set by the server on the row belonging to whoever asked.</param>
public sealed record BoardEntry(string Handle, int Moves, int Millis, bool IsYou);

/// <summary>A row as it is drawn, once the entries have been put in order.</summary>
public sealed record BoardRow(int Rank, string Handle, int Moves, int Millis, bool IsYou)
{
    /// <summary>The round's length, as in 2:14.</summary>
    public string Elapsed => Clock.Format(Millis);
}

/// <summary>
/// Turning what the server sent into the list of rows the overlay draws. Pure, so the shape of a
/// board can be tested without a network or a scene.
/// </summary>
public static class Leaderboard
{
    /// <summary>How many rows the board leads with.</summary>
    public const int TopRows = 10;

    /// <summary>
    /// Puts entries in board order and numbers them. The server sends them sorted already; doing
    /// it again here costs nothing on ten rows and means a rank is never taken on trust from the
    /// network.
    /// </summary>
    public static IReadOnlyList<BoardRow> Rank(IEnumerable<BoardEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return entries
            .Where(entry => entry is not null)
            .OrderBy(entry => entry, Comparer<BoardEntry>.Create(
                (a, b) => RoundOrder.Compare(a.Moves, a.Millis, b.Moves, b.Millis)))
            .Select((entry, index) => new BoardRow(index + 1, entry.Handle, entry.Moves, entry.Millis, entry.IsYou))
            .ToList();
    }

    /// <summary>
    /// The board as it is shown: the leading rows, with your own row on the end when you placed
    /// outside them. Somebody who is not on the board at all, or who is already among the leaders,
    /// gets the leaders unchanged.
    /// </summary>
    public static IReadOnlyList<BoardRow> Merge(IReadOnlyList<BoardRow> leaders, BoardRow you)
    {
        ArgumentNullException.ThrowIfNull(leaders);

        if (you is null || leaders.Any(row => row.IsYou)) return leaders;

        return [.. leaders, you];
    }
}
