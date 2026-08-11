using Godot;
using SortPaint.Core;

namespace SortPaint;

/// <summary>
/// The opening screen: a grid of level squares, each showing the picture you are being asked to
/// paint. Tap one to light it up, then Play. The grid holds one page of levels; Prev and Next
/// walk the pages. Squares past the end of the last page are blank, so the grid keeps its shape.
/// </summary>
[GlobalClass]
public partial class LevelSelectView : Control
{
    [Export] public LevelSet Levels { get; set; }
    [Export] public PackedScene TileScene { get; set; }

    [ExportGroup("Scene wiring")]
    [Export] public GridContainer Grid { get; set; }

    /// <summary>
    /// The room the grid is centred in. Its width decides how big a square can be, which is why it
    /// is a plain Control and not a container: a container would take its width back from the
    /// squares it is meant to be measuring, and the two would chase each other.
    /// </summary>
    [Export] public Control GridArea { get; set; }

    [Export] public Button PlayButton { get; set; }
    [Export] public Label SelectionLabel { get; set; }
    [Export] public Label ProgressLabel { get; set; }

    /// <summary>
    /// The board for whichever level is lit, on a card of its own. Sits under the level's name
    /// rather than by the Play button, so it reads as being about that picture.
    /// </summary>
    [ExportGroup("Leaderboard")]
    [Export] public Button BoardButton { get; set; }
    [Export] public Control BoardNotice { get; set; }
    [Export] public LeaderboardView Board { get; set; }
    [Export] public Button BoardCloseButton { get; set; }
    [Export] public LeaderboardClient Scores { get; set; }

    /// <summary>The card that answers a tap on a padlocked square. Hidden until one is tapped.</summary>
    [ExportGroup("Locks")]
    [Export] public Control LockNotice { get; set; }
    [Export] public Label LockNoticeTitle { get; set; }
    [Export] public Label LockNoticeBody { get; set; }
    [Export] public Button LockNoticeButton { get; set; }

    [ExportGroup("Paging")]
    [Export] public Button PrevButton { get; set; }
    [Export] public Button NextButton { get; set; }
    [Export] public Label PageLabel { get; set; }

    /// <summary>The row holding the pager, hidden when everything fits on one page.</summary>
    [Export] public Control Pager { get; set; }

    // Columns come from the GridContainer itself, so the two together give the 5x5 page.
    [ExportGroup("Layout")]
    [Export(PropertyHint.Range, "1,10,1")] public int Rows { get; set; } = 5;

    [Export(PropertyHint.Range, "24,256,1")] public int MaxTileSize { get; set; } = 120;

    [ExportGroup("Flow")]
    [Export(PropertyHint.File, "*.tscn")] public string GameScene { get; set; } = "res://scenes/Main.tscn";

    private LevelTileView[] _tiles = [];
    private LevelTileView _selected;
    private int _page;

    /// <summary>
    /// Gold trophies won across the whole campaign, which is what the locks are priced in. Read
    /// once when the menu is built: nothing can win a trophy while the menu is up.
    /// </summary>
    private int _gold;

    /// <summary>
    /// Which board request is the live one. A board asked for, closed, and asked for again on
    /// another level would otherwise be drawn over by whichever answer came back last.
    /// </summary>
    private int _boardRequest;

    /// <summary>How many squares a page holds. The grid's own columns times the rows asked for.</summary>
    private int PageSize => Mathf.Max(1, Grid?.Columns ?? 1) * Mathf.Max(1, Rows);

    /// <summary>Always at least one, so an empty level set still draws a page of blanks.</summary>
    private int PageCount => Mathf.Max(1, (LevelCount + PageSize - 1) / PageSize);

    private int LevelCount => Levels?.Count ?? 0;

    public override void _Ready()
    {
        if (Grid is null || TileScene is null)
        {
            GD.PushError("LevelSelectView is missing its Grid or TileScene. Wire them up in LevelSelect.tscn.");
            return;
        }

        if (PlayButton is not null) PlayButton.Pressed += Play;
        if (PrevButton is not null) PrevButton.Pressed += () => TurnTo(_page - 1);
        if (NextButton is not null) NextButton.Pressed += () => TurnTo(_page + 1);
        if (GridArea is not null) GridArea.Resized += Relayout;

        if (LockNotice is not null) LockNotice.Visible = false;
        if (LockNoticeButton is not null) LockNoticeButton.Pressed += HideLockNotice;

        if (BoardNotice is not null) BoardNotice.Visible = false;
        if (BoardButton is not null) BoardButton.Pressed += ShowBoardNotice;
        if (BoardCloseButton is not null) BoardCloseButton.Pressed += HideBoardNotice;

        // A build with nowhere to send scores has no boards to show, so the button is not offered
        // rather than left to open a card that can only apologise.
        if (BoardButton is not null) BoardButton.Visible = Scores?.Configured ?? false;

        Build();
    }

