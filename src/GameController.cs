using System;
using System.Collections.Generic;
using Godot;
using SortPaint.Core;

namespace SortPaint;

/// <summary>
/// Runs one level: owns the rules, feeds the views, and flies beads between the two.
/// The model updates the instant a tap lands; the animation is purely decoration on top,
/// so taps stay responsive even while spheres are still in the air.
/// </summary>
public partial class GameController : Control
{
    /// <summary>
    /// The level to play when nothing picked one, as when running Main.tscn straight from the editor.
    /// Coming in from the level select, the menu's choice wins.
    /// </summary>
    [Export] public LevelData Level { get; set; }

    [Export(PropertyHint.File, "*.tscn")] public string MenuScene { get; set; } = "res://scenes/LevelSelect.tscn";

    [ExportGroup("Scene wiring")]
    [Export] public BoardView Board { get; set; }
    [Export] public TrayView TrayRail { get; set; }
    [Export] public Control FlightLayer { get; set; }
    [Export] public PackedScene BeadScene { get; set; }
    [Export] public Label LevelNameLabel { get; set; }
    [Export] public Label ClockLabel { get; set; }
    [Export] public Label MovesLabel { get; set; }
    [Export] public Button RestartButton { get; set; }
    [Export] public Button MenuButton { get; set; }
    [Export] public Control Overlay { get; set; }
    [Export] public Label OverlayTitle { get; set; }
    [Export] public Label OverlayBody { get; set; }
    [Export] public Button OverlayButton { get; set; }
    [Export] public Button OverlayMenuButton { get; set; }

    [ExportGroup("Feel")]
    [Export(PropertyHint.Range, "0.05,1,0.01")] public float FlightDuration { get; set; } = 0.26f;
    [Export(PropertyHint.Range, "0,0.2,0.005")] public float FlightStagger { get; set; } = 0.025f;
    [Export] public Color WarningFlash { get; set; } = new(1f, 0.45f, 0.45f);

    /// <summary>What the move count turns once the round has gone past par.</summary>
    [Export] public Color OverParColor { get; set; } = new(0.82f, 0.25f, 0.3f);

    private LoadedLevel _level;
    private BoardState _state;

    /// <summary>The lift-then-drop tap game over <see cref="_state"/>. Owns what is hovering.</summary>
    private Interaction _play;

    /// <summary>What the tray rail is currently showing. Trails the model while beads are mid-flight.</summary>
    private Tray _display;

    private readonly List<Flight> _flights = [];

    /// <summary>One bead in the air, with the view update that belongs to its arrival.</summary>
    private sealed class Flight
    {
        public Tween Tween;
        public Control Bead;
        public Action OnLand;
    }

    /// <summary>Bumped on restart so callbacks from the previous round's beads become no-ops.</summary>
    private int _generation;

    private float _clock;

    /// <summary>Taps that actually moved spheres. Lifting a run and putting it back down is free.</summary>
    private int _moves;

    /// <summary>The move count to come in under, or 0 when this level has no par worked out.</summary>
    private int _par;

    /// <summary>The picture is painted. Only a restart resumes.</summary>
    private bool _finished;

    public override void _Ready()
    {
        if (Board is null || TrayRail is null)
        {
            GD.PushError("GameController is missing its Board or TrayRail. Wire them up in Main.tscn.");
            SetProcess(false);
            return;
        }

        // Whatever the menu chose beats the export, which is only there for running this scene alone.
        Level = GameSession.Instance?.SelectedLevel ?? Level;

        Board.CellTapped += OnCellTapped;
        TrayRail.TrayTapped += OnTrayTapped;
        if (RestartButton is not null) RestartButton.Pressed += StartLevel;
        if (OverlayButton is not null) OverlayButton.Pressed += StartLevel;
        if (MenuButton is not null) MenuButton.Pressed += BackToMenu;
        if (OverlayMenuButton is not null) OverlayMenuButton.Pressed += BackToMenu;

        StartLevel();
    }

    private void BackToMenu() => GetTree().ChangeSceneToFile(MenuScene);

    private void StartLevel()
    {
        if (Level is null)
        {
            GD.PushError("GameController has no LevelData assigned.");
            SetProcess(false);
            return;
        }

        _level = LevelLoader.Load(Level);
        if (_level is null)
        {
            SetProcess(false);
            return;
        }

        _generation++;
        ClearFlights();

        int[] spheres = Scrambler.Scramble(_level.Grid, Level.ShuffleSeed);
        _state = new BoardState(_level.Grid, spheres, Level.TrayCapacity);
        _play = new Interaction(_state);
        _display = new Tray(Level.TrayCapacity);

        Board.Build(_level);
        Board.Refresh(_state);
        TrayRail.Build(Level.TrayCapacity, _level.Palette);
        TrayRail.SetContents(_display.Contents);
        TrayRail.Modulate = Colors.White;

        _clock = 0f;
        _moves = 0;
        _par = Level.Par;
        _finished = false;

        if (Overlay is not null) Overlay.Visible = false;
        if (LevelNameLabel is not null) LevelNameLabel.Text = Level.DisplayName;

        SetProcess(true);
        UpdateHud();
    }

