using Godot;

namespace SortPaint;

/// <summary>
/// The levels the menu offers, in the order they appear on the grid. Drag <see cref="LevelData"/>
/// resources into the list in the inspector; leftover squares on the grid are drawn blank.
/// </summary>
[GlobalClass]
public partial class LevelSet : Resource
{
    [Export] public Godot.Collections.Array<LevelData> Levels { get; set; } = [];

    public int Count => Levels?.Count ?? 0;

    /// <summary>The level in a given square, or null once the list has run out.</summary>
    public LevelData At(int index) =>
        Levels is not null && index >= 0 && index < Levels.Count ? Levels[index] : null;
}