    /// <summary>
    /// Fills the grid with squares. The squares outlive a page turn (only what they show
    /// changes), so this runs once and <see cref="ShowPage"/> does the rest.
    /// </summary>
    private void Build()
    {
        foreach (Node child in Grid.GetChildren())
        {
            Grid.RemoveChild(child);
            child.QueueFree();
        }

        // One group across the whole page, so lighting a square puts the last one out.
        var group = new ButtonGroup();
        _tiles = new LevelTileView[PageSize];
        _selected = null;
        _gold = GameSession.Instance?.GoldTrophies(Levels) ?? 0;

        for (int i = 0; i < _tiles.Length; i++)
        {
            var tile = TileScene.Instantiate<LevelTileView>();
            tile.ButtonGroup = group;
            Grid.AddChild(tile);
            _tiles[i] = tile;

            // Wired for every square, not just the filled ones, because a square that is blank on
            // this page may hold a level on the next. A blank square is disabled, so it stays quiet.
            tile.Pressed += () => Choose(tile);
        }

        Relayout();
        ShowPage(PageOf(OpeningLevel()));
        UpdateProgressLine();
    }

    /// <summary>Puts a page's levels on the squares and lights the one Play should open.</summary>
    private void ShowPage(int page)
    {
        _page = Mathf.Clamp(page, 0, PageCount - 1);
        int first = _page * PageSize;

        for (int i = 0; i < _tiles.Length; i++)
        {
            LevelData level = Levels?.At(first + i);
            if (level is null)
            {
                _tiles[i].ShowBlank();
                continue;
            }

            _tiles[i].ShowLevel(
                level,
                GameSession.Instance?.IsCompleted(level) ?? false,
                GameSession.Instance?.MetPar(level) ?? true,
                !GameSession.IsUnlocked(level, _gold));
        }

        UpdatePager();
        Select(OpeningChoice());
    }

    /// <summary>A page turn, ignored when it would run off either end.</summary>
    private void TurnTo(int page)
    {
        if (page < 0 || page >= PageCount || page == _page) return;
        ShowPage(page);
    }

    private void UpdatePager()
    {
        if (Pager is not null) Pager.Visible = PageCount > 1;
        if (PrevButton is not null) PrevButton.Disabled = _page <= 0;
        if (NextButton is not null) NextButton.Disabled = _page >= PageCount - 1;
        if (PageLabel is not null) PageLabel.Text = $"{_page + 1} / {PageCount}";
    }

    /// <summary>
    /// The level the menu wants to open on: whatever was played last, otherwise the first one still
    /// unpainted, so Play is always ready to press. Looks across the whole set, not just a page, and
    /// never lands on a locked level, which Play could not open anyway.
    /// </summary>
    private LevelData OpeningLevel()
    {
        LevelData wanted = GameSession.Instance?.SelectedLevel;
        if (wanted is not null && IndexOf(wanted) >= 0 && IsOpen(wanted)) return wanted;

        for (int i = 0; i < LevelCount; i++)
        {
            LevelData level = Levels.At(i);
            if (level is null || !IsOpen(level)) continue;
            if (!(GameSession.Instance?.IsCompleted(level) ?? false)) return level;
        }

        return Levels?.At(0);
    }

    /// <summary>Which square on the current page to light: the opening choice if it is here, else the first open one.</summary>
    private LevelTileView OpeningChoice()
    {
        LevelData wanted = OpeningLevel();
        LevelTileView first = null;

        foreach (LevelTileView tile in _tiles)
        {
            if (tile.Level is null || tile.Locked) continue;
            if (tile.Level == wanted) return tile;
            first ??= tile;
        }

        return first;
    }

    private bool IsOpen(LevelData level) => GameSession.IsUnlocked(level, _gold);

    private int IndexOf(LevelData level)
    {
        for (int i = 0; i < LevelCount; i++)
            if (Levels.At(i) == level) return i;

        return -1;
    }

