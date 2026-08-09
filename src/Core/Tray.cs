using System;
using System.Collections.Generic;
using System.Linq;

namespace SortPaint.Core;

/// <summary>
/// The holding area at the bottom of the screen. Holds a fixed number of spheres
/// in total, of any mix of colours.
/// </summary>
public sealed class Tray
{
    private readonly Dictionary<int, int> _counts = new();

    public Tray(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        Capacity = capacity;
    }

    public int Capacity { get; }

    /// <summary>Total spheres held, across all colours.</summary>
    public int Count { get; private set; }

    public int FreeSlots => Capacity - Count;
    public bool IsFull => Count >= Capacity;
    public bool IsEmpty => Count == 0;

    public int CountOf(int color) => _counts.GetValueOrDefault(color);

    /// <summary>Colours held and how many of each, ordered by palette index so the view is stable.</summary>
    public IEnumerable<KeyValuePair<int, int>> Contents =>
        _counts.Where(pair => pair.Value > 0).OrderBy(pair => pair.Key);

    public void Add(int color, int amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (amount > FreeSlots)
            throw new InvalidOperationException($"Adding {amount} spheres would overflow the tray ({Count}/{Capacity}).");

        _counts[color] = CountOf(color) + amount;
        Count += amount;
    }

    public void Remove(int color, int amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        int held = CountOf(color);
        if (amount > held)
            throw new InvalidOperationException($"Tray holds {held} spheres of colour {color}, cannot remove {amount}.");

        _counts[color] = held - amount;
        Count -= amount;
    }

    /// <summary>
    /// Slot the next sphere of <paramref name="color"/> will land in, given that the tray is
    /// drawn left to right grouped by palette index. Used to aim the flight animation.
    /// </summary>
    public int SlotForNext(int color)
    {
        int slot = 0;
        foreach (var (held, count) in _counts.Where(pair => pair.Value > 0).OrderBy(pair => pair.Key).Select(pair => (pair.Key, pair.Value)))
        {
            if (held >= color) break;
            slot += count;
        }
        return slot + CountOf(color);
    }

    public void Clear()
    {
        _counts.Clear();
        Count = 0;
    }
}
