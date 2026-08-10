using Godot;
using SortPaint.Core;

namespace SortPaint;

/// <summary>
/// The one thing that outlives a scene change: which level the menu picked, and which levels have
/// been painted. Autoloaded, so the menu and the game can both reach it without knowing about
/// each other. Finishing a level writes the record straight to disk, so a crash cannot lose it.
/// </summary>
public partial class GameSession : Node
{
    private const string SavePath = "user://progress.json";
    private const string CompletedKey = "completed";
    private const string BestKey = "best";

    public static GameSession Instance { get; private set; }

    public Progress Progress { get; } = new();

    /// <summary>What the Play button chose. The game falls back to its own export when this is null.</summary>
    public LevelData SelectedLevel { get; set; }

    public override void _Ready()
    {
        Instance = this;
        Load();
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// A level's identity in the save file. The resource path is stable as long as the .tres file
    /// stays put; moving one forgets that level's tick rather than crediting the wrong picture.
    /// </summary>
    public static string KeyFor(LevelData level)
    {
        if (level is null) return null;
        return string.IsNullOrEmpty(level.ResourcePath) ? level.DisplayName : level.ResourcePath;
    }

    public bool IsCompleted(LevelData level) => Progress.IsCompleted(KeyFor(level));

    /// <summary>The shortest round on a level, or 0 when it is unfinished or was finished uncounted.</summary>
    public int BestMoves(LevelData level) => Progress.BestMoves(KeyFor(level));

    /// <summary>Whether the level's best round came in on par. Levels without a par always have.</summary>
    public bool MetPar(LevelData level) =>
        level is not null && IsCompleted(level) && Par.IsMet(BestMoves(level), level.Par);

    /// <summary>Banks a finish, and the round it took. Only a new best is written to disk.</summary>
    public void MarkCompleted(LevelData level, int moves)
    {
        if (Progress.Record(KeyFor(level), moves)) Save();
    }

    private void Load()
    {
        if (!FileAccess.FileExists(SavePath)) return;

        using FileAccess file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        if (file is null)
        {
            GD.PushWarning($"Could not read {SavePath}: {FileAccess.GetOpenError()}. Starting with a clean record.");
            return;
        }

        Variant parsed = Json.ParseString(file.GetAsText());
        if (parsed.VariantType != Variant.Type.Dictionary)
        {
            GD.PushWarning($"{SavePath} is not the progress file this game wrote. Starting with a clean record.");
            return;
        }

        Godot.Collections.Dictionary saved = parsed.AsGodotDictionary();

        // The list of finished levels came first and is still written, so a file from a build
        // before moves were counted loads whole. The move counts are read on top of it.
        if (saved.TryGetValue(CompletedKey, out Variant completed) && completed.VariantType == Variant.Type.Array)
            foreach (Variant id in completed.AsGodotArray()) Progress.MarkCompleted(id.AsString());

        if (!saved.TryGetValue(BestKey, out Variant best)) return;
        if (best.VariantType != Variant.Type.Dictionary) return;

        foreach (var (id, moves) in best.AsGodotDictionary())
            Progress.Record(id.AsString(), (int)moves.AsInt64());
    }

    private void Save()
    {
        var ids = new Godot.Collections.Array();
        var best = new Godot.Collections.Dictionary();

        foreach (string id in Progress.CompletedIds())
        {
            ids.Add(id);

            int moves = Progress.BestMoves(id);
            if (moves > 0) best[id] = moves;
        }

        using FileAccess file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        if (file is null)
        {
            GD.PushWarning($"Could not write {SavePath}: {FileAccess.GetOpenError()}. This run's progress will be lost.");
            return;
        }

        var record = new Godot.Collections.Dictionary
        {
            { CompletedKey, ids },
            { BestKey, best },
        };

        file.StoreString(Json.Stringify(record, "  "));
    }
}
