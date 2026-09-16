# COMBAT_SYSTEM.md

> **Status:** damage, healing, death and the basic attack are built and tested (Phase 2). Effects
> and threat are specified here and land in Phases 3 and 6; those sections are marked.

---

## 1. Structure

```
CombatEntity (ICombatEntity)
  ├── Stats        StatBlock      ✅ Phase 1
  ├── Health       Health         ✅ Phase 1
  ├── Resource     ResourcePool   ✅ Phase 1
  ├── Abilities    AbilityBook       Phase 3
  ├── Effects      EffectContainer   Phase 3
  ├── Threat       ThreatTable       Phase 6
  └── Events       via IEventBus  ✅ Phase 1
```

No component knows about the others. They are assembled by `CombatActor` on the Unity side and by
the encounter builder in headless runs, and they communicate through the event bus.

---

## 2. Pools ✅

### Health

Owns one number and nothing else — no damage types, no mitigation, no death handling. `CombatSystem`
owns that pipeline. Keeping the pool this dumb is what makes the damage formula testable in
isolation.

Two behaviours the rest of the system depends on:

* `Remove` and `Add` return **what was actually applied**. The difference from what was asked is
  overkill or overhealing, which is where those numbers come from — never a second calculation that
  could drift from the pool's own clamping.
* Raising maximum health preserves the current value, so a temporary health buff does not read as a
  sudden heal; lowering it clamps.

Healing never resurrects. Resurrection is an explicit operation, because "a heal landed on a corpse"
and "the corpse stood up" must not be the same event. A heal on a corpse therefore resolves to zero
applied and the whole amount as overhealing, which is exactly what a healer's statistics should show.

### Resource

Resource kind is authored per class. Mana and energy start full and regenerate; rage and focus start
empty and are built. `TrySpend` is all-or-nothing: an ability either pays its full cost or does not
fire, so a rejected cast never partially drains the pool.

### Stats

See [`DATA_ARCHITECTURE.md`](DATA_ARCHITECTURE.md), section 4, for modifier order and clamping.
Combat reads final values; it never reads a base value.

---

## 3. Damage ✅

### The pipeline

```
DamageRequest      source, target, base amount, type, power coefficient,
      │            ability multiplier, whether it may crit, a label for the log
      ▼
1. Power scaling            + AttackPower or SpellPower × PowerCoefficient
2. Ability multiplier       × AbilityMultiplier
3. Critical strike          × CriticalMultiplier, if the roll succeeds
4. Outgoing modifier        × DamageDoneModifier on the source
5. Mitigation               − Armor or Resistance; True damage skips this
6. Incoming modifier        × DamageTakenModifier on the target
7. Apply to health          returns what was removed → overkill is the remainder
      ▼
DamageResult       amount, applied, mitigated, overkill, was critical, was lethal
      ▼
DamageDealtEvent + DamageTakenEvent, then EntityDiedEvent if it was lethal
```

This is the brief's formula — `BaseDamage × AbilityMultiplier × CritMultiplier × Mitigation` —
written as ordered steps rather than one expression, so that resistance penetration, absorption
shields, reflects or a damage-over-time snapshot each become a step inserted at a known point rather
than a rewrite of an equation.

**The order is not arbitrary.** Mitigation applies after the attacker's own multipliers, so armour
reduces the whole hit including its critical bonus. The target's incoming modifier applies *after*
mitigation, so a damage-reduction cooldown is worth the same proportion whatever the target's armour
happens to be — otherwise a cooldown would be worth more on a cloth wearer than on a tank, which is
the opposite of the intent.

`DamagePipeline.Calculate` runs steps 1–6 without touching anything, so AI and UI can ask "how much
would this hit for?" and so the arithmetic is testable with no health pool in the way.

### Damage types

| Type | Scales from | Reduced by | Used for |
| --- | --- | --- | --- |
| Physical | `AttackPower` | `Armor` | weapon attacks, most melee abilities |
| Magic | `SpellPower` | `Resistance` | spells, most boss abilities |
| True | nothing | nothing | mechanics that must not be mitigated or out-geared |

True damage deliberately ignores power scaling as well as mitigation. That is what makes it usable
for a soak that has to hurt or an enrage that has to end the fight: its lethality stays predictable
no matter what gear the raid brings.

### Mitigation

```
fraction = rating / (rating + K)        K = ConstantPerLevel × attacker level
fraction = min(fraction, MaximumMitigation)
```

