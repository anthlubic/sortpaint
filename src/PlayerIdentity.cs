using System;
using Godot;
using SortPaint.Core;

namespace SortPaint;

/// <summary>
/// Who this player is on the leaderboard: an id nobody sees and a handle everybody does. Both are
/// drawn once, the first time the game runs, and then kept.
/// </summary>
/// <remarks>
/// Deliberately a different file from the progress record. Clearing your progress should not
/// change who you are, and losing your identity should not cost you your painted levels.
/// <para>
/// There is no account behind any of this. On the web the file lives in the browser's storage, so
/// clearing that, or opening the game in another browser, makes a new player. That is the price of
/// asking nobody to sign in, and it is worth being honest that a handle cannot be recovered.
/// </para>
/// </remarks>
public sealed class PlayerIdentity
{
    private const string SavePath = "user://player.json";
    private const string IdKey = "id";
    private const string HandleKey = "handle";
    private const string PendingKey = "pending";
    private const string LevelKey = "level";
    private const string MovesKey = "moves";
    private const string MillisKey = "ms";

    /// <summary>The id sent with a score. Not shown anywhere, and not tied to anything personal.</summary>
    public string Id { get; private set; }

    /// <summary>The name on the board, as in BriskAxolotl042.</summary>
    public string Handle { get; private set; }

    /// <summary>Finishes that have not reached the server yet.</summary>
    public PendingScores Pending { get; } = new();

    /// <summary>
    /// Reads the player back, drawing a new identity when there is nothing on file or what is
    /// there is unusable. A handle that is not one this game could have handed out is redrawn:
    /// the server would refuse it, so keeping it would mean silently never appearing on a board.
    /// </summary>
    public static PlayerIdentity LoadOrMint()
    {
        var player = new PlayerIdentity();
        player.Read();

        bool minted = false;

        if (!Guid.TryParse(player.Id, out _))
        {
            player.Id = Guid.NewGuid().ToString("D");
            minted = true;
        }

        if (!Core.Handle.IsWellFormed(player.Handle))
        {
            player.Handle = Core.Handle.Generate(new Random());
            minted = true;
        }

        // Written out at once rather than waiting for a score to send. An identity that is not on
        // disk is a different one next run, and the player would appear under a new name every
        // time they opened the game.
        if (minted) player.Save();

        return player;
    }

    /// <summary>Queues a finish to send, and writes the queue out so a crash does not lose it.</summary>
    public void Queue(string level, int moves, int millis)
    {
        if (Pending.Add(new Submission(level, moves, millis))) Save();
    }

    /// <summary>Drops a finish from the queue once the server has taken it.</summary>
    public void Delivered(string level)
    {
        if (Pending.Sent(level)) Save();
    }

    public void Save()
    {
        var waiting = new Godot.Collections.Array();
        foreach (Submission submission in Pending.Waiting)
        {
            waiting.Add(new Godot.Collections.Dictionary
            {
                { LevelKey, submission.Level },
                { MovesKey, submission.Moves },
                { MillisKey, submission.Millis },
            });
        }

        var record = new Godot.Collections.Dictionary
        {
            { IdKey, Id },
            { HandleKey, Handle },
            { PendingKey, waiting },
        };

        using FileAccess file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        if (file is null)
        {
            GD.PushWarning($"Could not write {SavePath}: {FileAccess.GetOpenError()}. This player will be a new one next run.");
            return;
        }

        file.StoreString(Json.Stringify(record, "  "));
    }

    private void Read()
    {
        if (!FileAccess.FileExists(SavePath)) return;

        using FileAccess file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        if (file is null)
        {
            GD.PushWarning($"Could not read {SavePath}: {FileAccess.GetOpenError()}. Starting as a new player.");
            return;
        }

        Variant parsed = Json.ParseString(file.GetAsText());
        if (parsed.VariantType != Variant.Type.Dictionary) return;

        Godot.Collections.Dictionary saved = parsed.AsGodotDictionary();

        if (saved.TryGetValue(IdKey, out Variant id)) Id = id.AsString();
        if (saved.TryGetValue(HandleKey, out Variant handle)) Handle = handle.AsString();

        if (!saved.TryGetValue(PendingKey, out Variant pending)) return;
        if (pending.VariantType != Variant.Type.Array) return;

        foreach (Variant entry in pending.AsGodotArray())
        {
            if (entry.VariantType != Variant.Type.Dictionary) continue;

            Godot.Collections.Dictionary waiting = entry.AsGodotDictionary();
            Pending.Add(new Submission(
                waiting.TryGetValue(LevelKey, out Variant level) ? level.AsString() : null,
                waiting.TryGetValue(MovesKey, out Variant moves) ? (int)moves.AsInt64() : 0,
                waiting.TryGetValue(MillisKey, out Variant millis) ? (int)millis.AsInt64() : 0));
        }
    }
}
