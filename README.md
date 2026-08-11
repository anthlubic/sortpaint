# Sort Painting

A tap-to-sort painting puzzle. Every square of the picture already knows its colour; the spheres
sitting on top of them do not. Sort the spheres back onto their matching squares to finish the piece.

[Play it in the browser.](https://anthlubic.github.io/sortpaint/) Every push to `main` rebuilds and
republishes it.

## Getting around

The game opens on the level select: a five by five page of pictures. Tap one to light it up, then
Play. A picture you have already painted keeps a trophy in its corner, gold when the best round
came in on par and silver when it did not, and the header counts the gold ones. Squares past the end of the
level list are blank, so the page keeps its shape while the set grows. In a level, `Levels` goes
back to the page and `Restart` deals the same puzzle again.

Finished levels are remembered in `user://progress.json`, keyed by each level resource's path,
along with the fewest moves each one has been finished in.

## Locked levels

A level can ask for a number of gold trophies before it opens, which is what `RequiredChecks` on
the `LevelData` resource holds. The first eighty five levels ask for none. After those, each chunk of
five asks for five more than the chunk before it: five trophies, then ten, and so on up to forty.

A locked square shows its picture washed out under a padlock. Tapping it says what it wants, and the
count in the header says how many gold trophies the player has. The rule itself is
`src/Core/Unlock.cs`, and gold goes to a level whose best round came in on par, counted the same way
the tile badges are. A silver trophy buys nothing; it only says the picture is finished.

## Par

Every level carries a move target, shown beside the clock as `Moves: 12/72`. Par is the shortest
solution the search in `scripts/sortpaint/par.py` can find, plus forty percent. Going over it is
allowed and the round can still be finished; the count just turns red and the trophy on the menu
comes out silver rather than gold.

The search is far too slow to run while a level opens, so it runs at authoring time and its answer
is stored in the level resource as `OptimalMoves`. `src/Core/Par.cs` is where the allowance is
applied. Levels with no number stored play without a target.

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

Two fingers pinch the picture open, up to four times its fitted size, and drag it around once it is
too big for the window. It can never be dragged past its own edges, and pinching back closed puts it
straight again. On a touch screen a tap lands when the finger lifts, so opening a pinch never counts
as a tap.

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
| `scripts/` | Utilities. `import_level.py` turns an image into a level; `update_par.py` works out each level's move target; `make_level_sprites.py` renders the hand-authored level PNGs from ASCII art. |

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

That downscales, quantises, and then plays the level through with a stand-in player, and once more
to find its par. Only if it finishes does anything get written: the PNG, the resource, and the
entry in `campaign.tres`. A
picture that cannot be scrambled clean, or that dead-ends, is reported with the reason and the tree
is left alone. The seed is searched for rather than picked, so re-running the same import is a no-op.

Add `--required-checks N` to lock the level behind that many gold trophies; leave it off and the
level opens from the start. Re-importing keeps whatever lock the level already had, the way it keeps its seed.

By hand: draw the PNG (or add it to `scripts/make_level_sprites.py`), make the resource in the
inspector, and add it to `campaign.tres` yourself. Then `python3 scripts/update_par.py <name>` for
its move target; the same command re-runs an existing level's, which is what a changed picture,
seed or tray size needs.

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
