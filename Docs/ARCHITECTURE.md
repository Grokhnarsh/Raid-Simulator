# ARCHITECTURE.md

How the project is put together, and why. The guiding constraint is the brief's closing rule:
**build small, architect large** — the playable slice may be tiny, the structure underneath it may
not, and no system may need rewriting when content grows.

---

## 1. The central decision: simulation / presentation split

The project is divided into two layers with a hard, mechanically enforced boundary.

```
┌─────────────────────────────────────────────────────────────┐
│  Presentation  (RaidSim.Runtime — Unity)                    │
│  MonoBehaviours, ScriptableObjects, camera, input, UI, VFX  │
│  Converts authored data in, renders simulation state out    │
└───────────────────────────┬─────────────────────────────────┘
                            │  interfaces, events, plain structs
┌───────────────────────────┴─────────────────────────────────┐
│  Simulation  (RaidSim.Core — plain C#, zero UnityEngine)    │
│  Stats, health, damage, threat, cooldowns, effects,         │
│  abilities, AI, phases, mechanics, loot, statistics         │
└─────────────────────────────────────────────────────────────┘
```

**The kernel does not know Unity exists.** It has its own `Vec3`, its own clock
(`ISimulationClock`), its own log sink (`ISimLogSink`), and its own entity registry. The boundary is
enforced three ways:

* `RaidSim.Core.asmdef` sets `"noEngineReferences": true`, so Unity itself rejects a violation.
* `Tools/CoreBuild/RaidSim.Core.csproj` compiles the same sources with the plain .NET SDK. A stray
  `using UnityEngine;` fails that build.
* Conversions live in exactly one file, `Game/Interop/VecConversions.cs`, so the seam is a line you
  can point at.

### Why pay for this

1. **The simulator is the product.** The long-term goal in the brief is a game about strategy and
   raid management. That means running an encounter faster than real time, many times, to compare
   compositions and cooldown plans. A headless kernel makes that a loop; a kernel wired into
   `MonoBehaviour.Update` makes it impossible.
2. **Tests are fast and real.** 139 unit tests currently run in about 100 ms with no editor. They
   test the real code, not a mock of it.
3. **Determinism is reachable.** Nothing in the kernel reads frame time, physics or random state it
   was not given, so replays and reproducible encounter statistics stay achievable.
4. **Rendering decisions stay cheap.** Swapping render pipeline, art style or even engine touches
   the presentation layer only.

### What the kernel deliberately does not own

Collision, navigation meshes, animation, rendering and audio. Those are engine strengths. The kernel
expresses *intent* (`MovementIntent`, "cast this at that") and the presentation layer realises it.
`ILocomotor` is the pattern: `KinematicLocomotor` in the kernel defines what movement means and is
unit tested; `CharacterControllerLocomotor` in Unity does the same thing with collision.

---

## 2. Assemblies

| Assembly | Location | Depends on | Purpose |
| --- | --- | --- | --- |
| `RaidSim.Core` | `Scripts/Core/` | nothing | The simulation kernel |
| `RaidSim.Runtime` | `Scripts/` | Core, Input System | Everything engine-facing |
| `RaidSim.Editor` | `Scripts/Editor/` | Core, Runtime | Editor menus and validation |
| `RaidSim.Tests.EditMode` | `Tests/EditMode/` | Core, Runtime | Kernel unit tests |
| `RaidSim.Tests.PlayMode` | `Tests/PlayMode/` | Core, Runtime | Engine integration tests |

Two runtime assemblies, not fifteen. The boundary that carries architectural weight is
kernel-versus-engine; splitting `Abilities` from `Combat` into separate assemblies would buy
compile-time isolation the project does not yet need and cost a reference graph to maintain. When
compile times justify it, `Scripts/<Domain>/` folders are already separated so an asmdef can be
dropped into any of them without moving a file.

---

## 3. Composition root instead of a GameManager

`GameBootstrap` is the only class allowed to know about many systems at once, because it contains no
rules. It constructs, wires and ticks:

