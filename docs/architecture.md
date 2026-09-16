# Architecture

This document covers the decisions that are hard to reverse. Anything not
mentioned here was a normal choice made normally.

---

## 1. Four assemblies, strictly one-directional

```
EmberDepths.Core  ←  EmberDepths.Content  ←  EmberDepths.Gameplay  ←  EmberDepths.Editor
```

| Assembly | Contains | Must not contain |
|---|---|---|
| **Core** | Grid maths, A*, sim clock, seeded RNG | Anything about dungeons, actors or combat |
| **Content** | ScriptableObject definitions, effect data | Behaviour, scene references, Gameplay types |
| **Gameplay** | Actors, combat, AI, generation, UI | Editor-only APIs |
| **Editor** | Importers, content builders, scene builder | Anything the game needs at runtime |

The boundary that does the most work is **Content → Gameplay**. Effects are
authored as data (`DamageEffect`, `SpawnHazardEffect`, …) and must act on a live
world without knowing the world exists. They do it through
[`IAbilityContext`](../Assets/_Project/Scripts/Content/Abilities/IAbilityContext.cs),
an interface declared in Content and implemented in Gameplay.

That interface is the vocabulary designers get. Widening it is a deliberate act:
adding a verb there is what turns "we need code for this" into "someone can
build it in the inspector".

---

## 2. Fixed-tick simulation, interpolated presentation

Everything that is *simulation* — AI decisions, cooldowns, damage over time,
hazard pulses, threat decay — runs on
[`SimClock`](../Assets/_Project/Scripts/Core/Sim/SimClock.cs) at **20 Hz**.
Everything that is *presentation* runs per frame and reads `Clock.Alpha` to
interpolate.

Three reasons, and the third is the one that matters most later:

1. **Balance numbers become comparable.** A burn that ticks 20 times a second on
   a fast machine and 12 on a slow one is not a tunable number.
2. **Runs replay.** Seed plus tick count reproduces a fight exactly, which makes
   encounter bugs reportable.
3. **It is the seam networking needs.** Ticks are what you send over a wire;
   frames are not. Nothing has to be rewritten to add a transport later — the
   division already exists.

`DungeonRunner.Update` is the only `Update` loop in the gameplay layer.
Everything else advances because it was called, so a frame's order of operations
is readable in one place.

```
Update ─► Clock.Advance ─► (per tick) ─► World.Tick ─► scheduled payloads
                                                    ─► actors  (status, abilities, motor)
                                                    ─► brains  (decisions)
                                                    ─► hazards, telegraphs
                                     ─► Director.Tick  (room activation)
LateUpdate ─► ActorView              (interpolate, pick sprite)
```

---

## 3. Seeded RNG, never `UnityEngine.Random`

[`DeterministicRandom`](../Assets/_Project/Scripts/Core/Sim/DeterministicRandom.cs)
is xorshift128 with a `Fork(label)` method that derives independent streams.

`UnityEngine.Random` is global mutable state. A particle system, an editor tool
or a third-party asset calling it silently changes the dungeon a seed produces.
Forked streams also mean re-rolling loot cannot shift a layout that was already
generated from the same seed.

Streams in use: `layout`, `liquid`, `encounters`, `tiles`, `room{id}`.

---

## 4. One `Actor` type for everything

There is no `Player` class and no `Enemy` class. A boss is an `Actor` with a
boss brain and a large `StatBlock`; a party member is an `Actor` whose intents
come from a controller rather than an AI.

The payoff is that every mechanic works on everyone without anyone deciding it
should. Knockback works on the boss (it just sets `ActorFlags.Unmovable`).
Burning works on party members. A charmed enemy would work on day one.

Simulation state lives on `Actor`. Everything visual lives on `ActorView`, which
reads and never writes. A headless balance run can spawn actors with no view
attached at all.

---

## 5. Threat is a first-class system

[`ThreatTable`](../Assets/_Project/Scripts/Gameplay/Combat/ThreatTable.cs) is what
makes five people a party rather than five people standing near each other.
Enemies pick targets from it, not by proximity.

Two details that are easy to get wrong:

- **Switch margin (1.1×).** A challenger must clearly exceed the current target,
  or a boss visibly stutters between two damage dealers trading the lead every
  tick.
- **Healing generates threat**, split across every engaged enemy. A healer who
  never touches an enemy still climbs its table — which is precisely why the
  tank has to hold aggro for them.

---

## 6. Telegraphs are a fairness system, not a visual effect

[`TelegraphSystem`](../Assets/_Project/Scripts/Gameplay/Combat/TelegraphSystem.cs)
draws where an enemy ability will land and how long the party has. The warning
fills over its lead time and pulses in the last 20%.

An untelegraphed area effect is indistinguishable from a bug: health vanishes
with no visible cause. Every enemy ability with a shape carries a lead time, and
the boss's biggest hits carry the longest (Meteor Fall: 2.4 s).

