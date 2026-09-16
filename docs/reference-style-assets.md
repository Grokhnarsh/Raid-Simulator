# Reference style asset pack

This pack was built from the four screenshots supplied on 2026-09-16. The
screenshots are visual references; the renders contain newly constructed shapes
and no copied UI, logos, text, or screenshot pixels.

## Style decisions

- Dark cocoa contours around large silhouettes; flat cel colours with one hard
  shadow and a small highlight.
- Warm terracotta and ochre for **Legion Hold**, cool stone and cyan water for
  **Marsh Maze**, mauve basalt and orange fire for **Inferno Wall**.
- Chunky chibi proportions for enemies, with a clear head, armour/body mass,
  high-contrast eyes, and minimal internal detail at the game's native scale.
- 2:1 diamond floor tiles at **64×32 px** and 32 pixels per unit. The tiles fit
  the current Unity isometric grid. Enemy and landmark sprites are **96×96 px**.

## Contents

| Biome | Floor variants | Enemy | Landmark |
|---|---:|---|---|
| Legion Hold | 3 sunbaked earth tiles | Legion Sentinel, 8 directions | carved monolith |
| Marsh Maze | 3 wet slate tiles | Marsh Basilisk, 8 directions | cyan rune lamp |
| Inferno Wall | 3 mauve flagstone tiles | Inferno Guardian, 8 directions | iron fire bowl |

The PNGs and Unity `.meta` files are under `Assets/_Project/Art`. Editable
Blender scenes are under `tools/blender/reference_style_sources`; the generator
is `tools/blender/build_reference_style.py`. The generation is deterministic:
rerunning it preserves existing `.meta` GUIDs and regenerates the same sprites.

```powershell
& "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" `
  --background --factory-startup --python tools/blender/build_reference_style.py
```

In Unity, run **EmberDepths ▸ Art ▸ Import Everything** to create Tile and
ActorVisualSet assets for the new sprites. The visuals are ready for content
authoring; the current playable volcano encounter still uses its original
content, and the three new biome kits have not been assigned to dungeons or
enemy definitions. These are static idle rotations, without walk or attack
animation. Unity verification requires an active Editor license.
