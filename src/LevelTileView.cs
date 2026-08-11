using Godot;

namespace SortPaint;

/// <summary>
/// One square of the level select: the finished picture in miniature, with a trophy over it once
/// the level has been painted, or a padlock over it while it is still being earned. A toggle
/// button, so the selected square stays lit.
/// </summary>
[GlobalClass]
public partial class LevelTileView : Button
{
    [Export] public TextureRect Preview { get; set; }
    [Export] public TrophyBadge Trophy { get; set; }
    [Export] public LockBadge Lock { get; set; }

    /// <summary>
    /// The trophy for a level painted in par. The badge in the scene carries this colour too, so
    /// the editor shows what a finished square looks like without the game running.
    /// </summary>
    [ExportGroup("Badge")]
    [Export] public Color GoldColor { get; set; } = new(0.898f, 0.706f, 0.204f);

    /// <summary>The trophy for a level that took more than par. Painted, but not a good round.</summary>
    [Export] public Color SilverColor { get; set; } = new(0.647f, 0.686f, 0.749f);

    /// <summary>
    /// What a locked square's picture is washed out to. The picture still shows through, so the
    /// square is a thing to work towards rather than a blank.
    /// </summary>
    [ExportGroup("Locked")]
    [Export] public Color LockedTint { get; set; } = new(0.78f, 0.76f, 0.82f, 0.55f);

    /// <summary>The level this square offers, or null when the level list has run out.</summary>
    public LevelData Level { get; private set; }

    /// <summary>
    /// Whether this square's level is still being earned. A locked square is still tappable: the
    /// tap is what asks how many trophies it wants.
    /// </summary>
    public bool Locked { get; private set; }

    public void ShowLevel(LevelData level, bool completed, bool metPar, bool locked)
    {
        Level = level;
        Locked = locked;
        Disabled = false;
        FocusMode = FocusModeEnum.All;
        TooltipText = level?.DisplayName ?? string.Empty;

        if (Preview is not null)
        {
            Preview.Texture = level?.Sprite;
            Preview.Visible = true;
            Preview.Modulate = locked ? LockedTint : Colors.White;
        }

        if (Lock is not null) Lock.Visible = locked;

        // A locked level cannot have been painted, so the two badges never share a square.
        SetCompleted(!locked && completed, metPar);
    }

    /// <summary>A square with no level behind it. It keeps the grid square rather than inviting a tap.</summary>
    public void ShowBlank()
    {
        Level = null;
        Locked = false;
        Disabled = true;
        FocusMode = FocusModeEnum.None;
        TooltipText = string.Empty;

        if (Preview is not null)
        {
            Preview.Texture = null;
            Preview.Visible = false;
            Preview.Modulate = Colors.White;
        }

        if (Lock is not null) Lock.Visible = false;

        SetCompleted(false, true);
    }

    public void SetCompleted(bool completed, bool metPar)
    {
        if (Trophy is null) return;

        Trophy.Visible = completed;
        Trophy.BadgeColor = metPar ? GoldColor : SilverColor;
    }
}
