# ROADMAP.md

Phases run in order. Each ends with a compiling project, passing tests, and updated documentation.
Work belonging to a later phase does not start early — see [`../CLAUDE.md`](../CLAUDE.md), section 6.

**Current position: Phase 2 complete. Phase 3 is next.**

---

## Phase 1 — Foundation ✅

Project structure, the simulation kernel's core services, bootstrap, camera, a player character,
movement, and first targeting.

**Delivered**

* Unity project scaffolding: assembly definitions, package manifest, `.meta` generation, git
  configuration, editor tooling.
* Simulation kernel (`RaidSim.Core`, no engine dependency): `Vec3`/`SimMath`, `EntityId`, `Faction`,
  `CombatRole`, `EventBus`, `SimulationClock`, `ISimulationSystem`/`SystemOrder`,
  `SimulationContext`, `GameStateMachine`, `StatType`/`StatBlock`/`StatSet`/`StatRules`, `Health`,
  `ResourcePool`, `EntityRegistry`, `TargetFilter`/`TargetQuery`/`TargetSelection`,
  `MovementIntent`/`ILocomotor`/`KinematicLocomotor`, log sinks.
* Unity layer: `GameBootstrap`, `BootstrapConfig`, `ActorSpawner`, `CombatActor`,
  `CharacterControllerLocomotor`, `RaidCameraRig`, `CameraRigSettings`, Input System asset and
  `InputSystemPlayerInput`, `PlayerController`, `PlayerTargetingController`, `PlayerDriverSystem`,
  `DevelopmentOverlay`.
* Data: four playable classes plus a practice target, the five demo characters, camera settings,
  bootstrap configuration.
* 139 kernel unit tests; headless compile, test and reference-verification tooling.

**Deliberately not included:** damage, abilities, cooldowns, effects, threat, AI, UI, loot.

---

## Phase 2 — Combat core ✅

Health, damage, death, basic attack.

**Delivered**

* `CombatSystem` — the single entry point for damage and healing, and the only thing that reports
  death. It reports each death exactly once, and `Kill` routes debug kills through the same funnel.
* `DamagePipeline` and `HealingPipeline` as ordered steps rather than one expression, so later
  additions insert at a known point. `Calculate` previews without touching anything.
* `DamageType`: physical scales from attack power and is reduced by armour, magic from spell power
  reduced by resistance, true damage does neither — so a mechanic built on it cannot be out-geared.
* `Mitigation` on a `rating / (rating + K)` curve with diminishing returns and level scaling; the
  curve's shape in code, its constants in `CombatTuningAsset`.
* Critical strikes from `CriticalChance` and `CriticalMultiplier`, suppressible per request.
* `IRandomSource` with a seeded xorshift implementation, so an encounter replays identically —
  the prerequisite for Phase 11's comparisons.
* Events: `AttackStartedEvent`, `DamageDealtEvent`, `DamageTakenEvent`, `HealAppliedEvent`,
  `EntityDiedEvent`. Damage raises two perspectives because the interested parties differ.
* Overkill and overhealing taken from what the pools actually applied, never recomputed.
* `AutoAttackSystem` driving every attacker from one loop, with profiles authored on
  `ClassDefinition`. The timer pauses rather than resets on losing a target, so swapping cannot be
  used to swing faster.
* `CombatEventFeed` in `DebugTools` rendering combat into the development overlay — the first proof
  that a listener can be added without touching combat code.
* 84 new unit tests (223 total).

**Deliberately not included:** absorption shields, which arrive in Phase 3 with the effects that
produce them. A permanently-zero `Absorbed` field would mislead the combat log.

**Verified:** at the authored tuning, a level-60 tank kills a practice target in about 23 seconds;
melee in 8, ranged in 11, healer in 25. Role ordering is correct and the fight is long enough to
observe. **Not yet verified:** the encounter running in the Unity editor.

---

## Phase 3 — Abilities

Data-driven abilities, casting, cooldowns, resource costs.

* `AbilityDefinition` ScriptableObject: cast time, cooldown, cost, range, target type, threat,
  effects, and presentation hooks.
* `AbilitySystem`: validation, cast bars, channels, interrupt windows, movement cancellation.
* `CooldownSystem`: per-ability, global, shared, charges, cooldown reduction — never implemented
  inside an individual ability.
* Composable effect executors: direct damage, direct heal, area damage, area heal, shield, taunt,
  damage reduction, crowd control, buff, debuff.
* Ability bar input.
* **New document:** `ABILITY_SYSTEM.md`.

**Done when:** each class has a small working ability set defined entirely in data, and adding an
ability requires no code.

---

## Phase 4 — Party

Five characters, roles, party management, raid frames.

* `PartyDefinition` / `RaidGroup`: an ordered set of slots with no fixed size.
* Party spawning replaces the single-character bootstrap path.
* Raid frames built from the live roster — never five hard-coded frames.
* Switching which character the player controls.

**Done when:** all five demo characters spawn, are visible in frames, and control can move between
them. Frames must handle a roster of ten without a code change.

