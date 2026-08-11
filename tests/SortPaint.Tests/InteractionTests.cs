using SortPaint.Core;
using Xunit;

namespace SortPaint.Tests;

/// <summary>
/// The two-step tap game: the first tap lifts a run, the second says where it goes.
/// </summary>
public class InteractionTests
{
    private static Interaction Play(string[] target, string[] spheres, int capacity = 24) =>
        new(Boards.State(target, spheres, capacity));

    [Fact]
    public void TappingASphereOnlyLiftsItsRun()
    {
        var play = Play(
            target: ["aaaa", "aaaa"],
            spheres: ["bbcb", "bbcb"]);

        MoveResult move = play.TapCell(0, 0);

        Assert.Equal(MoveKind.Hovered, move.Kind);
        Assert.Equal(HoverSource.Board, move.Source);
        Assert.Equal(Boards.Color('b'), move.Color);
        Assert.Equal(4, move.Count);

        // Nothing has actually moved yet.
        Assert.Equal(new[] { "bbcb", "bbcb" }, Boards.Spheres(play.Board));
        Assert.True(play.Board.Tray.IsEmpty);
    }

    [Fact]
    public void TappingTheTrayDropsTheLiftedRunIntoIt()
    {
        var play = Play(["aaaa"], ["bbbb"]);

        play.TapCell(0, 0);
        MoveResult move = play.TapTray(LevelGrid.Empty);

        Assert.Equal(MoveKind.ToTray, move.Kind);
        Assert.Equal(4, move.Count);
        Assert.Equal(new[] { "...." }, Boards.Spheres(play.Board));
        Assert.Equal(4, play.Board.Tray.CountOf(Boards.Color('b')));
        Assert.False(play.IsHovering);
    }

    [Fact]
    public void ASphereGoesNowhereUntilTheSecondTap()
    {
        var play = Play(["aaaa"], ["bbbb"]);

        play.TapCell(0, 0);

        Assert.True(play.Board.Tray.IsEmpty);
        Assert.True(play.IsHovering);
    }

    [Fact]
    public void TappingTheLiftedRunAgainPutsItBackDown()
    {
        var play = Play(["aaaa"], ["bbbb"]);

        play.TapCell(0, 0);
        MoveResult move = play.TapCell(2, 0);

        Assert.Equal(MoveKind.Cleared, move.Kind);
        Assert.False(play.IsHovering);
        Assert.Equal(new[] { "bbbb" }, Boards.Spheres(play.Board));
    }

    [Fact]
    public void TappingAnotherRunSwitchesToIt()
    {
        var play = Play(
            target: ["aaaa"],
            spheres: ["bbcc"]);

        play.TapCell(0, 0);
        MoveResult move = play.TapCell(3, 0);

        Assert.Equal(MoveKind.Hovered, move.Kind);
        Assert.Equal(Boards.Color('c'), move.Color);
        Assert.Equal(new[] { 3, 2 }, play.HoverCells);
    }

    [Fact]
    public void ALiftedRunCanGoStraightToWhereItBelongs()
    {
        var play = Play(
            target: ["bbaa"],
            spheres: ["aa.."]);

        play.TapCell(0, 0);
        MoveResult move = play.TapCell(2, 0);

        Assert.Equal(MoveKind.ToBoard, move.Kind);
        Assert.Equal(HoverSource.Board, move.Source);
        Assert.Equal(2, move.Count);
        Assert.Equal(new[] { "..aa" }, Boards.Spheres(play.Board));

        // The whole point of the shortcut: it never touches the tray.
        Assert.True(play.Board.Tray.IsEmpty);
    }

    [Fact]
    public void GoingStraightAcrossPaintsTheCellsItLandsOn()
    {
        var play = Play(
            target: ["bbaa"],
            spheres: ["aa.."]);

        play.TapCell(0, 0);
        play.TapCell(2, 0);

        Assert.Equal(2, play.Board.PaintedCount);
        Assert.True(play.Board.IsPainted(2));
        Assert.True(play.Board.IsPainted(3));
    }

    [Fact]
    public void GoingStraightAcrossWorksWithAFullTray()
    {
        var play = Play(
            target: ["bbaa"],
            spheres: ["aa.."],
            capacity: 1);

        play.Board.Tap(0, 0);          // fills the one slot the tray has
        play.Board.Tap(0, 0);          // and the rest of that run stays put

        Assert.True(play.Board.Tray.IsFull);

        play.TapCell(1, 0);
        MoveResult move = play.TapCell(3, 0);

        Assert.Equal(MoveKind.ToBoard, move.Kind);
        Assert.Equal(1, move.Count);
    }

