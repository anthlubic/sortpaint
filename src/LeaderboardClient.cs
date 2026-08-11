using System;
using System.Collections.Generic;
using Godot;
using SortPaint.Core;

namespace SortPaint;

/// <summary>What came back from the board, or did not.</summary>
/// <param name="Ok">False when the request failed. The overlay then simply shows no board.</param>
/// <param name="Rows">The leading rows, with the asking player's own row on the end if it fell outside them.</param>
public sealed record BoardResult(bool Ok, string Level, IReadOnlyList<BoardRow> Rows)
{
    public static BoardResult Failed(string level) => new(false, level, []);
}

/// <summary>
/// Talks to the leaderboard. One request at a time, so working through a backlog of unsent rounds
/// cannot stampede the server, and nothing here ever blocks the game: a board that does not arrive
/// just is not drawn.
/// </summary>
/// <remarks>
/// Godot's <see cref="HttpRequest"/> is used rather than .NET's HttpClient because it is pumped by
/// the scene tree rather than the WebAssembly runtime's scheduler, and because it behaves the same
/// on every export platform. Nothing in this file is web-specific, so a mobile export needs no
/// leaderboard changes.
/// </remarks>
public partial class LeaderboardClient : Node
{
    /// <summary>
    /// Where the leaderboard lives, with no trailing slash. Left blank the game simply has no
    /// leaderboard, which is what a local editor run wants unless a server is running to talk to.
    /// </summary>
    [Export] public string ApiOrigin { get; set; } = "";

    /// <summary>How long to wait before giving up. Short: nobody should wait on a scoreboard.</summary>
    [Export(PropertyHint.Range, "1,30,1")] public int TimeoutSeconds { get; set; } = 6;

    private HttpRequest _http;
    private readonly Queue<Job> _jobs = new();
    private Job _running;

    /// <summary>One request waiting its turn.</summary>
    private sealed class Job
    {
        public string Level;
        public string Url;
        public string Body;
        public Action<BoardResult> OnDone;
    }

    /// <summary>Whether there is anywhere to send scores. False in a build with no origin set.</summary>
    public bool Configured => !string.IsNullOrWhiteSpace(ApiOrigin);

    public override void _Ready()
    {
        _http = new HttpRequest { Timeout = TimeoutSeconds, UseThreads = false };
        AddChild(_http);
        _http.RequestCompleted += OnRequestCompleted;
    }

    /// <summary>
    /// Sends a finish and asks for the board around it. The result is queued first, so a request
    /// that fails is retried on a later run rather than lost.
    /// </summary>
    public void Submit(LevelData level, int moves, int millis, Action<BoardResult> onDone = null)
    {
        PlayerIdentity player = GameSession.Instance?.Player;
        if (level is null || player is null)
        {
            onDone?.Invoke(BoardResult.Failed(level?.Slug));
            return;
        }

        player.Queue(level.Slug, moves, millis);

        if (!Configured)
        {
            onDone?.Invoke(BoardResult.Failed(level.Slug));
            return;
        }

        Enqueue(new Job
        {
            Level = level.Slug,
            Url = $"{ApiOrigin}/v1/score",
            Body = Json.Stringify(new Godot.Collections.Dictionary
            {
                { "player", player.Id },
                { "handle", player.Handle },
                { "level", level.Slug },
                { "moves", moves },
                { "ms", millis },
            }),
            OnDone = onDone,
        });
    }

    /// <summary>Asks for a level's board without playing it.</summary>
    public void Fetch(LevelData level, Action<BoardResult> onDone)
    {
        PlayerIdentity player = GameSession.Instance?.Player;
        if (level is null || !Configured)
        {
            onDone?.Invoke(BoardResult.Failed(level?.Slug));
            return;
        }

        string who = player is null ? "" : $"&player={player.Id.URIEncode()}";
        Enqueue(new Job
        {
            Level = level.Slug,
            Url = $"{ApiOrigin}/v1/board?level={level.Slug.URIEncode()}{who}",
            OnDone = onDone,
        });
    }

    /// <summary>
    /// Sends anything left over from a round that could not reach the server, one at a time. Called
    /// on entering the game scene, which is the next chance a player's connection gets to be back.
    /// </summary>
    public void FlushPending()
    {
        PlayerIdentity player = GameSession.Instance?.Player;
        if (player is null || !Configured) return;

        foreach (Submission waiting in new List<Submission>(player.Pending.Waiting))
        {
            Enqueue(new Job
            {
                Level = waiting.Level,
                Url = $"{ApiOrigin}/v1/score",
                Body = Json.Stringify(new Godot.Collections.Dictionary
                {
                    { "player", player.Id },
                    { "handle", player.Handle },
                    { "level", waiting.Level },
                    { "moves", waiting.Moves },
                    { "ms", waiting.Millis },
                }),
            });
        }
    }