Enemy AI dodges these *unreliably* on purpose — `HazardAvoidance` below 1 means
they sometimes fail to react. An AI that dodges perfectly makes its own
telegraphs pointless. Companion AI dodges reliably, because a companion standing
in fire reads as broken rather than as characterful.

---

## 7. Damage flows through exactly one function

Every source — abilities, burning, standing in lava, falling meteors — reaches
`CombatSystem.DealDamage` or `DealFlatDamage`. Mitigation order is fixed:

```
crit  →  armour (physical only, diminishing)  →  school resistance  →  damage-taken multiplier
```

Order matters. Applying resistance before armour would make armour scale with
resistance. And funnelling everything through one place is what makes "fire
resistance" mean *takes less fire damage* rather than *takes less damage from
the four abilities someone remembered to wire it into*.

Coefficients, not final numbers. `DamageEffect.Coefficient` multiplies the
caster's `Power`, so one Power change rescales an actor's whole kit
consistently. `DealFlatDamage` is the exception, for hazards that must mean
"2% of max health per tick" literally.

---

## 8. Grid, not colliders

The world is a `GridMap` of integer cells. 2D physics is set to
`SimulationMode2D.Script` and never stepped.

`GridMap` deliberately separates two questions:

- **Terrain** — can this cell ever be walked?
- **Occupancy** — is someone standing there right now?

Melee AI has to path *towards* an occupied cell and stop beside it. That only
works if the two questions have separate answers.

The isometric projection lives entirely in
[`IsoGrid`](../Assets/_Project/Scripts/Core/Grid/IsoGrid.cs), whose constants are
chosen to match a Unity `Grid` set to Isometric with cell size (2, 1, 1) at 32
PPU — so tilemap-placed tiles and script-placed actors land on identical world
positions.

Sprite depth uses the camera's **custom transparency sort axis (0,1,0)**: an
actor further up the screen is further away and draws behind. Sorting layers
exist only for things that must ignore that rule — floor decals under everyone,
shadows under their owner, health bars on top.

---

## 9. Rooms populate lazily

`EncounterDirector` spawns a room's pack when the party first enters it, seals
the doors, and unseals them when the last enemy dies.

Spawning everything up front costs a hundred brains ticking for nothing and —
worse — lets a stray area effect pull three rooms at once.

---

## 10. Content generated in code, for now

[`VolcanoContentBuilder`](../Assets/_Project/Scripts/Editor/Content/VolcanoContentBuilder.cs)
creates every ScriptableObject: statuses, hazards, abilities, enemies, boss
phases, encounters, biome, dungeon, roster.

This is scaffolding with an expiry date. While the *shape* of the content is
still moving, fifty inspector-authored assets are the hardest thing in a Unity
project to keep consistent or to review; here the entire encounter design is one
file you can diff. It updates assets in place rather than replacing them, so
references survive a re-run and hand tweaks to untouched fields are preserved.

Once the numbers settle, designers own the `.asset` files and this becomes a
one-time bootstrap.

---

## 11. Items are stat modifiers, not special cases

Equipment adds nothing to the simulation. An item is a slot, a rarity and a
list of `StatModifier`; a set bonus is the same list behind a piece-count
threshold. Both feed the *same* `RuntimeStats.Recompute` that statuses already
feed, so nothing in combat, AI or the HUD knows that gear exists.

Recompute order is **base → equipment → statuses**, and it is load-bearing.
Gear is treated as part of who the character is, so a temporary +20% damage buff
multiplies the geared number. The reverse order would make every buff weaker the
better your gear, which is backwards from what a player expects.

Three details worth keeping:

- **Recompute from scratch, never incrementally.** The classic gear bug is a
  bonus that never comes off. `ItemSystemTests.UnequippingRemovesItsContribution`
  is the test that says so.
- **Gather then apply.** `StatAccumulator` collects every modifier before
  applying any, so the result cannot depend on the order items were equipped in.
- **Set bonuses ignore upgrade level.** They reward breadth, not depth —
  otherwise the cheapest route to a big bonus would be pouring embers into one
  piece.

`ItemInstance` is deliberately a separate type from `ItemDefinition` even though
it currently holds only an upgrade level. The moment items roll random affixes
or get re-forged, the line between "what this item is" and "what this particular
one became" is the difference between a small change and a rewrite.

---

## 12. Two curves: embers and sets

The reward economy has exactly two levers, and they pull against each other on
purpose.

**Embers** are the reliable curve. Trash always pays them, and they buy upgrade
levels at compounding cost. This is most of a run's power, and it is entirely
under the player's control.

**Sets** are the disruptive one. Each piece is weaker than the best standalone
gear for its slot, so equipping the second piece of a set is a downgrade *right
now* in exchange for a bonus later. That tension is the mechanic. If set pieces
were simply better, there would be no decision — and `Equipment.SetProgressBonus`
exists to make the companion AI weigh it the same way a player would, peaking
right before a threshold.

Loot rolls come from the run's seeded stream, forked per kill on the actor's id.
Without the id every enemy in a pack rolls identically; without the seed, "this
seed is unwinnable" is unfalsifiable and a thousand-run balance sweep measures
nothing.
