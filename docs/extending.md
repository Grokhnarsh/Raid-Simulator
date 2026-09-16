# Extending EmberDepths

The project is shaped so that the common additions are data, not code. This
lists what each addition actually costs.

| You want to add | Code needed | Where |
|---|---|---|
| An ability | none | New `AbilityDefinition` asset |
| An enemy | none | New `EnemyDefinition` asset |
| A trash pack | none | New `EncounterDefinition` asset |
| A boss phase | none | Entry in the `BossDefinition` phase list |
| A whole biome | none | New `BiomeDefinition` + its content |
| A second dungeon | none | New `DungeonDefinition` asset |
| A player class | none | New `ClassDefinition` asset |
| An item | none | New `ItemDefinition` asset |
| An item set | none | New `ItemSetDefinition` + its pieces |
| A loot table | none | New `LootTable` asset |
| A new *kind* of effect | one small class | `AbilityEffect` subclass |
| A new AI behaviour | one class | `BrainBase` subclass |
| A new *stat* an item can touch | one enum entry + one line | `StatKind` and `RuntimeStats.ApplyEquipment` |

---

## An ability

Create an `AbilityDefinition` (right-click ▸ Create ▸ EmberDepths ▸ Ability) and
fill in targeting, shape, timing and a list of effects. Effects are composed:
"damage + burn + knockback" is three entries, not a new class.

Things worth getting right:

- **`TelegraphLeadTime` on anything an enemy casts with a shape.** An
  untelegraphed area effect reads as a bug. The boss's biggest hits use 2.0–2.4 s.
- **`ThreatMultiplier`** on tank abilities. The Warden's Shield Bash is 2.5×,
  Shockwave 3×.
- **`AiMinTargets`** on area abilities, so the AI does not burn a long cooldown
  on one straggler.
- **`AiUseBelowTargetHealth`** turns an ability into a finisher — the Rogue's
  Eviscerate is 0.45.

`Coefficient` is a multiplier on the caster's `Power`, not a final number.

---

## An effect type

Subclass `AbilityEffect`, mark it `[Serializable]`, and it appears in the
inspector's add-effect menu automatically.

```csharp
[Serializable]
public sealed class LifestealEffect : AbilityEffect
{
    [Range(0f, 1f)] public float Fraction = 0.3f;

    public override string Describe() => $"heals the caster for {Fraction:P0} of damage dealt";

    public override void Apply(IAbilityContext ctx)
    {
        for (int i = 0; i < ctx.Targets.Count; i++)
            ctx.Heal(ctx.Caster, Fraction, canCrit: false);
    }
}
```

`Apply` may only use verbs on `IAbilityContext`. That restriction is what keeps
effects replayable and, later, networkable. If you need a verb that does not
exist, add it to the interface and implement it in `AbilityContext` — but treat
that as a real decision: the interface is the vocabulary designers get.

---

## An item, and an item set

An `ItemDefinition` is a slot, a rarity and a list of `StatModifier`. Each
modifier carries a `PerUpgrade` slope, which is the item's growth curve — author
it per modifier so a weapon can gain damage sharply while its crit chance barely
moves.

A set is an `ItemSetDefinition` plus pieces that point back at it. Tiers are
cumulative: four pieces of a set with a (2) and a (4) tier grants both.

What makes a set worth having:

- **Tiers must be big enough to change a decision.** Each piece is weaker than
  the best standalone gear for its slot, so a small bonus is simply never worth
  it. If the (2) tier would not make you equip a worse item, it is too small.
- **Give the capstone an `Aura`, not another stat line.** A `StatusEffectDefinition`
  shows up in the buff list, can tint the wearer, and can later carry behaviour
  that numbers cannot. `GrantedAbility` goes further and is the right payoff for
  a six-piece.
- **One piece per slot.** Two pieces competing for the same slot makes the top
  tier unreachable; `SimulationSmokeTests.SetsAreCompleteAndTieredCoherently`
  fails the build if you do it.

Loot tables reference items directly and roll an upgrade level per drop. `depth`
raises the *floor* of that roll, not the ceiling — a deep room cannot drop worse
than a shallow one, but its best roll is unchanged, which keeps the curve
monotonic without inflating it.

Tuning the economy: `MinEmbers`/`MaxEmbers` on trash is the main dial, because
embers are the reliable half of the power curve. `AlwaysDrops` is for bosses and
chests.

## A biome — the main extension point

`BiomeDefinition` is what the whole project is shaped around. A second biome
touches no simulation code at all.

