# CLAUDE.md — Working agreement for this repository

A 2.5D raid simulator built in Unity. The playable slice is deliberately small; the architecture
underneath it is not. Read [`Docs/ARCHITECTURE.md`](Docs/ARCHITECTURE.md) before making a structural
change and [`Docs/ROADMAP.md`](Docs/ROADMAP.md) to find out which phase is current.

**Project language:** code, comments, documentation and commit messages are in English. Only
authored display strings (character names, ability names, flavour text) are content.

---

## 1. The one rule that must never be broken

**Gameplay must never branch on a name.**

```csharp
if (boss.name == "Emberlord Varkuun")   // forbidden
if (character.name == "Aric")           // forbidden
if (raid == "RuinsOfAshenfall")         // forbidden
```

`DisplayName` exists for the combat log, raid frames and tooltips. Nothing else may read it. When
behaviour needs to vary, express the difference as one of:

| Instead of a name check | Use |
| --- | --- |
| "this specific boss" | a `BossDefinition` asset with the phases and abilities it owns |
| "this specific class" | a `ClassDefinition` asset, addressed through `CombatRole` |
| "this specific ability" | an `AbilityDefinition` asset with data-driven effects |
| "this kind of thing" | an interface, a component, an enum, or a tag on the data |
| "when X happens" | an event on the `IEventBus` |

If a change seems to need a name check, the data model is missing a field. Add the field.

---

## 2. Architecture rules

1. **The simulation kernel does not reference Unity.** Everything under
   `Assets/_Project/Scripts/Core/` is plain C#. Its assembly definition sets
   `"noEngineReferences": true`, and `Tools/CoreBuild/` compiles it with the plain .NET SDK, so a
   stray `using UnityEngine;` breaks the build immediately. This is what makes the encounter
   simulator, the unit tests and continuous integration possible without an editor.
2. **No monolithic manager.** `GameBootstrap` is a composition root: it constructs, wires and ticks.
   It holds no gameplay rules. Anything that makes a decision belongs in a system with one
   responsibility.
3. **Systems communicate through events.** A publisher must never need editing to add a listener.
4. **Static data and runtime state are separate.** ScriptableObjects are read-only authored data.
   Never write to one at runtime; see [`Docs/DATA_ARCHITECTURE.md`](Docs/DATA_ARCHITECTURE.md).
5. **No tuning constants in gameplay code.** Numbers live in data assets. The only constants allowed
   in code are system invariants (a probability cannot exceed 1; a multiplier cannot go negative),
   and they belong in a documented rules class such as `StatRules`.
6. **Tick order is explicit.** Systems implement `ISimulationSystem` and declare an order from
   `SystemOrder`. Do not rely on Unity's script execution order.
7. **No `FindObjectsOfType`, no per-entity `Update`.** Entities register with `EntityRegistry`;
   the encounter loop ticks systems. A forty-player raid must not cost forty `Update` callbacks.

---

## 3. Layout

```
Assets/_Project/
  Data/          Authored ScriptableObjects (classes, characters, camera, bootstrap)
  Scenes/        Bootstrap.unity — one object; everything else is built from data at load
  Scripts/
    Core/        RaidSim.Core — the simulation kernel. No UnityEngine, ever.
    Characters/  Data assets and the CombatActor bridge
    CameraRig/   The 2.5D camera
    Combat/      Targeting now; damage, threat and effects as they land
    Game/        Bootstrap, input, player driver, engine interop
    DebugTools/  Development-only tooling
    Editor/      Editor menus and validation
    Abilities/ AI/ Raid/ Boss/ Effects/ UI/ Items/ Loot/ VFX/ Animation/
                 Reserved for their phases — empty until then, on purpose
  Tests/
    EditMode/    Kernel unit tests (plain NUnit; also run headlessly)
    PlayMode/    Integration tests that need the engine
Tools/
  CoreBuild/     Headless compile + test of the kernel
  UnityStubs/    Type-checks engine-facing code without Unity installed
  Unity/         Asset generation and reference verification scripts
Docs/            Design and system documentation
Blender/         Source art files and the export pipeline
```

