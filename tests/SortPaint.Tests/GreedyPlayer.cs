using System.Collections.Generic;
using SortPaint.Core;

namespace SortPaint.Tests;

/// <summary>
/// A stand-in player, used to check that levels can actually be finished under the rules.
/// </summary>
/// <remarks>
/// It follows the chain the puzzle sets up: lift a colour, that leaves cells wanting some other
/// colour, so go and lift that one next, then place it. Placing first and lifting as little as
/// possible keeps room in the tray, which is what avoids the dead end.
/// </remarks>
internal static class GreedyPlayer
{
    public static bool Solve(BoardState state, int moveLimit = 5000)
    {
        int moves = 0;
        while (!state.IsSolved && moves++ < moveLimit)
            if (!Step(state)) break;

        return state.IsSolved;
    }

    private static bool Step(BoardState state)
    {
        // Placing paints cells and frees tray slots, so it always comes first.
        TapResult best = Best(state, TapKind.Place, preferLarger: true, wanted: null);
        if (best is null)
        {
            // Otherwise lift a colour that some bare cell is already waiting for, so the very
            // next step can put it down again.
            best = Best(state, TapKind.Pickup, preferLarger: false, wanted: ColoursWanted(state))
                ?? Best(state, TapKind.Pickup, preferLarger: false, wanted: null);
        }

        if (best is null) return false;

        state.Apply(best);
        return true;
    }

    private static HashSet<int> ColoursWanted(BoardState state)
    {
        var wanted = new HashSet<int>();
        for (int cell = 0; cell < state.Grid.CellCount; cell++)
            if (state.Grid.IsPlayable(cell) && state.SphereAt(cell) == BoardState.Bare)
                wanted.Add(state.Grid.TargetAt(cell));

        return wanted;
    }

    private static TapResult Best(BoardState state, TapKind kind, bool preferLarger, HashSet<int> wanted)
    {
        TapResult best = null;

        for (int cell = 0; cell < state.Grid.CellCount; cell++)
        {
            TapResult result = state.Resolve(cell);
            if (result.Kind != kind) continue;
            if (wanted is not null && !wanted.Contains(result.Color)) continue;

            if (best is null || (preferLarger ? result.Count > best.Count : result.Count < best.Count))
                best = result;
        }

        return best;
    }
}