---

## Phase 5 — AI

Enemy AI and raid AI.

* `AIProfile` assets and a state machine: Idle, Patrol, Alert, Combat, Cast, Move, Dead.
* Data-driven target selection rules composed from `TargetFilter` plus a scorer: highest threat,
  lowest health, role-based, random, within an area.
* Raid AI per role — tank, healer, damage — reading the same profile mechanism, never a character.
* Positioning decisions: reach melee range, hold ranged range, respect minimum range, face the
  target.
* AI drivers registered exactly like `PlayerDriverSystem`.
* `AI_SYSTEM.md` updated from specification to description.

**Done when:** the four non-player members fight a practice encounter unaided, and AI decisions are
covered by unit tests against a scripted world state.

---

## Phase 6 — Threat

* `ThreatTable` per hostile entity; `ThreatSystem` subscribing to damage, healing and support events.
* Threat multipliers from `StatType.ThreatModifier`; proximity rules and threat drop on death.
* Taunt as a time-limited override.
* Enemy target selection switched to read the threat table.
* Debug visualisation of the threat table in the development overlay.

**Done when:** the tank holds against the group's damage, losing threat has visible consequences, and
threat ordering and taunt expiry are unit tested.

---

## Phase 7 — Raid zone

* `RaidDefinition` and `RaidZoneDefinition`: an ordered set of encounters, extensible by asset.
* Ruins of Ashenfall: entrance, two trash packs, boss arena.
* Trash enemies as data: Ashen Cultist, Ashen Brute, Flamecaller.
* Pull triggers, encounter reset, wipe detection.
* Arena geometry replacing the placeholder floor; object pooling for spawned enemies.
* **Decision point:** Built-in Render Pipeline or URP, taken here, before art volume exists.

**Done when:** the zone can be walked from entrance to boss arena and adding a third trash pack is an
asset edit.

---

## Phase 8 — Boss

* `BossDefinition`, `PhaseDefinition`, `MechanicDefinition` — no ability wired into a boss script.
* Phase transitions on health thresholds, timers and events.
* Emberlord Varkuun's three phases and six abilities, all authored.
* Generic mechanic system: stack, spread, soak, dodge, interrupt, kill priority, move to position,
  stay away, stay close, add spawn, area control.
* Mechanic events: started, resolved, failed.
* Telegraph system with gameplay and visuals separated: circle, line, cone, ring, safe zone, danger
  zone.
* Interrupt system: cast bars, interruptible flags, interrupt windows, AI reaction.
* **New document:** `BOSS_SYSTEM.md`.

**Done when:** the encounter is winnable and losable for mechanical reasons, and a second boss would
be a second set of assets.

---

## Phase 9 — UI

* Player frame, group frames, target frame, boss frame, boss cast bar, ability bar with cooldowns,
  resource display.
* Combat log driven entirely by the event bus.
* Buff and debuff display.
* Every frame collection built from the live roster: 5, 10, 20 and 40 must all work.

**Done when:** an encounter is readable without the development overlay.

---

## Phase 10 — Loot

* `LootTable` and `ItemDefinition` assets; gold plus three items from the first boss.
* Loot generation on boss death, and a rewards screen.
* Structure reserved for rarity, equipment, sets, random stats and loot rules.

**Done when:** killing the boss produces authored loot and loot generation is unit tested.

---

## Phase 11 — Simulation and persistence

* "Run Encounter": the full fight, no player input, faster than real time.
* `StatisticsCollector` subscribing to combat events: damage, healing, damage taken, deaths,
  interrupts, mechanics avoided and failed, duration, threat, ability usage.
* Results screen and comparison between runs.
* `SaveSystem`: settings, progress, loot, character configuration — versioned from the start.

**Done when:** an encounter can be simulated repeatedly without an editor and the results are stable
enough to compare two group compositions.

---

## Documentation schedule

| Document | Status |
| --- | --- |
| `CLAUDE.md`, `GDD.md`, `ARCHITECTURE.md`, `ROADMAP.md`, `DATA_ARCHITECTURE.md` | written (Phase 1) |
| `COMBAT_SYSTEM.md`, `AI_SYSTEM.md` | written as specifications (Phase 1); become descriptions in Phases 2 and 5 |
| `BLENDER_PIPELINE.md`, `ART_STYLE_GUIDE.md`, `TESTING.md` | written (Phase 1) |
| `ABILITY_SYSTEM.md` | Phase 3, when there is a system to describe |
| `BOSS_SYSTEM.md` | Phase 8, when there is a system to describe |

The two missing documents are scheduled rather than stubbed. A document describing code that does not
exist is a guess that will be wrong; the systems they cover are specified in this roadmap until then.

---

## Beyond the first milestone

Group sizes of 10, 20 and 40. Additional classes and specialisations. Further raids and bosses.
Multi-boss encounters, puzzle and environmental mechanics. Weapons, armour, accessories, sets and
trinkets. Levels, stats, talents and builds. None of these require a system named in this roadmap to
be rewritten — that is the measure of whether the architecture held.
