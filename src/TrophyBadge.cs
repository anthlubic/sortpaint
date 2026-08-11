using Godot;

namespace SortPaint;

/// <summary>
/// The trophy a finished level keeps in its corner: gold when the best round came in on par, silver
/// when it took longer. Drawn rather than imported so it stays crisp at any tile size, with its look
/// exposed as inspector properties.
/// </summary>
/// <remarks>
/// The two metals are the same drawing in different colours, so nothing here knows which one it is.
/// <see cref="LevelTileView"/> hands it the colour, which keeps the rule about what earns gold in
/// one place instead of split between the tile and the badge.
/// </remarks>
[Tool]
[GlobalClass]
public partial class TrophyBadge : Control
{
    private Color _badgeColor = new(0.898f, 0.706f, 0.204f);
    private Color _rimColor = new(1f, 0.996f, 1f);
    private Color _trophyColor = new(1f, 1f, 1f);
    private float _handleThickness = 0.07f;
    private float _rimThickness = 0.1f;

    /// <summary>The disc the cup sits on. Gold or silver, which is the whole point of the badge.</summary>
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
    public Color TrophyColor
    {
        get => _trophyColor;
        set { _trophyColor = value; QueueRedraw(); }
    }

    /// <summary>Stroke widths are fractions of the badge, so everything scales together.</summary>
    [Export(PropertyHint.Range, "0.02,0.15,0.005")]
    public float HandleThickness
    {
        get => _handleThickness;
        set { _handleThickness = value; QueueRedraw(); }
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

        // The cup, in fractions of the badge box: wide at the rim, pinched in at the waist, which
        // is what reads as a trophy rather than a bucket once the badge is only a few pixels wide.
        DrawColoredPolygon(
        [
            At(center, side, -0.19f, -0.21f),
            At(center, side, 0.19f, -0.21f),
            At(center, side, 0.155f, -0.085f),
            At(center, side, 0.08f, 0.02f),
            At(center, side, -0.08f, 0.02f),
            At(center, side, -0.155f, -0.085f),
        ], TrophyColor);

        // Handles, one arc a side. They break the silhouette out past the cup, which is most of
        // what tells a trophy from a plain goblet at a glance.
        float handle = side * HandleThickness;
        DrawArc(At(center, side, -0.175f, -0.15f), side * 0.087f, Mathf.Pi / 2f, Mathf.Pi * 1.5f, 12, TrophyColor, handle, antialiased: true);
        DrawArc(At(center, side, 0.175f, -0.15f), side * 0.087f, -Mathf.Pi / 2f, Mathf.Pi / 2f, 12, TrophyColor, handle, antialiased: true);

        // Stem, then a foot wide enough to sit the whole thing down. The foot is what stops the
        // cup reading as an hourglass once the badge is down to a corner of a tile.
        DrawRect(new Rect2(At(center, side, -0.045f, 0.02f), new Vector2(0.09f, 0.085f) * side), TrophyColor);
        DrawColoredPolygon(
        [
            At(center, side, -0.105f, 0.105f),
            At(center, side, 0.105f, 0.105f),
            At(center, side, 0.175f, 0.205f),
            At(center, side, -0.175f, 0.205f),
        ], TrophyColor);
    }

    /// <summary>A point of the drawing, given in fractions of the badge so it scales with the tile.</summary>
    private static Vector2 At(Vector2 center, float side, float x, float y) =>
        center + new Vector2(x, y) * side;
}
