# ART_STYLE_GUIDE.md

---

## 1. The target

**Detailed, colourful, readable, stylised-realistic.** Proportions and materials are believable;
shapes, colours and silhouettes are pushed for clarity. Closer to a stylised fantasy game than to
photorealism or to flat cartoon.

## 2. The rule everything else bends to

**Gameplay legibility outranks visual richness.**

A raid encounter is won by reading the floor and the group. At any moment, at normal zoom, in a
crowded fight, the player must be able to identify without hesitation:

* their own character;
* each group member and their role;
* the boss and each add;
* every area-of-effect, and whether they are standing in it;
* what is being cast, and by whom;
* who carries which buff or debuff.

When a visual effect competes with any of these, the effect loses. This is the test to apply, not a
sentiment: *if a screenshot mid-mechanic does not answer those questions, the art is wrong,
regardless of how good it looks.*

---

## 3. The camera constrains the art

The camera sits at a fixed pitch of about 50° and a yaw of 45°, orthographic, at 12–38 metres. That
has direct consequences:

* **Silhouettes read from above and behind.** Detail on the front of a chest piece is nearly
  invisible. Shoulders, helmets, weapons and overall outline carry identity.
* **The top surface of everything is visible.** Ground materials, floor decals and the tops of props
  get seen constantly and deserve the attention usually spent on facades.
* **Vertical geometry occludes.** Tall pillars and arches hide the fight. Keep arena geometry low,
  or make it fade near the camera.
* **Orthographic projection keeps ground shapes constant.** A telegraph circle is the same size
  wherever it is on screen — which is exactly why the projection was chosen. See
  [`ARCHITECTURE.md`](ARCHITECTURE.md), section 9.

---

## 4. Colour

Colour is a gameplay channel first and a mood channel second.

| Meaning | Treatment |
| --- | --- |
| Your character | brightest accent in the scene, plus a ground ring only you have |
| Group members | the accent colour authored on their `CharacterDefinition`, matching their raid frame |
| Enemies | a distinct hostile range, desaturated relative to the group |
| Boss | the most distinct silhouette and palette on screen |
| Danger | warm, saturated, high contrast against the floor — never a floor colour |
| Safe | cool, calm, clearly the inverse of danger |
| Beneficial effects | cool and soft |
| Harmful effects | warm and sharp |

**Arena floors stay desaturated and mid-value.** The floor is the canvas telegraphs are drawn on; a
vivid floor destroys the only channel that matters most.

**Do not encode meaning in hue alone.** Roughly one in twelve players has a colour vision deficiency.
Danger and safe must differ in shape, animation and brightness as well as hue: danger pulses inward
with a hard edge, safe holds steady with a soft one.

---

## 5. Character design

### Read by role, then by class

Role is the first thing a player must see, because role determines what a character is supposed to be
doing. Give each role a distinct silhouette family:

| Role | Silhouette |
| --- | --- |
| Tank | broadest; heavy shoulders, shield, wide stance |
| Healer | tallest and narrowest; robes, a staff, a clean vertical line |
| Melee damage | compact and angular; paired weapons, forward-leaning |
| Ranged damage | medium; a clear held weapon or focus, visible ranged intent |

Class identity sits inside the role's family — recognisable, but never at the cost of role clarity.

### Scale

| | Height |
| --- | --- |
| Player characters | ~1.9 m |
| Trash enemies | 1.8–2.4 m |
| Boss | 3.5–4.5 m |

The boss must be unmistakable at a glance without filling the arena. Its footprint radius is authored
data, and range checks measure between footprints — so a physically larger boss is genuinely
reachable from further out, and the art and the rules agree.

### Modularity

Characters are built as `Body → Armor → Weapon → Accessories`, so equipment and skins swap later
without rebuilding a character. See [`BLENDER_PIPELINE.md`](BLENDER_PIPELINE.md).

---

## 6. Telegraphs

Telegraphs are the most important art in the game, because they are how the encounter speaks.

**Gameplay and visuals are separate systems.** The shape that decides who is hit is simulation data;
the thing on screen renders it. They must agree exactly — a telegraph that lies is worse than no
telegraph.

Rules:

* **Shape says what kind.** Circle, line, cone, ring, and a safe zone that is visibly the inverse.
* **Fill says when.** A telegraph fills over its cast time; resolution is when the fill completes.
  The player reads timing from fill, not from a number.
* **The edge is hard.** A soft gradient makes "am I inside it?" a guess. The boundary is exactly where
  the damage boundary is.
* **Overlap stays readable.** Three overlapping circles must still read as three. Use outlines that
  survive stacking rather than additive fills that saturate to white.
* **Nothing else looks like a telegraph.** No decorative ground effect may use telegraph language.
  This is a hard reservation on the visual vocabulary.

---

## 7. Effects

Restraint is the rule. A raid fight has five characters, several adds, a boss, and a dozen abilities
firing at once; effects that are individually impressive become collectively unreadable.

* Effects read as a **direction and an impact**, not as a particle cloud.
* Nothing opaque covers a character, the floor beneath them, or a telegraph.
* Persistent auras are small and attached; burst effects are brief.
* Screen-space effects (shake, flash, vignette) are reserved for moments that must not be missed.
* An effect that obscures the floor for more than a moment is a bug.

---

## 8. Environment

The arena is a stage. It sets the scene and then gets out of the way.

* Wide open fighting space with clear, unambiguous boundaries.
* Floor material desaturated and low-contrast so telegraphs dominate.
* Detail concentrated at the edges, where nobody fights.
* Anything that blocks line of sight is deliberate and obviously so — if a pillar blocks, it must
  look like it blocks.
* Lighting is even across the fighting area. Dramatic pools of light and shadow hide mechanics.

---

## 9. Technical budgets

First-pass targets for a Windows PC build; revisit when there is real content to measure.

| | Triangles | Texture | Materials |
| --- | --- | --- | --- |
| Player character | 8–15k | 2048² | 1–2 |
| Trash enemy | 5–10k | 1024² | 1 |
| Boss | 20–40k | 2048–4096² | 2–3 |
| Weapon | 1–3k | 512–1024² | 1 |
| Environment prop | 0.5–5k | 1024² | 1 |

Textures use a metallic-roughness set: base colour, normal, and a packed metallic / roughness /
ambient-occlusion map. One material per logical part so equipment swaps stay cheap.

The project starts on the **Built-in Render Pipeline** — no hand-authored pipeline assets to get
wrong, and with zero art committed a move to URP currently costs nothing. That decision is scheduled
for Phase 7, before art volume exists.

---

## 10. Placeholders

Until art exists, `GameBootstrap` generates a floor plane, a directional key light and capsule bodies
tinted with each character's authored accent colour.

This is intentional and has a rule attached: **placeholders must be visually correct in the ways that
matter**. Correct scale, correct footprint radius, correct accent colour. A placeholder at the wrong
size teaches the wrong thing about range and positioning, and that lesson has to be unlearned when
the real model arrives.
