"""Turns a render into pixel art.

Two operations, both of which matter more than they sound:

**Palette snapping.** Every pixel is moved to its nearest entry in
``palette.FULL_PALETTE``. This is what makes a Blender render sit next to a
PixelLab sprite without looking like it came from a different game. Without it
the props are subtly smoother and richer than the hand-styled art, and the
mismatch is immediately visible.

**Alpha thresholding.** Anti-aliased edges are removed outright — a pixel is
either fully opaque or fully transparent. Soft edges are the clearest tell that
art was rendered rather than drawn, and they produce halos when the sprite is
drawn over a bright lava floor.

Everything here works on raw PNG bytes via :mod:`edgen.png`, never through
Blender's image API. Blender applies colour management on both read and write,
and the two transforms are not exact inverses, so pixels come back *near* the
palette instead of on it. Since the whole point is exactness, the colour
management has to be out of the loop entirely.

A consequence worth having: this module imports no Blender, so it can be run
and tested with any Python.
"""

from __future__ import annotations

from . import palette, png

#: Below this the pixel becomes fully transparent, above it fully opaque.
ALPHA_THRESHOLD = 128


def _palette_bytes() -> list[tuple[int, int, int]]:
    out = []
    for entry in palette.FULL_PALETTE:
        value = entry.lstrip("#")
        out.append((int(value[0:2], 16), int(value[2:4], 16), int(value[4:6], 16)))
    return out


def quantise_file(path: str, out_path: str | None = None) -> int:
    """Snaps one PNG to the palette in place (or to ``out_path``).

    Returns the number of opaque pixels, which the caller uses to notice a
    render that came out empty — usually a framing bug, and much easier to spot
    here than by opening 64 files.
    """
    width, height, pixels = png.read_rgba(path)
    entries = _palette_bytes()

    # Renders use few distinct shades, so caching the mapping turns this from a
    # per-pixel search into a per-shade one.
    cache: dict[tuple[int, int, int], tuple[int, int, int]] = {}
    opaque = 0

    for i in range(0, len(pixels), 4):
        if pixels[i + 3] < ALPHA_THRESHOLD:
            pixels[i] = pixels[i + 1] = pixels[i + 2] = 0
            pixels[i + 3] = 0
            continue

        opaque += 1
        pixels[i + 3] = 255

        key = (pixels[i], pixels[i + 1], pixels[i + 2])
        snapped = cache.get(key)
        if snapped is None:
            snapped = nearest(key, entries)
            cache[key] = snapped

        pixels[i], pixels[i + 1], pixels[i + 2] = snapped

    png.write_rgba(out_path or path, width, height, bytes(pixels))
    return opaque


def nearest(colour: tuple[int, int, int], entries: list[tuple[int, int, int]]):
    """Nearest palette entry by "redmean" distance.

    Plain RGB distance drifts hues; luminance-weighted distance destroys them
    outright, snapping hot orange onto grey of the same brightness. Redmean is
    a cheap approximation of perceptual distance that keeps hue intact, which
    matters enormously here: the entire biome is orange against grey.
    """
    r, g, b = colour

    best = entries[0]
    best_distance = float("inf")

    for entry in entries:
        cr, cg, cb = entry
        rmean = (cr + r) * 0.5
        dr = cr - r
        dg = cg - g
        db = cb - b
        distance = (2.0 + rmean / 256.0) * dr * dr + 4.0 * dg * dg + (3.0 - rmean / 256.0) * db * db
        if distance < best_distance:
            best_distance = distance
            best = entry

    return best


def unique_colour_count(path: str) -> int:
    """Distinct opaque colours in a PNG. Used to verify that snapping happened."""
    _, _, pixels = png.read_rgba(path)

    seen = set()
    for i in range(0, len(pixels), 4):
        if pixels[i + 3] < ALPHA_THRESHOLD:
            continue
        seen.add((pixels[i], pixels[i + 1], pixels[i + 2]))

    return len(seen)