    [Fact]
    public void OnlyAsManySpheresMoveAsThereAreCellsWaiting()
    {
        var play = Play(
            target: ["bbbba."],
            spheres: ["aaaa.."]);

        play.TapCell(0, 0);
        MoveResult move = play.TapCell(4, 0);

        // One cell was waiting, so one sphere went: the one nearest where the run was lifted.
        Assert.Equal(1, move.Count);
        Assert.Equal(new[] { ".aaaa." }, Boards.Spheres(play.Board));

        // The three that had nowhere to go are still up, waiting to be sent somewhere.
        Assert.True(play.IsHovering);
        Assert.Equal(HoverSource.Board, play.Source);
        Assert.Equal(3, play.HoverCount);
        Assert.Equal(new[] { 1, 2, 3 }, play.HoverCells);
    }

    [Fact]
    public void TheLeftoversGoWhereverIsTappedNext()
    {
        // The hole splits the picture's two 'a' cells, so each drop only ever takes one sphere.
        var play = Play(
            target: ["bba.a"],
            spheres: ["aa..."]);

        play.TapCell(0, 0);
        play.TapCell(2, 0);

        MoveResult move = play.TapCell(4, 0);

        Assert.Equal(MoveKind.ToBoard, move.Kind);
        Assert.Equal(new[] { "..a.a" }, Boards.Spheres(play.Board));

        // That was the last of them, so the selection is spent.
        Assert.False(play.IsHovering);
        Assert.Equal(0, play.HoverCount);
    }

    [Fact]
    public void TappingTheLeftoversPutsThemBackDown()
    {
        var play = Play(
            target: ["bba.a"],
            spheres: ["aa..."]);

        play.TapCell(0, 0);
        play.TapCell(2, 0);

        MoveResult move = play.TapCell(1, 0);

        Assert.Equal(MoveKind.Cleared, move.Kind);
        Assert.False(play.IsHovering);
        Assert.Equal(new[] { ".aa.." }, Boards.Spheres(play.Board));
    }

    [Fact]
    public void ATrayWithLittleRoomTakesWhatItCanAndKeepsTheRestUp()
    {
        var play = Play(["aaa"], ["bbb"], capacity: 2);

        play.TapCell(0, 0);
        MoveResult move = play.TapTray(LevelGrid.Empty);

        Assert.Equal(MoveKind.ToTray, move.Kind);
        Assert.Equal(2, move.Count);
        Assert.Equal(new[] { 0, 1 }, move.From);
        Assert.Equal(new[] { "..b" }, Boards.Spheres(play.Board));
        Assert.True(play.Board.Tray.IsFull);

        Assert.True(play.IsHovering);
        Assert.Equal(HoverSource.Board, play.Source);
        Assert.Equal(new[] { 2 }, play.HoverCells);
    }

    [Fact]
    public void WhatIsLeftInTheTrayStaysUpAfterAPartialDrop()
    {
        // Cell 3 is the only one waiting for 'b', so two of the three in the tray stay put.
        var play = Play(
            target: ["aaab"],
            spheres: ["bbb."]);

        play.TapCell(0, 0);
        play.TapTray(LevelGrid.Empty);
        play.TapTray(Boards.Color('b'));

        MoveResult move = play.TapCell(3, 0);

        Assert.Equal(MoveKind.ToBoard, move.Kind);
        Assert.Equal(1, move.Count);

        Assert.True(play.IsHovering);
        Assert.Equal(HoverSource.Tray, play.Source);
        Assert.Equal(Boards.Color('b'), play.HoverColor);
        Assert.Equal(2, play.HoverCount);
        Assert.Empty(play.HoverCells);
    }

    [Fact]
    public void EmptyingTheTrayOfAColourEndsTheSelection()
    {
        var play = Play(
            target: ["bbaa"],
            spheres: ["aa.."]);

        play.TapCell(0, 0);
        play.TapTray(LevelGrid.Empty);
        play.TapTray(Boards.Color('a'));

        play.TapCell(2, 0);

        Assert.False(play.IsHovering);
        Assert.Equal(LevelGrid.Empty, play.HoverColor);
    }

