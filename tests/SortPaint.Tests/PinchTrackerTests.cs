using System.Numerics;
using SortPaint.Core;
using Xunit;

namespace SortPaint.Tests;

public class PinchTrackerTests
{
    private static Vector2 At(float x, float y = 0f) => new(x, y);

    private static PinchDelta Reported(PinchDelta? delta)
    {
        Assert.NotNull(delta);
        return delta.Value;
    }

    [Fact]
    public void OneFingerIsNeverAGesture()
    {
        var pinch = new PinchTracker();

        Assert.False(pinch.Down(0, At(10f)));
        Assert.Null(pinch.Move(0, At(90f)));
        Assert.False(pinch.IsPinching);
    }

    [Fact]
    public void ASecondFingerStartsThePinch()
    {
        var pinch = new PinchTracker();
        pinch.Down(0, At(0f));

        Assert.True(pinch.Down(1, At(100f)));
        Assert.True(pinch.IsPinching);
        Assert.Equal(2, pinch.TouchCount);
    }

    [Fact]
    public void FingersLandingChangesNothingUntilTheyMove()
    {
        var pinch = new PinchTracker();
        pinch.Down(0, At(0f));
        pinch.Down(1, At(100f));

        PinchDelta delta = Reported(pinch.Move(1, At(100f)));

        Assert.Equal(1f, delta.Scale, 3);
        Assert.Equal(Vector2.Zero, delta.Drag);
    }

    [Fact]
    public void SpreadingApartReportsHowMuchWider()
    {
        var pinch = new PinchTracker();
        pinch.Down(0, At(0f));
        pinch.Down(1, At(100f));

        PinchDelta delta = Reported(pinch.Move(1, At(200f)));

        Assert.Equal(2f, delta.Scale, 3);
        Assert.Equal(At(50f), delta.Drag);
        Assert.Equal(At(100f), delta.Focus);
    }

    [Fact]
    public void EachReportIsMeasuredFromTheLastOne()
    {
        var pinch = new PinchTracker();
        pinch.Down(0, At(0f));
        pinch.Down(1, At(100f));
        pinch.Move(1, At(200f));

        PinchDelta delta = Reported(pinch.Move(1, At(400f)));

        Assert.Equal(2f, delta.Scale, 3);
    }

    [Fact]
    public void MovingBothFingersTogetherIsADragAndNotAZoom()
    {
        var pinch = new PinchTracker();
        pinch.Down(0, At(0f, 0f));
        pinch.Down(1, At(100f, 0f));

        // The fingers report one at a time, so the middle wanders while only one of them has
        // moved. What matters is that the pair of reports adds up to the drag and no zoom.
        PinchDelta first = Reported(pinch.Move(0, At(30f, 20f)));
        PinchDelta second = Reported(pinch.Move(1, At(130f, 20f)));

        Assert.Equal(1f, first.Scale * second.Scale, 3);
        Assert.Equal(At(30f, 20f), first.Drag + second.Drag);
        Assert.Equal(At(80f, 20f), second.Focus);
    }

    [Fact]
    public void AThirdFingerJoiningDoesNotJumpThePicture()
    {
        var pinch = new PinchTracker();
        pinch.Down(0, At(0f));
        pinch.Down(1, At(100f));
        pinch.Move(1, At(120f));

        pinch.Down(2, At(600f));
        PinchDelta delta = Reported(pinch.Move(2, At(600f)));

        Assert.Equal(1f, delta.Scale, 3);
        Assert.Equal(Vector2.Zero, delta.Drag);
    }

    [Fact]
    public void LiftingBackToOneFingerEndsTheGesture()
    {
        var pinch = new PinchTracker();
        pinch.Down(0, At(0f));
        pinch.Down(1, At(100f));

        Assert.True(pinch.Up(1));
        Assert.False(pinch.IsPinching);
        Assert.Null(pinch.Move(0, At(400f)));
    }

    [Fact]
    public void FingersItIsNotFollowingAreIgnored()
    {
        var pinch = new PinchTracker();
        pinch.Down(0, At(0f));
        pinch.Down(1, At(100f));

        Assert.False(pinch.IsTracking(7));
        Assert.False(pinch.Up(7));
        Assert.Null(pinch.Move(7, At(500f)));
        Assert.Equal(2, pinch.TouchCount);
    }

    [Fact]
    public void ClearForgetsEveryFinger()
    {
        var pinch = new PinchTracker();
        pinch.Down(0, At(0f));
        pinch.Down(1, At(100f));

        pinch.Clear();

        Assert.Equal(0, pinch.TouchCount);
        Assert.False(pinch.IsTracking(0));
    }
}
