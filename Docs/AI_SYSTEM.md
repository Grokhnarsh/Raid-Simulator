# AI_SYSTEM.md

> **Status: specification.** Phase 1 built the pieces the AI will be assembled from — the driver
> seam, `MovementIntent`, `TargetFilter`/`TargetQuery` and the tick order. The AI itself lands in
> Phase 5. This becomes a description of shipped code then.

---

## 1. The governing constraint

Four of the five group members are AI-controlled, and in a simulated run all five are. The AI is
therefore not a fallback for an absent player — **it is how most of the game is played**, and its
decisions are the thing the player is really tuning when they choose a composition or a strategy.

Two rules follow, and both are already enforceable:

1. **AI is never written against a character.** Nothing may branch on a display name. Behaviour comes
   from an `AIProfile` asset selected by `CombatRole`.
2. **AI and player use the same seam.** Both produce a `MovementIntent` and ability decisions; the
   locomotion and ability systems cannot tell them apart. This is what makes "hand this character to
   the AI" and "run the encounter with no player" work without a second code path.

---

## 2. The driver seam ✅

```
PlayerController  ──┐
                    ├──▶ MovementIntent + ability decisions ──▶ ILocomotor / AbilitySystem
AIDriver (Phase 5) ─┘
```

`PlayerDriverSystem` already registers the player's driver as an ordinary `ISimulationSystem` at
`SystemOrder.Locomotion`. An AI driver registers the same way at `SystemOrder.Ai`. Consequences that
come for free:

* AI freezes on pause because the clock stopped, with no pause check in AI code;
* AI runs in the headless simulator with no input device present;
* AI decisions land at a defined point in the tick order, after threat has settled.

---

## 3. Structure

```
AIProfile (ScriptableObject)
  ├── StateMachine       which states exist and how they are entered
  ├── TargetRules        an ordered list of target selection rules
  ├── AbilityRules       conditions under which each ability is used
  └── PositioningRules   where this role wants to stand
```

A behaviour tree is the alternative. The project starts with a **state machine plus ordered rule
lists**, because the decisions in a raid encounter are mostly priority lists ("keep the tank alive,
then the lowest ally, then top people up") rather than deep conditional trees, and a flat ordered
list is far easier to author, debug and explain in a UI the player will eventually see. If encounter
complexity outgrows it, a behaviour tree becomes an alternative `IDecisionMaker` behind the same
seam rather than a rewrite.

### States

`Idle` · `Patrol` · `Alert` · `Combat` · `Cast` · `Move` · `Dead`

Transitions are data. `Cast` and `Move` are states rather than flags because both suppress other
actions, and expressing that as a state removes a class of "cast while walking" bug.

---

## 4. Target selection ✅ (mechanism) / Phase 5 (rules)

`TargetQuery.SelectBest` already takes a `TargetFilter` plus a scorer. Every rule the design calls for
is one of those two parts, never a new code path:

| Rule | Filter | Scorer |
| --- | --- | --- |
| Highest threat | hostile | threat value |
| Lowest health | friendly | `-Health.Fraction` |
| Lowest health in range | friendly, within heal range | `-Health.Fraction` |
| The healer | hostile, role = Healer | distance |
| Random raider | friendly | seeded random |
| Nearest | hostile | `-EdgeDistance` |
| Inside an area | any, within radius of a point | anything |
| Kill priority | hostile, matching a data tag | authored priority |

A profile names a rule; the scorer is looked up from a small registry. Adding a rule is adding a
scorer, not editing the AI.

Ties resolve to the lowest entity id, so the AI does not flicker between equally valid targets.

---

## 5. Role behaviour

Written against `CombatRole`, never a class or a character.

### Tank

* Engage the highest-priority hostile; keep threat above the group.
* Watch for adds that have targeted someone else and taunt them back.
* Position the boss where the encounter wants it — away from the group, facing away from melee.
* Spend damage-reduction cooldowns against telegraphed heavy hits rather than on cooldown.

### Healer

* Rank allies by health fraction, weighted by role: a tank at 50% is usually more urgent than a
  ranged damage dealer at 40%, because the tank is about to be hit again.
* Prefer an area heal when enough allies are hurt and clustered, otherwise a single-target heal.
* Respect the resource pool: a healer out of mana has lost the fight for everyone. Cheap heals when
  the pool is low, expensive ones when it is not.
* Stay within heal range of the group while obeying positioning constraints.

### Damage

* Select a target by the profile's rules — usually highest priority, respecting kill order.
* Use abilities by priority, subject to cooldown, resource and range.
* Prefer area damage when enough targets are clustered.
* Melee: reach and hold melee range, accounting for the target's radius. Ranged: hold preferred
  range, respect minimum range, move only when a mechanic or line of sight demands it.

### Everyone

* Leave danger zones. Reaching safety outranks damage, healing and threat.
* Honour mechanic instructions: stack, spread, soak, move to a marker.
* Interrupt when the profile says the cast matters and the ability is available.

---

## 6. Positioning ✅ (primitives) / Phase 5 (decisions)

Already available: melee and ranged range measured between footprints, minimum range dead zones,
height limits, facing, `MovementIntent.MoveTo` with a stopping distance, and `MoveFacing` for
strafing while keeping facing.

Phase 5 adds the decision layer, evaluated in this order:

1. **Am I standing in something?** Leave it. Nothing outranks this.
2. **Does a mechanic want me somewhere?** Go there.
3. **Am I in range of my target?** Move into range if not.
4. **Am I where my role wants to be?** Melee behind, ranged spread, healer within range of the group.
5. Otherwise stand still — movement that achieves nothing costs damage and looks broken.

Movement is intent-based, so navigation mesh pathing can be introduced later by swapping the
locomotor, with no change to the AI.

---

## 7. Enemy AI

The same mechanism with different profiles. An enemy profile adds:

* **Threat-based targeting** — read the threat table, which settles earlier in the tick.
* **Aggro radius and leashing** — pull range, and returning home when dragged too far.
* **Ability rotation** — priority list with cooldowns, the same mechanism the group uses.
* **Cast selection** — including abilities the group is meant to interrupt.

A boss is an enemy whose profile is driven by a `BossDefinition` with phases (Phase 8). It is not a
special kind of AI, which is why an add that gains a boss mechanic is an authoring change.

---

## 8. Interrupts

An interruptible cast publishes its start, its duration and whether it can be interrupted. An AI with
an interrupt available and the profile's permission reacts within its reaction window.

Reaction time is **authored, not instant**. A perfect AI would trivialise interrupt mechanics and
teach the player nothing about their importance, so profiles carry a reaction delay — which also
becomes a difficulty and group-quality dial.

---

## 9. Debugging

A simulator whose AI cannot be inspected cannot be tuned. The development overlay grows with each
phase: currently state, time, entity count and the player's target; then AI state per entity, chosen
target and why, threat tables, evaluated positioning, ability rule outcomes, and navigation paths.
All of it is inside `UNITY_EDITOR || DEVELOPMENT_BUILD`.

---

## 10. Testing

AI is a pure decision function over world state, which makes it unusually testable: build a registry
with known entities, ask a rule what it would do, assert the answer. No engine, no frames, no timing.

Phase 5 tests: healer picks the lowest-health ally; healer prefers the tank at equal fractions; area
heal chosen only above the clustering threshold; damage AI respects kill priority; melee AI stops at
its reach against a large target; ranged AI backs out of minimum range; interrupt fires only on
interruptible casts; danger-zone avoidance outranks every other decision.
