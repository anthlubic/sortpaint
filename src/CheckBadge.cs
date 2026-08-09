using Godot;

namespace SortPaint;

/// <summary>
/// The tick that marks a finished level. Drawn rather than imported so it stays crisp at any tile
/// size, with its look exposed as inspector properties.
/// </summary>
[Tool]
[GlobalClass]
public partial class CheckBadge : Control
{
    private Color _badgeColor = new(0.42f, 0.796f, 0.62f);
    private Color _rimColor = new(1f, 0.996f, 1f);
    private Color _markColor = new(1f, 1f, 1f);
    private float _markThickness = 0.14f;
    private float _rimThickness = 0.1f;

    [Export]
    public Color BadgeColor
    {
        get => _badgeColor;
        set { _badgeColor = value; QueueRedraw(); }
    }

    /// <summary>A ring in the tile's own colour, so the badge still reads over a busy corner of the picture.</summary>
    [Export]
    public Color RimColor
    {
        get => _rimColor;
        set { _rimColor = value; QueueRedraw(); }
    }

    [Export]
    public Color MarkColor
    {
        get => _markColor;
        set { _markColor = value; QueueRedraw(); }
    }

    /// <summary>Stroke widths are fractions of the badge, so everything scales together.</summary>
    [Export(PropertyHint.Range, "0.02,0.3,0.005")]
    public float MarkThickness
    {
        get => _markThickness;
        set { _markThickness = value; QueueRedraw(); }
    }

    [Export(PropertyHint.Range, "0,0.2,0.005")]
    public float RimThickness
    {
        get => _rimThickness;
        set { _rimThickness = value; QueueRedraw(); }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) QueueRedraw();
    }

    public override void _Draw()
    {
        float side = Mathf.Min(Size.X, Size.Y);
        if (side <= 0f) return;

        Vector2 center = Size * 0.5f;
        float radius = side * 0.5f;

        if (RimThickness > 0f) DrawCircle(center, radius, RimColor);
        DrawCircle(center, radius - side * RimThickness, BadgeColor);

        // Three points of a tick, in fractions of the badge box.
        Vector2[] mark =
        [
            center + new Vector2(-0.22f, 0.02f) * side,
            center + new Vector2(-0.06f, 0.18f) * side,
            center + new Vector2(0.24f, -0.16f) * side,
        ];

        DrawPolyline(mark, MarkColor, side * MarkThickness, antialiased: true);
    }
}