    /// <summary>The page a level sits on, or the first page when it is not in the set at all.</summary>
    private int PageOf(LevelData level)
    {
        int index = level is null ? -1 : IndexOf(level);
        return index < 0 ? 0 : index / PageSize;
    }

    /// <summary>
    /// A tap on a square. A locked one says what it wants and hands the light back to whatever was
    /// selected, so the tap reads as an answered question rather than a choice.
    /// </summary>
    private void Choose(LevelTileView tile)
    {
        if (tile is null || !tile.Locked)
        {
            Select(tile);
            return;
        }

        ShowLockNotice(tile.Level);

        if (_selected is not null) _selected.ButtonPressed = true;
        else tile.ButtonPressed = false;
    }

    private void ShowLockNotice(LevelData level)
    {
        if (LockNotice is null || level is null) return;

        if (LockNoticeTitle is not null) LockNoticeTitle.Text = level.DisplayName;
        if (LockNoticeBody is not null) LockNoticeBody.Text = Unlock.LockedMessage(level.RequiredChecks);

        LockNotice.Visible = true;
    }

    private void HideLockNotice()
    {
        if (LockNotice is not null) LockNotice.Visible = false;
    }

    /// <summary>
    /// Opens the board for the lit square. The card goes up straight away saying the board is on
    /// its way, so pressing the button always does something, even on a slow connection.
    /// </summary>
    private void ShowBoardNotice()
    {
        LevelData level = _selected?.Level;
        if (BoardNotice is null || Board is null || level is null) return;

        int request = ++_boardRequest;

        Board.ShowWaiting(level);
        BoardNotice.Visible = true;

        Scores?.Fetch(level, result =>
        {
            if (request != _boardRequest) return;

            if (result is not null && result.Ok) Board.ShowBoard(level, result);
            else Board.ShowUnreachable(level);
        });
    }

    private void HideBoardNotice()
    {
        // Anything still in the air belongs to a card that is no longer up.
        _boardRequest++;

        if (BoardNotice is not null) BoardNotice.Visible = false;
    }

    private void Select(LevelTileView tile)
    {
        _selected = tile;
        if (tile is not null) tile.ButtonPressed = true;

        LevelData level = tile?.Level;
        bool completed = level is not null && (GameSession.Instance?.IsCompleted(level) ?? false);

        if (SelectionLabel is not null)
            SelectionLabel.Text = level is null ? "Pick a picture" : level.DisplayName;

        if (PlayButton is not null)
        {
            PlayButton.Disabled = level is null;
            PlayButton.Text = completed ? "Paint again" : "Play";
        }

        if (BoardButton is not null) BoardButton.Disabled = level is null;
    }

    /// <summary>
    /// Counts the whole campaign, not the page, so paging does not move the total. The trophies are
    /// on the same line because they are what the padlocks are spending, so a player who has just
    /// been told to win more can see how many they have.
    /// </summary>
    private void UpdateProgressLine()
    {
        if (ProgressLabel is null) return;

        int painted = 0;

        for (int i = 0; i < LevelCount; i++)
        {
            LevelData level = Levels.At(i);
            if (level is not null && (GameSession.Instance?.IsCompleted(level) ?? false)) painted++;
        }

        ProgressLabel.Text = $"{painted} of {LevelCount} painted, {Unlock.GoldTrophies(_gold)}";
    }

    private void Play()
    {
        LevelData level = _selected?.Level;
        if (level is null || _selected.Locked) return;

        if (GameSession.Instance is not null) GameSession.Instance.SelectedLevel = level;
        GetTree().ChangeSceneToFile(GameScene);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) Relayout();
    }

    /// <summary>Squares up the tiles to whatever width the page has, capped so they stay chunky.</summary>
    private void Relayout()
    {
        if (_tiles.Length == 0 || Grid is null) return;

        int columns = Mathf.Max(1, Grid.Columns);
        int gap = Grid.GetThemeConstant("h_separation");

        float available = GridArea is not null && GridArea.Size.X > 0f ? GridArea.Size.X : Size.X;
        float side = Mathf.Floor((available - (columns - 1) * gap) / columns);
        side = Mathf.Clamp(side, 24f, MaxTileSize);

        var square = new Vector2(side, side);
        foreach (LevelTileView tile in _tiles) tile.CustomMinimumSize = square;
    }
}