1. **Art.** Six terrain tiles via PixelLab, props via Blender. Follow
   [art-pipeline.md](art-pipeline.md).
2. **Signature debuff.** The volcano has Burning; a frozen crypt would have
   Frostbite. This is the thing the biome's gear and healer answer.
3. **Terrain hazard.** `LiquidHazard` + `LiquidCoverage`. Above ~0.25 coverage
   rooms stop being navigable for melee.
4. **Enemies and encounters.** Give each `EncounterDefinition` an honest
   `Threat`; the generator spends a per-room budget against it, so a value that
   lies mistunes the whole floor.
5. **A boss.** `EnemyDefinition` with `Rank = Boss` plus a `BossDefinition`.
6. **Assemble the `BiomeDefinition`**, point a new `DungeonDefinition` at it.

The fastest route is to copy `VolcanoContentBuilder`, rename it, and change the
numbers — you get a whole coherent biome as one reviewable file.

---

## A boss fight

Phases are entered in order as health drops and never re-entered, so a fight
always escalates. Vulcanor's three:

| Phase | At | Adds | Teaches |
|---|---|---|---|
| Ember Wrath | 100% | Cleave (cone), Meteor (ground circle) | Don't stand in front. Don't stand in the marker |
| Molten Fury | 65% | Eruption Ring (donut), summons, +15% haste | Close is safe — the inverse of the usual rule |
| Cataclysm | 30% | Magma Wave (line), +30% damage, +35% haste | The damage check the enrage timer measures |

Design notes that generalise:

- **Give each phase one new idea**, not three. A phase transition the party
  cannot read is just a spike in damage taken.
- **Ground-targeted abilities pick a random party member**, not the tank —
  otherwise every meteor lands where someone is already standing and the
  mechanic asks nothing of the group.
- **A donut is worth having.** Inverting "get away from the boss" is what stops
  ranged players parking at max distance for the whole fight.
- **`OnEnter` effects** fire once at the transition: knock the party back, erupt
  the floor, summon the adds.
- **The enrage timer is a soft damage check.** It should kill a party that
  stalls, not one that plays well. 300 s here.
- Bosses set `ActorFlags.Unmovable` (knockback would be silly) and
  `LeashRadius = 0` — a leashing boss walks home the first time the party kites
  it. `OnValidate` enforces the second one.

---

## A player class

`ClassDefinition` carries stats, a basic attack, up to six abilities, and the
companion-AI hints (`PreferredRange`, `FollowRadius`, `HazardAvoidance`). Add it
to a `PartyRoster`.

`Role` is mechanically load-bearing: `CompanionBrain` branches on it, and
`OnValidate` nudges `ThreatModifier` towards 4 for tanks and 0.5 for healers.

The party size is `DungeonDefinition.PartySize`. Five is tuned; the systems
handle 1–8.

---

## Multiplayer

Not built, but the groundwork is deliberate. What exists:

- **Fixed 20 Hz tick** — the unit you would send over a wire.
- **Seeded, forked RNG** — clients can agree on outcomes.
- **`DungeonWorld` is a plain object, not a singleton** — a server could run
  several.
- **Every party slot already has a `CompanionBrain`**, disabled on the locally
  controlled one. Handing control between human and AI is one flag, which is
  also what happens today when the player's character dies.
- **All input funnels through `LocalPlayerController`** into the same
  `TryCast` / `SetPath` calls the AI uses. There is no privileged path for the
  human.

What is missing: a transport, state serialisation, and a decision about
authority. The likeliest shape is a server-authoritative tick with clients
sending intents, because intents are already the only thing the controller
produces.

---

## Tuning without recompiling

| Knob | Where |
|---|---|
| Tick rate | `SimClock.TicksPerSecond` |
| Tile size / PPU | `IsoGrid` — then re-run **Setup ▸ Configure Project** |
| Global cooldown | `AbilityBook.BaseGlobalCooldown` |
| Armour curve | `CombatSystem.ArmorHalvingConstant` |
| Threat switch margin | `ThreatTable.SwitchMargin` |
| Healing threat share | `CombatSystem.HealThreatFactor` |
| Room difficulty curve | `DungeonDefinition.DifficultyByDepth` |
| Layout loopiness | `DungeonDefinition.ExtraConnectionRatio` (0 = all dead ends) |

For a reproducible layout, set `UseRandomSeed = false` and a `FixedSeed`, or put
a seed in `DungeonRunner.SeedOverride`.
