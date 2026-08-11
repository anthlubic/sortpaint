using System.Collections.Generic;
using Godot;
using SortPaint.Core;

namespace SortPaint;

/// <summary>
/// Draws the picture as a grid of <see cref="CellView"/> squares and turns pointer input into
/// cell coordinates. Knows nothing about the rules: it reports taps and shows what it is told.
/// </summary>
[GlobalClass]
public partial class BoardView : Control
{
    [Signal]
    public delegate void CellTappedEventHandler(int x, int y);

    [Export] public PackedScene CellScene { get; set; }

    /// <summary>Cells never grow past this, so a small sprite stays chunky instead of filling the screen.</summary>
    [Export(PropertyHint.Range, "4,128,1")]
    public int MaxCellSize { get; set; } = 44;

    [ExportGroup("Pinch")]

    /// <summary>How far two fingers can open the picture up. 1 turns pinching off.</summary>
    [Export(PropertyHint.Range, "1,8,0.1")]
    public float MaxZoom { get; set; } = 4f;

    /// <summary>A finger that slides further than this before lifting was a drag, not a tap.</summary>
    [Export(PropertyHint.Range, "0,64,1")]
    public float TapSlop { get; set; } = 12f;

    private const int NoTouch = -1;

    /// <summary>Godot's <c>InputEvent.DEVICE_ID_EMULATION</c>, which marks an event it made up itself.</summary>
    private const int EmulatedDevice = -1;

    private LevelGrid _grid;
    private Color[] _palette;
    private CellView[] _cells;

    private readonly BoardZoom _zoom = new();
    private readonly PinchTracker _pinch = new();

    /// <summary>The lone finger that might still turn out to be a tap, and where it landed.</summary>
    private int _tapTouch = NoTouch;
    private Vector2 _tapStart;

    /// <summary>Edge length of one cell in pixels, after fitting the grid to the available room.</summary>
    public float CellSize { get; private set; }

    /// <summary>Top-left of the grid within this control, once centred and pinched.</summary>
    public Vector2 BoardOrigin { get; private set; }

    public void Build(LoadedLevel level)
    {
        if (CellScene is null)
        {
            GD.PushError("BoardView has no CellScene assigned.");
            return;
        }

        foreach (Node child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        _grid = level.Grid;
        _palette = level.Palette;
        _cells = new CellView[_grid.CellCount];

        // A new picture arrives fitted and centred, whatever the last one was left pinched to.
        _zoom.Reset();
        _pinch.Clear();
        _tapTouch = NoTouch;

        for (int i = 0; i < _grid.CellCount; i++)
        {
            if (!_grid.IsPlayable(i)) continue;

            var cell = CellScene.Instantiate<CellView>();
            // Cells must not swallow input; the board resolves taps itself.
            cell.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(cell);
            cell.SetPixelColor(_palette[_grid.TargetAt(i)]);
            cell.HideSphere();
            _cells[i] = cell;
        }

        Relayout();
    }

    /// <summary>Redraws every sphere from the model. Used on build, restart and undo.</summary>
    public void Refresh(BoardState state)
    {
        if (_cells is null) return;

        for (int i = 0; i < _cells.Length; i++)
        {
            CellView cell = _cells[i];
            if (cell is null) continue;

            int sphere = state.SphereAt(i);
            if (sphere == BoardState.Bare) cell.HideSphere();
            else cell.ShowSphere(_palette[sphere]);
        }
    }

    public void ShowSphere(int index, int color)
    {
        if (color >= 0 && color < _palette.Length) _cells[index]?.ShowSphere(_palette[color]);
    }

    public void HideSphere(int index) => _cells[index]?.HideSphere();

    /// <summary>
    /// Raises exactly these beads out of their divots, to show they are picked up and waiting for
    /// a home, and puts every other one down. Beads that are already up stay up without a flicker,
    /// which is what lets the highlight be redrawn whole after every tap.
    /// </summary>
    public void SetHoveredCells(IReadOnlyCollection<int> cells)
    {
        if (_cells is null) return;

        var raised = cells as HashSet<int> ?? new HashSet<int>(cells);
        for (int i = 0; i < _cells.Length; i++) _cells[i]?.SetHovered(raised.Contains(i));
    }

    public Vector2 CellCenterGlobal(int index)
    {
        CellView cell = _cells?[index];
        if (cell is not null) return cell.SphereCenterGlobal();

        return GlobalPosition + BoardOrigin
             + new Vector2((_grid.XOf(index) + 0.5f) * CellSize, (_grid.YOf(index) + 0.5f) * CellSize);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) Relayout();
    }

