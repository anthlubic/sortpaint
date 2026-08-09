using System;
using System.Collections.Generic;
using SortPaint.Core;

namespace SortPaint.Tests;

/// <summary>
/// Builds boards from little ASCII pictures. '.' is a hole in the sprite (or a bare cell when
/// describing spheres); any lowercase letter is a colour, so "aab" reads as red red blue.
/// </summary>
internal static class Boards
{
    public const char Hole = '.';

    public static int Color(char symbol) => symbol == Hole ? LevelGrid.Empty : symbol - 'a';

    public static LevelGrid Grid(params string[] rows)
    {
        int width = rows[0].Length;
        var targets = new List<int>(width * rows.Length);

        foreach (string row in rows)
        {
            if (row.Length != width)
                throw new ArgumentException($"Row \"{row}\" is {row.Length} wide, expected {width}.", nameof(rows));

            foreach (char symbol in row) targets.Add(Color(symbol));
        }

        return new LevelGrid(width, rows.Length, targets);
    }

    public static BoardState State(string[] target, string[] spheres, int capacity = 24)
    {
        LevelGrid grid = Grid(target);
        var beads = new List<int>(grid.CellCount);

        foreach (string row in spheres)
            foreach (char symbol in row)
                beads.Add(symbol == Hole ? BoardState.Bare : symbol - 'a');

        return new BoardState(grid, beads, capacity);
    }

    /// <summary>Renders the spheres back into ASCII, for whole-board assertions.</summary>
    public static string[] Spheres(BoardState state)
    {
        var rows = new string[state.Grid.Height];

        for (int y = 0; y < state.Grid.Height; y++)
        {
            var symbols = new char[state.Grid.Width];
            for (int x = 0; x < state.Grid.Width; x++)
            {
                int sphere = state.SphereAt(x, y);
                symbols[x] = sphere == BoardState.Bare ? Hole : (char)('a' + sphere);
            }
            rows[y] = new string(symbols);
        }

        return rows;
    }

    public static Dictionary<int, int> Tally(IEnumerable<int> spheres)
    {
        var counts = new Dictionary<int, int>();
        foreach (int sphere in spheres)
        {
            if (sphere == BoardState.Bare) continue;
            counts.TryGetValue(sphere, out int seen);
            counts[sphere] = seen + 1;
        }
        return counts;
    }
}
