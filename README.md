# Raid Simulator

A 2.5D raid simulator built in Unity — a tactical game about running a group through a coordinated
boss encounter, designed from the start to scale from a five-person party to a forty-person raid.

The player controls one character inside the group while the rest are run by AI. The skill the game
asks for is the set of decisions a raid leader makes: who to bring, where to stand, when to spend
cooldowns, what to focus, interrupt or avoid.

> **Status: Phase 2 complete.** The simulation kernel, bootstrap, camera, a player character,
> movement, targeting, and now the combat core — damage, healing, death and a data-driven basic
> attack — are in place, with 223 passing unit tests. Abilities, AI, threat, the raid zone, the
> boss, UI and loot are scheduled — see [`Docs/ROADMAP.md`](Docs/ROADMAP.md).

---

## Getting started

Open with **Unity 6 LTS (6000.0.x)**, then follow the four first-open steps in
[`CLAUDE.md`](CLAUDE.md#5-first-time-you-open-the-project) — enabling the Input System backend,
repairing the one reference that cannot be authored outside the editor, validating the data, and
opening `Assets/_Project/Scenes/Bootstrap.unity`.

### Working without Unity

The simulation kernel has no engine dependency, so it compiles and tests with the plain .NET SDK:

```bash
dotnet build Tools/CoreBuild/RaidSim.Core.csproj          # compile the kernel
dotnet test  Tools/CoreBuild/RaidSim.Core.Tests.csproj    # run 223 unit tests
dotnet build Tools/UnityStubs/RaidSim.UnityTypeCheck.csproj   # type-check engine-facing code
python3 Tools/Unity/verify_references.py                  # check every asset reference resolves
```

---

## Documentation

| Document | What it covers |
| --- | --- |
| [`CLAUDE.md`](CLAUDE.md) | Working agreement: the architecture rules, commands, first-open steps |
| [`Docs/GDD.md`](Docs/GDD.md) | What the game is, the loop, roles, combat model, the first raid |
| [`Docs/ARCHITECTURE.md`](Docs/ARCHITECTURE.md) | The simulation/presentation split, assemblies, tick order, events |
| [`Docs/ROADMAP.md`](Docs/ROADMAP.md) | The eleven phases and what "done" means for each |
| [`Docs/DATA_ARCHITECTURE.md`](Docs/DATA_ARCHITECTURE.md) | Static data versus runtime state, stats, assets, GUIDs |
| [`Docs/COMBAT_SYSTEM.md`](Docs/COMBAT_SYSTEM.md) | Health, damage, healing, events, threat, effects, death |
| [`Docs/AI_SYSTEM.md`](Docs/AI_SYSTEM.md) | Driver seam, target selection, role behaviour, positioning |
| [`Docs/TESTING.md`](Docs/TESTING.md) | How tests run, current coverage, what to test per phase |
| [`Docs/BLENDER_PIPELINE.md`](Docs/BLENDER_PIPELINE.md) | Units, modularity, rigging, export, Unity import |
| [`Docs/ART_STYLE_GUIDE.md`](Docs/ART_STYLE_GUIDE.md) | Legibility rules, colour, silhouettes, telegraphs, budgets |

`ABILITY_SYSTEM.md` and `BOSS_SYSTEM.md` arrive with Phases 3 and 8, when there is a system to
describe rather than a guess to record.

---

## The one architectural rule

**Gameplay never branches on a name.** No `if (boss.name == ...)`, no `if (character.name == ...)`.
Display names exist for the combat log and the UI; behaviour comes from data, interfaces, components
and events. Adding a class, a character, an enemy, an ability, a boss or a loot table requires no
code changes at all.

## Licence

GNU Affero General Public License v3.0 — see [`LICENSE`](LICENSE).
