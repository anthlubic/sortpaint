---
name: new-level
description: Add a level to Sort Painting from an image. Use when asked to add, make, import, or generate a level or puzzle, when handed a PNG or JPG to turn into one, or when asked to draw level art with Gemini / Nano Banana. Covers scripts/import_level.py, the checks a picture has to pass, the LevelSprites.cs fixture, and verifying the level in the running game.
---

# Adding a level

## When to use

Adding a new picture to the game. Either the developer hands over an image, or they ask for one
to be generated.

Not for changing an existing level's tuning: tray size, shuffle seed, and display name live in
`levels/<name>.tres` and are editable in the Godot inspector. Changing the tray or the seed does
mean re-running `python3 scripts/update_par.py <name>`, since both change how short the level can
be played. Not for the hand-authored ASCII sprites either; those are
`scripts/make_level_sprites.py`, a separate and older path.

## Steps

### 1. Get an image

If the developer supplied one, use it as-is. Otherwise generate one:

```sh
export GEMINI_API_KEY=...   # from https://aistudio.google.com/apikey
python3 .claude/skills/new-level/scripts/generate_image.py \
    "<prompt>" --out /tmp/<name>.png
```

If `GEMINI_API_KEY` is not set, ask the developer for one rather than looking for a way around
it. Do not fall back to drawing the picture some other way without saying so.

The prompt decides whether the import succeeds, because the checks in step 2 are strict. Ask for:

- **a single subject, filling the frame**: a small object on a wide background fails the
  "no colour covers more than half" check
- **flat blocks of colour, no gradients, no shading, no texture**: gradients survive
  quantisation as speckle
- **a hard dark outline**
- **five or six colours, plainly different in hue, not just in brightness**: a colour and a
  darker version of itself is the one failure the eye forgives on the page and the game does not.
  The bead shader lights every sphere from 45% to 100% brightness, so a bead already covers more
  than twice its own lightness and a shadow colour has nothing left to distinguish it. Say "six
  flat colours, each a different hue" rather than "six colours"
- **a plain background in a colour used elsewhere in the picture**, or a transparent one
- **square, front-on, centred, no perspective**

A prompt that works: `"a red toadstool with white spots and a cream stem, flat vector sticker
art, bold dark outline, six flat colours, no gradients or shading, centred, fills the frame,
plain background, square"`.

### 2. Import it

```sh
python3 scripts/import_level.py /tmp/<name>.png --size 16 --colors 6
```

`--size 16` matches every shipped level; `--colors 6` is the usual count. The script downscales,
quantises, then plays the level through with a stand-in player before writing anything. On
success it writes `levels/<name>.png` and `levels/<name>.tres` and adds the level to
`levels/campaign.tres`.

It plays the level a second time to find par, which is the slow part (a few seconds). The
shortest solution it finds lands in the .tres as `OptimalMoves`; the game adds the allowance and
shows the result as the `Moves: 12/72` target. `scripts/update_par.py` redoes that for levels
that already exist.

Add `--dry-run` to see the verdict without writing. Add `--name` when the filename is not the
level name you want.

**If it refuses**, the message names the check and the fix. In practice:

| Refusal | What to do |
| --- | --- |
| one colour covers more than half | crop tighter to the subject, or raise `--colors` to split the dominant area |
| too few colours | raise `--colors`, or regenerate with more contrast in the prompt |
| more than 8 colours | lower `--colors` |
| fewer cells than the tray | raise `--size`, or use art with less transparency |
| no finishable level from N seeds | the picture is too fragmented; lower `--colors`, or regenerate something bolder |
| two colours too close to tell apart | two of the quantised colours are the same hue at different brightness. Regenerate asking for distinct hues, or lower `--colors` so the two merge into one |

Regenerate the art rather than fighting the flags when two or more refusals stack up. A picture
that barely passes makes a poor puzzle.

### 3. Cover it with tests

The importer prints a block to paste into `tests/SortPaint.Tests/LevelSprites.cs`: an array
plus one `yield return` line in `Shipped()`. Paste both. Until you do, `dotnet test` does not
play the level, and `ShippedLevelsTests` is what stands between a broken level and the menu.

Then refresh the fixture that keeps the Python ports honest, and run the suite:

```sh
python3 scripts/import_level.py --write-parity
dotnet test tests/SortPaint.Tests
```

All of it must pass. A `ScrambleParityTests` failure means the Python rules and the C# rules
disagree. Do not paper over it by regenerating the fixture, because the importer's verdict on
every level is only worth what that agreement is worth.

### 4. See it in the game

```sh
godot --headless --path . --import   # Godot writes levels/<name>.png.import
```

Then run the project and open the level. The tile should appear on the level select in list
order with a legible thumbnail, and the board should open with no cell already painted.

Godot MCP is wired up for this: `run_project`, then `game_screenshot`. Clicking the tile through
`game_click` needs the screenshot coordinates scaled to the 720x1280 viewport; it is usually less
fuss to jump straight in:

```gdscript
var session = get_node("/root/GameSession")
session.SelectedLevel = load("res://levels/<name>.tres")
get_tree().change_scene_to_file("res://scenes/Main.tscn")
```

Look at the screenshot before calling it done. The checks prove a level is *solvable*; only
looking proves it is worth playing. A picture that reads as mush at 16x16 passes every test.

## Notes

- The PNG is the level. `src/LevelLoader.cs` reads it at runtime: one pixel is one cell, every
  distinct opaque colour is a sphere colour, transparent pixels are holes. There is no other
  level format.
- Seeds are searched, not chosen. The importer starts at `20260809 + <levels so far>` and walks
  up until a seed yields a level the stand-in player finishes. Re-importing a level keeps the
  seed it already has, so a repeat run is a no-op.
- The menu is a five by five page. Past 25 levels the extras exist but are not shown, and the
  importer warns about it.
- The importer never writes until every check passes, so a refusal leaves the tree untouched.
- Par is worked out at authoring time, never in the game: the search in `scripts/sortpaint/par.py`
  takes seconds, which a level opening cannot spend. Every plan is replayed through
  `scripts/sortpaint/rules.py` before its length is written, so a shipped target is one that has
  been played. `python3 scripts/update_par.py --check` reports levels whose stored number no
  longer matches the search and exits non-zero, without writing.
- Colour separation is measured in `scripts/sortpaint/contrast.py`, which is CIEDE2000 with the
  lightness term discounted because the bead shader spends that lightness on lighting. It is not
  a WCAG ratio. Under 10 is refused, under 14 is reported as tight. A picture drawn from
  `scripts/level_art.py` inherits the shared palette, but that palette is only as safe as the
  combinations already in use: pairing two entries no other picture pairs can still land under
  the threshold, which is what `make_level_sprites.py --check` is for.
