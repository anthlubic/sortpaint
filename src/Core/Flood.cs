using System;
using System.Collections.Generic;

namespace SortPaint.Core;

/// <summary>
/// Eight-way flood fill used to work out how much a single tap affects.
/// </summary>
public static class Flood
{
    /// <summary>Offsets of the eight surrounding cells, orthogonals first then diagonals.</summary>
    private static readonly (int Dx, int Dy)[] Neighbours =
    [
        (-1, 0), (1, 0), (0, -1), (0, 1),
        (-1, -1), (1, -1), (-1, 1), (1, 1),
    ];

    /// <summary>
    /// Collects the connected run of cells reachable from <paramref name="start"/> where every
    /// cell satisfies <paramref name="include"/>. Cells that fail the predicate act as walls, so
    /// two same-coloured patches split by a different colour stay separate regions.
    /// </summary>
    /// <remarks>
    /// Cells touching only at a corner count as connected, so a run can slip through a diagonal
    /// gap. A wall has to be two cells thick on the diagonal to actually separate two patches.
    /// </remarks>
    /// <returns>
    /// Cell indices in breadth-first order from the tap, so callers that can only take part of
    /// the region take the cells nearest the finger first. Empty if the start cell itself fails.
    /// </returns>
    public static List<int> Region(LevelGrid grid, int start, Func<int, bool> include)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(include);

        var region = new List<int>();
        if (start < 0 || start >= grid.CellCount || !include(start)) return region;

        var visited = new bool[grid.CellCount];
        var queue = new Queue<int>();

        visited[start] = true;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            region.Add(index);

            int x = grid.XOf(index);
            int y = grid.YOf(index);

            foreach ((int dx, int dy) in Neighbours) Visit(x + dx, y + dy);
        }

        return region;

        void Visit(int x, int y)
        {
            if (!grid.InBounds(x, y)) return;
            int neighbour = grid.Index(x, y);
            if (visited[neighbour]) return;
            visited[neighbour] = true;
            if (include(neighbour)) queue.Enqueue(neighbour);
        }
    }
}
