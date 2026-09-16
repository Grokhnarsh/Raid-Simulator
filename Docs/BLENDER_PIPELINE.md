# BLENDER_PIPELINE.md

How art gets from Blender into the game, and the conventions that make equipment swapping,
animation retargeting and skin variants possible later without rebuilding assets.

---

## 1. Layout

```
Blender/
  Characters/
    Tank/            Tank.blend
    Healer/          Healer.blend
    MeleeDamage/     MeleeDamage.blend
    RangedDamage/    RangedDamage.blend
  Enemies/
    TrashCultist/    TrashCultist.blend
    TrashBrute/      TrashBrute.blend
    TrashCaster/     TrashCaster.blend
  Bosses/
    EmberlordVarkuun/  EmberlordVarkuun.blend, Weapon.blend
  Environment/
    RaidArena/       Modular arena pieces
  Shared/
    Rigs/            Reference rigs and armatures
    References/      Concept art and scale references

Assets/_Project/Art/
  Characters/        Exported .fbx and textures
  Environment/
  Weapons/
```

`.blend` files are the source of truth and are committed. `.blend1`/`.blend2` autosaves are ignored
by `.gitignore`. Exported `.fbx` files are committed too, because Unity needs them and regenerating
them requires Blender on every machine.

Blender source folders are named by **role and function**, not by the demo character names. The five
demo characters are data assets; the art is four class bodies they reference, so a new character
using an existing class needs no new art.

---

## 2. Naming

Consistent names are what let a pipeline be automated later.

```
<Category>_<Subject>_<Part>[_<Variant>]

SK_Tank_Body            skinned mesh, tank body
SK_Tank_Armor_Chest     skinned mesh, chest armour
SM_Tank_Weapon_Sword    static mesh, weapon
SM_Arena_Pillar_A       static mesh, environment
ARM_Humanoid            armature
MAT_Tank_Body           material
TEX_Tank_Body_BaseColor texture
```

`SK_` skinned, `SM_` static, `ARM_` armature, `MAT_` material, `TEX_` texture. Lower-case variants,
no spaces, no umlauts, no punctuation beyond underscores.

---

## 3. Units and orientation

| | Value |
| --- | --- |
| Unit scale | 1 Blender unit = 1 metre |
| Character height | ~1.9 m (see [`ART_STYLE_GUIDE.md`](ART_STYLE_GUIDE.md)) |
| Origin | at the feet, centred on the footprint |
| Forward (Blender) | −Y |
| Up (Blender) | +Z |
| Export forward | −Z (Unity forward) |
| Export up | +Y (Unity up) |

**Apply all transforms before export.** Object scale must be 1,1,1 and rotation 0 in Blender.
Unapplied transforms produce a mesh that looks right in Blender, imports at the wrong scale, and then
fights every attachment point and every collider.

The origin sits at the feet because the simulation treats an entity's position as the centre of its
ground footprint. An origin at the hips makes every spawn point, every range check and every ground
telegraph subtly wrong.

---

## 4. Modularity

Characters are assembled, not sculpted as one mesh:

```
Character (armature root)
  ├── Body           always present
  ├── Armor
  │     ├── Head
  │     ├── Chest
  │     ├── Hands
  │     ├── Legs
  │     └── Feet
  ├── Weapon         attached to a hand socket
  └── Accessories    cloaks, effects attachments
```

This is not speculative. The roadmap has equipment slots, sets and skins, and the difference between
"swap a mesh" and "re-author a character" is exactly whether the model was built in parts.

Rules that make it work:

* Every armour piece is skinned to the **same shared armature**, so any piece fits any body of that
  class.
* Hide the body faces an armour piece covers via a mask, rather than deleting them — the body must
  still work unarmoured.
* Weapons are separate objects parented to a named socket bone (`Socket_Hand_R`, `Socket_Hand_L`,
  `Socket_Back`), never skinned.

---

## 5. Rigging

* One shared humanoid armature (`ARM_Humanoid`) for every player character and humanoid enemy, so a
  single animation set covers all of them and Unity's humanoid retargeting works.
* Bosses and non-humanoid enemies get their own armature and use generic rigs.
* Bone names follow Unity's humanoid expectations so the avatar maps automatically.
* Maximum four bone influences per vertex.
* No scale animation on bones; Unity's humanoid rig does not carry it reliably.
* Socket bones are part of the armature, not empties, so attachments follow animation.

---

## 6. Export

### FBX settings

| Setting | Value |
| --- | --- |
| Path mode | Copy, with textures embedded off |
| Apply scalings | FBX All |
| Forward | −Z Forward |
| Up | Y Up |
| Apply unit | on |
| Apply transform | on |
| Object types | Armature, Mesh |
| Add leaf bones | **off** |
| Bake animation | only for animation exports |
| Mesh smoothing | Face |

"Add leaf bones" on is the most common cause of an extra bone appearing at the end of every chain and
breaking retargeting. Leave it off.

### Rules

* Export the mesh and the skeleton **without animation** as the character file.
* Export animations as separate files against the same skeleton.
* Never export Blender's camera or lights.
* Triangulate on export, not in the source file — a triangulated source is painful to edit.

---

## 7. Unity import

| Setting | Value |
| --- | --- |
| Scale factor | 1 |
| Convert units | on |
| Import cameras / lights | off |
| Mesh compression | off during production |
| Read/Write | off unless a system needs mesh data |
| Rig | Humanoid for characters, Generic for bosses |
| Avatar | Create from this model for the base; Copy from it for animations |
| Material creation | Off — materials are created in Unity |

**Materials are authored in Unity, not imported from Blender.** Blender's material graph does not
translate faithfully, and an imported material is regenerated on every reimport, silently discarding
adjustments.

---

## 8. Making a model into a usable actor

1. Export the FBX into `Assets/_Project/Art/Characters/<Class>/`.
2. Create materials in `Assets/_Project/Materials/` and assign them.
3. Make a prefab in `Assets/_Project/Prefabs/Characters/`:
   * root with a `CharacterController` whose radius matches the class's authored `Radius` and whose
     height matches the model — the controller is also the click-targeting collider, so a mismatch
     makes clicking disagree with range checks;
   * the model as a **child** of the root, so swapping the visual never disturbs the collider,
     the actor component or the attachment points;
   * a `CombatActor` component on the root.
4. Assign the prefab to the class's characters via `CharacterDefinition.ViewPrefab`.
5. Play. The placeholder capsule is replaced with no code change.

Step 4 is the point of the whole pipeline: art arrives by assigning an asset, never by editing code.

---

## 9. First asset scope

In order, matching the roadmap:

1. Tank, Healer, Melee damage, Ranged damage character bodies
2. One trash enemy (proves modularity against a second body type)
3. Boss — Emberlord Varkuun
4. Boss weapon
5. Raid arena environment set

Each is complete only when it is in the game as a prefab assigned to its data asset, not when the
`.blend` is finished.

---

## 10. Checklist before committing art

* [ ] Transforms applied: scale 1, rotation 0
* [ ] Origin at the feet, centred on the footprint
* [ ] Height matches the scale table in the style guide
* [ ] Names follow the convention in section 2
* [ ] Skinned to the shared armature; no more than four influences per vertex
* [ ] No leaf bones in the export
* [ ] Imports into Unity at the right scale, facing +Z
* [ ] Prefab's controller radius matches the class's authored `Radius`
* [ ] Silhouette still reads at the camera's angle and zoom range
