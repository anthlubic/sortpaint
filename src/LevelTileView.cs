using Godot;

namespace SortPaint;

/// <summary>
/// One square of the level select: the finished picture in miniature, with a tick over it once
/// the level has been painted. A toggle button, so the selected square stays lit.
/// </summary>
[GlobalClass]
public partial class LevelTileView : Button
{
    [Export] public TextureRect Preview { get; set; }
    [Export] public CheckBadge Check { get; set; }

    /// <summary>
    /// The tick for a level painted in par. The badge in the scene carries this colour too, so
    /// the editor shows what a finished square looks like without the game running.
    /// </summary>
    [ExportGroup("Badge")]
    [Export] public Color ParColor { get; set; } = new(0.42f, 0.796f, 0.62f);

    /// <summary>The tick for a level that took more than par. Painted, but not a good round.</summary>
    [Export] public Color OverParColor { get; set; } = new(0.376f, 0.353f, 0.42f);

    /// <summary>The level this square offers, or null when the level list has run out.</summary>
    public LevelData Level { get; private set; }

    public void ShowLevel(LevelData level, bool completed, bool metPar)
    {
        Level = level;
        Disabled = false;
        FocusMode = FocusModeEnum.All;
        TooltipText = level?.DisplayName ?? string.Empty;

        if (Preview is not null)
        {
            Preview.Texture = level?.Sprite;
            Preview.Visible = true;
        }

        SetCompleted(completed, metPar);
    }

    /// <summary>A square with no level behind it. It keeps the grid square rather than inviting a tap.</summary>
    public void ShowBlank()
    {
        Level = null;
        Disabled = true;
        FocusMode = FocusModeEnum.None;
        TooltipText = string.Empty;

        if (Preview is not null)
        {
            Preview.Texture = null;
            Preview.Visible = false;
        }

        SetCompleted(false, true);
    }

    public void SetCompleted(bool completed, bool metPar)
    {
        if (Check is null) return;

        Check.Visible = completed;
        Check.BadgeColor = metPar ? ParColor : OverParColor;
    }
}