Two folder names deviate from the brief's suggested layout, deliberately: `CameraRig/` rather than
`Camera/`, and `DebugTools/` rather than `Debug/`. A namespace called `RaidSim.Camera` or
`RaidSim.Debug` shadows `UnityEngine.Camera` and `UnityEngine.Debug` inside its own files, which
turns every use of those engine types into a compile error or a confusing fully-qualified name.

---

## 4. Commands

Everything below runs without Unity installed.

```bash
# Compile the simulation kernel (warnings are errors)
dotnet build Tools/CoreBuild/RaidSim.Core.csproj

# Run the kernel unit tests
dotnet test Tools/CoreBuild/RaidSim.Core.Tests.csproj

# Type-check the engine-facing scripts against the Unity API stubs
dotnet build Tools/UnityStubs/RaidSim.UnityTypeCheck.csproj

# Stamp .meta files onto any asset that lacks one (idempotent)
python3 Tools/Unity/generate_meta.py
python3 Tools/Unity/generate_meta.py --check     # report only, non-zero exit if any are missing

# Verify every asset reference resolves
python3 Tools/Unity/verify_references.py

# Restore any deleted default data asset (never overwrites an existing file)
python3 Tools/Unity/generate_default_data.py
```

Run all four checks before committing. In Unity, additionally run the Test Runner in Edit Mode — it
compiles the same test sources against the real engine.

---

## 5. First time you open the project

1. Open with **Unity 6 LTS (6000.0.x)**. The version is recorded in
   `ProjectSettings/ProjectVersion.txt`; a different 6000.0 patch is fine.
2. Unity will prompt to enable the **Input System** package backend and restart. Accept it. If it
   does not prompt: *Edit → Project Settings → Player → Active Input Handling → Input System
   Package (New)*.
3. Run **Raid Simulator → Repair Data References**. This assigns
   `Assets/_Project/Settings/RaidSimControls.inputactions` to the bootstrap config. It is the one
   reference that cannot be authored outside the editor, because its sub-object id is assigned by
   the Input System's scripted importer at import time.
4. Run **Raid Simulator → Validate Project Data**. It should report no problems.
5. Open `Assets/_Project/Scenes/Bootstrap.unity` and press Play.

Unity may reserialise the hand-written `.meta` files on first import to normalise importer settings.
That is expected and harmless: the GUIDs are preserved, so no reference breaks.

### Controls

| Action | Keyboard / mouse | Gamepad |
| --- | --- | --- |
| Move | `WASD` or arrow keys | left stick |
| Zoom | mouse wheel | right stick Y |
| Select target | left click | south button |
| Cycle target | `Tab` | right shoulder |
| Clear target | `Backspace` | east button |
| Pause | `Escape` | start |

---

## 6. How to work in this repository

The brief asks for strictly incremental work, and the phase plan in
[`Docs/ROADMAP.md`](Docs/ROADMAP.md) is the order to follow. For each step:

1. Read the current phase. Do not start work belonging to a later one.
2. Make the smallest change that completes a coherent step.
3. Compile the kernel, run its tests, type-check the engine-facing code.
4. Fix everything before moving on.
5. Update the documentation that the change affects, in the same commit.

**Write the test with the behaviour, not after it.** The kernel is testable without an engine
specifically so that damage formulas, threat ordering, cooldown timing and target selection are
covered by fast tests. A kernel change with no test is incomplete.

### Adding content vs. adding systems

Adding a class, a character, an enemy, an ability, a boss or a loot table should require **no code
changes at all**. If it does, that is the defect to fix — not the content.

### Merge conflicts in Unity YAML

Scenes, prefabs and `.asset` files are YAML and merge badly by hand. `.gitattributes` marks them for
Unity's SmartMerge; configure it once per machine with
`git config merge.unityyamlmerge.driver "'<UnityPath>/Tools/UnityYAMLMerge' merge -p %O %B %A %A"`.
The bootstrap scene is kept nearly empty precisely so this is rarely needed.
