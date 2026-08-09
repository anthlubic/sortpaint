using System.Collections.Generic;
using Godot;
using SortPaint.Core;

namespace SortPaint;

/// <summary>
/// The holding rail along the bottom. One socket per tray slot, filled left to right and
/// grouped by palette index so like colours always sit together. Reports taps as a palette
/// index, so the controller can tell "put what you are holding down here" from "pick this
/// colour back up".
/// </summary>
[GlobalClass]
public partial class TrayView : Control
{
    /// <summary>Tapped with the palette index under the finger, or <see cref="LevelGrid.Empty"/> over a bare socket.</summary>
    [Signal]
    public delegate void TrayTappedEventHandler(int color);

    /// <summary>Reuses the board cell scene: the pixel becomes the hollow socket, the bead the sphere.</summary>
    [Export] public PackedScene SlotScene { get; set; }

    [Export(PropertyHint.Range, "1,32,1")] public int Columns { get; set; } = 12;
    [Export(PropertyHint.Range, "8,96,1")] public int MaxSlotSize { get; set; } = 44;
    [Export(PropertyHint.Range, "0,24,1")] public int SlotGap { get; set; } = 4;
    [Export] public Color SocketColor { get; set; } = new(0.776f, 0.722f, 0.863f);

    private CellView[] _slots;

    /// <summary>Palette index showing in each socket, or <see cref="LevelGrid.Empty"/> when it is bare.</summary>
    private int[] _slotColors;

    private Color[] _palette;
    private bool _emulatesMouseFromTouch = true;

    public int Capacity => _slots?.Length ?? 0;

    /// <summary>Edge length of one socket after fitting the rail to the available room.</summary>
    public float SlotSize { get; private set; }

    public override void _Ready()
    {
        _emulatesMouseFromTouch = (bool)ProjectSettings.GetSetting("input_devices/pointing/emulate_mouse_from_touch", true);
    }

    public void Build(int capacity, Color[] palette)
    {
        if (SlotScene is null)
        {
            GD.PushError("TrayView has no SlotScene assigned.");
            return;
        }

        foreach (Node child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        _palette = palette;
        _slots = new CellView[capacity];
        _slotColors = new int[capacity];

        for (int i = 0; i < capacity; i++)
        {
            var slot = SlotScene.Instantiate<CellView>();
            slot.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(slot);
            slot.SetPixelColor(SocketColor);
            slot.HideSphere();
            _slots[i] = slot;
            _slotColors[i] = LevelGrid.Empty;
        }

        int rows = Mathf.CeilToInt(capacity / (float)Columns);
        CustomMinimumSize = new Vector2(0, rows * MaxSlotSize + (rows + 1) * SlotGap);

        Relayout();
    }

    /// <summary>Fills the sockets from the tray's contents, which arrive ordered by palette index.</summary>
    public void SetContents(IEnumerable<KeyValuePair<int, int>> contents)
    {
        if (_slots is null) return;

        int slot = 0;
        foreach ((int color, int count) in contents)
        {
            for (int i = 0; i < count && slot < _slots.Length; i++, slot++)
            {
                _slots[slot].ShowSphere(_palette[color]);
                _slotColors[slot] = color;
            }
        }

        for (; slot < _slots.Length; slot++)
        {
            _slots[slot].HideSphere();
            _slotColors[slot] = LevelGrid.Empty;
        }
    }

    /// <summary>Raises every bead of one colour, or lowers them all when given no colour.</summary>
    public void SetHoveredColor(int color)
    {
        if (_slots is null) return;

        for (int i = 0; i < _slots.Length; i++)
            _slots[i].SetHovered(color != LevelGrid.Empty && _slotColors[i] == color);
    }

    public Vector2 SlotCenterGlobal(int slot)
    {
        if (_slots is null || _slots.Length == 0) return GetGlobalRect().GetCenter();
        slot = Mathf.Clamp(slot, 0, _slots.Length - 1);
        return _slots[slot].SphereCenterGlobal();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) Relayout();
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

        if (_slots is null) return;

        AcceptEvent();
        EmitSignal(SignalName.TrayTapped, ColorAt(local));
    }

    /// <summary>What is showing under a point on the rail. Anywhere off a filled socket is bare rail.</summary>
    private int ColorAt(Vector2 local)
    {
        for (int i = 0; i < _slots.Length; i++)
            if (new Rect2(_slots[i].Position, _slots[i].Size).HasPoint(local)) return _slotColors[i];

        return LevelGrid.Empty;
    }

    private void Relayout()
    {
        if (_slots is null || _slots.Length == 0) return;

        int columns = Mathf.Min(Columns, _slots.Length);
        int rows = Mathf.CeilToInt(_slots.Length / (float)columns);

        float widthFit = (Size.X - (columns + 1) * SlotGap) / columns;
        float heightFit = (Size.Y - (rows + 1) * SlotGap) / rows;
        float size = Mathf.Max(4f, Mathf.Floor(Mathf.Min(MaxSlotSize, Mathf.Min(widthFit, heightFit))));
        SlotSize = size;

        var block = new Vector2(columns * size + (columns - 1) * SlotGap, rows * size + (rows - 1) * SlotGap);
        Vector2 origin = ((Size - block) * 0.5f).Floor();
        var slotSize = new Vector2(size, size);

        for (int i = 0; i < _slots.Length; i++)
        {
            int column = i % columns;
            int row = i / columns;
            _slots[i].Position = origin + new Vector2(column * (size + SlotGap), row * (size + SlotGap));
            _slots[i].Size = slotSize;
        }
    }
}
