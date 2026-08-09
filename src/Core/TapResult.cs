using System;
using System.Collections.Generic;

namespace SortPaint.Core;

public enum TapKind
{
    /// <summary>The tap did nothing. <see cref="TapResult.Rejection"/> says why.</summary>
    None,

    /// <summary>Spheres travel from the board down to the tray.</summary>
    Pickup,

    /// <summary>Spheres travel from the tray up onto the board.</summary>
    Place,
}

public enum TapRejection
{
    None,
    OutOfBounds,

    /// <summary>The cell is a hole in the sprite, not part of the picture.</summary>
    NotPlayable,

    /// <summary>The sphere already sits on its matching colour, so it is locked in place.</summary>
    AlreadyPainted,

    /// <summary>Nowhere to put the spheres: the tray is at capacity.</summary>
    TrayFull,

    /// <summary>The cell is bare but the tray holds no sphere of the colour it needs.</summary>
    NoMatchingSpheres,
}

/// <summary>
/// What a single tap resolves to, before it is applied. Holds every cell that moves so the
/// view can animate exactly the spheres the model is about to change.
/// </summary>
public sealed class TapResult
{
    private static readonly int[] NoCells = [];

    private TapResult(TapKind kind, TapRejection rejection, int color, IReadOnlyList<int> cells)
    {
        Kind = kind;
        Rejection = rejection;
        Color = color;
        Cells = cells;
    }

    public TapKind Kind { get; }

    /// <summary>Why nothing happened. Only meaningful when <see cref="Kind"/> is <see cref="TapKind.None"/>.</summary>
    public TapRejection Rejection { get; }

    /// <summary>Palette index of the spheres that move.</summary>
    public int Color { get; }

    /// <summary>Cells that change, nearest the tap first.</summary>
    public IReadOnlyList<int> Cells { get; }

    public int Count => Cells.Count;
    public bool IsMove => Kind != TapKind.None;

    public static TapResult Rejected(TapRejection reason) => new(TapKind.None, reason, LevelGrid.Empty, NoCells);

    public static TapResult Pickup(int color, IReadOnlyList<int> cells) => Move(TapKind.Pickup, color, cells);

    public static TapResult Place(int color, IReadOnlyList<int> cells) => Move(TapKind.Place, color, cells);

    private static TapResult Move(TapKind kind, int color, IReadOnlyList<int> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        if (cells.Count == 0) throw new ArgumentException("A move must affect at least one cell.", nameof(cells));
        return new TapResult(kind, TapRejection.None, color, cells);
    }
}
