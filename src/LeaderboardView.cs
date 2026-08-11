using System.Collections.Generic;
using Godot;
using SortPaint.Core;

namespace SortPaint;

/// <summary>
/// The board under the finish message: who has painted this level in the fewest moves.
/// </summary>
/// <remarks>
/// Every way this can fail ends up hidden rather than shouted about. A player who has just
/// finished a level should see how they did against par whether or not a server answered, so a
/// missing board leaves the overlay looking exactly as it did before there was one.
/// </remarks>
public partial class LeaderboardView : VBoxContainer
{
    [ExportGroup("Scene wiring")]
    [Export] public PackedScene RowScene { get; set; }
    [Export] public Label HeaderLabel { get; set; }
    [Export] public Control Rows { get; set; }
    [Export] public Label StatusLabel { get; set; }

    [ExportGroup("Wording")]
    [Export] public string WaitingText { get; set; } = "Checking the board...";
    [Export] public string EmptyText { get; set; } = "Nobody has painted this one yet.";

    /// <summary>Shows the header and says the board is on its way.</summary>
    public void ShowWaiting(LevelData level)
    {
        Visible = true;
        ClearRows();

        SetHeader(level);
        SetStatus(WaitingText);
    }

    /// <summary>
    /// Draws what came back. A request that failed hides the board entirely: there is nothing
    /// useful to say about a scoreboard that could not be reached, and saying it would only push
    /// the buttons down the card.
    /// </summary>
    public void ShowBoard(LevelData level, BoardResult result)
    {
        if (result is null || !result.Ok)
        {
            HideBoard();
            return;
        }

        Visible = true;
        ClearRows();
        SetHeader(level);

        if (result.Rows.Count == 0)
        {
            SetStatus(EmptyText);
            return;
        }

        SetStatus(null);
        foreach (BoardRow row in result.Rows) AddRow(row);
    }

    /// <summary>
    /// Takes the board off the card, leaving the overlay as it was without one. Named apart from
    /// Control.Hide rather than shadowing it, so there is no way to call the wrong one and leave
    /// stale rows behind.
    /// </summary>
    public void HideBoard()
    {
        Visible = false;
        ClearRows();
    }

    private void SetHeader(LevelData level)
    {
        if (HeaderLabel is null) return;

        // Par, not the optimal. The finish message above says par for the same reason: the optimal
        // is the smaller number, and quoting it here would read as par dropping at the last moment.
        HeaderLabel.Text = level is null ? "Fewest moves"
            : level.Par > 0 ? $"{level.DisplayName}, par {level.Par}"
            : level.DisplayName;
    }

    private void SetStatus(string text)
    {
        if (StatusLabel is null) return;

        StatusLabel.Text = text ?? "";
        StatusLabel.Visible = !string.IsNullOrEmpty(text);
    }

    private void AddRow(BoardRow row)
    {
        if (Rows is null || RowScene is null) return;

        if (RowScene.Instantiate() is not LeaderboardRowView view) return;

        Rows.AddChild(view);
        view.Show(row);
    }

    private void ClearRows()
    {
        if (Rows is null) return;

        foreach (Node child in new List<Node>(Rows.GetChildren()))
        {
            Rows.RemoveChild(child);
            child.QueueFree();
        }
    }
}
