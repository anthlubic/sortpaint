using Godot;

namespace SortPaint;

/// <summary>
/// The padlock on a level that has not been earned yet. Drawn rather than imported, like
/// <see cref="TrophyBadge"/>, so it stays crisp at whatever size a tile ends up and its look is
/// editable in the inspector.
/// </summary>
[Tool]
[GlobalClass]
public partial class LockBadge : Control
{
    private Color _badgeColor = new(0.376f, 0.353f, 0.42f);
    private Color _rimColor = new(1f, 0.996f, 1f);
    private Color _lockColor = new(1f, 0.996f, 1f);
    private float _diameter = 0.6f;
    private float _shackleThickness = 0.075f;
    private float _rimThickness = 0.08f;

    /// <summary>The disc the padlock sits on, so it reads over a busy picture.</summary>
    [Export]
    public Color BadgeColor
    {
        get => _badgeColor;
        set { _badgeColor = value; QueueRedraw(); }
    }

    [Export]
    public Color RimColor
    {
        get => _rimColor;
        set { _rimColor = value; QueueRedraw(); }
    }

    [Export]
    public Color LockColor
    {
        get => _lockColor;
        set { _lockColor = value; QueueRedraw(); }
    }

    /// <summary>
    /// How much of the box the badge fills. A tile hands the whole square over, so the badge is
    /// centred in the picture at whatever size looks right rather than pinned to a corner.
    /// </summary>
    [Export(PropertyHint.Range, "0.1,1,0.01")]
    public float Diameter
    {
        get => _diameter;
        set { _diameter = value; QueueRedraw(); }
    }

    /// <summary>Stroke widths are fractions of the badge, so everything scales together.</summary>
    [Export(PropertyHint.Range, "0.02,0.2,0.005")]
    public float ShackleThickness
    {
        get => _shackleThickness;
        set { _shackleThickness = value; QueueRedraw(); }
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
        float side = Mathf.Min(Size.X, Size.Y) * Diameter;
        if (side <= 0f) return;

        Vector2 center = Size * 0.5f;
        float radius = side * 0.5f;

        if (RimThickness > 0f) DrawCircle(center, radius, RimColor);
        DrawCircle(center, radius - side * RimThickness, BadgeColor);

        // The padlock, in fractions of the badge box: a shackle over a body with a keyhole in it.
        DrawArc(
            center + new Vector2(0f, -0.09f) * side,
            side * 0.145f,
            Mathf.Pi,
            Mathf.Tau,
            16,
            LockColor,
            side * ShackleThickness,
            antialiased: true);

        DrawRect(
            new Rect2(center + new Vector2(-0.19f, -0.04f) * side, new Vector2(0.38f, 0.26f) * side),
            LockColor);

        DrawCircle(center + new Vector2(0f, 0.09f) * side, side * 0.045f, BadgeColor);
    }
}