    private void OnCellTapped(int x, int y) => Play(() => _play.TapCell(x, y));

    private void OnTrayTapped(int color) => Play(() => _play.TapTray(color));

    /// <summary>
    /// Runs one tap through the rules and shows what it did. Beads still in the air are snapped
    /// down first: that keeps the rail level with the model, so the next move aims at the right
    /// sockets, and it makes fast tapping feel immediate rather than queued.
    /// </summary>
    private void Play(Func<MoveResult> tap)
    {
        if (_finished || _play is null) return;

        SettleFlights();
        MoveResult move = tap();

        switch (move.Kind)
        {
            case MoveKind.Hovered:
                ShowHover();
                return;

            case MoveKind.Cleared:
                ShowHover();
                return;

            case MoveKind.None:
                if (move.Rejection == TapRejection.TrayFull) FlashTray();
                return;
        }

        _moves++;

        if (move.Kind == MoveKind.ToTray) AnimateToTray(move);
        else if (move.Source == HoverSource.Tray) AnimateFromTray(move);
        else AnimateAcrossBoard(move);

        // A drop that only had room for part of the run leaves the rest up, so the highlight is
        // redrawn from the model rather than simply cleared. It runs after the animation, which
        // is what settles where the tray's beads are.
        ShowHover();
        UpdateHud();

        // Only the finished picture ends a level. A board that has dead-ended is left alone: the
        // player can sit and look at it, and Restart is already there when they want a fresh deal.
        if (_state.IsSolved) WinWhenBeadsLand(move.Count);
    }

    /// <summary>
    /// Raises whatever is hovering, wherever it is sitting, and puts everything else down. Drawn
    /// from the model rather than from one move, so it is equally right after a lift, after a
    /// drop that took the lot, and after one that left some of it up.
    /// </summary>
    private void ShowHover()
    {
        bool fromTray = _play.IsHovering && _play.Source == HoverSource.Tray;

        TrayRail.SetHoveredColor(fromTray ? _play.HoverColor : LevelGrid.Empty);
        Board.SetHoveredCells(fromTray ? [] : _play.HoverCells);
    }

    private void AnimateToTray(MoveResult move)
    {
        // Same-coloured beads land in consecutive slots, and slot geometry never moves,
        // so every destination can be worked out before the first one takes off.
        int firstSlot = _display.SlotForNext(move.Color);
        int generation = _generation;

        for (int i = 0; i < move.Count; i++)
        {
            int cell = move.From[i];
            Vector2 from = Board.CellCenterGlobal(cell);
            Vector2 to = TrayRail.SlotCenterGlobal(firstSlot + i);

            Board.HideSphere(cell);
            int color = move.Color;

            LaunchBead(color, from, Board.CellSize, to, TrayRail.SlotSize, i, () =>
            {
                if (generation != _generation) return;
                _display.Add(color, 1);
                TrayRail.SetContents(_display.Contents);
            });
        }
    }

    private void AnimateFromTray(MoveResult move)
    {
        // Beads leave from the tail of this colour's run so the sockets empty right to left.
        int lastSlot = _display.SlotForNext(move.Color) - 1;
        int generation = _generation;

        var origins = new Vector2[move.Count];
        for (int i = 0; i < move.Count; i++) origins[i] = TrayRail.SlotCenterGlobal(lastSlot - i);

        _display.Remove(move.Color, move.Count);
        TrayRail.SetContents(_display.Contents);

        for (int i = 0; i < move.Count; i++)
        {
            int cell = move.To[i];
            Vector2 to = Board.CellCenterGlobal(cell);
            int color = move.Color;

            LaunchBead(color, origins[i], TrayRail.SlotSize, to, Board.CellSize, i, () =>
            {
                if (generation != _generation) return;
                Board.ShowSphere(cell, color);
            });
        }
    }

    /// <summary>The shortcut move: beads fly from where they were lifted straight to where they belong.</summary>
    private void AnimateAcrossBoard(MoveResult move)
    {
        int generation = _generation;

        for (int i = 0; i < move.Count; i++)
        {
            int origin = move.From[i];
            int landing = move.To[i];
            Vector2 from = Board.CellCenterGlobal(origin);
            Vector2 to = Board.CellCenterGlobal(landing);

            Board.HideSphere(origin);
            int color = move.Color;

            LaunchBead(color, from, Board.CellSize, to, Board.CellSize, i, () =>
            {
                if (generation != _generation) return;
                Board.ShowSphere(landing, color);
            });
        }
    }