```
GameBootstrap.Awake()
  ├── new SimulationContext(UnityLogSink)     clock, event bus, registry, targeting, state machine
  ├── State → Booting
  ├── BuildArena()                            arena prefab, or placeholder floor + key light
  ├── BuildCamera()                           RaidCameraRig from CameraRigSettings
  ├── BuildPlayerRig()                        input source + drivers, created inactive
  ├── SpawnRosterAndBind()                    ActorSpawner from BootstrapConfig, then bind
  └── State → Playing

GameBootstrap.Update()
  ├── pause toggle          unscaled — must work while the simulation is frozen
  ├── camera zoom           unscaled — must work while the simulation is frozen
  └── context.Tick(dt)      no-op while paused
```

Everything it builds talks through interfaces and events, never back through this class. That is the
difference between a composition root and the manager the brief forbids: a manager accumulates
behaviour, a composition root cannot, because it has none to accumulate.

`SimulationContext` is the runtime counterpart — a service container plus a tick scheduler, with no
gameplay knowledge of its own.

---

## 4. Tick order

Unity's script execution order is a global setting in a window nobody reads. The order in which
combat systems run is a design decision, so it is declared as code in `SystemOrder`:

| Order | System | Why here |
| --- | --- | --- |
| 100 | Vitals | resource regeneration, passive bookkeeping |
| 200 | Cooldowns | timers advance before anything asks "is it ready?" |
| 300 | Effects | buffs tick and expire before anything reads a stat |
| 400 | Threat | decay and taunt expiry settle before AI reads the table |
| 500 | Encounter | boss phases and mechanics decide what happens this tick |
| 600 | AI | targets, abilities and destinations are chosen |
| 700 | Abilities | casts progress and resolve |
| 800 | Locomotion | movement integrates after everyone has decided where to go |
| 900 | Combat resolution | deaths, end conditions, statistics |

A system registers with `SimulationContext.AddSystem`; the context sorts by `Order` and ticks. A
system that throws is logged and the rest still tick — one broken system must not take the encounter
down with it.

---

## 5. Event architecture

```
AbilitySystem ──AbilityCastEvent──┐
CombatSystem  ──DamageEvent───────┼──▶ ThreatSystem
                                  ├──▶ CombatLog
                                  ├──▶ UI
                                  └──▶ StatisticsCollector
```

`IEventBus` is a typed, synchronous publish/subscribe channel. Adding a listener must never require
editing the publisher — that is the property the whole design rests on, and it is why the combat
log, the statistics collector and the UI can all be added later without touching combat code.

Three properties the implementation guarantees, each because combat needs it:

* **Re-entrancy.** Handlers publish further events and unsubscribe themselves mid-dispatch (an
  entity dying inside the damage event it is handling). Dispatch iterates a snapshot and defers
  structural edits until the outermost publish drains.
* **Fault isolation.** A throwing subscriber is logged and the remaining subscribers still receive
  the event, so the combat log never silently diverges from the fight.
* **Meaningful events only.** `TargetSelection` publishes a change only when the target actually
  changed, so no listener needs to filter duplicates.

Events are `readonly struct`s: no allocation per damage tick, and no listener can mutate what another
listener sees.

---

## 6. Entities

`ISimEntity` is the narrowest useful contract — identity, faction, position, facing, radius, alive.
Targeting, range checks and area-of-effect shapes need nothing more, so they work unchanged for
players, enemies, bosses, pets, and later for non-combat props like soak orbs or sight-blocking
pillars.

`ICombatEntity` adds stats, pools, role and level. The damage pipeline, the threat table and the AI
all take this interface and cannot tell a player from a boss. That is deliberate: mind control, a
friendly NPC, or an add that heals its boss are then not special cases.

`EntityRegistry` is the authoritative list, with per-faction buckets maintained on write because
reads happen many times per tick and writes a handful of times per pull. Nothing in the project calls
`FindObjectsOfType`.

`CombatActor` is the single Unity-side body for every fighting thing. Player, AI group member, trash
mob and boss are all this component with different data and a different driver.

---

## 7. Drivers: how control changes hands

A driver reads something and produces a `MovementIntent` plus ability decisions. `PlayerController`
reads input; a Phase 5 AI driver will read an authored profile. They produce the same struct, so:

* control of a group member moves between player and AI by swapping the driver;
* "Run Encounter" works by giving every member an AI driver and no player;
* the headless simulator runs with no input device present at all.