Chosen for two properties the design needs. It has **diminishing returns**, so stacking armour
approaches but never reaches immunity — doubling armour from 100 to 200 takes reduction from 50% to
66.7%, not to 100%. And it is **scale-free**: the same rating is worth proportionally less against a
higher-level attacker with no special case anywhere.

The shape of the curve is a system rule and lives in `Mitigation`. The constants that position it are
tuning and live in `CombatTuningAsset`. The ceiling is clamped below 1 in code regardless of what is
authored, because total immunity is an effect, not something gear alone may reach.

At the shipped tuning (55 per level), a level-60 attacker needs the target to hold 3300 armour to
halve the hit.

### Critical strikes

Rolled from `CriticalChance` (a probability, clamped to 0..1 by `StatRules`) and multiplied by
`CriticalMultiplier`. A request may forbid criticals outright — mechanics whose damage must be exact
set `canCritical: false`.

### Randomness and replay ✅

Every roll goes through `IRandomSource`. `DeterministicRandomSource` implements xorshift128 rather
than wrapping `System.Random`, whose sequence is not contractually stable across .NET versions or
platforms — a replay that produced different criticals on a different machine would make the Phase 11
encounter simulator's output impossible to compare, which is the one thing it exists to do.

The seed is authored on `CombatTuningAsset`. Tests use `FixedRandomSource` so that "did the critical
multiply correctly?" is a separate question from "did the critical happen?".

---

## 4. Healing ✅

The mirror of damage, with the differences that matter: healing always scales from `SpellPower`
regardless of who casts it, nothing mitigates it, and the wasted part is overhealing rather than
overkill.

```
1. Power scaling       + SpellPower × PowerCoefficient
2. Ability multiplier  × AbilityMultiplier
3. Critical strike     × CriticalMultiplier, if the roll succeeds
4. Outgoing modifier   × HealingDoneModifier on the source
5. Apply to health     returns what was added → overhealing is the remainder
```

Attack power never contributes to healing, so a plate-wearing class with a heal does not scale it
from its weapon damage.

---

## 5. The combat system ✅

`CombatSystem` is the single entry point for hurting and healing anything. Everything that deals
damage calls `ApplyDamage`; nothing else touches a health pool as a result of combat. That single
funnel is what guarantees no damage source can skip mitigation, forget to publish its event, or fail
to notice that it killed something.

It is **not** an `ISimulationSystem` and is never ticked — it is a service that resolves requests and
publishes results.

### Death is reported exactly once

The system remembers who it has already declared dead, so a corpse taking a second hit in the same
tick, or two sources landing simultaneously, still produces one `EntityDiedEvent`. The record clears
when an entity leaves the simulation or the encounter resets, so an entity revived for a new attempt
can die again.

`CombatSystem.Kill` exists for debug tooling and for mechanics whose whole purpose is to be
unsurvivable. It routes through the same funnel as true damage rather than emptying the pool
directly, so the death is reported and logged exactly like any other.

---

## 6. The basic attack ✅

`AutoAttackSystem` swings for every entity that has a profile and a legal target in range. One system
serves the whole encounter rather than each entity running its own timer, so a forty-player raid plus
thirty adds is one loop over a dense list.

`AutoAttackProfile` is authored on `ClassDefinition`: swing interval, base damage, power coefficient,
damage type, range and a label. A tank's slow heavy swing, a ranged attacker's shot from
twenty-five metres and a caster's magical bolt are the same system with different numbers — and a
practice target that never attacks is simply a swing interval of zero, which the system declines to
register at all.

**Swing timing.** The timer advances only while a legal target is in range, and a swing is ready the
moment an entity first engages. Losing the target — by walking out of range, by it dying, or by
switching — *pauses* the timer rather than resetting it. So the first attack of an encounter is
immediate, but rapid target-swapping cannot be used to swing faster than the interval allows.

The system never decides *who* to attack. It reads whatever the entity's `ITargetProvider` reports,
so the player's click and (from Phase 5) an AI's choice arrive here identically.

Range is measured between footprints, so a boss with a four-metre radius is reachable from further
out than a trash mob.

---

## 7. Events ✅

