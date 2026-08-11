using System;
using System.Collections.Generic;

namespace SortPaint.Core;

/// <summary>
/// Which levels have been painted, and in how few moves. Ids are opaque strings (the Godot layer
/// hands it each level resource's path), so the record itself stays plain C# and can be tested on
/// its own.
/// </summary>
/// <remarks>
/// A level in the record has been finished. The move count alongside it is the best round so far,
/// and zero when it is not known: rounds played before the game counted moves are still finishes,
/// and are treated as good ones rather than punished for arriving without a number. The clock is
/// kept beside it as the tie-break between two rounds of equal length, and is zero on rounds banked
/// before the clock was kept.
/// </remarks>
public sealed class Progress
{
    /// <summary>One finish: how few moves it took, and how long it took, either possibly unknown.</summary>
    private readonly record struct Round(int Moves, int Millis);

    /// <summary>Finished levels, each with the best round it has been finished in.</summary>
    private readonly Dictionary<string, Round> _best = new(StringComparer.Ordinal);

    public Progress()
    {
    }

    /// <summary>Rebuilds a record from saved ids. Blank and repeated ids are dropped.</summary>
    public Progress(IEnumerable<string> completed)
    {
        ArgumentNullException.ThrowIfNull(completed);
        foreach (string id in completed) MarkCompleted(id);
    }

    public int CompletedCount => _best.Count;

    public bool IsCompleted(string id) => !string.IsNullOrEmpty(id) && _best.ContainsKey(id);

    /// <summary>The best round on a level, or 0 when it is unfinished or was finished uncounted.</summary>
    public int BestMoves(string id) =>
        string.IsNullOrEmpty(id) ? 0 : _best.GetValueOrDefault(id).Moves;

    /// <summary>
    /// How long the best round took, in milliseconds, or 0 when it is unfinished or was banked
    /// before the clock was kept. This is the tie-break the leaderboard sorts equal rounds by.
    /// </summary>
    public int BestMillis(string id) =>
        string.IsNullOrEmpty(id) ? 0 : _best.GetValueOrDefault(id).Millis;

    /// <summary>Records a finish with no move count. True the first time a level is painted.</summary>
    public bool MarkCompleted(string id) => Record(id, 0, 0);

    /// <summary>Records a finish whose clock was not kept.</summary>
    public bool Record(string id, int moves) => Record(id, moves, 0);

    /// <summary>
    /// Records a finish. True when it is news, which is the first finish, a shorter round than the
    /// one on file, or the same length done quicker; anything longer leaves the record alone.
    /// </summary>
    public bool Record(string id, int moves, int millis)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (moves < 0) moves = 0;
        if (millis < 0) millis = 0;

        if (!_best.TryGetValue(id, out Round best))
        {
            _best[id] = new Round(moves, millis);
            return true;
        }

        // A finish that arrives without a count never displaces one that has a number on it.
        if (moves == 0) return false;

        // A round on file with no count is beaten by any counted round, however long.
        if (best.Moves != 0 && !RoundOrder.IsBetter(moves, millis, best.Moves, best.Millis)) return false;

        _best[id] = new Round(moves, millis);
        return true;
    }

    /// <summary>
    /// Whether a level's best round came in on par, which is what a green check marks. A level
    /// with no par worked out, and a finish banked before the game counted moves, both count: see
    /// <see cref="Par.IsMet"/>. The tile badge is decided the same way, so the ticks on the menu
    /// and the number the locks count never disagree.
    /// </summary>
    public bool MetPar(string id, int par) => IsCompleted(id) && Par.IsMet(BestMoves(id), par);

    /// <summary>How many of these levels are painted in par. The currency the locks are priced in.</summary>
    public int CountMetPar(IEnumerable<(string Id, int Par)> levels)
    {
        ArgumentNullException.ThrowIfNull(levels);

        int checks = 0;
        foreach ((string id, int par) in levels)
            if (MetPar(id, par)) checks++;

        return checks;
    }

    public bool Forget(string id) => !string.IsNullOrEmpty(id) && _best.Remove(id);

    public void ForgetAll() => _best.Clear();

    /// <summary>How many of these levels are finished. Drives the "3 of 6 painted" line.</summary>
    public int CountCompleted(IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        int found = 0;
        foreach (string id in ids)
            if (IsCompleted(id)) found++;

        return found;
    }

    /// <summary>Sorted, so a saved file does not churn from one run to the next.</summary>
    public IReadOnlyList<string> CompletedIds()
    {
        var ids = new List<string>(_best.Keys);
        ids.Sort(StringComparer.Ordinal);
        return ids;
    }
}
