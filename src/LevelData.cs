using Godot;

namespace SortPaint;

/// <summary>
/// One level, authored in the Godot inspector. The picture itself is a small PNG: every
/// distinct opaque colour in it becomes a sphere colour, and transparent pixels are holes.
/// </summary>
[GlobalClass]
public partial class LevelData : Resource
{
    [Export] public string DisplayName { get; set; } = "Untitled";

    /// <summary>The target picture. Keep it small: one pixel is one cell.</summary>
    [Export] public Texture2D Sprite { get; set; }

    [Export(PropertyHint.Range, "1,64,1")]
    public int TrayCapacity { get; set; } = 24;

    /// <summary>Fixes the opening layout, so retrying a level gives the same puzzle.</summary>
    [Export] public int ShuffleSeed { get; set; } = 20260809;

    /// <summary>Pixels fainter than this are treated as holes in the sprite.</summary>
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float AlphaThreshold { get; set; } = 0.5f;

    /// <summary>
    /// The fewest moves this level is known to be finishable in, which is what par is measured
    /// against. Written by scripts/update_par.py; re-run it after changing the picture, the seed
    /// or the tray, since all three change the answer. Zero means nobody has worked it out, and
    /// the game then plays the level without a target.
    /// </summary>
    [Export(PropertyHint.Range, "0,999,1")]
    public int OptimalMoves { get; set; }

    /// <summary>
    /// Gold trophies needed before this level opens, gold being what a level painted in par
    /// awards. Zero, which every level of the original campaign carries, means open from the start.
    /// </summary>
    [Export(PropertyHint.Range, "0,200,1")]
    public int RequiredChecks { get; set; }

    /// <summary>The move count to beat: <see cref="OptimalMoves"/> plus its allowance.</summary>
    public int Par => Core.Par.From(OptimalMoves);

    /// <summary>
    /// What this level is called on the leaderboard: the .tres file's own name, so apple.tres is
    /// "apple". Short and readable, unlike the full resource path the save file keys on, and it is
    /// the name the server seeds its level table with.
    /// </summary>
    public string Slug =>
        string.IsNullOrEmpty(ResourcePath) ? DisplayName.ToLowerInvariant() : ResourcePath.GetFile().GetBaseName();
}