| Event | Raised when | Listened to by |
| --- | --- | --- |
| `EntityRegisteredEvent` | an entity joins | UI, AI, encounter |
| `EntityUnregisteredEvent` | an entity leaves | anything holding a reference |
| `TargetChangedEvent` | a selection changes | target frame, ability bar, highlight |
| `GameStateChangedEvent` | the game state changes | HUD, menus, input maps |
| `AttackStartedEvent` | a swing or cast begins | animation, cast bar, AI interrupt logic |
| `DamageDealtEvent` | after damage resolves | combat log, statistics, threat |
| `DamageTakenEvent` | after damage is applied | UI, healer AI, statistics |
| `HealAppliedEvent` | after healing resolves | combat log, statistics, threat |
| `EntityDiedEvent` | health reaches zero | encounter, loot, threat, UI, AI |
| `AbilityCastEvent` | an ability completes (Phase 3) | combat log, statistics, cooldowns |
| `EffectAppliedEvent` / `EffectRemovedEvent` | a buff or debuff changes (Phase 3) | UI, AI |
| `ThreatChangedEvent` | a threat table reorders (Phase 6) | UI, enemy AI |

Damage raises **two** events rather than one because the interested parties differ: the combat log,
the statistics collector and the threat system care who dealt it, while the healer AI and the health
UI care who took it. A single event would force every listener to filter.

A fully mitigated or zero-amount hit is still published. An attack that did nothing still happened,
and a combat log that hides it would mislead a player about whether their cooldown worked.

All events are `readonly struct`: no allocation per damage tick, and no listener can mutate what
another listener sees.

`CombatEventFeed` in `DebugTools` is the first consumer — it turns damage, healing and death into
readable lines for the development overlay without a single line of combat code knowing it exists.
It is debug tooling, compiled out of release builds; the player-facing combat log is Phase 9 and will
subscribe the same way.

---

## 8. Threat — Phase 6

Every hostile entity owns a `ThreatTable` of `(entity, value)` entries and attacks the highest. The
threat system subscribes to damage, healing and support events; it is never called directly by the
thing that generated the threat.

* Threat is scaled by the source's `ThreatModifier` — a tank's authored multiplier is what makes
  holding possible without dealing the most damage.
* Taunt is a **time-limited** override: it forces the target for a duration, after which the table
  decides again. Tanking is therefore an ongoing job rather than one button.
* Threat is dropped when an entity dies, leaves range, or the encounter resets.

Tick order matters: threat settles at order 400, before AI reads the table at 600. See
[`ARCHITECTURE.md`](ARCHITECTURE.md), section 4.

---

## 9. Effects — Phase 3

One generic system covers buff, debuff, damage over time, heal over time, shield, stun, silence,
slow, damage reduction and damage increase. An effect instance carries duration, stacks, source,
target and its `EffectDefinition`.

Stat changes are applied as `StatModifier`s tagged with the effect instance's `SourceKey`, so expiry
is a single `RemoveModifiersFromSource` call — no bookkeeping, no drift.

Absorption shields insert into the damage pipeline between step 6 and step 7: after every multiplier
has settled, before health is touched. `DamageResult` gains an `Absorbed` field at that point. It is
not there yet because nothing can produce a shield, and a field that is permanently zero would
mislead the combat log.

Stacking is configurable per effect: refresh the duration, add a stack, run as independent instances,
or reject while one is active. Effects tick at order 300, before anything reads a stat.

---

## 10. Death ✅

Death is `Health.Current` reaching zero. The pool does not decide what that means:

1. `EntityDiedEvent` is published with the killer and the killing blow's label.
2. The entity stops being a legal target for anything except effects that allow the dead.
3. Its threat is dropped from every table *(Phase 6)*.
4. The encounter checks its end conditions — boss dead, or the whole raid dead *(Phase 7)*.
5. Loot, statistics and the combat log react to the event *(Phases 9–11)*.

A dead entity stays registered so the UI can grey its frame out and a resurrection has something to
target. Unregistering happens when the encounter resets.

---

## 11. Testing

Kernel combat is covered by fast unit tests with no engine — **223 tests, about 340 ms**.

Phase 2 added 84: the damage pipeline (power scaling per type, ability multipliers, criticals and
their interaction with mitigation, each mitigation stat against each damage type, diminishing
returns, the ceiling, level scaling, outgoing and incoming modifiers, immunity, overkill, corpses,
sourceless damage, preview purity), the healing pipeline (spell-power-only scaling, criticals,
overhealing, no resurrection), the combat system (both event perspectives, death reported once,
chain-reaction deaths, debug kills, encounter reset), the basic attack (engage timing, interval,
range between footprints, hostility, target-swap abuse, pause-not-reset, registration lifecycle,
killing a target over time) and the random source (reproducibility, range, certainty at the
extremes).

See [`TESTING.md`](TESTING.md).
