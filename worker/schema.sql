-- The leaderboard's whole database.
--
-- Applied with:
--   wrangler d1 execute sortpaint-leaderboard --file=schema.sql --remote
-- and then seeded from the shipped levels with scripts/seed_levels.py.

-- Every level the game ships, with the fewest moves it is known to be finishable in. Submissions
-- are measured against this, so a level missing from here cannot be scored on at all. Regenerated
-- from levels/*.tres on every deploy, so a new level or a re-solved optimal lands here by itself.
CREATE TABLE IF NOT EXISTS levels (
  slug    TEXT PRIMARY KEY,
  optimal INTEGER NOT NULL
);

-- One row per player per level: their best round on it, never a history. There is no account
-- behind `player`; it is a random id the game drew for itself, and nothing else is stored about
-- whoever is holding it.
CREATE TABLE IF NOT EXISTS scores (
  player TEXT    NOT NULL,
  level  TEXT    NOT NULL,
  handle TEXT    NOT NULL,
  moves  INTEGER NOT NULL,
  ms     INTEGER NOT NULL,
  at     INTEGER NOT NULL,
  PRIMARY KEY (player, level)
);

-- The order every board is drawn in, so reading the top ten does not scan the table. `at` is in
-- the index as the last tie-break: whoever got there first keeps the higher row.
CREATE INDEX IF NOT EXISTS scores_board ON scores (level, moves, ms, at);
