using System;
using System.Collections.Generic;

namespace SortPaint.Core;

/// <summary>
/// The whole playable state of one level: which sphere sits on each cell, and what is in the tray.
/// Pure logic, no Godot types, so the rules can be unit tested on their own.
/// </summary>
/// <remarks>
/// Two rules drive everything:
/// <list type="bullet">
/// <item>A sphere that is on the wrong colour moves as its whole connected run of that sphere
/// colour, into the tray or straight across the board.</item>
/// <item>A bare cell fills as its whole connected run of bare cells needing that colour.</item>
/// </list>
/// Spheres are only ever placed onto a matching cell, so once a sphere is down it is painted
/// and can never be lifted again. This class is only the rules; <see cref="Interaction"/> is
/// what turns the player's taps into them.
/// </remarks>
public sealed class BoardState
{
    /// <summary>No sphere on this cell.</summary>
    public const int Bare = -1;

    private readonly int[] _spheres;

    public BoardState(LevelGrid grid, IReadOnlyList<int> spheres, int trayCapacity)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(spheres);
        if (spheres.Count != grid.CellCount)
            throw new ArgumentException($"Expected {grid.CellCount} sphere entries, got {spheres.Count}.", nameof(spheres));

        Grid = grid;
        Tray = new Tray(trayCapacity);
        _spheres = new int[spheres.Count];

