# Art pipeline

Two generators feed one importer. Nothing in `Assets/_Project/Art` is hand-drawn,
and all of it can be reproduced from the manifests in `tools/`.

```
PixelLab  ──► tools/pixellab/fetch_assets.ps1 ──┐
                                                ├─► Assets/_Project/Art/*.png ──► EmberDepths ▸ Art ▸ Import Everything ──► Tile + ActorVisualSet assets
Blender   ──► tools/blender/build_props.py    ──┘
```

The split is by strength, not by preference:

| | Used for | Why |
|---|---|---|
| **PixelLab** | Characters, enemies, boss, terrain tiles | Hand-styled pixel art with real character; 8 rotations and animation templates out of the box |
| **Blender** | Props (pillars, braziers, chests, gates) | Eight rotations of the *same object* with consistent lighting and an exact grid footprint. A pillar drawn eight times drifts; a pillar modelled once and rendered eight times cannot |

---

## The numbers everything agrees on

| Constant | Value | Defined in |
|---|---|---|
| Tile top face | 64 × 32 px | `IsoGrid.TileWidthPx/TileHeightPx` |
| Pixels per unit | 32 | `IsoGrid.PixelsPerUnit` |
| Unity cell size | (2, 1, 1), Isometric | derived, applied by `DungeonBuilder` |
| Camera elevation | **30°** | `edgen/isocam.py` |
| Camera yaw | 45° | `edgen/isocam.py` |

### Why 30° and not 35.264°

A 1×1 ground square with the camera yawed 45° projects to a horizontal extent of
its diagonal, √2. Its vertical extent is √2·sin(elevation). Pixel-art isometric
wants those in a 2:1 ratio:

```
        √2 / (√2 · sin θ) = 2
                    sin θ = 0.5
                        θ = 30°
```

35.264° is *engineering* isometric and yields √3:1. Using it makes every prop
render slightly too tall and stop sitting flush on a 64×32 tile. This is the
single easiest thing to get wrong in the whole pipeline.

---

## PixelLab

`tools/pixellab/assets.json` holds a permanent UUID per asset. Fetching is
reproducible: a fresh clone plus one run gets the exact art the project was
built with.

```bash
powershell -File tools/pixellab/fetch_assets.ps1
powershell -File tools/pixellab/fetch_assets.ps1 -Only vulcanor -Force
```

Notes learned the hard way:

- The script uses `curl.exe`, not `Invoke-WebRequest`. The CDN rejects some
  default user agents, and `Invoke-WebRequest` opens an interactive credential
  prompt on a 4xx, which hangs non-interactive shells.
- **HTTP 423** means a generation job is still running against that character —
  it is the normal path, not an error. The script polls.
- Regenerating an asset produces a **new UUID**. Update `assets.json` and add a
  line to its `changelog` array, so a reviewer can tell a re-roll from a new
  asset.

### Terrain tiles are palette-snapped after download

`fetch_assets.ps1` finishes by running `tools/blender/quantise.py` over
`Art/Tiles`, snapping every pixel onto `palette.ENVIRONMENT_PALETTE`. Pass
`-NoPaletteSnap` to keep the raw art.

This is not cosmetic tidying. Generated "volcanic rock" reliably comes back with
a **cool blue-grey cast** — the first tile set rendered as teal stone, which
collides directly with the party's teal identity colour and made five characters
hard to pick out against their own floor. Re-prompting for "warm charcoal"
helped the lava cracks and produced a mauve wall instead; prompt wording is not
a reliable control for hue.

The environment palette omits the cool `STEEL` and `PLAYER` ramps entirely, so a
bluish floor has nowhere cool to snap to and lands in `ROCK` / `ASH` /
`OBSIDIAN`. Terrain comes out warm by construction, and the cool half of the
palette stays reserved for the party. It also puts the tiles in exactly the same
colour space as the Blender props.

Characters are deliberately **not** snapped. They are the visual identity of the
game and PixelLab's output is stronger than a 50-colour reduction of it; the
split is "environment unified, heroes hand-styled", which is what
`palette.py` describes.

Downloaded layout, which the importer expects:

```
Actors/<Group>/<Actor>/Idle/rotations/<direction>.png
Actors/<Group>/<Actor>/Idle/animations/<name>/<direction>/frame_000.png
```

Directions are `south`, `south-east`, `east`, `north-east`, `north`,
`north-west`, `west`, `south-west` — deliberately the same strings as the
`IsoDirection` enum, so the importer maps them without a lookup table.

### Keeping a cast visually coherent

Generate one character in `pro` mode as a style anchor, then generate the rest
with `style_character_id` pointing at it. The project has two anchors:

