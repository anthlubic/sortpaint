using System;
using System.Collections.Generic;

namespace SortPaint.Core;

/// <summary>
/// Which levels have been painted. Ids are opaque strings (the Godot layer hands it each
/// level resource's path), so the record itself stays plain C# and can be tested on its own.
/// </summary>
public sealed class Progress
{
    private readonly HashSet<string> _completed = new(StringComparer.Ordinal);

    public Progress()
    {
    }

    /// <summary>Rebuilds a record from saved ids. Blank and repeated ids are dropped.</summary>
    public Progress(IEnumerable<string> completed)
    {
        ArgumentNullException.ThrowIfNull(completed);
        foreach (string id in completed) MarkCompleted(id);
    }

    public int CompletedCount => _completed.Count;

    public bool IsCompleted(string id) => !string.IsNullOrEmpty(id) && _completed.Contains(id);

    /// <summary>Records a finish. True the first time a level is painted, false on a replay.</summary>
    public bool MarkCompleted(string id) => !string.IsNullOrEmpty(id) && _completed.Add(id);

    public bool Forget(string id) => !string.IsNullOrEmpty(id) && _completed.Remove(id);

    public void ForgetAll() => _completed.Clear();

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
        var ids = new List<string>(_completed);
        ids.Sort(StringComparer.Ordinal);
        return ids;
    }
}
