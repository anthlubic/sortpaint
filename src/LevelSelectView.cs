using Godot;

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

        for (int i = 0; i < _tiles.Length; i++)
        {
            var tile = TileScene.Instantiate<LevelTileView>();
            tile.ButtonGroup = group;
            Grid.AddChild(tile);
            _tiles[i] = tile;

            // Wired for every square, not just the filled ones, because a square that is blank on
            // this page may hold a level on the next. A blank square is disabled, so it stays quiet.
            tile.Pressed += () => Select(tile);
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
                GameSession.Instance?.MetPar(level) ?? true);
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
    /// unpainted, so Play is always ready to press. Looks across the whole set, not just a page.
    /// </summary>
    private LevelData OpeningLevel()
    {
        LevelData wanted = GameSession.Instance?.SelectedLevel;
        if (wanted is not null && IndexOf(wanted) >= 0) return wanted;

        for (int i = 0; i < LevelCount; i++)
        {
            LevelData level = Levels.At(i);
            if (level is not null && !(GameSession.Instance?.IsCompleted(level) ?? false)) return level;
        }

        return Levels?.At(0);
    }

    /// <summary>Which square on the current page to light: the opening choice if it is here, else the first.</summary>
    private LevelTileView OpeningChoice()
    {
        LevelData wanted = OpeningLevel();
        LevelTileView first = null;

        foreach (LevelTileView tile in _tiles)
        {
            if (tile.Level is null) continue;
            if (tile.Level == wanted) return tile;
            first ??= tile;
        }

        return first;
    }

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
    }

    /// <summary>Counts the whole campaign, not the page, so paging does not move the total.</summary>
    private void UpdateProgressLine()
    {
        if (ProgressLabel is null) return;

        int painted = 0;

        for (int i = 0; i < LevelCount; i++)
        {
            LevelData level = Levels.At(i);
            if (level is not null && (GameSession.Instance?.IsCompleted(level) ?? false)) painted++;
        }

        ProgressLabel.Text = $"{painted} of {LevelCount} painted";
    }

    private void Play()
    {
        LevelData level = _selected?.Level;
        if (level is null) return;

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