- **Warden** → the party (cool teal / steel / bone)
- **Ember Imp** → the fire faction (molten orange)

The palette split is load-bearing, not decoration: five party members have to
stay readable on a screen full of orange lava. Do not add warm colours to the
party ramp without re-checking contrast against `LAVA` in `edgen/palette.py`.

---

## Blender

```bash
& "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" `
    --background --factory-startup `
    --python tools/blender/build_props.py -- --only brazier,rune_stone
```

`--factory-startup` is not optional. Add-ons and user preferences change colour
management and render defaults, and this pipeline depends on those being exactly
what `edgen/render.py` sets.

The `edgen` package:

| Module | Job |
|---|---|
| `palette.py` | Every colour the game may emit. The single source of truth |
| `isocam.py` | The 2:1 camera and the grid arithmetic |
| `mats.py` | Palette-driven materials and the three-light rig |
| `props.py` | Procedural prop meshes; add an entry to `CATALOGUE` and it renders next run |
| `png.py` | A minimal 8-bit PNG codec |
| `pixelize.py` | Palette snapping and alpha thresholding |
| `render.py` | Orchestration: 8 rotations per prop |

### Render settings that are not defaults

Blender's defaults are tuned for photographic output; this is the opposite.

| Setting | Value | Why |
|---|---|---|
| `view_transform` | `Standard` | AgX/Filmic desaturate everything. The palette is authored in sRGB and must arrive in sRGB |
| `filter_size` | `0.01` | Reconstruction filtering is anti-aliasing by another name; at 64 px it is just blur |
| `film_transparent` | on | Sprites need alpha, not a sky |
| Emission strength | **~1.0** | See below |
| Shading | flat | Smooth shading becomes banding after quantisation |

**Emission strength must stay near 1.0.** At 1.0 an emissive surface renders as
exactly its base colour, so it snaps back to itself. Anything above about 3 clips
to pure white *before* quantisation runs, and the lava ramp's hues are lost — the
brazier's coals come out as a white blob. Low emission on a hot palette colour
reads far better than high emission on a clipped one.

### Why there is a hand-written PNG codec

`pixelize.py` works on raw file bytes through `png.py` rather than through
Blender's image API. Every path through that API runs pixels through colour
management, and the transform applied on read is not the exact inverse of the one
applied on write. The result is a file whose colours are *near* the palette but
not on it — which defeats the entire point, and does so silently.

Two further traps, both encountered while building this:

- `image.pixels = list` does not reliably mark the datablock dirty; use
  `foreach_set` if you use the API at all.
- `Image.save()` on a **file-backed** image re-saves from its source file when
  Blender does not consider it dirty, so pixel edits vanish with no error.

Working on bytes removes all three questions. It also means the quantiser has no
Blender dependency and can be exercised from any Python REPL.

Verify a build with:

```powershell
# every opaque pixel should be on-palette
```

The last full run: **64 sprites, 115,111 opaque pixels, 0 off-palette.**

---

## The Unity side

**EmberDepths ▸ Art ▸ Import Everything** runs three steps.

1. **Fix Pivots.** The `AssetPostprocessor` sets filtering, compression, PPU and
   mesh type on import, but pivots need the actual pixels, so they are a separate
   pass that measures each sprite's opaque bounds.

   | Folder | Pivot | Reason |
   |---|---|---|
   | `Art/Tiles` | centre of the **top face** | A tile on cell (x,y) must cover exactly that cell, with its block body hanging over the cell in front |
   | everything else | **bottom centre** of the artwork | The feet land on the cell centre, so the character stands on the tile rather than hovering |

   Measured, not hard-coded — actors range from 64 px to 128 px canvases.

2. **Build Tile Assets.** One `Tile` per PNG in `Art/Tiles`.

3. **Rebuild Visual Sets.** One `ActorVisualSet` per actor folder, filling the
   eight idle rotations and mapping animation folders onto `ActorAnimState`.
   Existing assets are updated in place, so references from enemy and class
   definitions survive.

An unrecognised animation folder name logs a warning rather than being guessed
at — add it to `ArtTools.TryMapAnimationName`.

---

## Adding a prop

1. Write a builder in `edgen/props.py` returning a `Prop` (pivot, footprint in
   cells, height in tile-widths, blocking flag).
2. Add it to `CATALOGUE`.
3. Re-run `build_props.py`.
4. In Unity: **Art ▸ Import Everything**.

Build from cones and cubes. They are the two bmesh primitives whose signatures
have been stable across Blender versions, and at 64 px per tile nobody can tell
a six-sided cone from a sculpted rock. Shape reads at this resolution; detail
does not.

Seed the randomness from the prop name. Committed art that changes when you
re-run the generator is art nobody trusts.