    private void Enqueue(Job job)
    {
        _jobs.Enqueue(job);
        StartNext();
    }

    private void StartNext()
    {
        if (_running is not null || _jobs.Count == 0 || _http is null) return;

        Job job = _jobs.Dequeue();
        _running = job;

        Error started = job.Body is null
            ? _http.Request(job.Url)
            // text/plain keeps this a simple cross-origin request, so the browser does not spend a
            // preflight round trip asking permission before every score.
            : _http.Request(job.Url, ["Content-Type: text/plain;charset=UTF-8"], HttpClient.Method.Post, job.Body);

        if (started == Error.Ok) return;

        // Never left the machine, so it is still worth sending later.
        _running = null;
        Finish(job, BoardResult.Failed(job.Level), settled: false);
    }

    private void OnRequestCompleted(long result, long responseCode, string[] headers, byte[] body)
    {
        Job job = _running;
        _running = null;

        if (job is null) return;

        bool reached = result == (long)HttpRequest.Result.Success;
        bool ok = reached && responseCode is >= 200 and < 300;

        // A score the server actively refused will be refused again on the same evidence, so it is
        // done with rather than retried on every launch for ever. Being told to slow down is not a
        // refusal, and neither is the server having a bad minute: both of those come back.
        bool refused = reached && responseCode is >= 400 and < 500 && responseCode != 429;

        Finish(job, ok ? Parse(job.Level, body) : BoardResult.Failed(job.Level), settled: ok || refused);
    }

    /// <summary>
    /// Closes a job out. <paramref name="settled"/> is whether the server has given its answer, in
    /// which case the round comes off the queue; anything else leaves it there to try again.
    /// </summary>
    private void Finish(Job job, BoardResult result, bool settled)
    {
        if (settled && job.Body is not null) GameSession.Instance?.Player?.Delivered(job.Level);

        job.OnDone?.Invoke(result);
        StartNext();
    }

    /// <summary>
    /// Reads the board out of a response. Anything unexpected in there is treated as no board
    /// rather than an error worth showing: this is a scoreboard, not the game.
    /// </summary>
    private static BoardResult Parse(string level, byte[] body)
    {
        Variant parsed = Json.ParseString(System.Text.Encoding.UTF8.GetString(body));
        if (parsed.VariantType != Variant.Type.Dictionary) return BoardResult.Failed(level);

        Godot.Collections.Dictionary payload = parsed.AsGodotDictionary();

        var entries = new List<BoardEntry>();
        if (payload.TryGetValue("rows", out Variant rows) && rows.VariantType == Variant.Type.Array)
        {
            foreach (Variant row in rows.AsGodotArray())
            {
                if (row.VariantType != Variant.Type.Dictionary) continue;
                entries.Add(EntryFrom(row.AsGodotDictionary()));
            }
        }

        IReadOnlyList<BoardRow> ranked = Leaderboard.Rank(entries);
        return new BoardResult(true, level, Leaderboard.Merge(ranked, YouFrom(payload)));
    }

    private static BoardEntry EntryFrom(Godot.Collections.Dictionary row) =>
        new(
            row.TryGetValue("handle", out Variant handle) ? handle.AsString() : "",
            row.TryGetValue("moves", out Variant moves) ? (int)moves.AsInt64() : 0,
            row.TryGetValue("ms", out Variant millis) ? (int)millis.AsInt64() : 0,
            row.TryGetValue("you", out Variant you) && you.AsBool());

    /// <summary>The asking player's own row, when they placed outside the rows above.</summary>
    private static BoardRow YouFrom(Godot.Collections.Dictionary payload)
    {
        if (!payload.TryGetValue("you", out Variant you)) return null;
        if (you.VariantType != Variant.Type.Dictionary) return null;

        Godot.Collections.Dictionary mine = you.AsGodotDictionary();
        if (!mine.TryGetValue("rank", out Variant rank)) return null;

        BoardEntry entry = EntryFrom(mine);
        return new BoardRow((int)rank.AsInt64(), entry.Handle, entry.Moves, entry.Millis, true);
    }
}
