# TESTING.md

---

## 1. Why the kernel is engine-free

The simulation kernel has no Unity dependency, so its tests run as plain NUnit in about a tenth of a
second with no editor. That is the difference between tests that get run on every change and tests
that get run before a release.

**The same test sources run in both places.** `Assets/_Project/Tests/EditMode/` is compiled by
Unity's Test Runner through `RaidSim.Tests.EditMode.asmdef`, and by
`Tools/CoreBuild/RaidSim.Core.Tests.csproj` with the plain .NET SDK. Kernel tests use plain NUnit
attributes and no `UnityEngine` types, so there is one set of tests, not two.

---

## 2. Running them

```bash
# Kernel unit tests, headless
dotnet test Tools/CoreBuild/RaidSim.Core.Tests.csproj

# Kernel compile on its own (warnings are errors)
dotnet build Tools/CoreBuild/RaidSim.Core.csproj

# Type-check engine-facing scripts against the Unity API stubs
dotnet build Tools/UnityStubs/RaidSim.UnityTypeCheck.csproj

# Asset integrity
python3 Tools/Unity/generate_meta.py --check
python3 Tools/Unity/verify_references.py
```

In Unity: *Window → General → Test Runner*, Edit Mode for the kernel tests, Play Mode for integration
tests.

---

## 3. Current coverage

**139 tests, all passing.**

| Area | Tests | What is asserted |
| --- | --- | --- |
| `EventBus` | 14 | ordering, type isolation, unsubscribe, dispose idempotence, subscribe and unsubscribe *during* dispatch, fault isolation, nested publish |
| `StatBlock` | 18 | flat / additive / multiplicative order, source-tagged removal, clamping, change notification, caching |
| `Health` | 10 | overkill reporting, no healing the dead, max-health changes not reading as heals, no NaN from an unsized pool |
| `ResourcePool` | 9 | all-or-nothing spending, regeneration, mana-versus-rage encounter start |
| `TargetFilter` | 8 | allegiance, footprint-relative range, minimum range, height separated from distance |
| `TargetQuery` | 11 | nearest, cone, distance-ordered cycling with wrap, deterministic ties, lowest-health scoring |
| `TargetSelection` | 7 | publishes only real changes, auto-clears on despawn, keeps a corpse but flags it |
| `SimulationClock` | 13 | delta clamping, pause, time scale, float-drift tolerance in `HasReached` |
| `GameStateMachine` | 10 | legal and illegal transitions, clock policy, pause toggle |
| `KinematicLocomotor` | 17 | speed, normalisation, no overshoot, stopping distance, turn rate, facing independent of travel |
| `EntityRegistry` | 12 | registration, idempotence, faction buckets, hostile and ally queries |
| `SimulationContext` | 10 | tick order independent of registration order, pause, fault isolation, reset |

---

## 4. What a good test looks like here

Tests state a **rule of the game**, not the shape of an implementation. The name says the rule and
the assertion message says why it matters:

```csharp
[Test]
public void MultiplicativePercentModifiers_Compound()
{
    var block = new StatBlock(new StatSet().With(StatType.DamageTakenModifier, 1f));

    block.AddModifier(StatModifier.PercentMultiplicative(StatType.DamageTakenModifier, -0.50f));
    block.AddModifier(StatModifier.PercentMultiplicative(StatType.DamageTakenModifier, -0.50f));

    // Two 50% reductions leave 25% damage taken, not 0%. This is why damage-reduction
    // cooldowns must be multiplicative.
    Assert.That(block.Get(StatType.DamageTakenModifier), Is.EqualTo(0.25f).Within(0.001f));
}
```

A reader who has never seen the class learns a balance rule from it. A test that only restated the
formula would break on every refactor and teach nothing.

### Conventions

* One behaviour per test. The name is `Subject_DoesThing_WhenCondition`.
* Compare floats with `Within`. Use the tolerance that matches the domain, not the smallest one that
  passes.
* Assert a consequence, not an internal field.
* Add the assertion message when the *reason* is not obvious from the name.
* `TestEntity` is the shared stub. It implements `ICombatEntity` and nothing more — if a kernel
  system ever needs more than the published interfaces, `TestEntity` stops compiling and the leak is
  caught immediately.

---

## 5. Planned coverage by phase

| Phase | Unit tests | Integration tests |
| --- | --- | --- |
| 2 Combat | mitigation per damage type, criticals, shield absorption, overkill, healing and overhealing, death | killing a practice target end to end |
| 3 Abilities | cost validation, range validation, cast completion, cooldown expiry, global and shared cooldowns, charges, cooldown reduction | cast interrupted by movement; ability resolving on a live target |
| 4 Party | roster construction at sizes 5, 10, 20, 40; role queries | party spawns and frames populate from data |
| 5 AI | every target rule; healer priority; melee reach against a large target; ranged minimum range; interrupt only on interruptible casts; danger-zone avoidance wins | AI group clears a practice encounter unaided |
| 6 Threat | threat ordering, threat multipliers, taunt override and expiry, threat dropped on death | tank holds against the group's damage |
| 7 Raid | zone progression, pull triggers, encounter reset | walk entrance to boss arena |
| 8 Boss | phase transition conditions, mechanic resolution and failure, telegraph timing | full encounter: kill, wipe, phase transition, interrupt |
| 9 UI | combat-log formatting from events | frames update from live events |
| 10 Loot | loot generation, table weighting, determinism under a seed | loot appears on boss death |
| 11 Simulation | statistics aggregation | a headless encounter completes and reports stable statistics |

Integration tests that need the engine live in `Tests/PlayMode/`. The rule for choosing: if the
behaviour can be expressed without the engine, it belongs in the kernel and gets a unit test. If
verifying it requires a collider, a frame or a renderer, it is a play-mode test.

---

## 6. Before every commit

1. `dotnet build Tools/CoreBuild/RaidSim.Core.csproj` — clean.
2. `dotnet test Tools/CoreBuild/RaidSim.Core.Tests.csproj` — all green.
3. `dotnet build Tools/UnityStubs/RaidSim.UnityTypeCheck.csproj` — clean.
4. `python3 Tools/Unity/verify_references.py` — all references resolve.
5. In Unity, if it is open: Edit Mode tests green, no console errors on entering play mode.

Warnings are errors in all three build targets. A warning that is genuinely acceptable is suppressed
explicitly, with a comment explaining why — see the `CS0649` suppression in the type-check project,
which exists because `[SerializeField]` fields are assigned by Unity's serializer and no compiler
outside Unity can see that.

---

## 7. What the headless checks do not prove

The Unity API stubs in `Tools/UnityStubs/` are hand-written. They prove the project's own C# is
internally consistent and compiles; they do not prove that every Unity call matches the real engine,
because a stub is only as accurate as the signature written into it.

The stubs have already caught two real defects — a missing `using` for extension methods, and an
incorrect assumption about `UnityEngine.SerializableAttribute` — so they earn their place. But
**Unity's compile and the Play Mode test run remain authoritative**, and no phase is complete until
the project has been opened and played.
