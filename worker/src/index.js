import words from '../words.json';
import { checkScore, isSlug, isUuid } from './validate.js';

/** How many rows the board leads with. Matches Leaderboard.TopRows in src/Core/Leaderboard.cs. */
const TOP_ROWS = 10;

/**
 * Where the game is served from. Nothing else may call this from a browser.
 *
 * CORS is a browser concept, so a native build sends no Origin at all and is let through: there is
 * nothing to protect here that a request without an Origin could not already do, and the real
 * defences are the validation rules and the rate limiter, not this list.
 */
const ALLOWED_ORIGINS = [
  'https://anthlubic.github.io',
  'http://localhost:8060',
  'http://127.0.0.1:8060',
];

function corsHeaders(request) {
  const origin = request.headers.get('Origin');
  const headers = {
    'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
    'Access-Control-Allow-Headers': 'Content-Type',
    'Access-Control-Max-Age': '86400',
    Vary: 'Origin',
  };

  if (origin && ALLOWED_ORIGINS.includes(origin)) headers['Access-Control-Allow-Origin'] = origin;

  return headers;
}

function json(request, body, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json', ...corsHeaders(request) },
  });
}

/**
 * The leading rows for a level. The asking player is marked in SQL rather than by returning ids,
 * so no player id ever leaves the server.
 */
async function leaders(env, level, player) {
  const { results } = await env.DB.prepare(
    `SELECT handle, moves, ms, (player = ?2) AS you
       FROM scores
      WHERE level = ?1
   ORDER BY moves ASC, ms ASC, at ASC
      LIMIT ?3`
  )
    .bind(level, player ?? '', TOP_ROWS)
    .all();

  return (results ?? []).map((row) => ({
    handle: row.handle,
    moves: row.moves,
    ms: row.ms,
    you: row.you === 1,
  }));
}

/** The asking player's own row and where it placed, or null when they have not painted this one. */
async function standing(env, level, player) {
  if (!isUuid(player)) return null;

  const mine = await env.DB.prepare('SELECT handle, moves, ms FROM scores WHERE level = ?1 AND player = ?2')
    .bind(level, player)
    .first();

  if (!mine) return null;

  const ahead = await env.DB.prepare(
    `SELECT COUNT(*) AS ahead
       FROM scores
      WHERE level = ?1 AND (moves < ?2 OR (moves = ?2 AND ms < ?3))`
  )
    .bind(level, mine.moves, mine.ms)
    .first();

  return { rank: (ahead?.ahead ?? 0) + 1, handle: mine.handle, moves: mine.moves, ms: mine.ms };
}

/**
 * The board as the game draws it: the leading rows, plus the caller's own row when it fell outside
 * them. A caller already among the leaders is not sent twice.
 */
async function board(env, level, player) {
  const rows = await leaders(env, level, player);
  const you = await standing(env, level, player);

  return {
    level,
    rows,
    you: you && !rows.some((row) => row.you) ? you : undefined,
  };
}

async function optimalFor(env, level) {
  const row = await env.DB.prepare('SELECT optimal FROM levels WHERE slug = ?1').bind(level).first();
  return row ? row.optimal : null;
}

async function withinLimits(env, key) {
  if (!env.SCORE_LIMIT) return true;

  const { success } = await env.SCORE_LIMIT.limit({ key });
  return success;
}

async function postScore(request, env) {
  // Sent as text/plain so it stays a simple cross-origin request, which saves the browser a
  // preflight round trip before every score. It is still JSON in the body.
  let score;
  try {
    score = JSON.parse(await request.text());
  } catch {
    return json(request, { error: 'bad request' }, 400);
  }

  if (!isUuid(score?.player)) return json(request, { error: 'bad request' }, 400);

  const ip = request.headers.get('CF-Connecting-IP') ?? 'unknown';
  if (!(await withinLimits(env, score.player)) || !(await withinLimits(env, ip))) {
    return json(request, { error: 'slow down' }, 429);
  }

  const optimal = isSlug(score?.level) ? await optimalFor(env, score.level) : null;

  const verdict = checkScore(score, optimal, words);
  if (!verdict.ok) {
    console.log(`refused a score on ${score?.level}: ${verdict.reason}`);
    return json(request, { error: 'not a round we can record' }, 422);
  }

  // Only ever improves a row, by the same rule the game's own save file uses: fewer moves wins,
  // and the clock separates two rounds of the same length.
  await env.DB.prepare(
    `INSERT INTO scores (player, level, handle, moves, ms, at)
          VALUES (?1, ?2, ?3, ?4, ?5, ?6)
     ON CONFLICT(player, level) DO UPDATE SET
          handle = excluded.handle,
          moves = excluded.moves,
          ms = excluded.ms,
          at = excluded.at
          WHERE excluded.moves < scores.moves
             OR (excluded.moves = scores.moves AND excluded.ms < scores.ms)`
  )
    .bind(score.player, score.level, score.handle, score.moves, score.ms, Date.now())
    .run();

  return json(request, await board(env, score.level, score.player));
}

async function getBoard(request, env, url) {
  const level = url.searchParams.get('level');
  if (!isSlug(level)) return json(request, { error: 'bad request' }, 400);

  const player = url.searchParams.get('player');
  return json(request, await board(env, level, isUuid(player) ? player : null));
}

export default {
  async fetch(request, env) {
    const url = new URL(request.url);

    if (request.method === 'OPTIONS') {
      return new Response(null, { status: 204, headers: corsHeaders(request) });
    }

    try {
      if (request.method === 'POST' && url.pathname === '/v1/score') return await postScore(request, env);
      if (request.method === 'GET' && url.pathname === '/v1/board') return await getBoard(request, env, url);
    } catch (error) {
      console.log(`failed on ${url.pathname}: ${error}`);
      return json(request, { error: 'the board is having a moment' }, 500);
    }

    return json(request, { error: 'not found' }, 404);
  },
};
