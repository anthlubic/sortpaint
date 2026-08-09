"""How far apart two bead colours look on the board.

Plain colour distance is the wrong measure here. shaders/sphere.gdshader lights every bead from
`ambient` (0.45) up to full brightness before adding a specular hit, so one bead already covers
more than twice its own lightness: the lit side of a dark bead lands on the shaded side of a light
one. Lightness is spent on the lighting, which leaves hue and chroma as the only stable cue.

`separation` is therefore CIEDE2000 with the lightness term divided by LIGHTNESS_DISCOUNT, which is
roughly the range the shader eats. It is not a WCAG contrast ratio and does not try to be one: it
answers "would a player mix these two up", not "is this readable text".

The thresholds are set from the shipped art. After the palette was rotated apart in hue, the
tightest pair any picture actually uses is straw against white at 10.8, so REFUSE sits just below
that and CLOSE sits above it: nothing shipped is refused, and nothing may get worse than what
shipped.
"""

import math

LIGHTNESS_DISCOUNT = 2.5
"""How much of a lightness difference survives the bead shader. See the module docstring."""

REFUSE = 10.0
"""Below this two beads are a coin flip, and a picture using both is not a level."""

CLOSE = 14.0
"""Below this they are distinguishable but they are work. Worth reporting, not worth refusing."""


def _to_lab(rgb):
    """sRGB bytes to CIE L*a*b* under D65."""

    def linear(channel):
        channel /= 255.0
        return channel / 12.92 if channel <= 0.04045 else ((channel + 0.055) / 1.055) ** 2.4

    red, green, blue = (linear(float(c)) for c in rgb)
    x = red * 0.4124564 + green * 0.3575761 + blue * 0.1804375
    y = red * 0.2126729 + green * 0.7151522 + blue * 0.0721750
    z = red * 0.0193339 + green * 0.1191920 + blue * 0.9503041

    def f(t):
        return t ** (1 / 3) if t > (6 / 29) ** 3 else t / (3 * (6 / 29) ** 2) + 4 / 29

    fx, fy, fz = f(x / 0.95047), f(y), f(z / 1.08883)
    return 116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz)


def _ciede2000(lab1, lab2, lightness_discount=1.0):
    """CIEDE2000, optionally with the lightness term divided down. See the module docstring."""
    l1, a1, b1 = lab1
    l2, a2, b2 = lab2

    c1, c2 = math.hypot(a1, b1), math.hypot(a2, b2)
    cbar = (c1 + c2) / 2
    g = 0.5 * (1 - math.sqrt(cbar**7 / (cbar**7 + 25**7))) if cbar else 0.5
    a1p, a2p = (1 + g) * a1, (1 + g) * a2
    c1p, c2p = math.hypot(a1p, b1), math.hypot(a2p, b2)

    h1p = math.degrees(math.atan2(b1, a1p)) % 360 if (a1p or b1) else 0.0
    h2p = math.degrees(math.atan2(b2, a2p)) % 360 if (a2p or b2) else 0.0

    delta_lp = (l2 - l1) / lightness_discount
    delta_cp = c2p - c1p
    if c1p * c2p == 0:
        delta_hp = 0.0
    elif abs(h2p - h1p) <= 180:
        delta_hp = h2p - h1p
    elif h2p - h1p > 180:
        delta_hp = h2p - h1p - 360
    else:
        delta_hp = h2p - h1p + 360
    delta_big_hp = 2 * math.sqrt(c1p * c2p) * math.sin(math.radians(delta_hp) / 2)

    lbar = (l1 + l2) / 2
    cbarp = (c1p + c2p) / 2
    if c1p * c2p == 0:
        hbarp = h1p + h2p
    elif abs(h1p - h2p) <= 180:
        hbarp = (h1p + h2p) / 2
    elif h1p + h2p < 360:
        hbarp = (h1p + h2p + 360) / 2
    else:
        hbarp = (h1p + h2p - 360) / 2

    t = (
        1
        - 0.17 * math.cos(math.radians(hbarp - 30))
        + 0.24 * math.cos(math.radians(2 * hbarp))
        + 0.32 * math.cos(math.radians(3 * hbarp + 6))
        - 0.20 * math.cos(math.radians(4 * hbarp - 63))
    )
    delta_theta = 30 * math.exp(-(((hbarp - 275) / 25) ** 2))
    rc = 2 * math.sqrt(cbarp**7 / (cbarp**7 + 25**7)) if cbarp else 0.0
    sl = 1 + (0.015 * (lbar - 50) ** 2) / math.sqrt(20 + (lbar - 50) ** 2)
    sc = 1 + 0.045 * cbarp
    sh = 1 + 0.015 * cbarp * t
    rt = -math.sin(math.radians(2 * delta_theta)) * rc

    return math.sqrt(
        (delta_lp / sl) ** 2
        + (delta_cp / sc) ** 2
        + (delta_big_hp / sh) ** 2
        + rt * (delta_cp / sc) * (delta_big_hp / sh)
    )


def separation(first, second):
    """How far apart two (r, g, b) bead colours look once the shader has had its share."""
    return _ciede2000(_to_lab(first), _to_lab(second), LIGHTNESS_DISCOUNT)


def distance(first, second):
    """Plain CIEDE2000, for reporting alongside `separation` when the two disagree."""
    return _ciede2000(_to_lab(first), _to_lab(second))


def pairs(palette):
    """Every pair of colours in one picture, closest first.

    `palette` is a sequence of (r, g, b). Yields (separation, index, index) so callers can name
    the colours however they name them.
    """
    found = []
    for i in range(len(palette)):
        for j in range(i + 1, len(palette)):
            found.append((separation(palette[i], palette[j]), i, j))
    found.sort()
    return found


def tightest(palette):
    """The closest pair in a picture, or None when it has fewer than two colours."""
    found = pairs(palette)
    return found[0] if found else None
