# Leaderboard

A Cloudflare Worker over a D1 database holding one row per player per level: their best round on
it. The game posts a finish and gets back the top ten, plus its own row when it placed outside them.

There are no accounts. A player is a random id the game drew for itself the first time it ran,
carrying a handle assembled from two closed word lists. Nothing else is stored about anybody.

## Which Cloudflare account this belongs in

**Its own, separate from the one hosting the `cloudflared` tunnel for riftworlds.net.** A second
free account under the same login is enough. That keeps quotas, API tokens, the workers.dev
subdomain, and the blast radius of anything this repository's CI does entirely apart from
riftworlds.

Two rules follow from that, and neither should be undone:

- `wrangler.toml` has **no `routes` and no `custom_domain`**. Those are the only things that attach
  a Worker to a zone or write DNS records. Without them this is reachable only at
  `sortpaint-leaderboard.<subdomain>.workers.dev` and no zone is touched.
- The API token is created **inside that second account** and scoped to `Workers Scripts:Edit` plus
  `D1:Edit`. Never a Global API Key.

## Setting it up

1. Create the second Cloudflare account and switch to it.
2. `npx wrangler d1 create sortpaint-leaderboard`, then put the returned `database_id` into
   `wrangler.toml`.
3. Optionally pin `account_id` in `wrangler.toml` from `npx wrangler whoami`, so a local deploy
   cannot land in the wrong account.
4. Add `CLOUDFLARE_API_TOKEN` and `CLOUDFLARE_ACCOUNT_ID` as repository secrets. The workflow at
   `.github/workflows/worker.yml` applies the schema, seeds the levels, and deploys.
5. Set `ApiOrigin` on the `LeaderboardClient` node in `scenes/Main.tscn` to the deployed
   `https://...workers.dev` URL. **Until that is filled in the game has no leaderboard at all**,
   which is what an editor run wants, and is also the fallback if the board is ever a problem.

## Locally

```sh
npm install
npm test                    # the acceptance rules
npx wrangler d1 execute sortpaint-leaderboard --file=schema.sql --local
python3 ../scripts/seed_levels.py -o levels.sql
npx wrangler d1 execute sortpaint-leaderboard --file=levels.sql --local
npm run dev                 # http://localhost:8787
```

Point `ApiOrigin` at `http://localhost:8787` and play a level in the editor to see it end to end.

## What it accepts

`POST /v1/score` with `{player, handle, level, moves, ms}`, sent as `text/plain` so it stays a
simple cross-origin request and skips the preflight round trip. A submission is written only when
all of these hold, and the rules live in `src/validate.js` where they are unit tested:

| Check | Why |
| --- | --- |
| `player` is a UUID | Nothing else could have come from the game |
| `handle` is two words from `words.json` plus three digits | Closed lists, so no arbitrary text can reach a board |
| `level` is in the `levels` table | Seeded from `levels/*.tres` |
| `moves >= floor(optimal * 0.75)` | See below |
| `moves <= optimal * 20` | Past this it is not a real round |
| `ms >= moves * 50` and `ms <= 6h` | Faster than tapping allows, or a tab left open |
| The rate limiter passes for the player id and the IP | 30 a minute each |

The floor sits **below** the level's known optimal on purpose. `OptimalMoves` comes from the beam
search in `scripts/sortpaint/par.py`, which finds a good solution rather than a proven minimum, so
a sharp player really can come in under it. A floor at the optimal itself would reject exactly the
record-breaking rounds a leaderboard exists to celebrate.

## What it does not do

Scores are not verified by replaying the moves. Somebody determined can post any number at or above
the floor. That was a deliberate trade: replaying would mean a third implementation of the game's
rules to keep in step with the C# and the Python, and this is a puzzle game's scoreboard, not a
competition with anything at stake.

## Keeping the two sides honest

`words.json` is the copy the server validates against; `src/Core/Handle.cs` is the copy the game
draws from. `tests/SortPaint.Tests/HandleWordsTests.cs` fails the build if they drift, because the
symptom otherwise is every submission being refused and nothing saying why.

Adding a word to the lists is fine. Removing or reordering one is not: handles already in D1 would
stop validating.
