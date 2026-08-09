using System;
using System.Collections.Generic;

namespace SortPaint.Core;

/// <summary>Where a run of hovering spheres was lifted from.</summary>
public enum HoverSource
{
    None,

    /// <summary>A connected run of spheres lifted off the picture.</summary>
    Board,

    /// <summary>Every sphere of one colour held in the tray.</summary>
    Tray,
}

public enum MoveKind
{
    /// <summary>The tap changed nothing. <see cref="MoveResult.Rejection"/> says why.</summary>
    None,

    /// <summary>Spheres were lifted and are now waiting for somewhere to go.</summary>
    Hovered,

    /// <summary>The hovering spheres were put back where they came from.</summary>
    Cleared,

    /// <summary>Hovering spheres came down into the tray.</summary>
    ToTray,

    /// <summary>Hovering spheres came down onto the picture, from the tray or straight across the board.</summary>
    ToBoard,
}

/// <summary>
/// What one tap did, once the two-step lift-then-drop interaction has had its say. Holds every
/// cell that changed so the view can animate exactly the spheres the model just moved.
/// </summary>
public sealed class MoveResult
{
    private static readonly int[] NoCells = [];

    private MoveResult(MoveKind kind, HoverSource source, int color, IReadOnlyList<int> from, IReadOnlyList<int> to, int count, TapRejection rejection)
    {
        Kind = kind;
        Source = source;
        Color = color;
        From = from;
        To = to;
        Count = count;
        Rejection = rejection;
    }

    public MoveKind Kind { get; }

    /// <summary>Where the spheres involved were lifted from.</summary>
    public HoverSource Source { get; }

    /// <summary>Palette index of the spheres that moved, or that are now hovering.</summary>
    public int Color { get; }

    /// <summary>Board cells the spheres left, nearest the tap first. Empty when the tray is the source.</summary>
    public IReadOnlyList<int> From { get; }

    /// <summary>Board cells the spheres landed on, nearest the tap first. Empty when the tray is the destination.</summary>
    public IReadOnlyList<int> To { get; }

    /// <summary>How many spheres moved, or how many are hovering.</summary>
    public int Count { get; }

    /// <summary>Why nothing happened. Only meaningful when <see cref="Kind"/> is <see cref="MoveKind.None"/>.</summary>
    public TapRejection Rejection { get; }

    public bool IsMove => Kind is MoveKind.ToTray or MoveKind.ToBoard;

    public static MoveResult Nothing(TapRejection reason) =>
        new(MoveKind.None, HoverSource.None, LevelGrid.Empty, NoCells, NoCells, 0, reason);

    public static MoveResult Cleared() =>
        new(MoveKind.Cleared, HoverSource.None, LevelGrid.Empty, NoCells, NoCells, 0, TapRejection.None);

    public static MoveResult Hovered(HoverSource source, int color, IReadOnlyList<int> cells, int count)
    {
        ArgumentNullException.ThrowIfNull(cells);
        return new MoveResult(MoveKind.Hovered, source, color, cells, NoCells, count, TapRejection.None);
    }

    public static MoveResult ToTray(int color, IReadOnlyList<int> from)
    {
        ArgumentNullException.ThrowIfNull(from);
        return new MoveResult(MoveKind.ToTray, HoverSource.Board, color, from, NoCells, from.Count, TapRejection.None);
    }

    public static MoveResult ToBoard(HoverSource source, int color, IReadOnlyList<int> from, IReadOnlyList<int> to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        return new MoveResult(MoveKind.ToBoard, source, color, from, to, to.Count, TapRejection.None);
    }
}
