using System;
using System.Collections.Generic;

namespace SortPaint.Core;

/// <summary>
/// The target picture for a level: the colour index every cell must end up
/// showing. Cells whose target is <see cref="Empty"/> are holes in the sprite:
/// they are never painted and never hold a sphere.
/// </summary>
public sealed class LevelGrid
{
    public const int Empty = -1;

    private readonly int[] _targets;

    public LevelGrid(int width, int height, IReadOnlyList<int> targets)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        ArgumentNullException.ThrowIfNull(targets);
        if (targets.Count != width * height)
            throw new ArgumentException($"Expected {width * height} target entries, got {targets.Count}.", nameof(targets));

        Width = width;
        Height = height;
        _targets = new int[targets.Count];

        for (int i = 0; i < targets.Count; i++)
        {
            int target = targets[i];
            if (target < Empty)
                throw new ArgumentException($"Target at index {i} is {target}; valid values are {Empty} or a palette index.", nameof(targets));

            _targets[i] = target;
            if (target != Empty) PlayableCount++;
        }
    }

    public int Width { get; }
    public int Height { get; }
    public int CellCount => Width * Height;

    /// <summary>Number of cells that are part of the sprite and therefore hold a sphere.</summary>
    public int PlayableCount { get; }

    public int Index(int x, int y) => y * Width + x;
    public int XOf(int index) => index % Width;
    public int YOf(int index) => index / Width;

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public int TargetAt(int index) => _targets[index];
    public int TargetAt(int x, int y) => _targets[Index(x, y)];

    public bool IsPlayable(int index) => _targets[index] != Empty;

    /// <summary>Indices of every cell that is part of the sprite, in reading order.</summary>
    public IEnumerable<int> PlayableCells()
    {
        for (int i = 0; i < _targets.Length; i++)
            if (_targets[i] != Empty) yield return i;
    }

    /// <summary>How many cells each palette colour occupies. Also the sphere count a solved board needs.</summary>
    public Dictionary<int, int> ColorCounts()
    {
        var counts = new Dictionary<int, int>();
        foreach (int target in _targets)
        {
            if (target == Empty) continue;
            counts.TryGetValue(target, out int n);
            counts[target] = n + 1;
        }
        return counts;
    }
}