`PlayerDriverSystem` registers the player's driver as an ordinary `ISimulationSystem`. The player
therefore moves at the point in the tick order where movement belongs, and freezes on pause for the
same reason everything else does — the clock stopped — with no pause check written anywhere.

---

## 8. Pause

Pause is a property of `SimulationClock`, not a flag each system checks. A paused clock reports a
zero delta and stops advancing `Now`. Because every duration in the kernel — cooldowns, casts,
effect durations, damage-over-time ticks, boss timers — measures against that clock, all of them
freeze together, and no gameplay code contains `if (paused)`.

`GameStateMachine` owns the transition table and drives the clock. Input handled outside the tick
(pause, zoom) uses unscaled time so the player can still leave a pause and study a frozen mechanic.

---

## 9. 2.5D: a camera decision, not a simulation constraint

Positions are real 3D throughout. The 2.5D feel comes from a fixed, authored camera angle, which is
why `CameraRigSettings` holds pitch, yaw, zoom range and bounds, and why the camera never rotates
during play: a player who learns where a danger zone appears relative to their character keeps that
knowledge.

Because the simulation is genuinely 3D, height differences, bridges, stairs, platforms, pillars and
line of sight remain expressible. `TargetFilter` already separates horizontal range from height
difference, so a target on a balcony overhead is close but not meleeable.

The project starts on the **Built-in Render Pipeline**. No pipeline assets are hand-authored, which
removes a class of first-open failure, and with zero art in the repository a later move to URP costs
nothing. The decision point is scheduled for Phase 7, before art volume exists — see
[`ROADMAP.md`](ROADMAP.md).

---

## 10. Verification without an editor

| Check | Command | Covers |
| --- | --- | --- |
| Kernel compile | `dotnet build Tools/CoreBuild/RaidSim.Core.csproj` | warnings-as-errors; enforces no engine reference |
| Kernel tests | `dotnet test Tools/CoreBuild/RaidSim.Core.Tests.csproj` | the same sources Unity's Test Runner uses |
| Engine-facing type check | `dotnet build Tools/UnityStubs/RaidSim.UnityTypeCheck.csproj` | the project's own C# against Unity API stubs |
| Asset references | `python3 Tools/Unity/verify_references.py` | every GUID reference resolves |
| Meta coverage | `python3 Tools/Unity/generate_meta.py --check` | no asset lacks a `.meta` |

The stub check is a genuine compile of the project's code, but the stubs are hand-written: it proves
the code is internally consistent, not that every Unity call matches the engine. Unity's compile
remains authoritative. See `Tools/UnityStubs/README.md`.

---

## 11. Systems: built, and scheduled

| System | State | Phase |
| --- | --- | --- |
| `EventBus`, `SimulationClock`, `GameStateMachine`, `SimulationContext` | built, tested | 1 |
| `StatBlock`, `Health`, `ResourcePool`, `EntityRegistry` | built, tested | 1 |
| `TargetQuery`, `TargetFilter`, `TargetSelection` | built, tested | 1 |
| `ILocomotor` + kinematic and CharacterController implementations | built, tested | 1 |
| `GameBootstrap`, `RaidCameraRig`, `PlayerController` | built | 1 |
| `DevelopmentOverlay` | built, minimal | 1 |
| `CombatSystem`, damage pipeline | scheduled | 2 |
| `AbilitySystem`, `CooldownSystem` | scheduled | 3 |
| Party composition, raid frames | scheduled | 4 |
| `AISystem` (enemy and raid) | scheduled | 5 |
| `ThreatSystem` | scheduled | 6 |
| `RaidSystem`, zones, trash | scheduled | 7 |
| `BossSystem`, phases, mechanics, telegraphs, interrupts | scheduled | 8 |
| `UISystem`, combat log | scheduled | 9 |
| `LootSystem` | scheduled | 10 |
| Encounter simulation, statistics | scheduled | 11 |
| `SaveSystem` | scheduled | 11 |

Scheduled systems have no placeholder classes. An empty interface that nothing implements is not
architecture; it is a file to delete later. The contracts that already exist —
`ISimulationSystem`, `SystemOrder`, `IEventBus`, `ICombatEntity`, `ILocomotor` — are the ones Phase 1
code actually uses, and they are the sockets the rest plug into.
