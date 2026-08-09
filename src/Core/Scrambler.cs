using System;
using System.Collections.Generic;
using System.Linq;

namespace SortPaint.Core;

/// <summary>
/// Builds a level's opening layout: every sphere the finished picture needs, but shuffled so
/// that no sphere starts on its own colour.
/// </summary>
public static class Scrambler
{
    /// <summary>
    /// Deals out exactly the multiset of colours the picture needs, arranged so no cell starts
    /// painted. Preserving the multiset is what keeps a level solvable: the board always holds
    /// precisely the spheres required, no spares.
    /// </summary>
    /// <remarks>
    /// Cells are grouped by colour, largest group first, then the colours are rotated by the size
    /// of that largest group. That shift is always big enough to carry every cell clear of its own
    /// colour block, so the result is fixed-point free whenever one is possible at all, which is
    /// whenever no single colour covers more than half the sprite. When one colour does exceed
    /// half, the same rotation leaves the smallest unavoidable number of already-painted cells.
    /// </remarks>
    /// <param name="seed">Same seed, same layout, so a level always opens the same way.</param>
    public static int[] Scramble(LevelGrid grid, int seed)
    {
        ArgumentNullException.ThrowIfNull(grid);

        var spheres = new int[grid.CellCount];
        Array.Fill(spheres, BoardState.Bare);

        var random = new Random(seed);

        // Largest colour group first; equal-sized groups take a random order, and the cells
        // inside each group are shuffled, so two seeds give visibly different openings.
        List<int> cells = grid.PlayableCells()
            .GroupBy(grid.TargetAt)
            .OrderByDescending(group => group.Count())
            .ThenBy(_ => random.Next())
            .SelectMany(group => Shuffled(group.ToList(), random))
            .ToList();

        int n = cells.Count;
        if (n == 0) return spheres;

        int shift = cells.GroupBy(grid.TargetAt).Max(group => group.Count());

        for (int i = 0; i < n; i++)
            spheres[cells[i]] = grid.TargetAt(cells[(i + shift) % n]);

        return spheres;
    }

    /// <summary>True when the sprite admits an opening with no cell already painted.</summary>
    public static bool CanFullyScramble(LevelGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        if (grid.PlayableCount == 0) return true;

        int largest = grid.ColorCounts().Values.Max();
        return largest * 2 <= grid.PlayableCount;
    }

    private static List<int> Shuffled(List<int> items, Random random)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
        return items;
    }
}
