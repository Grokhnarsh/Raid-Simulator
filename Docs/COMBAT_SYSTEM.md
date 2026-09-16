# COMBAT_SYSTEM.md

> **Status: specification.** Phase 1 built the foundation this describes — stats, pools, entities,
> targeting. The damage and healing pipelines land in Phase 2 and the threat table in Phase 6. This
> document becomes a description of shipped code as those phases complete; sections are marked.

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

No component knows about the others. They are assembled by `CombatActor` on the Unity side and by the
encounter builder in headless runs, and they communicate through the event bus.

---

## 2. What exists now ✅

### Health

Owns one number and nothing else — no damage types, no mitigation, no death handling. The combat
system owns that pipeline. Keeping the pool this dumb is what makes the damage formula testable in
isolation.

Two behaviours that the rest of the system depends on:

* `Remove` and `Add` return **what was actually applied**. The difference from what was asked is
  overkill or overhealing, which is where the statistics collector gets those numbers from rather
  than recomputing them.
* Raising maximum health preserves the current value, so a temporary health buff does not read as a
  sudden heal; lowering it clamps.

Healing never resurrects. Resurrection is an explicit operation, because "a heal landed on a corpse"
and "the corpse stood up" must not be the same event.

### Resource

Resource kind is authored per class. Mana and energy start full and regenerate; rage and focus start
empty and are built. `TrySpend` is all-or-nothing: an ability either pays its full cost or does not
fire, so a rejected cast never partially drains the pool.

### Stats

See [`DATA_ARCHITECTURE.md`](DATA_ARCHITECTURE.md), section 4, for modifier order and clamping.
Combat reads final values; it never reads a base value.

### Targeting

Every targeting decision in the project is one query: a `TargetFilter` (who is legal) plus a scorer
(who is best).

`TargetFilter` covers allegiance, maximum and minimum range, height difference, whether the dead
qualify, and whether the observer may target itself. Two details that matter in a raid:

* **Range is measured between footprints**, not centres. A boss with a four-metre radius is reachable
  from further out than a trash mob, which is what makes melee range work against large enemies.
* **Height is checked separately** from horizontal distance, so an enemy on a balcony directly
  overhead is horizontally close but not meleeable.

`TargetQuery` provides nearest, nearest-in-cone, distance-ordered cycling, and `SelectBest` with an
arbitrary scorer. Ties resolve to the lowest entity id, so repeated queries are stable — an AI that
flickers between two equally valid targets looks broken.

`TargetSelection` holds one entity's current target, publishes only real changes, and clears itself
when its target leaves the simulation, so nothing downstream defends against a stale reference.

---

## 3. Damage pipeline — Phase 2

```
Ability or auto-attack
      ▼
DamageRequest      source, target, base amount, type, ability, can crit
      ▼
1. Roll critical            CriticalChance → CriticalMultiplier
2. Scale by power           AttackPower for physical, SpellPower for magic
3. Apply outgoing modifier  DamageDoneModifier on the source
4. Apply mitigation         Armor for physical, Resistance for magic, none for true
5. Apply incoming modifier  DamageTakenModifier on the target
6. Absorb into shields      shields consume before health
7. Remove from health       returns what was applied → overkill is the difference
      ▼
DamageResult       final amount, mitigated, absorbed, overkill, was critical, was lethal
      ▼
DamageDealtEvent / DamageTakenEvent / EntityDiedEvent
```

The first formula is intentionally simple:

```
Damage = BaseDamage × AbilityMultiplier × CritMultiplier × Mitigation
```

It is a **pipeline of steps**, not one expression, so a later step — resistance penetration, a
damage-over-time snapshot, a reflect, an absorb shield — is inserted rather than bolted onto an
equation.

### Damage types

| Type | Reduced by | Used for |
| --- | --- | --- |
| Physical | Armor | weapon attacks, most melee abilities |
| Magic | Resistance | spells, most boss abilities |
| True | nothing | mechanics that must not be mitigated away |

`DamageResult` reports `mitigated` and `absorbed` separately from the final amount, because a combat
log that cannot show how much was prevented cannot teach a player whether a cooldown helped.

### Healing

The same shape: roll critical, scale by `SpellPower`, apply `HealingDoneModifier`, add to health, and
report effective healing and overhealing from what the pool actually applied.

---

## 4. Events ✅ (bus) / Phase 2 (payloads)

| Event | Raised when | Listened to by |
| --- | --- | --- |
| `EntityRegisteredEvent` ✅ | an entity joins | UI, AI, encounter |
| `EntityUnregisteredEvent` ✅ | an entity leaves | anything holding a reference |
| `TargetChangedEvent` ✅ | a selection changes | target frame, ability bar, highlight |
| `GameStateChangedEvent` ✅ | the game state changes | HUD, menus, input maps |
| `AttackStartedEvent` | an attack or cast begins | animation, cast bar, AI interrupt logic |
| `DamageDealtEvent` | after damage resolves | combat log, statistics, threat |
| `DamageTakenEvent` | after damage is applied | UI, healer AI, statistics |
| `HealAppliedEvent` | after healing resolves | combat log, statistics, threat |
| `EntityDiedEvent` | health reaches zero | encounter, loot, threat, UI, AI |
| `AbilityCastEvent` | an ability completes | combat log, statistics, cooldowns |
| `EffectAppliedEvent` / `EffectRemovedEvent` | a buff or debuff changes | UI, AI |
| `ThreatChangedEvent` | a threat table reorders | UI, enemy AI |

All are `readonly struct`: no allocation per damage tick, and no listener can mutate what another
listener sees.

---

## 5. Threat — Phase 6

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

## 6. Effects — Phase 3

One generic system covers buff, debuff, damage over time, heal over time, shield, stun, silence,
slow, damage reduction and damage increase. An effect instance carries duration, stacks, source,
target and its `EffectDefinition`.

Stat changes are applied as `StatModifier`s tagged with the effect instance's `SourceKey`, so expiry
is a single `RemoveModifiersFromSource` call — no bookkeeping, no drift.

Stacking is configurable per effect: refresh the duration, add a stack, run as independent instances,
or reject while one is active. Effects tick at order 300, before anything reads a stat.

---

## 7. Death

Death is `Health.Current` reaching zero. The pool does not decide what that means:

1. `EntityDiedEvent` is published with the killer and the killing blow.
2. The entity stops being a legal target for anything except effects that allow the dead.
3. Its threat is dropped from every table.
4. The encounter checks its end conditions — boss dead, or the whole raid dead.
5. Loot, statistics and the combat log react to the event.

A dead entity stays registered so the UI can grey its frame out and a resurrection has something to
target. Unregistering happens when the encounter resets.

---

## 8. Testing

Kernel combat is covered by fast unit tests with no engine. Already covered: stat modifiers and
clamping, health and resource behaviour including overkill and overhealing, target filters and
queries, selection lifetime, the clock, the tick loop and the event bus — 139 tests.

Phase 2 adds: mitigation per damage type, critical strikes, shield absorption, overkill reporting,
death, and healing including overhealing. See [`TESTING.md`](TESTING.md).
