using System;
using System.Collections.Generic;

namespace SortPaint.Core;

/// <summary>A finish waiting to be sent, on the level it was played on.</summary>
public sealed record Submission(string Level, int Moves, int Millis);

/// <summary>
/// Finishes that have not reached the server yet. A phone loses its connection constantly, and a
/// round is too hard-won to drop because a request timed out, so unsent results wait here and go
/// up on the next launch.
/// </summary>
/// <remarks>
/// At most one entry per level, and it is the best round on that level, decided by the same
/// <see cref="RoundOrder"/> the save file uses. That is what bounds the queue in normal play:
/// there are only so many levels. <see cref="Capacity"/> is the backstop for a save file that
/// arrives holding nonsense.
/// </remarks>
public sealed class PendingScores
{
    /// <summary>More waiting results than this and the oldest are dropped.</summary>
    public const int Capacity = 100;

    private readonly List<Submission> _queue = [];

    public int Count => _queue.Count;

    /// <summary>What is waiting, oldest first, which is the order it should be sent in.</summary>
    public IReadOnlyList<Submission> Waiting => _queue;

    /// <summary>
    /// Queues a finish. A better round on a level replaces the one already waiting rather than
    /// queueing behind it, since only the best one is worth sending. True when the queue changed.
    /// </summary>
    public bool Add(Submission submission)
    {
        if (submission is null) return false;
        if (string.IsNullOrEmpty(submission.Level)) return false;
        if (submission.Moves <= 0) return false;

        int existing = IndexOf(submission.Level);
        if (existing >= 0)
        {
            Submission waiting = _queue[existing];
            if (!RoundOrder.IsBetter(submission.Moves, submission.Millis, waiting.Moves, waiting.Millis))
                return false;

            // Keeps its place in the queue: it is the same level's turn, just a better round.
            _queue[existing] = submission;
            return true;
        }

        _queue.Add(submission);
        while (_queue.Count > Capacity) _queue.RemoveAt(0);

        return true;
    }

    /// <summary>Drops a level's waiting result, once it has been sent.</summary>
    public bool Sent(string level)
    {
        int index = IndexOf(level);
        if (index < 0) return false;

        _queue.RemoveAt(index);
        return true;
    }

    public void Clear() => _queue.Clear();

    private int IndexOf(string level)
    {
        if (string.IsNullOrEmpty(level)) return -1;

        for (int i = 0; i < _queue.Count; i++)
            if (string.Equals(_queue[i].Level, level, StringComparison.Ordinal)) return i;

        return -1;
    }
}
