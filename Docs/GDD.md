# GDD.md — Game Design Document

## 1. What this game is

A **raid simulator**: a tactical game about running a group through a coordinated boss encounter,
built to scale from a five-person party to a forty-person raid.

The player controls one character inside the group. The others are run by AI. The skill the game
asks for is not execution speed — it is the set of decisions a raid leader makes:

* Which group do I bring, and which role does each slot fill?
* Where does the group stand, and when does it move?
* Which cooldowns are spent, and when?
* What is focused, interrupted, dispelled, soaked or avoided?
* Which strategy actually works against this encounter?

The long-term fantasy is **raid leader and tactician**, not action hero. Direct control exists
because being inside the fight teaches it better than watching from above, not because the game is
about reflexes.

### What it is not

Not an action RPG. Not a twitch game. Not an open world. Not a game whose difficulty comes from
input precision.

---

## 2. The loop

```
Choose a group ─▶ Enter a raid zone ─▶ Clear trash ─▶ Fight a boss
      ▲                                                     │
      └──────────── Learn, adjust, retry ◀── Loot / wipe ────┘
```

Each attempt teaches something specific: a mechanic's timing, a positioning requirement, a cooldown
that must be saved. The retry is the game. Loot and progression exist to widen the option space for
the next encounter, not to remove the need to learn one.

---

## 3. Group and roles

The first slice uses one party of five:

| Slot | Role | Job in a fight |
| --- | --- | --- |
| 1 | Tank | Hold enemies, keep threat, soak the damage aimed at the group |
| 2 | Healer | Keep people alive, triage, manage a pool that does not refill |
| 3 | Melee damage | Damage from inside melee range, where most mechanics land |
| 4 | Ranged damage | Damage from distance, with the mobility to keep it |
| 5 | Ranged damage | As above |

**Nothing in the code assumes this composition.** Group size, the number of tanks and healers, and
which classes fill which slots are data. Two tanks, three healers, a twenty-person raid, or a
composition with no healer at all are configuration, not new code.

Roles are separate from classes. A class may later offer several roles through specialisations; all
group-composition logic reasons about `CombatRole`, never about a class.

---

## 4. Combat model

A fight is a conversation between four pressures:

1. **Damage output** — is the boss dying before its enrage?
2. **Survival** — is incoming damage inside what healing and mitigation can cover?
3. **Threat** — is the tank holding, or is the boss free to kill someone squishier?
4. **Mechanics** — is the group doing the thing the encounter demands right now?

A group that fails any one of these loses, which is what stops the game becoming a damage race.

### Damage

Phase 2 starts with an intentionally simple formula, structured so it can grow:

```
Damage = BaseDamage × AbilityMultiplier × CritMultiplier × Mitigation
```

Three damage types: **physical** (reduced by armour), **magic** (reduced by resistance) and **true**
(unreduced, used by mechanics that must not be mitigated away).

### Resources

Resource kind is authored per class, because they behave in opposite directions: mana starts full and
regenerates, rage starts empty and is built in combat, energy is a small fast-refilling pool. They
are the same pool with a different fill policy — a healer running dry and a tank starting cold are
the same mechanism.

### Threat

Every relevant action generates threat: damage, healing, some support abilities, and taunts. Each
enemy owns a threat table and attacks whoever is highest. Taunt is a time-limited override, not a
permanent one, so tanking is an ongoing job rather than a single button.

Threat is what makes the tank's role real: it turns "deal damage" into "deal damage without pulling
aggro", which is a decision rather than a reflex.

---

## 5. Positioning

Positioning is a primary mechanic, not a movement tax. The systems that make it matter:

* melee range, ranged range, and minimum range dead zones;
* facing — cones and cleaves hit what the boss is looking at;
* line of sight — pillars and geometry can block;
* area-of-effect shapes: circle, line, cone, ring;
* safe zones and danger zones;
* stack, spread, and soak mechanics that require the group to be somewhere specific.

Because the world is genuinely 3D under a fixed camera, height differences, bridges, stairs and
platforms stay available for later encounters.

---

## 6. The first vertical slice

### Raid: Ruins of Ashenfall

```
Entrance ─▶ Trash pack ─▶ Trash pack ─▶ Boss arena ─▶ Emberlord Varkuun
```

Trash: **Ashen Cultist**, **Ashen Brute**, **Flamecaller**. Each teaches something the boss will
demand — an interrupt, a tank swap, an add priority — so trash is a tutorial rather than a delay.

### Boss: Emberlord Varkuun

| Phase | Adds to the fight | What the group must learn |
| --- | --- | --- |
| 1 | Heavy Strike, Flame Breath, basic adds | tanking, facing the cone away, add priority |
| 2 | Burning Ground, more adds, a movement mechanic | fighting while the safe floor shrinks |
| 3 | Enrage: rising damage on a timer | a damage check, and cooldowns spent to survive it |

Abilities: Heavy Strike, Flame Breath, Inferno, Burning Ground, Summon Ashen Minions, Enrage.

**None of this is in code.** The boss is a `BossDefinition` asset holding phases; each phase holds
abilities and mechanics. A second boss is a second asset.

### Demo roster

Aric (tank), Lyra (healer), Kael (melee), Elena (ranged), Torin (ranged). These are display names on
data assets. No gameplay code may read them.

---

## 7. Progression

Deliberately shallow at first, with room reserved:

* **Now:** gold and three items from the first boss, defined by a loot table asset.
* **Later:** rarity, equipment slots, sets, random stats, unique items, loot rules; then levels,
  talents, builds, and a campaign across several raids.

Progression must widen the tactical option space. A reward that only raises a number, without
changing a decision, is a weaker reward.

---

## 8. Encounter simulation

The simulator is a first-class feature, not a debug tool. "Run Encounter" fights the whole encounter
with every group member under AI control and reports:

damage done · healing done · damage taken · deaths · interrupts · mechanics avoided · mechanics
failed · encounter duration · threat over time · ability usage.

This is what lets a player reason about a composition or a cooldown plan instead of guessing, and it
is why the simulation kernel is engine-independent — see [`ARCHITECTURE.md`](ARCHITECTURE.md).

---

## 9. Presentation

2.5D: a real 3D world under a fixed, slightly isometric camera. Stylised-realistic, colourful,
readable. **Gameplay legibility outranks visual richness** — at a glance the player must be able to
identify their character, the group, the boss, the adds, every area-of-effect, what is being cast,
and who has which buff or debuff. See [`ART_STYLE_GUIDE.md`](ART_STYLE_GUIDE.md).

---

## 10. Scope discipline

The first milestone is a small, complete, playable five-person raid — not an unfinished MMO.
Priority order when they conflict:

1. working gameplay
2. clean architecture
3. extensibility
4. testability
5. performance
6. visual quality

Complex systems are built when the content needs them, never in advance. What is built early is the
*shape* that lets them arrive without a rewrite.
