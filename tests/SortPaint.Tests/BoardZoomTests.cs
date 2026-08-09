using SortPaint.Core;
using Xunit;

namespace SortPaint.Tests;

public class BoardZoomTests
{
    /// <summary>A square picture that exactly fills a square window, which keeps the sums readable.</summary>
    private static BoardZoom Fitted()
    {
        var zoom = new BoardZoom { MaxZoom = 4f };
        zoom.SetLayout(100f, 100f, 100f, 100f);
        return zoom;
    }

    [Fact]
    public void StartsFittedAndCentred()
    {
        var zoom = new BoardZoom();
        zoom.SetLayout(100f, 200f, 60f, 60f);

        Assert.Equal(1f, zoom.Zoom);
        Assert.False(zoom.IsZoomed);
        Assert.Equal(20f, zoom.OriginX, 3);
        Assert.Equal(70f, zoom.OriginY, 3);
    }

    [Fact]
    public void PinchingClosedNeverGoesBelowFitted()
    {
        BoardZoom zoom = Fitted();

        Assert.False(zoom.ZoomBy(0.5f, 50f, 50f));
        Assert.Equal(1f, zoom.Zoom);
    }

    [Fact]
    public void PinchingOpenStopsAtMaxZoom()
    {
        BoardZoom zoom = Fitted();

        zoom.ZoomBy(10f, 50f, 50f);

        Assert.Equal(4f, zoom.Zoom);
    }

    [Fact]
    public void WhatIsUnderTheFingersStaysUnderThem()
    {
        BoardZoom zoom = Fitted();

        // The picture starts at the origin, so the point pinched about is 25 into the picture.
        zoom.ZoomBy(2f, 25f, 25f);

        Assert.Equal(25f, zoom.OriginX + 25f * zoom.Zoom, 3);
        Assert.Equal(25f, zoom.OriginY + 25f * zoom.Zoom, 3);
    }

    [Fact]
    public void DraggingStopsWhenAnEdgeReachesTheWindow()
    {
        BoardZoom zoom = Fitted();
        zoom.ZoomBy(2f, 50f, 50f);

        zoom.PanBy(1000f, -1000f);

        // Dragged right until the picture's left edge is flush, and up until its bottom edge is.
        Assert.Equal(0f, zoom.OriginX, 3);
        Assert.Equal(-100f, zoom.OriginY, 3);
    }

    [Fact]
    public void APictureThatFitsCannotBeDragged()
    {
        BoardZoom zoom = Fitted();

        Assert.False(zoom.PanBy(30f, 30f));
        Assert.Equal(0f, zoom.PanX);
        Assert.Equal(0f, zoom.PanY);
    }

    [Fact]
    public void PinchingBackClosedRecentresThePicture()
    {
        BoardZoom zoom = Fitted();
        zoom.ZoomBy(4f, 0f, 0f);
        zoom.PanBy(1000f, 1000f);

        zoom.ZoomBy(0.25f, 0f, 0f);

        Assert.Equal(1f, zoom.Zoom);
        Assert.Equal(0f, zoom.PanX, 3);
        Assert.Equal(0f, zoom.PanY, 3);
    }

    [Fact]
    public void ARoomierWindowPullsTheDragBackIntoBounds()
    {
        BoardZoom zoom = Fitted();
        zoom.ZoomBy(2f, 50f, 50f);
        zoom.PanBy(1000f, 0f);
        Assert.Equal(50f, zoom.PanX, 3);

        // The same picture in a window twice as wide has half the slack to be dragged into.
        zoom.SetLayout(200f, 100f, 100f, 100f);

        Assert.Equal(0f, zoom.PanX, 3);
        Assert.Equal(0f, zoom.OriginX, 3);
    }

    [Fact]
    public void LoweringTheCeilingPullsTheZoomDownWithIt()
    {
        BoardZoom zoom = Fitted();
        zoom.ZoomBy(4f, 50f, 50f);

        zoom.MaxZoom = 2f;

        Assert.Equal(2f, zoom.Zoom);
        Assert.Equal(-50f, zoom.OriginX, 3);
    }

    [Fact]
    public void NonsensePinchesAreIgnored()
    {
        BoardZoom zoom = Fitted();
        zoom.ZoomBy(2f, 50f, 50f);
        float was = zoom.Zoom;

        Assert.False(zoom.ZoomBy(float.NaN, 50f, 50f));
        Assert.False(zoom.ZoomBy(0f, 50f, 50f));
        Assert.False(zoom.PanBy(float.NaN, 0f));
        Assert.Equal(was, zoom.Zoom);
    }

    [Fact]
    public void ResetPutsThePictureBackAsItStarted()
    {
        BoardZoom zoom = Fitted();
        zoom.ZoomBy(3f, 10f, 90f);
        zoom.PanBy(20f, -20f);

        zoom.Reset();

        Assert.Equal(1f, zoom.Zoom);
        Assert.Equal(0f, zoom.PanX);
        Assert.Equal(0f, zoom.PanY);
    }
}
