using System.Collections.Generic;
using System.Numerics;

namespace SortPaint.Core;

/// <summary>How far a pinch has spread and shifted since it was last reported on.</summary>
/// <param name="Scale">Spread since the last report, where 1 is unchanged and 2 is twice as wide.</param>
/// <param name="Drag">How far the middle of the fingers moved.</param>
/// <param name="Focus">Where the middle of the fingers is now.</param>
public readonly record struct PinchDelta(float Scale, Vector2 Drag, Vector2 Focus);

/// <summary>
/// Follows the fingers on the screen and turns them into a pinch: how much they spread apart,
/// and how far their middle slid. One finger is never a gesture, since that is a tap in the
/// making, so nothing is reported until a second one lands.
/// </summary>
/// <remarks>
/// Spread is measured as the mean distance from the middle of the fingers rather than the gap
/// between two of them, so a third finger joining in neither breaks the gesture nor jumps it.
/// Every count change rebases the measurement for the same reason.
/// </remarks>
public sealed class PinchTracker
{
    private const float Tiny = 0.001f;

    private readonly Dictionary<int, Vector2> _touches = [];
    private Vector2 _focus;
    private float _spread;

    public int TouchCount => _touches.Count;

    public bool IsPinching => _touches.Count >= 2;

    public bool IsTracking(int index) => _touches.ContainsKey(index);

    /// <summary>A finger went down. Returns whether that makes this a pinch rather than a tap.</summary>
    public bool Down(int index, Vector2 at)
    {
        _touches[index] = at;
        Rebase();
        return IsPinching;
    }

    /// <summary>A finger lifted. Returns whether it was one being followed.</summary>
    public bool Up(int index)
    {
        if (!_touches.Remove(index)) return false;

        Rebase();
        return true;
    }

    /// <summary>
    /// A finger moved. Reports the pinch since the last move, or nothing while fewer than two
    /// fingers are down or the finger is not one being followed.
    /// </summary>
    public PinchDelta? Move(int index, Vector2 to)
    {
        if (!_touches.ContainsKey(index)) return null;

        _touches[index] = to;
        if (!IsPinching) return null;

        Vector2 focus = MiddleOfFingers();
        float spread = SpreadAbout(focus);
        float scale = _spread > Tiny && spread > Tiny ? spread / _spread : 1f;
        Vector2 drag = focus - _focus;

        _focus = focus;
        _spread = spread;
        return new PinchDelta(scale, drag, focus);
    }

    /// <summary>Forgets every finger, for when the board underneath is rebuilt.</summary>
    public void Clear()
    {
        _touches.Clear();
        _focus = Vector2.Zero;
        _spread = 0f;
    }

    private void Rebase()
    {
        _focus = MiddleOfFingers();
        _spread = SpreadAbout(_focus);
    }

    private Vector2 MiddleOfFingers()
    {
        if (_touches.Count == 0) return Vector2.Zero;

        Vector2 sum = Vector2.Zero;
        foreach (Vector2 point in _touches.Values) sum += point;
        return sum / _touches.Count;
    }

    private float SpreadAbout(Vector2 focus)
    {
        if (_touches.Count < 2) return 0f;

        float total = 0f;
        foreach (Vector2 point in _touches.Values) total += (point - focus).Length();
        return total / _touches.Count;
    }
}
