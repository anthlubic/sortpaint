# Sort Painting

A tap-to-sort painting puzzle. Every square of the picture already knows its colour; the spheres
sitting on top of them do not. Sort the spheres back onto their matching squares to finish the piece.

[Play it in the browser.](https://anthlubic.github.io/sortpaint/) Every push to `main` rebuilds and
republishes it.

## Getting around

The game opens on the level select: a five by five page of pictures. Tap one to light it up, then
Play. A picture you have already painted keeps a green tick in its corner, and the header counts
them. Squares past the end of the level list are blank, so the page keeps its shape while the set
grows. In a level, `Levels` goes back to the page and `Restart` deals the same puzzle again.

Finished levels are remembered in `user://progress.json`, keyed by each level resource's path.

## Rules

Nothing moves on one tap. The first tap picks a run up, and it hovers until a second tap says where
it goes.

- Tap a sphere that is on the wrong colour and its whole connected run of that sphere colour lifts.
  Two runs of the same colour split by a different one are separate, and need a tap each.
- Tap the tray and the hovering run comes down into it. The tray holds 24 spheres and takes as many
  as will fit, nearest the first tap first.
- Tap a bare square waiting on that colour and the run goes straight there instead, skipping the
  tray and costing no tray room. It fills that square's whole connected run of squares.
- Tap a colour in the tray to lift all of it, then a bare square to put it down.
- Tap the run that is already up (the same spheres, or the tray colour again) to put it back and
  choose something else. Taps that mean nothing leave it alone.
- Spheres only ever land on a matching square, so once one is down it is painted and stays put.
- Nothing ends a level but finishing the picture. A board that has run out of moves just sits there
  until you press `Restart`.

## Running it

```sh
godot --path .          # play
dotnet build            # compile the C# without opening the editor
dotnet test tests/SortPaint.Tests    # the rules, ~50 tests, no Godot needed
```

## Layout

| Path | What lives there |
| --- | --- |
| `src/Core/` | The rules and the progress record. Plain C#, no Godot types, so `dotnet test` can drive them directly. |
| `src/` | Godot-facing code: the level resource, the board and tray views, the menu, the controller. |
| `scenes/` | `LevelSelect.tscn` is the menu and the scene the game starts on, `Main.tscn` is a level. `Cell.tscn` is one square and doubles as a tray socket; `LevelTile.tscn` is one square of the menu. |
| `shaders/` | The bead's lighting and the divot pressed into each square, with their look exposed as inspector uniforms. |
| `themes/` | `pastel.tres`, the theme every screen draws from. Buttons, panels, and label roles live here rather than in the scenes. |
| `fonts/` | Fira Sans, bundled so the web build has the same type as the desktop one. |
| `levels/` | One PNG per level plus a `LevelData` resource pointing at it, and `campaign.tres` listing the levels the menu offers. |
| `scripts/` | Utilities. `import_level.py` turns an image into a level; `make_level_sprites.py` renders the hand-authored level PNGs from ASCII art. |

`GameSession` is the one autoload: it carries the menu's choice into the level and owns the record
of what has been painted.

## Authoring a level

A level is a small PNG: one pixel is one square, every distinct opaque colour becomes a sphere
colour, and transparent pixels are holes in the picture. The `LevelData` resource beside it sets the
tray size and the shuffle seed, and `levels/campaign.tres` lists the levels the menu offers, in order.

From an image:

```sh
python3 scripts/import_level.py art/dog.png --size 16 --colors 6
```

That downscales, quantises, and then plays the level through with a stand-in player. Only if it
finishes does anything get written: the PNG, the resource, and the entry in `campaign.tres`. A
picture that cannot be scrambled clean, or that dead-ends, is reported with the reason and the tree
is left alone. The seed is searched for rather than picked, so re-running the same import is a no-op.

By hand: draw the PNG (or add it to `scripts/make_level_sprites.py`), make the resource in the
inspector, and add it to `campaign.tres` yourself.

Either way, add the picture to `tests/SortPaint.Tests/LevelSprites.cs`; the importer prints the
block to paste. The tests scramble every shipped level at the seed its resource carries and play it
through, so a level that cannot be finished never reaches the menu.

One catch worth knowing: an opening where nothing starts already painted only exists when no single
colour covers more than half the picture. Both scripts say so when a level crosses that line.

`scripts/sortpaint/` holds Python ports of the rules, including one of .NET's seeded
`System.Random`, which is how the importer can play a level without a Godot runtime or a build.
`scripts/scramble_parity.json` and `ScrambleParityTests` hold those ports to the C#; regenerate the
fixture with `python3 scripts/import_level.py --write-parity` when a level changes, and never to
quiet a failing test.
