using System;

namespace SortPaint.Core;

/// <summary>
/// How far the picture is pinched open, and how far it has been dragged around inside the
/// window it is looked at through. Keeps both honest: the picture never shrinks below its
/// fitted size, and it can never be dragged so far that an edge pulls into view.
/// </summary>
/// <remarks>
/// Everything here is plain geometry in view space, where the origin is the top left of the
/// window and the picture at rest sits centred inside it. The two axes are independent, so the
/// same clamp does duty for both.
/// </remarks>
public sealed class BoardZoom
{
    /// <summary>Pinched all the way closed is the picture fitted to the window, never smaller.</summary>
    public const float MinZoom = 1f;

    private float _viewWidth;
    private float _viewHeight;
    private float _contentWidth;
    private float _contentHeight;
    private float _maxZoom = 4f;

    /// <summary>How far the picture can be pinched open. Below <see cref="MinZoom"/> means no zoom at all.</summary>
    public float MaxZoom
    {
        get => _maxZoom;
        set
        {
            _maxZoom = MathF.Max(MinZoom, value);
            Zoom = Math.Clamp(Zoom, MinZoom, _maxZoom);
            ClampPan();
        }
    }

    public float Zoom { get; private set; } = MinZoom;

    /// <summary>How far the picture is dragged from centred, in view pixels.</summary>
    public float PanX { get; private set; }

    public float PanY { get; private set; }

    public bool IsZoomed => Zoom > MinZoom + 0.0005f;

    /// <summary>Left edge of the picture in view space, once zoomed and dragged.</summary>
    public float OriginX => Origin(PanX, _viewWidth, _contentWidth);

    /// <summary>Top edge of the picture in view space, once zoomed and dragged.</summary>
    public float OriginY => Origin(PanY, _viewHeight, _contentHeight);

    /// <summary>
    /// The room available, and the size of the picture when it is not pinched open. Call this
    /// whenever either changes: a smaller window can leave the drag out of bounds.
    /// </summary>
    public void SetLayout(float viewWidth, float viewHeight, float contentWidth, float contentHeight)
    {
        _viewWidth = MathF.Max(0f, viewWidth);
        _viewHeight = MathF.Max(0f, viewHeight);
        _contentWidth = MathF.Max(0f, contentWidth);
        _contentHeight = MathF.Max(0f, contentHeight);
        ClampPan();
    }

    /// <summary>Back to fitted and centred, as a fresh level starts.</summary>
    public void Reset()
    {
        Zoom = MinZoom;
        PanX = 0f;
        PanY = 0f;
    }

    /// <summary>
    /// Pinches by <paramref name="factor"/> about a point in view space, so whatever sits under
    /// the fingers stays under them. Returns whether anything actually moved.
    /// </summary>
    public bool ZoomBy(float factor, float focusX, float focusY)
    {
        // Written as a positive test so a NaN factor falls out here rather than poisoning the pan.
        if (!(factor > 0f)) return false;

        float wanted = Math.Clamp(Zoom * factor, MinZoom, MaxZoom);
        if (wanted == Zoom) return false;

        PanX = HoldFocus(PanX, focusX, _viewWidth, _contentWidth, Zoom, wanted);
        PanY = HoldFocus(PanY, focusY, _viewHeight, _contentHeight, Zoom, wanted);
        Zoom = wanted;
        ClampPan();
        return true;
    }

    /// <summary>Drags the picture. Returns whether the clamp left any of the movement.</summary>
    public bool PanBy(float dx, float dy)
    {
        if (float.IsNaN(dx) || float.IsNaN(dy)) return false;

        float wasX = PanX;
        float wasY = PanY;
        PanX += dx;
        PanY += dy;
        ClampPan();
        return PanX != wasX || PanY != wasY;
    }

    private float Origin(float pan, float view, float content) => (view - content * Zoom) * 0.5f + pan;

    /// <summary>
    /// The drag that keeps the point under <paramref name="focus"/> where it is while the zoom
    /// goes from <paramref name="from"/> to <paramref name="to"/>.
    /// </summary>
    private static float HoldFocus(float pan, float focus, float view, float content, float from, float to)
    {
        float origin = (view - content * from) * 0.5f + pan;
        float moved = focus - (focus - origin) * (to / from);
        return moved - (view - content * to) * 0.5f;
    }

    private void ClampPan()
    {
        PanX = ClampAxis(PanX, _viewWidth, _contentWidth * Zoom);
        PanY = ClampAxis(PanY, _viewHeight, _contentHeight * Zoom);
    }

    /// <summary>
    /// A picture bigger than the window may be dragged until one of its edges reaches the
    /// matching edge of the window, and no further. One that fits stays centred.
    /// </summary>
    private static float ClampAxis(float pan, float view, float content)
    {
        float slack = (content - view) * 0.5f;
        return slack <= 0f ? 0f : Math.Clamp(pan, -slack, slack);
    }
}
