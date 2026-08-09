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
}
