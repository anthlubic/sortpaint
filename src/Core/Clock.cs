using System;

namespace SortPaint.Core;

/// <summary>How a round's length is written down, wherever it is shown.</summary>
public static class Clock
{
    /// <summary>
    /// Minutes and seconds, as in 2:14. Rounded up, so a round that has barely started reads 0:01
    /// rather than sitting on 0:00 for its first second.
    /// </summary>
    public static string Format(int millis)
    {
        int whole = Math.Max(0, (int)Math.Ceiling(millis / 1000.0));
        return $"{whole / 60}:{whole % 60:00}";
    }
}
