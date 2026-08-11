// What the leaderboard will accept. Kept free of D1 and of the request object so the rules can be
// tested on their own, which matters more here than anywhere else in the worker: this file is the
// only thing standing between an open endpoint and a defaced scoreboard.

/**
 * How far under a level's known optimal a submission may go.
 *
 * OptimalMoves is whatever the beam search in scripts/sortpaint/par.py managed to find, not a
 * proven minimum, so a sharp player really can come in under it. A floor at the optimal itself
 * would reject exactly the record-breaking rounds a leaderboard exists to celebrate, so it sits
 * below, low enough to leave room and high enough that "1 move" is still nonsense.
 */
export const MOVES_FLOOR_FRACTION = 0.75;

/** Well past the worst honest round. Anything above it is a typo or a joke. */
export const MAX_MOVES_FACTOR = 20;

/** Nobody taps faster than this for a whole level, even chaining large runs. */
export const MIN_MS_PER_MOVE = 50;

/** Six hours. Longer than that and the tab was left open, not played. */
export const MAX_MS = 6 * 60 * 60 * 1000;

/** How many digits close a handle. Matches Handle.DigitCount in src/Core/Handle.cs. */
export const HANDLE_DIGITS = 3;

// None of these carry the `m` flag, so `$` means the end of the string and nothing else. A handle
// or slug with a newline stuck on the end is refused here, the same as it would be by the game.
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const DIGITS = /^[0-9]{3}$/;
const SLUG = /^[a-z0-9][a-z0-9_-]{0,63}$/;

export function isUuid(value) {
  return typeof value === 'string' && UUID.test(value);
}

export function isSlug(value) {
  return typeof value === 'string' && SLUG.test(value);
}

/**
 * Whether a handle is one the game could have handed out: two words from the closed lists, then
 * three digits. Mirrors Handle.IsWellFormed in src/Core/Handle.cs, and HandleWordsTests keeps the
 * two word lists from drifting apart.
 *
 * This is the whole moderation story. Because the lists are closed, there is no arrangement of
 * characters a player can push through here that puts a word of their own on somebody's screen.
 */
export function isWellFormedHandle(handle, words) {
  if (typeof handle !== 'string' || handle.length === 0) return false;

  const split = handle.length - HANDLE_DIGITS;
  if (split <= 0) return false;
  if (!DIGITS.test(handle.slice(split))) return false;

  const nouns = new Set(words.nouns);

  // Adjectives are not prefix-free (Dawn and Dapper both start with D), so every adjective that
  // fits is tried rather than the first one that matches.
  for (const adjective of words.adjectives) {
    if (adjective.length >= split) continue;
    if (!handle.startsWith(adjective)) continue;
    if (nouns.has(handle.slice(adjective.length, split))) return true;
  }

  return false;
}

/** The fewest moves this level will accept. A level with no optimal worked out accepts any round. */
export function movesFloor(optimal) {
  if (!Number.isInteger(optimal) || optimal <= 0) return 1;
  return Math.max(1, Math.floor(optimal * MOVES_FLOOR_FRACTION));
}

/** The most moves worth recording, past which the submission is not a real round. */
export function movesCeiling(optimal) {
  if (!Number.isInteger(optimal) || optimal <= 0) return 100000;
  return optimal * MAX_MOVES_FACTOR;
}

function isCount(value) {
  return Number.isInteger(value) && value >= 0;
}

/**
 * Whether a submission is worth writing down. `optimal` is the level's OptimalMoves as the server
 * knows it, or null when the level is not one of ours.
 *
 * Returns { ok: true } or { ok: false, reason }, the reason being for logs rather than the player:
 * a rejected score is not something the game says anything about.
 */
export function checkScore(score, optimal, words) {
  if (!score || typeof score !== 'object') return { ok: false, reason: 'not an object' };

  const { player, handle, level, moves, ms } = score;

  if (!isUuid(player)) return { ok: false, reason: 'player is not a uuid' };
  if (!isWellFormedHandle(handle, words)) return { ok: false, reason: 'handle is not one we hand out' };
  if (!isSlug(level)) return { ok: false, reason: 'level is not a slug' };
  if (optimal === null || optimal === undefined) return { ok: false, reason: 'unknown level' };

  if (!isCount(moves)) return { ok: false, reason: 'moves is not a count' };
  if (!isCount(ms)) return { ok: false, reason: 'ms is not a count' };

  if (moves < movesFloor(optimal)) return { ok: false, reason: 'too few moves to be a real round' };
  if (moves > movesCeiling(optimal)) return { ok: false, reason: 'more moves than a real round' };

  if (ms < moves * MIN_MS_PER_MOVE) return { ok: false, reason: 'quicker than the taps would allow' };
  if (ms > MAX_MS) return { ok: false, reason: 'longer than a sitting' };

  return { ok: true };
}
