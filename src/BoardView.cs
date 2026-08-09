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

    private LevelGrid _grid;
    private Color[] _palette;
    private CellView[] _cells;
    private bool _emulatesMouseFromTouch = true;

    /// <summary>Edge length of one cell in pixels, after fitting the grid to the available room.</summary>
    public float CellSize { get; private set; }

    /// <summary>Top-left of the grid within this control, once centred.</summary>
    public Vector2 BoardOrigin { get; private set; }

    public override void _Ready()
    {
        _emulatesMouseFromTouch = (bool)ProjectSettings.GetSetting("input_devices/pointing/emulate_mouse_from_touch", true);
    }

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

    /// <summary>Raises a bead out of its divot, to show it is picked up and waiting for a home.</summary>
    public void SetHovered(int index, bool hovered)
    {
        if (_cells is not null && index >= 0 && index < _cells.Length) _cells[index]?.SetHovered(hovered);
    }

    public void ClearHover()
    {
        if (_cells is null) return;
        foreach (CellView cell in _cells) cell?.SetHovered(false);
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
        CellSize = Mathf.Max(1f, Mathf.Floor(Mathf.Min(fitted, MaxCellSize)));

        var gridSize = new Vector2(CellSize * _grid.Width, CellSize * _grid.Height);
        BoardOrigin = ((Size - gridSize) * 0.5f).Floor();

        var cellSize = new Vector2(CellSize, CellSize);
        for (int i = 0; i < _cells.Length; i++)
        {
            CellView cell = _cells[i];
            if (cell is null) continue;

            cell.Position = BoardOrigin + new Vector2(_grid.XOf(i) * CellSize, _grid.YOf(i) * CellSize);
            cell.Size = cellSize;
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        Vector2 local;
        switch (@event)
        {
            case InputEventMouseButton mouse when mouse.Pressed && mouse.ButtonIndex == MouseButton.Left:
                local = mouse.Position;
                break;

            // Only when Godot is not already synthesising a mouse click from the same touch.
            case InputEventScreenTouch touch when touch.Pressed && !_emulatesMouseFromTouch:
                local = touch.Position;
                break;

            default:
                return;
        }

        if (_grid is null || CellSize <= 0f) return;

        Vector2 onGrid = local - BoardOrigin;
        int x = (int)Mathf.Floor(onGrid.X / CellSize);
        int y = (int)Mathf.Floor(onGrid.Y / CellSize);
        if (!_grid.InBounds(x, y)) return;

        AcceptEvent();
        EmitSignal(SignalName.CellTapped, x, y);
    }
}
