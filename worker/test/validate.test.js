import { describe, expect, it } from 'vitest';
import cases from '../handle-cases.json';
import words from '../words.json';
import {
  MAX_MS,
  checkScore,
  isSlug,
  isUuid,
  isWellFormedHandle,
  movesCeiling,
  movesFloor,
} from '../src/validate.js';

const PLAYER = 'a8465a17-a439-4496-ab75-bd3e0da1c917';

/** A round that should sail through, so each test can spoil exactly one thing about it. */
function score(overrides = {}) {
  return { player: PLAYER, handle: 'BriskAxolotl042', level: 'apple', moves: 94, ms: 200_000, ...overrides };
}

describe('handles', () => {
  it('accepts every pairing the game can hand out', () => {
    for (const adjective of words.adjectives) {
      for (const noun of words.nouns) {
        expect(isWellFormedHandle(`${adjective}${noun}000`, words)).toBe(true);
      }
    }
  });

  it('refuses a word that is not on the lists', () => {
    // The whole moderation story: there is no way to get an arbitrary word onto a board.
    expect(isWellFormedHandle('BriskWombat042', words)).toBe(false);
    expect(isWellFormedHandle('SneakyAxolotl042', words)).toBe(false);
    expect(isWellFormedHandle('DropTableScores042', words)).toBe(false);
    expect(isWellFormedHandle('<script>alert(1)</script>', words)).toBe(false);
  });

  it('refuses anything that is not a handle at all', () => {
    for (const bad of [null, undefined, 42, {}, []]) {
      expect(isWellFormedHandle(bad, words)).toBe(false);
    }
  });

  // The shared fixture. The game's own suite runs these same cases against src/Core/Handle.cs, so
  // the two implementations are held to one answer rather than merely to one word list.
  it.each(cases.valid)('accepts %j, as the game would', (handle) => {
    expect(isWellFormedHandle(handle, words)).toBe(true);
  });

  it.each(cases.invalid)('refuses %j, as the game would', (handle) => {
    expect(isWellFormedHandle(handle, words)).toBe(false);
  });
});

describe('ids and slugs', () => {
  it('accepts a uuid and refuses anything else', () => {
    expect(isUuid(PLAYER)).toBe(true);
    expect(isUuid('not-a-uuid')).toBe(false);
    expect(isUuid('')).toBe(false);
    expect(isUuid(null)).toBe(false);
  });

  it('accepts a level slug and refuses path tricks', () => {
    expect(isSlug('apple')).toBe(true);
    expect(isSlug('sea-turtle')).toBe(true);
    expect(isSlug('../../etc/passwd')).toBe(false);
    expect(isSlug('Apple')).toBe(false);
    expect(isSlug('')).toBe(false);
  });
});

describe('the moves floor', () => {
  it('sits below the known optimal, because the optimal is not proven', () => {
    // scripts/sortpaint/par.py runs a beam search, so a player can genuinely beat OptimalMoves.
    // A floor at the optimal would throw away exactly the rounds worth celebrating.
    expect(movesFloor(88)).toBe(66);
    expect(movesFloor(88)).toBeLessThan(88);
  });

  it('accepts a record that beats the optimal', () => {
    expect(checkScore(score({ moves: 80, ms: 200_000 }), 88, words).ok).toBe(true);
  });

  it('refuses a round nobody could have played', () => {
    expect(checkScore(score({ moves: 1, ms: 200_000 }), 88, words).ok).toBe(false);
    expect(checkScore(score({ moves: 0, ms: 200_000 }), 88, words).ok).toBe(false);
  });

  it('refuses a round with more moves than a real one', () => {
    expect(movesCeiling(88)).toBe(1760);
    expect(checkScore(score({ moves: 5000, ms: 200_000 }), 88, words).ok).toBe(false);
  });

  it('lets a level with no optimal worked out through on any honest count', () => {
    expect(movesFloor(0)).toBe(1);
    expect(checkScore(score({ moves: 200, ms: 200_000 }), 0, words).ok).toBe(true);
  });
});

describe('the clock', () => {
  it('refuses a round quicker than the taps would allow', () => {
    expect(checkScore(score({ moves: 94, ms: 10 }), 88, words).ok).toBe(false);
  });

  it('refuses a tab that was left open rather than played', () => {
    expect(checkScore(score({ ms: MAX_MS + 1 }), 88, words).ok).toBe(false);
  });
});

describe('checkScore', () => {
  it('accepts an ordinary round', () => {
    expect(checkScore(score(), 88, words)).toEqual({ ok: true });
  });

  it('refuses a level the server has never heard of', () => {
    expect(checkScore(score({ level: 'not-a-level' }), null, words).ok).toBe(false);
  });

  it('refuses a handle it would never have handed out', () => {
    expect(checkScore(score({ handle: 'Anything I Like' }), 88, words).ok).toBe(false);
  });

  it('refuses a player id that is not a uuid', () => {
    expect(checkScore(score({ player: 'me' }), 88, words).ok).toBe(false);
  });

  it('refuses counts that are not whole numbers', () => {
    expect(checkScore(score({ moves: 94.5 }), 88, words).ok).toBe(false);
    expect(checkScore(score({ moves: '94' }), 88, words).ok).toBe(false);
    expect(checkScore(score({ ms: -1 }), 88, words).ok).toBe(false);
  });

  it('refuses nothing at all', () => {
    expect(checkScore(null, 88, words).ok).toBe(false);
    expect(checkScore('a score, honest', 88, words).ok).toBe(false);
  });
});
