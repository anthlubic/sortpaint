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
/// and are treated as good ones rather than punished for arriving without a number.
/// </remarks>
public sealed class Progress
{
    /// <summary>Finished levels, each with the fewest moves it has been finished in, or 0.</summary>
    private readonly Dictionary<string, int> _best = new(StringComparer.Ordinal);

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
        string.IsNullOrEmpty(id) ? 0 : _best.GetValueOrDefault(id);

    /// <summary>Records a finish with no move count. True the first time a level is painted.</summary>
    public bool MarkCompleted(string id) => Record(id, 0);

    /// <summary>
    /// Records a finish. True when it is news, which is the first finish or a shorter round than
    /// the one on file; a longer round leaves the record alone.
    /// </summary>
    public bool Record(string id, int moves)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (moves < 0) moves = 0;

        if (!_best.TryGetValue(id, out int best))
        {
            _best[id] = moves;
            return true;
        }

        if (moves == 0 || (best != 0 && moves >= best)) return false;

        _best[id] = moves;
        return true;
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
