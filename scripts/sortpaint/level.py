"""Reading and writing the things around a level: the PNG, the .tres, and campaign.tres.

`grid_from_pixels` is the part that has to agree with the game: it is src/LevelLoader.cs,
so a grid built here is numbered the way the running game numbers it. The rest is plain text
handling of Godot resource files, deliberately narrow: it understands the shape the project
already uses and refuses anything it does not recognise rather than guessing.
"""

import re
from pathlib import Path

from .rules import EMPTY, LevelGrid

DEFAULT_ALPHA_THRESHOLD = 0.5
DEFAULT_TRAY_CAPACITY = 24

# The first shipped level's seed. New levels continue upwards from here.
BASE_SEED = 20260809


def grid_from_pixels(width, height, pixels, alpha_threshold=DEFAULT_ALPHA_THRESHOLD):
    """LevelLoader.Load: pixels to a grid, colours numbered in first-encountered order.

    `pixels` is a flat sequence of (r, g, b, a) byte tuples in reading order. Colours are keyed
    on RGB alone, so partial transparency in the source does not split a colour in two.
    Returns (grid, palette) where palette[i] is the (r, g, b) of colour i.
    """
    targets = []
    lookup = {}
    palette = []

    for r, g, b, a in pixels:
        if a / 255.0 < alpha_threshold:
            targets.append(EMPTY)
            continue

        key = (r, g, b)
        index = lookup.get(key)
        if index is None:
            index = len(palette)
            lookup[key] = index
            palette.append(key)

        targets.append(index)

    return LevelGrid(width, height, targets), palette


def pixels_of(image):
    """Every pixel in reading order. Pillow renamed getdata partway through 12.x."""
    flatten = getattr(image, "get_flattened_data", None)
    return list(flatten() if flatten else image.getdata())


def grid_from_png(path, alpha_threshold=DEFAULT_ALPHA_THRESHOLD):
    """The same, straight off disk. Needs Pillow."""
    from PIL import Image

    with Image.open(path) as handle:
        image = handle.convert("RGBA")
        return grid_from_pixels(image.width, image.height, pixels_of(image), alpha_threshold)


CANONICAL_LETTERS = "orwsdgnbycakepl"
"""Letters for palette indices 0, 1, 2… in order. Positional, not meaningful."""


def as_ascii(grid):
    """The grid as lowercase rows, in the shape tests/SortPaint.Tests/LevelSprites.cs uses.

    The letters are assigned by palette index, so they will not match the hand-authored blocks,
    where a letter names the colour it stands for ('o' outline, 'r' red, and so on). Only the
    grouping carries meaning (LevelSprites.Grid renumbers by first-encountered letter), so any
    consistent assignment describes the same level. ScrambleParityTests re-letters both sides
    into this alphabet before comparing, which is what lets the two live side by side.
    """
    letters = CANONICAL_LETTERS
    if len(grid.color_counts()) > len(letters):
        raise ValueError(f"no letter for colour {len(letters)} and beyond")

    rows = []
    for y in range(grid.height):
        row = []
        for x in range(grid.width):
            target = grid.target_at(grid.index(x, y))
            row.append("." if target == EMPTY else letters[target])
        rows.append("".join(row))
    return rows


LEVEL_TRES = """[gd_resource type="Resource" script_class="LevelData" load_steps=3 format=3]

[ext_resource type="Script" path="res://src/LevelData.cs" id="1_script"]
[ext_resource type="Texture2D" path="res://levels/{name}.png" id="2_sprite"]

[resource]
script = ExtResource("1_script")
DisplayName = "{display_name}"
Sprite = ExtResource("2_sprite")
TrayCapacity = {tray_capacity}
ShuffleSeed = {seed}
AlphaThreshold = {alpha_threshold}
"""


def level_tres(name, display_name, seed, tray_capacity, alpha_threshold):
    """The LevelData resource, shaped exactly like the hand-authored ones in levels/."""
    return LEVEL_TRES.format(
        name=name,
        display_name=display_name,
        seed=seed,
        tray_capacity=tray_capacity,
        alpha_threshold=_trim_float(alpha_threshold),
    )


def _trim_float(value):
    """Godot writes 0.5 rather than 0.500000, and a whole number keeps its point."""
    text = f"{value:g}"
    return text if "." in text or "e" in text else text + ".0"


_LEVEL_FIELD = re.compile(r"^(\w+) = (.+)$", re.MULTILINE)


def read_level_tres(path):
    """The fields of a LevelData resource, as strings, plus its sprite name."""
    text = Path(path).read_text()
    fields = dict(_LEVEL_FIELD.findall(text))
    sprite = re.search(r'type="Texture2D" path="res://levels/([^"]+)\.png"', text)
    return {
        "display_name": fields.get("DisplayName", "").strip('"'),
        "seed": int(fields.get("ShuffleSeed", BASE_SEED)),
        "tray_capacity": int(fields.get("TrayCapacity", DEFAULT_TRAY_CAPACITY)),
        "alpha_threshold": float(fields.get("AlphaThreshold", DEFAULT_ALPHA_THRESHOLD)),
        "sprite": sprite.group(1) if sprite else None,
    }


_CAMPAIGN_EXT = re.compile(r'^\[ext_resource type="Resource" path="res://levels/([^"]+)\.tres" id="([^"]+)"\]$', re.MULTILINE)


def read_campaign(path):
    """The level names campaign.tres offers, in menu order."""
    text = Path(path).read_text()
    ids = {ext_id: name for name, ext_id in _CAMPAIGN_EXT.findall(text)}

    listed = re.search(r"^Levels = Array\[Resource\]\(\[(.*)\]\)$", text, re.MULTILINE)
    if listed is None:
        raise ValueError(f"{path} has no Levels array in the shape this script understands")

    order = re.findall(r'ExtResource\("([^"]+)"\)', listed.group(1))
    return [ids[ext_id] for ext_id in order if ext_id in ids]


def campaign_with(path, name):
    """campaign.tres with `name` appended, or unchanged text if it is already listed."""
    text = Path(path).read_text()
    if name in read_campaign(path):
        return text

    script_step = 1  # the LevelSet script's own ext_resource
    ext_id = f"{len(read_campaign(path)) + 1 + script_step}_{name}"
    entry = f'[ext_resource type="Resource" path="res://levels/{name}.tres" id="{ext_id}"]'

    last_ext = list(_CAMPAIGN_EXT.finditer(text))[-1]
    text = text[: last_ext.end()] + "\n" + entry + text[last_ext.end() :]

    text = re.sub(
        r"(\[gd_resource [^\]]*load_steps=)(\d+)",
        lambda m: m.group(1) + str(int(m.group(2)) + 1),
        text,
        count=1,
    )

    return re.sub(
        r"^(Levels = Array\[Resource\]\(\[.*)\]\)$",
        lambda m: f'{m.group(1)}, ExtResource("{ext_id}")])',
        text,
        count=1,
        flags=re.MULTILINE,
    )
