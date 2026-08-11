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
    private const string TimeKey = "time";

    public static GameSession Instance { get; private set; }

    public Progress Progress { get; } = new();

    /// <summary>
    /// Who this player is on the leaderboard. Kept here because it outlives a scene change for the
    /// same reason the progress record does, though it is saved to a file of its own.
    /// </summary>
    public PlayerIdentity Player { get; private set; }

    /// <summary>What the Play button chose. The game falls back to its own export when this is null.</summary>
    public LevelData SelectedLevel { get; set; }

    public override void _Ready()
    {
        Instance = this;
        Load();

        Player = PlayerIdentity.LoadOrMint();
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

    /// <summary>
    /// How many of a set's levels are painted in par: the green checks the locks are priced in.
    /// Counted over the whole set rather than a page, so paging never moves the number.
    /// </summary>
    public int GreenChecks(LevelSet levels)
    {
        if (levels is null) return 0;

        int checks = 0;
        for (int i = 0; i < levels.Count; i++)
            if (MetPar(levels.At(i))) checks++;

        return checks;
    }

    /// <summary>Whether a level is open to play, given the checks earned so far.</summary>
    public static bool IsUnlocked(LevelData level, int greenChecks) =>
        level is null || Unlock.IsOpen(greenChecks, level.RequiredChecks);

    /// <summary>How long the best round on a level took, in milliseconds, or 0 when it was untimed.</summary>
    public int BestMillis(LevelData level) => Progress.BestMillis(KeyFor(level));

    /// <summary>Banks a finish, the round it took, and how long it ran. Only a new best reaches disk.</summary>
    public void MarkCompleted(LevelData level, int moves, int millis)
    {
        if (Progress.Record(KeyFor(level), moves, millis)) Save();
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

        // Clocks came later still, and are read alongside the counts rather than after them, so a
        // round is restored in one go. A file without them loads with every clock at zero.
        Godot.Collections.Dictionary times = ReadTimes(saved);

        foreach (var (id, moves) in best.AsGodotDictionary())
        {
            int millis = times.TryGetValue(id, out Variant time) ? (int)time.AsInt64() : 0;
            Progress.Record(id.AsString(), (int)moves.AsInt64(), millis);
        }
    }

    private static Godot.Collections.Dictionary ReadTimes(Godot.Collections.Dictionary saved)
    {
        if (!saved.TryGetValue(TimeKey, out Variant times)) return [];
        return times.VariantType == Variant.Type.Dictionary ? times.AsGodotDictionary() : [];
    }

    private void Save()
    {
        var ids = new Godot.Collections.Array();
        var best = new Godot.Collections.Dictionary();
        var times = new Godot.Collections.Dictionary();

        foreach (string id in Progress.CompletedIds())
        {
            ids.Add(id);

            int moves = Progress.BestMoves(id);
            if (moves > 0) best[id] = moves;

            int millis = Progress.BestMillis(id);
            if (millis > 0) times[id] = millis;
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
            { TimeKey, times },
        };

        file.StoreString(Json.Stringify(record, "  "));
    }
}
