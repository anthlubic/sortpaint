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
    [Export] public Control Check { get; set; }

    /// <summary>The level this square offers, or null when the level list has run out.</summary>
    public LevelData Level { get; private set; }

    public void ShowLevel(LevelData level, bool completed)
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

        SetCompleted(completed);
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

        SetCompleted(false);
    }

    public void SetCompleted(bool completed)
    {
        if (Check is not null) Check.Visible = completed;
    }
}