    private void LaunchBead(int color, Vector2 from, float fromSize, Vector2 to, float toSize, int order, Action onLand)
    {
        if (BeadScene is null || FlightLayer is null)
        {
            onLand?.Invoke();
            return;
        }

        var bead = BeadScene.Instantiate<ColorRect>();
        FlightLayer.AddChild(bead);
        bead.MouseFilter = MouseFilterEnum.Ignore;
        bead.Color = _level.Palette[color];

        var startSize = new Vector2(fromSize, fromSize);
        var endSize = new Vector2(toSize, toSize);
        bead.Size = startSize;
        bead.GlobalPosition = from - startSize * 0.5f;

        // Position and size share the same curve, so the bead's centre travels a clean line
        // between the two anchors even as it grows or shrinks.
        float delay = order * FlightStagger;
        Tween tween = CreateTween().SetParallel(true);
        tween.TweenProperty(bead, "global_position", to - endSize * 0.5f, FlightDuration)
             .SetDelay(delay).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(bead, "size", endSize, FlightDuration)
             .SetDelay(delay).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);

        var flight = new Flight { Tween = tween, Bead = bead, OnLand = onLand };
        _flights.Add(flight);
        tween.Finished += () =>
        {
            if (!_flights.Remove(flight)) return;
            Land(flight);
        };
    }

    private void Land(Flight flight)
    {
        flight.OnLand?.Invoke();
        if (IsInstanceValid(flight.Bead)) flight.Bead.QueueFree();
    }

    /// <summary>Finishes every bead in the air right now, applying the view updates they owed.</summary>
    private void SettleFlights()
    {
        if (_flights.Count == 0) return;

        Flight[] pending = _flights.ToArray();
        _flights.Clear();

        foreach (Flight flight in pending)
        {
            if (IsInstanceValid(flight.Tween)) flight.Tween.Kill();
            Land(flight);
        }
    }

    /// <summary>Throws the beads away without applying their updates, for when the view is rebuilt anyway.</summary>
    private void ClearFlights()
    {
        foreach (Flight flight in _flights) if (IsInstanceValid(flight.Tween)) flight.Tween.Kill();
        _flights.Clear();

        if (FlightLayer is null) return;
        foreach (Node bead in FlightLayer.GetChildren())
        {
            FlightLayer.RemoveChild(bead);
            bead.QueueFree();
        }
    }

    private void FlashTray()
    {
        Tween tween = CreateTween();
        tween.TweenProperty(TrayRail, "modulate", WarningFlash, 0.08f);
        tween.TweenProperty(TrayRail, "modulate", Colors.White, 0.22f);
    }

    /// <summary>Holds the overlay back until the last bead has landed, so the board reads as finished.</summary>
    private void WinWhenBeadsLand(int beadCount)
    {
        _finished = true;

        // Banked the moment the picture is done, rather than when the overlay appears, so a win
        // is never lost to the wait.
        GameSession.Instance?.MarkCompleted(Level, _moves);

        float wait = FlightDuration + FlightStagger * Mathf.Max(0, beadCount - 1) + 0.2f;
        SceneTreeTimer timer = GetTree().CreateTimer(wait);
        int generation = _generation;
        timer.Timeout += () =>
        {
            if (generation == _generation) ShowOverlay();
        };
    }

    private void ShowOverlay()
    {
        if (Overlay is null) return;

        if (OverlayTitle is not null) OverlayTitle.Text = "Painted!";
        if (OverlayBody is not null) OverlayBody.Text = $"{FormatClock(_clock)}\n{Verdict()}";
        if (OverlayButton is not null) OverlayButton.Text = "Play again";

        Overlay.Visible = true;
    }

    /// <summary>The line under the clock: how the round went against par.</summary>
    private string Verdict()
    {
        int optimal = Level?.OptimalMoves ?? 0;
        if (optimal <= 0) return $"You solved it in {_moves} moves.";

        string ending = Par.IsMet(_moves, _par) ? "Good Job!" : "Try Again!";
        return $"You solved it in {_moves} moves. The optimal moves is {optimal}. {ending}";
    }

    public override void _Process(double delta)
    {
        if (_state is null || _finished) return;

        _clock += (float)delta;
        UpdateHud();
    }

    private void UpdateHud()
    {
        if (_state is null) return;

        if (ClockLabel is not null) ClockLabel.Text = FormatClock(_clock);
        if (MovesLabel is null) return;

        MovesLabel.Text = _par > 0 ? $"Moves: {_moves}/{_par}" : $"Moves: {_moves}";

        // Over par the count goes red and stays red: the round can still be finished, it just
        // will not be a good one. Clearing the override puts the theme's colour back.
        if (_par > 0 && _moves > _par) MovesLabel.AddThemeColorOverride("font_color", OverParColor);
        else MovesLabel.RemoveThemeColorOverride("font_color");
    }

    private static string FormatClock(float seconds)
    {
        int whole = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return $"{whole / 60}:{whole % 60:00}";
    }
}
