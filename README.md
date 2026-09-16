# EmberDepths

A 2D isometric dungeon simulator in Unity 6. Five-person party, one procedurally
generated volcanic dungeon, trash packs, elites, and a three-phase boss.

Art is generated: characters and terrain tiles come from **PixelLab**, props are
modelled and rendered in **Blender**. Both pipelines are scripted and
reproducible — nothing in `Assets/_Project/Art` is drawn by hand, and all of it
can be regenerated from the manifests in `tools/`.

| | |
|---|---|
| Engine | Unity **6000.6.0f1** (Built-in render pipeline) |
| Blender | **5.2** for the prop pipeline |
| Projection | 2:1 isometric, 64×32 px tiles, 32 pixels per unit |
| Simulation | Fixed 20 Hz tick, seeded and deterministic |
| Party | 5 slots — one human, four companion AI |

---

## Quick start

1. Open this repository's root directory in Unity 6000.6.0f1.
2. Run **EmberDepths ▸ Setup ▸ First-Time Setup (everything)**.
3. Open `Assets/_Project/Scenes/EmberDepths.unity` and press Play.

The Unity MCP bridge is included as a Package Manager dependency. In the editor,
open **Window ▸ MCP for Unity** and configure Codex if you want live editor control.

That menu item runs four steps in order, and each is also available on its own:

| Step | Menu item | What it does |
|---|---|---|
| 1 | Setup ▸ Configure Project | Creates the sorting layers the isometric renderer needs |
| 2 | Art ▸ Import Everything | Fixes sprite pivots, builds Tile assets, builds visual sets |
| 3 | Content ▸ Build Volcano Dungeon | Generates ~50 ScriptableObjects: classes, abilities, enemies, boss, biome |
| 4 | Setup ▸ Build Playable Scene | Wires camera, runner, input and HUD into a scene |

### Controls

| Input | Action |
|---|---|
| `WASD` / arrows | Move (screen-relative) |
| Left click | Enemy → target it. Ground → path there |
| `Tab` / `Esc` | Cycle target / clear target |
| `1`–`6` | Cast the ability in that action-bar slot |
| `Space` | Basic attack |
| `C` | Gear panel — equipment and set progress |
| `U` | Reforge: spend embers upgrading the cheapest worthwhile item |

You control the Warden (tank). The other four run on companion AI — the healer
watches health bars, damage dealers focus whatever the tank is holding.

### Loot and progression

Enemies drop **embers** and, less often, **equipment**. Items auto-equip onto
whichever party member gains most and the displaced piece goes to the shared
stash, so the run keeps moving without an inventory screen.

Power comes from two places:

- **Reforging.** Embers upgrade an item one level at a time (`U`). Costs
  compound, so early upgrades are cheap and the last level of a legendary is a
  project. This is the reliable curve — trash always pays embers.
- **Sets.** Three six-piece sets — the Emberward (tank), the Ashstrider
  (crit and speed) and the Tidecaller's Vigil (power and resource) — grant
  cumulative bonuses at 2, 4 and 6 pieces, with the capstone granting a
  permanent aura. Each piece is deliberately weaker than the best standalone
  gear for its slot, so wearing the set is a decision rather than an
  inevitability.

---

## Regenerating the art

Neither step is needed to run the game; the generated PNGs are committed.

**PixelLab** (characters and terrain tiles). The UUIDs in
`tools/pixellab/assets.json` are permanent, so this reproduces exactly the art
the project was built with. Terrain tiles are snapped to the environment palette
as the last step, which is what keeps the floor warm and the party readable:

```bash
powershell -File tools/pixellab/fetch_assets.ps1
```

**Blender** (props). Models, renders 8 rotations each, and snaps every pixel to
the game palette:

```bash
& "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --factory-startup --python tools/blender/build_props.py
```

Then in Unity: **EmberDepths ▸ Art ▸ Import Everything**.

Three additional screenshot-inspired biome art kits (floor tiles, enemy idle
rotations, and landmarks) are documented in
[docs/reference-style-assets.md](docs/reference-style-assets.md).

---

## Layout

