using Godot;

namespace SortPaint;

/// <summary>
/// One square of the picture: the painted pixel with its divot, and a bead sitting in it.
/// Also does duty as a tray socket, where the pixel is the empty hollow.
/// </summary>
[GlobalClass]
public partial class CellView : Control
{
    [Export] public ColorRect Pixel { get; set; }
    [Export] public ColorRect Sphere { get; set; }

    /// <summary>How far a hovering bead rises out of its divot, as a fraction of the cell.</summary>
    [Export(PropertyHint.Range, "0,0.6,0.01")] public float HoverLift { get; set; } = 0.24f;

    /// <summary>Hovering beads grow a little as well as rise, so they read as nearer.</summary>
    [Export(PropertyHint.Range, "1,1.5,0.01")] public float HoverScale { get; set; } = 1.12f;

    [Export(PropertyHint.Range, "0.01,0.6,0.01")] public float HoverDuration { get; set; } = 0.11f;

    /// <summary>A raised bead leans up into the cell above, so it has to draw in front of the squares.</summary>
    private const int RaisedLayer = 1;

    private bool _hovered;
    private Tween _tween;

    public void SetPixelColor(Color color)
    {
        if (Pixel is not null) Pixel.Color = color;
    }

    public void ShowSphere(Color color)
    {
        if (Sphere is null) return;
        Sphere.Color = color;
        Sphere.Visible = true;
    }

    public void HideSphere()
    {
        _hovered = false;
        Settle();
        if (Sphere is not null) Sphere.Visible = false;
    }

    /// <summary>Lifts the bead out of its divot to show it is picked up and waiting for a home.</summary>
    public void SetHovered(bool hovered, bool animate = true)
    {
        if (_hovered == hovered) return;
        _hovered = hovered;

        if (animate) Animate();
        else Settle();
    }

    /// <summary>Where a bead flying to or from this cell should aim.</summary>
    public Vector2 SphereCenterGlobal()
    {
        Control anchor = Sphere ?? (Control)this;
        return anchor.GetGlobalRect().GetCenter();
    }

    public override void _Notification(int what)
    {
        // The bead is anchored to the cell, so a resize rebuilds its rect and drops the lift.
        if (what == NotificationResized) Settle();
    }

    private Vector2 RestingOffset => _hovered ? new Vector2(0, -Size.Y * HoverLift) : Vector2.Zero;

    private Vector2 RestingScale => _hovered ? new Vector2(HoverScale, HoverScale) : Vector2.One;

    private void Animate()
    {
        if (Sphere is null) return;

        if (IsInstanceValid(_tween)) _tween.Kill();
        Sphere.PivotOffset = Sphere.Size * 0.5f;

        // In front for the whole trip, both ways. Dropping a bead out of the raised layer the
        // moment it is put down would duck it behind the square above while it is still falling.
        Sphere.ZIndex = RaisedLayer;

        _tween = CreateTween().SetParallel(true);
        _tween.TweenProperty(Sphere, "position", RestingOffset, HoverDuration)
              .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        _tween.TweenProperty(Sphere, "scale", RestingScale, HoverDuration)
              .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

        if (!_hovered)
            _tween.TweenCallback(Callable.From(Land)).SetDelay(HoverDuration);
    }

    /// <summary>A bead that has finished coming down goes back to sitting among its neighbours.</summary>
    private void Land()
    {
        if (Sphere is not null && !_hovered) Sphere.ZIndex = 0;
    }

    private void Settle()
    {
        if (Sphere is null) return;

        if (IsInstanceValid(_tween)) _tween.Kill();
        Sphere.PivotOffset = Sphere.Size * 0.5f;
        Sphere.Position = RestingOffset;
        Sphere.Scale = RestingScale;
        Sphere.ZIndex = _hovered ? RaisedLayer : 0;
    }
}
