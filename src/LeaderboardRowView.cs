using Godot;
using SortPaint.Core;

namespace SortPaint;

/// <summary>
/// One line of the leaderboard: where they placed, who they are, and the round they did it in.
/// </summary>
public partial class LeaderboardRowView : HBoxContainer
{
    [ExportGroup("Scene wiring")]
    [Export] public Label RankLabel { get; set; }
    [Export] public Label HandleLabel { get; set; }
    [Export] public Label MovesLabel { get; set; }
    [Export] public Label TimeLabel { get; set; }

    [ExportGroup("Feel")]
    /// <summary>What your own row turns, so you can find yourself without reading the names.</summary>
    [Export] public Color YouColor { get; set; } = new(0.408f, 0.286f, 0.667f);

    /// <summary>Fills the line in. Your own row is tinted rather than renamed, so you learn your handle.</summary>
    public void Show(BoardRow row)
    {
        if (row is null) return;

        SetText(RankLabel, $"{row.Rank}");
        SetText(HandleLabel, row.Handle);
        SetText(MovesLabel, $"{row.Moves}");
        SetText(TimeLabel, row.Elapsed);

        Tint(RankLabel, row.IsYou);
        Tint(HandleLabel, row.IsYou);
        Tint(MovesLabel, row.IsYou);
        Tint(TimeLabel, row.IsYou);
    }

    private static void SetText(Label label, string text)
    {
        if (label is not null) label.Text = text;
    }

    private void Tint(Label label, bool isYou)
    {
        if (label is null) return;

        if (isYou) label.AddThemeColorOverride("font_color", YouColor);
        else label.RemoveThemeColorOverride("font_color");
    }
}