    [Fact]
    public void ALiftedRunWillNotDropOnTheWrongColour()
    {
        var play = Play(
            target: ["bbca"],
            spheres: ["aa.."]);

        MoveResult move = play.TapCell(2, 0);

        Assert.Equal(MoveKind.None, move.Kind);

        play.TapCell(0, 0);
        move = play.TapCell(2, 0);

        Assert.Equal(MoveKind.None, move.Kind);

        // A tap that means nothing costs the player nothing.
        Assert.True(play.IsHovering);
        Assert.Equal(2, play.HoverCount);
    }

    [Fact]
    public void TappingAColourInTheTrayLiftsAllOfIt()
    {
        var play = Play(["aaab"], ["bbb."]);

        play.TapCell(0, 0);
        play.TapTray(LevelGrid.Empty);

        MoveResult move = play.TapTray(Boards.Color('b'));

        Assert.Equal(MoveKind.Hovered, move.Kind);
        Assert.Equal(HoverSource.Tray, move.Source);
        Assert.Equal(3, move.Count);
        Assert.Empty(move.From);
    }

    [Fact]
    public void ATrayColourDropsOntoAMatchingBareRun()
    {
        var play = Play(
            target: ["bbaa"],
            spheres: ["aa.."]);

        play.TapCell(0, 0);
        play.TapTray(LevelGrid.Empty);
        play.TapTray(Boards.Color('a'));

        MoveResult move = play.TapCell(2, 0);

        Assert.Equal(MoveKind.ToBoard, move.Kind);
        Assert.Equal(HoverSource.Tray, move.Source);
        Assert.Equal(2, move.Count);
        Assert.Equal(new[] { "..aa" }, Boards.Spheres(play.Board));
        Assert.True(play.Board.Tray.IsEmpty);
    }

    [Fact]
    public void TappingTheTrayColourAgainPutsItBack()
    {
        var play = Play(["aaab"], ["bbb."]);

        play.TapCell(0, 0);
        play.TapTray(LevelGrid.Empty);
        play.TapTray(Boards.Color('b'));

        MoveResult move = play.TapTray(Boards.Color('b'));

        Assert.Equal(MoveKind.Cleared, move.Kind);
        Assert.False(play.IsHovering);
        Assert.Equal(3, play.Board.Tray.Count);
    }

    [Fact]
    public void AFullTrayRefusesTheDropAndKeepsTheRunUp()
    {
        var play = Play(["aa"], ["bb"], capacity: 1);

        play.Board.Tap(0, 0);
        Assert.True(play.Board.Tray.IsFull);

        play.TapCell(1, 0);
        MoveResult move = play.TapTray(LevelGrid.Empty);

        Assert.Equal(MoveKind.None, move.Kind);
        Assert.Equal(TapRejection.TrayFull, move.Rejection);
        Assert.True(play.IsHovering);
    }

    [Fact]
    public void PaintedSpheresCannotBeLifted()
    {
        var play = Play(["ab"], ["ab"]);

        MoveResult move = play.TapCell(0, 0);

        Assert.Equal(MoveKind.None, move.Kind);
        Assert.Equal(TapRejection.AlreadyPainted, move.Rejection);
        Assert.False(play.IsHovering);
    }

    [Fact]
    public void HolesAndCellsOffTheEdgeDoNothing()
    {
        var play = Play(["a.a"], ["b.b"]);

        Assert.Equal(TapRejection.NotPlayable, play.TapCell(1, 0).Rejection);
        Assert.Equal(TapRejection.OutOfBounds, play.TapCell(9, 9).Rejection);
    }

    [Fact]
    public void AnEmptyTrayIgnoresATapOnBareRail()
    {
        var play = Play(["aa"], ["bb"]);

        MoveResult move = play.TapTray(LevelGrid.Empty);

        Assert.Equal(MoveKind.None, move.Kind);
        Assert.False(play.IsHovering);
    }

    [Fact]
    public void APictureCanBePaintedWithTheThreeMovesTogether()
    {
        // A full board has nowhere to drop anything, so the first run always has to be parked in
        // the tray. After that the shortcut is open, and the tray gives back what it was holding.
        var play = Play(
            target: ["ab"],
            spheres: ["ba"],
            capacity: 2);

        play.TapCell(0, 0);
        play.TapTray(LevelGrid.Empty);

        play.TapCell(1, 0);
        play.TapCell(0, 0);

        play.TapTray(Boards.Color('b'));
        play.TapCell(1, 0);

        Assert.Equal(new[] { "ab" }, Boards.Spheres(play.Board));
        Assert.True(play.Board.IsSolved);
        Assert.True(play.Board.Tray.IsEmpty);
    }
}
