using System;
using System.Linq;
using SortPaint.Core;
using Xunit;

namespace SortPaint.Tests;

public class TrayTests
{
    [Fact]
    public void SlotForNextGroupsColoursByPaletteIndex()
    {
        var tray = new Tray(10);
        tray.Add(0, 2);
        tray.Add(2, 3);

        Assert.Equal(2, tray.SlotForNext(0));
        Assert.Equal(5, tray.SlotForNext(2));

        // A colour it does not hold yet slots in between, pushing later colours along.
        Assert.Equal(2, tray.SlotForNext(1));
    }

    [Fact]
    public void ContentsComeBackOrderedByPaletteIndex()
    {
        var tray = new Tray(10);
        tray.Add(3, 1);
        tray.Add(1, 2);

        Assert.Equal(new[] { 1, 3 }, tray.Contents.Select(pair => pair.Key).ToArray());
    }

    [Fact]
    public void OverfillingThrows()
    {
        var tray = new Tray(3);
        tray.Add(0, 3);

        Assert.Throws<InvalidOperationException>(() => tray.Add(1, 1));
    }

    [Fact]
    public void RemovingMoreThanIsHeldThrows()
    {
        var tray = new Tray(3);
        tray.Add(0, 1);

        Assert.Throws<InvalidOperationException>(() => tray.Remove(0, 2));
    }

    [Fact]
    public void ClearEmptiesEveryColour()
    {
        var tray = new Tray(5);
        tray.Add(0, 2);
        tray.Add(1, 1);

        tray.Clear();

        Assert.True(tray.IsEmpty);
        Assert.Equal(0, tray.CountOf(0));
        Assert.Equal(5, tray.FreeSlots);
    }
}
