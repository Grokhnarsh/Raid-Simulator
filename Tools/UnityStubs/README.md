# Unity API stubs

These files are **not part of the game** and are never imported by Unity — they live outside
`Assets/` on purpose.

They declare just enough of the Unity API surface (with matching signatures) for the .NET SDK to
type-check `Assets/_Project/Scripts/**` on a machine that has no Unity installation. That gives the
continuous-integration and agent workflow a real compile of the engine-facing code — catching typos,
missing `using` directives, wrong generic arguments and broken call signatures — instead of waiting
for the editor to report them.

## What this does and does not prove

* **Does prove:** the project's own C# is internally consistent and compiles.
* **Does not prove:** that every Unity call matches the real engine. A stub is only as accurate as
  the signature written here.

The authoritative compile is still Unity's. `RaidSim.Core` needs none of this — it has no engine
dependency at all and is compiled and unit tested directly (see `Tools/CoreBuild/`).

## Adding to the stubs

When engine-facing code starts using a Unity member that is not declared here, add it with the
**exact** signature from the Unity scripting reference. A stub that differs from the real API is
worse than no stub, because it turns a real error into a false pass.