```
Assets/_Project/
  Art/            generated PNGs  (Actors, Tiles, Props)
  Content/        generated ScriptableObjects
  Scenes/         the playable scene
  Scripts/
    Core/         grid maths, A*, sim clock, seeded RNG   — no game rules
    Content/      ScriptableObject definitions            — no behaviour
    Gameplay/     actors, combat, AI, dungeon, UI         — the simulation
    Editor/       import and content tooling              — editor only
tools/
  pixellab/       asset manifest + fetch script
  blender/        the prop generator (edgen package)
docs/             architecture, art pipeline, how to extend
```

The four assemblies depend strictly downward: `Core ← Content ← Gameplay ← Editor`.
Content describes what exists; Gameplay decides what happens. That split is what
lets a whole second biome be added without touching a line of simulation code —
see [docs/extending.md](docs/extending.md).

---

## Where to look first

| Question | File |
|---|---|
| How does the isometric projection work? | [`IsoGrid.cs`](Assets/_Project/Scripts/Core/Grid/IsoGrid.cs) |
| Why is there a fixed tick? | [`SimClock.cs`](Assets/_Project/Scripts/Core/Sim/SimClock.cs) |
| How is damage calculated? | [`CombatSystem.cs`](Assets/_Project/Scripts/Gameplay/Combat/CombatSystem.cs) |
| Why does the tank matter? | [`ThreatTable.cs`](Assets/_Project/Scripts/Gameplay/Combat/ThreatTable.cs) |
| How is the boss fight scripted? | [`BossBrain.cs`](Assets/_Project/Scripts/Gameplay/AI/BossBrain.cs) |
| Where is the dungeon laid out? | [`DungeonGenerator.cs`](Assets/_Project/Scripts/Gameplay/World/DungeonGenerator.cs) |
| What are all the numbers? | [`VolcanoContentBuilder.cs`](Assets/_Project/Scripts/Editor/Content/VolcanoContentBuilder.cs) |

## Verified

Last full pass in the source project on Unity 6000.6.0f1 / Blender 5.2.1.
The imported copy still needs its first Unity test run on a machine with an
active Editor license:

| Check | Result |
|---|---|
| EditMode suite | **217 / 217 pass** (~45 s) |
| PlayMode render test | **pass** — scene loads, run starts, world *and* HUD draw |
| Headless run | 2400 ticks (120 simulated seconds) with no exceptions |
| Dungeon connectivity | 60 seeds — every room and the boss room reachable |
| Determinism | same seed ⇒ identical layout, RNG streams and loot rolls |
| Occupancy invariant | 800 ticks, no two actors ever share a cell |
| Loot loop | a started run equips its gear; fighting yields embers and drops |
| Set bonuses | tiers activate at 2/4/6, are cumulative, and come off cleanly |
| Prop palette compliance | 64 sprites, 115,111 opaque pixels, **0 off-palette** |

```bash
Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform EditMode
```

The PlayMode test writes a rendered frame to
`%TEMP%/emberdepths_frame.png` — the quickest way to see what a change did
without opening the editor.

### If you already had the project open

The terrain tiles were re-generated and palette-snapped after the first build.
Unity picks the new PNGs up on focus, but run **EmberDepths ▸ Content ▸ Build
Volcano Dungeon** once so the biome's tile references are rebuilt with the
exact-match asset lookup. It takes a few seconds and touches nothing else.

## Known gaps

This is a foundation, not a finished game. Deliberately not built yet:

- **Networking.** Five slots exist and input is abstracted behind one controller,
  but there is no transport. The fixed tick and seeded RNG are the groundwork.
- **No inventory screen.** Drops auto-equip and the stash is invisible. The
  scoring that decides who gets what lives in `Equipment.UpgradeDelta`, and the
  displaced item is kept, so a manual swap UI is additive rather than a rewrite.
- **Item affixes.** Items have fixed stat lines; nothing rolls randomly yet.
  `ItemInstance` exists as a separate type from `ItemDefinition` precisely so
  that per-copy randomness can be added without touching anything else.
- **Audio.** Fields exist on the definitions; nothing plays them.
- **The HUD is code-built.** Fine for iteration, should become a prefab or UI
  Toolkit document once the visual design settles.
