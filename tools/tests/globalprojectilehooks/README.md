# GlobalProjectile: checking the 14 hooks

## Analysis

Input: native or mod projectiles, their local and Global callbacks, optional
returns and mutable parameters. Output: drawing, damage, movement, cutting
and grapple decisions, with the documented combination order.

Verified facts: the signatures are in `refs/dump.cs`; drawing extras is built
into `DrawProjDirect` on mobile; pets have an early exit in `Damage`; complex
collision is selected by a Set; `CutTiles` asks `CanCutTiles` before cutting.

Assumption: content implements the documented return types. The existing
`ModProjectile` API stays compatible. The new Globals use `Ref` for color,
hitbox and grapple type, as in tModLoader.

Edge cases: an undefined return, a veto after a permission, callbacks that
throw, a type without a `ModProjectile` class, late registration, entity
reuse, two entities of the same type, death on the ground, vetoed drawing with
extras and restoring state after a native error.

## Design

| Approach | Time per call | Space | Maintenance |
|---|---|---|---|
| Global wrappers separate from the local ones | Up to two JS entries and two lookups per entity | Duplicated movement and drawing contexts | Order depends on installation; higher risk of repeated callbacks |
| A shared flow with a subscriber list per method | O(S), with S applicable callbacks; cached O(1) lookup | Per-entity, per-method lists, kept in a `WeakMap` | Return and restoration rules live in a single flow |

The implementation uses the second approach. With no Global, the native
filter selects only the local types that implement the method. When a Global
is registered, the local filter is turned off and a global entry takes over
the same flow, also serving types registered through the native API. The
turned-off filter is rejected in C++, without entering JS. The movement and
drawing helpers still use `whileIn` and native flags.

## Running

At the repository root:

```powershell
node tools/tests/globalprojectilehooks/check.mjs
node tools/tests/projectilekill/check.mjs
node tools/tests/modplayerhooks/check.mjs
node tools/tests/globalprojectilehooks/bench.mjs 1bae565
node tools/tests/globalprojectilehooks/bench.mjs
```

The benchmark compares 8 subscribers among 64 Globals, warms up the engine,
measures seven samples of 100,000 calls and checks the number of callbacks.
It measures JavaScript dispatch, without attributing the result to the game's
FPS.

To build with Gradle 8.14.3 and Android Studio's JDK, without fighting over
the output with launcher builds:

```powershell
$env:JAVA_HOME = 'C:\Program Files\Android\Android Studio\jbr'
$env:CMAKE_BUILD_PARALLEL_LEVEL = '1'
gradle -I tools/tests/globalprojectilehooks/isolated.gradle :app:assembleDebug --no-daemon --max-workers=1 '-Dorg.gradle.jvmargs=-Xmx1536m' '-Pkotlin.compiler.execution.strategy=in-process'
```

The APK lands in `build/globalprojectilehooks/builds/app/outputs/apk/debug/`.
The fixture uses UID `c54c31ec-a54d-4ed9-aa3e-265e0341fb48`. Copy only
`manifest.json` and `content/` into that UID's folder in `bunny_packs` and
open a world. The log ends with `globalprojectilehooks FIM`.

The fixture creates and removes its projectiles, NPC and platforms, refuses
positions with existing blocks and disables the test projectiles' drops.
Remove that UID's folder after the run. Do not remove other packages.

## Sources checked

- [GlobalProjectile contracts](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/ModLoader/GlobalProjectile.cs).
- [How ProjectileLoader combines the callbacks](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/ModLoader/ProjectileLoader.cs).
- `refs/dump.cs` and the disassembly of `Damage`, `HandleMovement`, `CutTiles`, `CanCutTiles` and `FireGrapple` from the mobile version.

The results and limits of the measurements are in [RESULTADOS.md](RESULTADOS.md).
