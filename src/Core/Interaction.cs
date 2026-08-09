using System;
using System.Collections.Generic;

namespace SortPaint.Core;

/// <summary>
/// The two-step tap game on top of <see cref="BoardState"/>. A tap never moves anything on its
/// own: the first tap lifts a run of spheres, which then hover waiting for somewhere to go, and
/// the second tap says where.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Tap a sphere to lift its whole connected run of that colour.</item>
/// <item>Tap the tray to drop what is hovering into it.</item>
/// <item>Tap a bare cell waiting for that colour to drop them straight onto the picture, whether
/// they are hovering off the board or out of the tray. Going board to board skips the tray
/// entirely, so it costs no tray room.</item>
/// <item>Tap the run that is already hovering (the same spheres on the board, or the tray
/// again) to put it back down and choose something else.</item>
/// </list>
/// Taps that mean nothing leave whatever is hovering alone, so a slip of the finger never costs
/// the player their selection.
/// </remarks>
public sealed class Interaction
{
    private List<int> _hoverCells = [];

    public Interaction(BoardState board)
    {
        ArgumentNullException.ThrowIfNull(board);
        Board = board;
    }

    public BoardState Board { get; }

    public HoverSource Source { get; private set; } = HoverSource.None;

    /// <summary>Palette index of the hovering spheres.</summary>
    public int HoverColor { get; private set; } = LevelGrid.Empty;

    /// <summary>Cells the hovering spheres were lifted from. Empty when they came out of the tray.</summary>
    public IReadOnlyList<int> HoverCells => _hoverCells;

    public int HoverCount { get; private set; }

    public bool IsHovering => Source != HoverSource.None;

    /// <summary>A tap on the picture, at a cell coordinate.</summary>
    public MoveResult TapCell(int x, int y) =>
        Board.Grid.InBounds(x, y) ? TapCell(Board.Grid.Index(x, y)) : MoveResult.Nothing(TapRejection.OutOfBounds);

    public MoveResult TapCell(int index)
    {
        if (index < 0 || index >= Board.Grid.CellCount) return MoveResult.Nothing(TapRejection.OutOfBounds);
        if (Board.Grid.TargetAt(index) == LevelGrid.Empty) return MoveResult.Nothing(TapRejection.NotPlayable);

        return Board.SphereAt(index) == BoardState.Bare ? TapBareCell(index) : TapSphere(index);
    }

    /// <summary>
    /// A tap on the tray rail. <paramref name="color"/> is the palette index under the finger, or
    /// <see cref="LevelGrid.Empty"/> for an empty socket, which still counts as a tap on the tray
    /// when something is hovering, because the whole rail is the target.
    /// </summary>
    public MoveResult TapTray(int color)
    {
        if (Source == HoverSource.Board) return DropIntoTray();
        if (Source == HoverSource.Tray && color == HoverColor) return PutBack();

        if (color == LevelGrid.Empty || Board.Tray.CountOf(color) == 0)
            return MoveResult.Nothing(TapRejection.NoMatchingSpheres);

        return Lift(HoverSource.Tray, color, [], Board.Tray.CountOf(color));
    }

    /// <summary>Puts down whatever is hovering, without moving anything.</summary>
    public MoveResult PutBack()
    {
        if (!IsHovering) return MoveResult.Nothing(TapRejection.None);
        Release();
        return MoveResult.Cleared();
    }

    private MoveResult TapSphere(int index)
    {
        if (Board.IsPainted(index)) return MoveResult.Nothing(TapRejection.AlreadyPainted);

        // Tapping into the run that is already up puts it back down.
        if (Source == HoverSource.Board && _hoverCells.Contains(index)) return PutBack();

        List<int> run = Board.LiftRegion(index);
        if (run.Count == 0) return MoveResult.Nothing(TapRejection.AlreadyPainted);

        return Lift(HoverSource.Board, Board.SphereAt(index), run, run.Count);
    }

    private MoveResult TapBareCell(int index)
    {
        if (!IsHovering) return MoveResult.Nothing(TapRejection.NoMatchingSpheres);
        if (Board.Grid.TargetAt(index) != HoverColor) return MoveResult.Nothing(TapRejection.NoMatchingSpheres);

        return Source == HoverSource.Tray ? DropFromTray(index) : DropAcrossBoard(index);
    }

    /// <summary>Everything hovering comes down into the tray, as far as there is room for it.</summary>
    private MoveResult DropIntoTray()
    {
        // Resolving from the cell the run was lifted at reaches the same run and trims it to the
        // slots left, so the tray's own rules stay the one place that decides what fits.
        TapResult pickup = Board.Resolve(_hoverCells[0]);
        if (!pickup.IsMove) return MoveResult.Nothing(pickup.Rejection);

        Board.Apply(pickup);
        Release();
        return MoveResult.ToTray(pickup.Color, pickup.Cells);
    }

    private MoveResult DropFromTray(int index)
    {
        TapResult place = Board.Resolve(index);
        if (!place.IsMove) return MoveResult.Nothing(place.Rejection);

        Board.Apply(place);
        Release();
        return MoveResult.ToBoard(HoverSource.Tray, place.Color, [], place.Cells);
    }

    /// <summary>The shortcut: spheres go straight from where they were lifted to where they belong.</summary>
    private MoveResult DropAcrossBoard(int index)
    {
        List<int> landing = Board.BareRegion(index);
        int moved = Math.Min(_hoverCells.Count, landing.Count);
        if (moved == 0) return MoveResult.Nothing(TapRejection.NoMatchingSpheres);

        // Both runs are in breadth-first order from their taps, so a partial move takes the
        // spheres nearest the lift and fills the cells nearest the drop.
        List<int> from = _hoverCells.GetRange(0, moved);
        List<int> to = landing.GetRange(0, moved);
        int color = HoverColor;

        Board.MoveOnBoard(color, from, to);
        Release();
        return MoveResult.ToBoard(HoverSource.Board, color, from, to);
    }

    private MoveResult Lift(HoverSource source, int color, List<int> cells, int count)
    {
        Source = source;
        HoverColor = color;
        _hoverCells = cells;
        HoverCount = count;
        return MoveResult.Hovered(source, color, cells, count);
    }

    private void Release()
    {
        Source = HoverSource.None;
        HoverColor = LevelGrid.Empty;
        _hoverCells = [];
        HoverCount = 0;
    }
}
