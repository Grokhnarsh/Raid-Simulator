# DATA_ARCHITECTURE.md

How authored content is stored, and how it becomes runtime state.

---

## 1. The rule

**Static data and runtime state are different things and never share an object.**

| | Static data | Runtime state |
| --- | --- | --- |
| Lives in | ScriptableObject assets under `Assets/_Project/Data/` | plain C# objects in `RaidSim.Core` |
| Written by | designers, in the editor | the simulation, during a fight |
| Lifetime | the project | one encounter |
| Shared | yes — one asset serves every instance | no — one instance each |
| Example | `ClassDefinition` (a tank has 5200 base health) | `Health` (this tank has 3180 right now) |

### Why this is not negotiable

A ScriptableObject is a single shared instance. Writing runtime values into one means:

* every character of that class shares the same health pool;
* in the editor, the change is **saved into the asset** and survives leaving play mode, so the
  project is silently re-balanced by playing it;
* a headless simulation cannot run two encounters without the second inheriting the first's state.

The failure is quiet and the cause is far from the symptom, so the separation is structural rather
than a convention: kernel types cannot reference a ScriptableObject at all, because the kernel has no
engine reference.

---

## 2. The data flow

```
ScriptableObject asset          ClassDefinition, CharacterDefinition, later
        │                       AbilityDefinition, BossDefinition, LootTable
        │  BuildBaseStats() / ToRuntime()    ← always returns a fresh object
        ▼
Plain value object              StatSet, and later immutable ability/phase definitions
        │
        │  constructor
        ▼
Runtime state                   StatBlock, Health, ResourcePool, TargetSelection
        │
        │  read
        ▼
Presentation                    raid frames, cast bars, nameplates, VFX
```

Each arrow points one way. Nothing downstream writes back upstream.

`ClassDefinition.BuildBaseStats(level)` returns a **new** `StatSet` every call, so a character can
never mutate the asset it was created from. `CharacterDefinition.BuildBaseStats()` layers its
overrides on top of that copy. This is the pattern every future data type follows.

---

## 3. Folder layout

```
Assets/_Project/Data/
  Abilities/    AbilityDefinition                       (Phase 3)
  Bootstrap/    BootstrapConfig, CameraRigSettings      ✅
  Bosses/       BossDefinition, PhaseDefinition         (Phase 8)
  Characters/   CharacterDefinition                     ✅
  Classes/      ClassDefinition                         ✅
  Effects/      EffectDefinition                        (Phase 3)
  Enemies/      EnemyDefinition                         (Phase 7)
  Items/        ItemDefinition                          (Phase 10)
  LootTables/   LootTable                               (Phase 10)
  Mechanics/    MechanicDefinition                      (Phase 8)
  Raid/         RaidDefinition, RaidZoneDefinition      (Phase 7)
```

Empty folders exist because the layout is part of the plan. A folder appearing later would mean the
plan changed; a folder sitting empty means the phase has not arrived.

`Assets/_Project/Settings/` holds engine-level configuration that is not gameplay data — currently
`RaidSimControls.inputactions`.

---

## 4. Stats: a bag, not a struct

Stats are a `StatType` key and a float. A `StatSet` is a bag of those pairs; a `StatBlock` is a
`StatSet` plus every modifier currently applied.

Authored stats are a **list of `StatEntry`**, not a class with one field per stat:

```yaml
_baseStats:
- Stat: 1     # MaxHealth
  Value: 5200
- Stat: 7     # Armor
  Value: 900
```

Adding a stat is one line in the `StatType` enum. No data asset changes, no inspector changes, no
migration — an asset that does not mention a stat simply leaves it at its neutral value.

`StatType` values are explicit and stable because they are written into assets and save files.
**Never renumber an existing entry; append.** `Tools/Unity/generate_default_data.py` mirrors the
enum, and the C# declaration says so.

### Modifier order

```
final = (base + Σflat) × (1 + Σadditive) × Π(1 + multiplicative)
```

then clamped by `StatRules`. Additive percentages sum (+20% and +30% is +50%); multiplicative ones
compound. Two 50% damage reductions therefore leave 25% damage taken, not 0% — which is why
damage-reduction cooldowns must be multiplicative.

Every modifier carries a `SourceKey`, so an expiring effect or an unequipped item withdraws exactly
its own contributions with `RemoveModifiersFromSource`, without recomputing anything else.

### Defaults are invariants, not balance

`StatRules.DefaultBase` gives each stat its **identity element**: multipliers default to 1, rates and
ratings to 0. `StatRules.Clamp` enforces what a stat may never be — a probability outside 0..1, a
negative pool, a negative multiplier that would turn damage into healing. No tuning value appears in
either; those all live in data. This is how the "no constants in gameplay code" rule and "a clamp has
to live somewhere" coexist.

---

## 5. Validation

Every data asset exposes `string Validate()` returning a human-readable problem or `null`.

A data-driven project fails in a specific way: the code is fine and a field is empty. That failure is
cheap to catch and expensive to debug at runtime, so:

* `GameBootstrap` validates its config before building anything and refuses to start with a message
  naming the field;
* **Raid Simulator → Validate Project Data** runs every asset's validation at once;
* `python3 Tools/Unity/verify_references.py` checks, without Unity, that every GUID reference
  resolves to a file that exists.

New asset types add their validation to the same menu rather than inventing a new mechanism.

---

## 6. Assets, GUIDs and authoring outside the editor

Unity identifies assets by the GUID in their `.meta` file, and stores every reference as that GUID. A
project authored outside the editor therefore needs `.meta` files up front, or Unity invents fresh
GUIDs on import and every hand-written reference breaks.

`Tools/Unity/generate_meta.py` derives each GUID from the asset's project-relative path
(`md5("raidsim::" + path)`), so:

* the same path always yields the same GUID, in any checkout;
* a generator can compute the GUID of a file it wants to reference, with no lookup table;
* re-running never changes an existing asset's identity — files that already have a `.meta` are left
  alone, so the editor can take over maintenance at any point.

`Tools/Unity/generate_default_data.py` uses the same function to write the default assets and their
references, then skips any file that already exists.

**The one exception** is `RaidSimControls.inputactions`. It is produced by the Input System's
scripted importer, and a reference to it needs a sub-object id the editor assigns at import time.
That cannot be predicted from outside, so the field is left empty and **Raid Simulator → Repair Data
References** fills it in one click. See [`../CLAUDE.md`](../CLAUDE.md), section 5.

---

## 7. Save data

The save system lands in Phase 11, with two commitments made now:

* Saves are **versioned from the first release**. A save format without a version number cannot be
  migrated.
* Saves store **references and values, never object graphs**: an asset GUID or a stable id plus the
  runtime numbers. A serialized `StatBlock` with embedded modifier objects would be unreadable the
  moment an effect is renamed.

Save scope grows: settings and progress first, then loot, character configuration, inventory,
equipment and builds.

---

## 8. Checklist for a new data type

1. Create the ScriptableObject in the matching `Scripts/<Domain>/Data/` folder.
2. `[CreateAssetMenu]` under `Raid Simulator/<Domain>/`.
3. Private serialized fields with `[Tooltip]`; public read-only properties. No public fields.
4. A `Validate()` returning a human-readable problem or `null`, wired into the validation menu.
5. Conversion to runtime state that returns a **fresh** object every call.
6. Assets in the matching `Data/` folder.
7. Unit tests for the conversion in `Tests/EditMode/`.
8. A row in this document's folder table.