        for (int i = 0; i < spheres.Count; i++)
        {
            int sphere = spheres[i];
            if (!grid.IsPlayable(i) && sphere != Bare)
                throw new ArgumentException($"Cell {i} is not part of the sprite but carries a sphere.", nameof(spheres));

            _spheres[i] = sphere;
            if (sphere != Bare && sphere == grid.TargetAt(i)) PaintedCount++;
        }
    }

    public LevelGrid Grid { get; }
    public Tray Tray { get; }

    /// <summary>Playable cells whose sphere already matches the picture.</summary>
    public int PaintedCount { get; private set; }

    public bool IsSolved => PaintedCount == Grid.PlayableCount;

    /// <summary>
    /// Whether any tap would still do something. A board can dead-end: once the tray fills with
    /// colours that no bare cell is waiting for, nothing can be lifted and nothing can be placed,
    /// and the level has to be restarted.
    /// </summary>
    public bool HasLegalMove
    {
        get
        {
            bool roomToLift = !Tray.IsFull;

            for (int i = 0; i < Grid.CellCount; i++)
            {
                int target = Grid.TargetAt(i);
                if (target == LevelGrid.Empty) continue;

                int sphere = _spheres[i];
                if (sphere == Bare)
                {
                    if (Tray.CountOf(target) > 0) return true;
                }
                else if (roomToLift && sphere != target)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public int SphereAt(int index) => _spheres[index];
    public int SphereAt(int x, int y) => _spheres[Grid.Index(x, y)];

    /// <summary>A sphere sitting on its own colour. Painted spheres never move again.</summary>
    public bool IsPainted(int index) => _spheres[index] != Bare && _spheres[index] == Grid.TargetAt(index);

    /// <summary>
    /// Works out what tapping a cell would do, without changing anything.
    /// </summary>
    public TapResult Resolve(int x, int y) =>
        Grid.InBounds(x, y) ? Resolve(Grid.Index(x, y)) : TapResult.Rejected(TapRejection.OutOfBounds);

    public TapResult Resolve(int index)
    {
        if (index < 0 || index >= Grid.CellCount) return TapResult.Rejected(TapRejection.OutOfBounds);

        int target = Grid.TargetAt(index);
        if (target == LevelGrid.Empty) return TapResult.Rejected(TapRejection.NotPlayable);

        int sphere = _spheres[index];
        return sphere == Bare ? ResolvePlace(index, target) : ResolvePickup(index, sphere, target);
    }

    /// <summary>
    /// The connected run of spheres that would come up with the one on <paramref name="index"/>.
    /// Empty when that cell is bare or already painted, since painted spheres never move again.
    /// </summary>
    public List<int> LiftRegion(int index)
    {
        if (index < 0 || index >= Grid.CellCount) return [];

        int sphere = _spheres[index];
        if (sphere == Bare || IsPainted(index)) return [];

        // Painted spheres are excluded, which also makes them walls the fill cannot cross.
        return Flood.Region(Grid, index, cell => _spheres[cell] == sphere && !IsPainted(cell));
    }

    /// <summary>
    /// The connected run of bare cells waiting for the same colour as <paramref name="index"/>.
    /// Empty when that cell is not itself a bare cell of the picture.
    /// </summary>
    public List<int> BareRegion(int index)
    {
        if (index < 0 || index >= Grid.CellCount) return [];

        int target = Grid.TargetAt(index);
        if (target == LevelGrid.Empty || _spheres[index] != Bare) return [];

        return Flood.Region(Grid, index, cell => _spheres[cell] == Bare && Grid.TargetAt(cell) == target);
    }

    private TapResult ResolvePickup(int index, int sphere, int target)
    {
        if (sphere == target) return TapResult.Rejected(TapRejection.AlreadyPainted);
        if (Tray.IsFull) return TapResult.Rejected(TapRejection.TrayFull);

        return TapResult.Pickup(sphere, Trim(LiftRegion(index), Tray.FreeSlots));
    }

    private TapResult ResolvePlace(int index, int target)
    {
        int held = Tray.CountOf(target);
        if (held == 0) return TapResult.Rejected(TapRejection.NoMatchingSpheres);

        return TapResult.Place(target, Trim(BareRegion(index), held));
    }

    /// <summary>Keeps the cells nearest the tap when the whole region will not fit.</summary>
    private static List<int> Trim(List<int> region, int limit)
    {
        if (region.Count > limit) region.RemoveRange(limit, region.Count - limit);
        return region;
    }

    public void Apply(TapResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.IsMove) return;

        switch (result.Kind)
        {
            case TapKind.Pickup:
                foreach (int cell in result.Cells) _spheres[cell] = Bare;
                Tray.Add(result.Color, result.Count);
                break;

            case TapKind.Place:
                foreach (int cell in result.Cells) _spheres[cell] = result.Color;
                Tray.Remove(result.Color, result.Count);
                PaintedCount += result.Count;
                break;
        }
    }

    /// <summary>
    /// Carries spheres straight from one run of cells to another without going through the tray,
    /// which is what lets a run be dropped where it belongs in one move and at no cost in tray
    /// room. Every destination cell is waiting for <paramref name="color"/>, so all of them end
    /// up painted.
    /// </summary>
    public void MoveOnBoard(int color, IReadOnlyList<int> from, IReadOnlyList<int> to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        if (from.Count != to.Count)
            throw new ArgumentException($"Cannot move {from.Count} spheres onto {to.Count} cells.", nameof(to));

        foreach (int cell in from)
        {
            if (_spheres[cell] != color)
                throw new InvalidOperationException($"Cell {cell} does not carry a sphere of colour {color}.");
            if (IsPainted(cell))
                throw new InvalidOperationException($"Cell {cell} is painted, so its sphere cannot move.");
        }

        foreach (int cell in to)
        {
            if (_spheres[cell] != Bare)
                throw new InvalidOperationException($"Cell {cell} already carries a sphere.");
            if (Grid.TargetAt(cell) != color)
                throw new InvalidOperationException($"Cell {cell} is not waiting for colour {color}.");
        }

        foreach (int cell in from) _spheres[cell] = Bare;

        foreach (int cell in to)
        {
            _spheres[cell] = color;
            PaintedCount++;
        }
    }

    /// <summary>Resolves a tap and applies it in one go.</summary>
    public TapResult Tap(int x, int y)
    {
        TapResult result = Resolve(x, y);
        Apply(result);
        return result;
    }

    /// <summary>Snapshot of the board, for views that want to rebuild from scratch.</summary>
    public IReadOnlyList<int> Spheres => _spheres;
}
