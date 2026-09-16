"""Volcanic pixel-art palette for EmberDepths.

Every colour the generator can emit lives here. The pixelizer snaps each rendered
pixel to the nearest entry, which is what gives the Blender renders a coherent,
hand-authored pixel-art feel instead of a photoreal look.

Colours are sRGB hex, exactly as they will appear in the final PNG.

Design intent:
  * Environment owns the warm half of the wheel (basalt -> ember -> white-hot).
  * Players own the cool half (teal / steel / bone) so five party members stay
    readable on top of a screen full of orange lava. Do not add warm colours to
    the PLAYER ramp without re-checking contrast against LAVA.
"""

from __future__ import annotations

# --- rock & structure ------------------------------------------------------
VOID = "#0b0809"
OUTLINE = "#120d0e"

ROCK = [
    "#1b1517",
    "#271f21",
    "#362b2d",
    "#47393a",
    "#5a4849",
    "#6e5a59",
]

ASH = [
    "#544c4a",
    "#6d635f",
    "#8a7d76",
    "#a6968c",
    "#c2b2a4",
]

OBSIDIAN = [
    "#14111c",
    "#1f1a2b",
    "#2d2440",
    "#3d3055",
]

# --- heat ramp: the single most important ramp in the game ------------------
# Ordered coolest -> hottest. Hazard intensity, enemy "charge" tells and boss
# phase tinting all sample this ramp by index so they stay visually consistent.
LAVA = [
    "#5c1410",
    "#8a1f13",
    "#b8331a",
    "#d9541d",
    "#ee7a1b",
    "#f7a325",
    "#fbc93f",
    "#ffe98a",
    "#fff8d4",
]

EMBER_GLOW = "#ff9d3c"

# --- cool ramp: players, UI, friendly VFX ----------------------------------
PLAYER = [
    "#16303a",
    "#1f4a57",
    "#2b6d78",
    "#3a9aa0",
    "#5cc7c3",
    "#9ce8df",
]

STEEL = [
    "#2a2f38",
    "#3d4550",
    "#59616e",
    "#7c8794",
    "#a5b0bc",
    "#d2d9e2",
]

BONE = [
    "#5c5041",
    "#8a7a60",
    "#b5a481",
    "#d8c9a3",
    "#efe6cb",
]

# --- accents ---------------------------------------------------------------
CRYSTAL = ["#1d4f52", "#2e7d7a", "#46b3a8", "#7fe3d2"]
VENOM = ["#3a1a3d", "#6a2145", "#9c3160"]

#: Every colour the pixelizer may snap to.
FULL_PALETTE: list[str] = (
    [VOID, OUTLINE, EMBER_GLOW]
    + ROCK
    + ASH
    + OBSIDIAN
    + LAVA
    + PLAYER
    + STEEL
    + BONE
    + CRYSTAL
    + VENOM
)

#: Terrain-only subset: the warm half of the wheel plus neutrals.
#:
#: Floors and walls are snapped to THIS rather than to FULL_PALETTE, and the
#: omission is the whole point. Generated stone tends to come back with a cool
#: cast, and if STEEL and PLAYER are available to snap to, a bluish floor simply
#: stays bluish. Removing them forces terrain into the ROCK / ASH / OBSIDIAN
#: ramps, which keeps the cool half of the palette reserved for the five party
#: members — the thing that makes them readable on a screen full of lava.
ENVIRONMENT_PALETTE: list[str] = (
    [VOID, OUTLINE, EMBER_GLOW]
    + ROCK
    + ASH
    + OBSIDIAN
    + LAVA
)


def hex_to_rgb(value: str) -> tuple[float, float, float]:
    """'#ff8800' -> (1.0, 0.533, 0.0) in 0..1 sRGB."""
    value = value.lstrip("#")
    return tuple(int(value[i : i + 2], 16) / 255.0 for i in (0, 2, 4))  # type: ignore[return-value]


def hex_to_linear(value: str) -> tuple[float, float, float]:
    """sRGB hex -> Blender's scene-linear working space."""
    return tuple(_srgb_to_linear(c) for c in hex_to_rgb(value))  # type: ignore[return-value]


def _srgb_to_linear(c: float) -> float:
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def linear_to_srgb(c: float) -> float:
    """Inverse of :func:`_srgb_to_linear`.

    Needed because Blender hands out pixel data in scene-linear space, while
    this palette — and the eye that authored it — lives in sRGB. Snapping in
    linear space would bunch every dark tone together and pull midtones apart.
    """
    if c <= 0.0:
        return 0.0
    if c >= 1.0:
        return 1.0
    return c * 12.92 if c <= 0.0031308 else 1.055 * (c ** (1.0 / 2.4)) - 0.055


def heat(t: float) -> str:
    """Sample the lava ramp with t in 0..1. The canonical way to pick a heat colour."""
    t = min(max(t, 0.0), 1.0)
    return LAVA[round(t * (len(LAVA) - 1))]