    private void Relayout()
    {
        if (_grid is null || _cells is null) return;

        float fitted = Mathf.Min(Size.X / _grid.Width, Size.Y / _grid.Height);
        float restingCell = Mathf.Max(1f, Mathf.Floor(Mathf.Min(fitted, MaxCellSize)));

        _zoom.MaxZoom = MaxZoom;
        _zoom.SetLayout(Size.X, Size.Y, restingCell * _grid.Width, restingCell * _grid.Height);

        CellSize = restingCell * _zoom.Zoom;
        BoardOrigin = new Vector2(_zoom.OriginX, _zoom.OriginY);

        for (int i = 0; i < _cells.Length; i++)
        {
            CellView cell = _cells[i];
            if (cell is null) continue;

            // Each cell is snapped to whole pixels by its edges rather than its corner and size,
            // so a half-open zoom leaves no seams or overlaps between neighbours.
            float left = BoardOrigin.X + _grid.XOf(i) * CellSize;
            float top = BoardOrigin.Y + _grid.YOf(i) * CellSize;
            var corner = new Vector2(Mathf.Round(left), Mathf.Round(top));

            cell.Position = corner;
            cell.Size = new Vector2(Mathf.Round(left + CellSize), Mathf.Round(top + CellSize)) - corner;
        }
    }

    /// <summary>
    /// Presses land here, where the GUI has already worked out that this board is the thing
    /// under the finger. Everything after the press is followed in <see cref="_Input"/>.
    /// </summary>
    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            // Godot synthesises a click from the first finger down, and it arrives ahead of the
            // touch it was made from. Those are left to the touch handling, which can still tell
            // a tap from the opening of a pinch; only a real mouse taps on the way down.
            case InputEventMouseButton mouse when mouse.Pressed && mouse.ButtonIndex == MouseButton.Left:
                if (mouse.Device == EmulatedDevice) return;
                TapAt(mouse.Position);
                return;

            case InputEventScreenTouch touch when touch.Pressed:
                AcceptEvent();
                BeginTouch(touch.Index, touch.Position);
                return;
        }
    }

    /// <summary>
    /// Fingers are followed here once they are down: a pinch soon wanders off the board, and a
    /// second finger's lift is not routed back to the control it started on.
    /// </summary>
    public override void _Input(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventScreenDrag drag when _pinch.IsTracking(drag.Index):
                DragTouch(drag.Index, ToLocal(drag.Position));
                return;

            case InputEventScreenTouch touch when !touch.Pressed && _pinch.IsTracking(touch.Index):
                EndTouch(touch.Index);
                return;

            // A trackpad pinch, which is the same gesture without the fingers on the glass.
            case InputEventMagnifyGesture magnify:
                Vector2 spot = ToLocal(magnify.Position);
                if (new Rect2(Vector2.Zero, Size).HasPoint(spot)) Pinch(magnify.Factor, spot, Vector2.Zero);
                return;
        }
    }

    private void BeginTouch(int index, Vector2 local)
    {
        // The second finger turns what was going to be a tap into a gesture.
        if (_pinch.Down(index, Point(local))) _tapTouch = NoTouch;
        else
        {
            _tapTouch = index;
            _tapStart = local;
        }
    }

    private void DragTouch(int index, Vector2 local)
    {
        if (_tapTouch == index && local.DistanceTo(_tapStart) > TapSlop) _tapTouch = NoTouch;

        if (_pinch.Move(index, Point(local)) is PinchDelta delta)
            Pinch(delta.Scale, new Vector2(delta.Focus.X, delta.Focus.Y), new Vector2(delta.Drag.X, delta.Drag.Y));
    }

    private void EndTouch(int index)
    {
        _pinch.Up(index);
        if (_tapTouch != index) return;

        // The tap lands where the finger came down, not where it drifted to before lifting.
        _tapTouch = NoTouch;
        TapAt(_tapStart);
    }

    private void Pinch(float scale, Vector2 focus, Vector2 drag)
    {
        bool moved = _zoom.ZoomBy(scale, focus.X, focus.Y);
        moved |= _zoom.PanBy(drag.X, drag.Y);
        if (moved) Relayout();
    }

    private void TapAt(Vector2 local)
    {
        if (_grid is null || CellSize <= 0f) return;

        Vector2 onGrid = local - BoardOrigin;
        int x = (int)Mathf.Floor(onGrid.X / CellSize);
        int y = (int)Mathf.Floor(onGrid.Y / CellSize);
        if (!_grid.InBounds(x, y)) return;

        AcceptEvent();
        EmitSignal(SignalName.CellTapped, x, y);
    }

    /// <summary>Viewport coordinates, as the raw input events carry, into this control's own.</summary>
    private Vector2 ToLocal(Vector2 viewportPoint) => GetGlobalTransformWithCanvas().AffineInverse() * viewportPoint;

    private static System.Numerics.Vector2 Point(Vector2 local) => new(local.X, local.Y);
}
