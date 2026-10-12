# Shader research in Bunny Loader

An internal engineering document to guide the investigation and record new
evidence. It does not describe a shader API already available to mods.

**Updated:** 2026-10-03. **Status:** static analysis; no custom shader was
loaded or validated at runtime in this research.

## Goal and current conclusion

Investigate support for effects on sprites, dyes and screen filters, with a
modding experience similar to tModLoader's. Separate three capabilities:

1. Reusing shaders that already ship with the game.
2. Loading custom shaders and integrating them into Terraria's drawing.
3. Porting tModLoader effects, including their parameters and assets.

The mobile implementation contains a concrete bridge between Terraria's
effects and Unity materials. Reusing that bridge is the best-grounded path.
Loading a new shader is still the main unproven point. There is no evidence of
direct compatibility with tModLoader's `.fx` or `.fxc` files, nor of a public
custom shader loader in Bunny.

## How to interpret the evidence

| Classification | Meaning |
|---|---|
| Confirmed in Bunny's code | Behavior observed in the loader's files. |
| Confirmed in the dump | Class, field or signature present; does not prove the method body. |
| Confirmed in the binary | Flow or instruction verified in the ARM64 library, without running the game. |
| External reference | Behavior documented by Unity or tModLoader; does not guarantee availability in this build. |
| Hypothesis | A possible path that still needs investigation or an experiment. |
| Validated at runtime | Reserved for a practical proof with logs and a visual result. None at this stage. |

A name present in the metadata, a string in the binary or a call that does not
throw is not enough to classify a shader as functional.

## Reference build

The [refs README](../../refs/README.md) identifies the current dump as
Terraria **1.4.5.8.6**. The header of
`terraria1456_assets/src/main/assets/bin/Data/data.unity3d` identifies Unity
as **2021.3.56f2**. The older hosting documentation covers 1.4.5.6.4; do not
use its addresses as a reference for this research.

Repository state consulted: HEAD `d7d47e4`, with pending local changes. The
commit identifies the context, not a clean working tree.

The pairs below had identical SHA-256 between the research copy and the copy
integrated into the build:

| File | Research copy | Integrated copy |
|---|---|---|
| `libil2cpp.so` | `refs/libil2cpp.so` | `app/src/main/jniLibs/arm64-v8a/libil2cpp.so` |
| `libunity.so` | `refs/libunity.so` | `app/src/main/jniLibs/arm64-v8a/libunity.so` |
| `global-metadata.dat` | `refs/global-metadata.dat` | `terraria1456_assets/src/main/assets/bin/Data/Managed/Metadata/global-metadata.dat` |

Recorded SHA-256:

- `libil2cpp.so`: `f33b48c09e96d17234af40c7d4ce16da52d8fb72a4ead729bfad8dc88a081814`
- `libunity.so`: `8a13b23554371fe0c9c6f55a7f013c1772c99b75cf1bfa8e3c5a6379801f59cf`
- `global-metadata.dat`: `4c904c49d168913d28ef547c4f461e539d926d2ed27c0253ed53a2954e9585f7`

The binaries, assets, `refs/dump.cs` and `refs/script.json` are local
references ignored by Git. Generate the dump from your own files as described
in the refs README. Revalidate hashes and addresses after any game update.

## Findings about the game's drawing

### The XNA layer uses Unity objects

The [texture loader](../../app/src/main/cpp/script/api/Texture.cpp) documents
and uses a `UnityEngine.Texture2D` inside the game's texture. The
`Microsoft.Xna.Framework.Graphics` name does not imply an implementation
identical to PC Terraria's.

The dump has `Effect`, `EffectPass`, `SpriteBatch`, `RenderTarget2D`,
`GameShaders`, `ArmorShaderData`, `MiscShaderData`, `ScreenShaderData`,
`Filter` and `FilterManager`. `EffectPass`'s constructor explicitly receives a
`UnityEngine.Material`.

### Each shader is looked up in Resources

**Confirmed in the binary:** `PixelShader.LoadPass` and `ScreenShader.LoadPass`
concatenate the requested name with a path, load a `Shader` with
`Resources.Load<Shader>`, create a material and build an `EffectPass`.

| Family | Path looked up | Fallback when missing |
|---|---|---|
| Pixel/sprite | `EffectShaders/PixelShader/<name>` | `EffectShaders/PixelShader/Default` |
| Screen | `EffectShaders/ScreenShader/<name>` | `EffectShaders/ScreenShader/Default` |

In that case the game also logs warnings, found among the local strings:
`Using Default as temporary replacement for new pixel shader` and
`Using Default as temporary replacement for new screen shader`.

**Consequence:** registering a pass with a new name may keep drawing without
running a new shader. The practical proof needs to check the shader actually
attached to the material and produce an unmistakable visual change.

The current flow provides an integration point for a custom material, but
does not prove an external asset can be loaded. A loose file in the mod's
folder does not automatically become part of the `Resources` catalog.

### There are native passes that are candidates for the first proof

`ScreenShader.get_Effect` calls `LoadPass` for names like `FilterColor`,
`FilterHeatDistortion`, `FilterWaterDistortion`, `FilterInvert`,
`FilterNoir`, `FilterSepia`, `FilterCRT` and `FilterGraveyard`.

Registering those names was confirmed in the binary. Whether each asset
exists and what each effect produces in this build remain pending; the default
fallback prevents concluding that from the list of passes alone.

### Parameters are forwarded by specific classes

**Confirmed in the binary:** `Effect.ApplyValuesToMaterial` contains only a
return instruction. `PixelShader` and `ScreenShader` override that method; the
screen implementation calls `Material.SetColor`, `SetFloat` and `SetVector`.

`ScreenShader`'s initializer registers property IDs like `_Color`,
`_Opacity`, `_SecondaryColor`, `_ShaderTime`, `_ScreenResolution`,
`_ScreenPosition`, `_TargetPosition`, `_Intensity`, `_Progress`,
`_Direction`, `_ImageOffset`, `_ImageSize1`, `_ImageSize2`, `_ImageSize3`,
`_VPPI` and `_Zoom`.

Do not assume that adding an entry to `Effect.Parameters` forwards an
arbitrary parameter to the shader. The integration has to respect the
properties the class in use expects, or provide its own forwarding.

The game also has `MaterialBuffer`, partitions and texture binding. The full
lifecycle of those materials, and whether changes to the base material reach
every instance used in drawing, still need to be checked. Do not assume that
changing an isolated material modifies every batch.

### Bunny already has a hook point for scene effects

[SceneEffectLoader](../../app/src/main/cpp/script/js/mod/Loaders/SceneEffectLoader.js)
calls `SpecialVisuals(player, active)` for both active and inactive effects.
That allows investigating activation and deactivation by biome/event.

In the dump, `FilterManager` has `BeginCapture` and `EndCapture`, receiving a
`RenderTarget2D`. That signature points to an existing structure for screen
filters; it does not prove it is available in every graphics setting, nor
which image layers, such as the UI, go into the capture.

## Loading custom shaders

### AssetBundles: partial evidence

In `libunity.so` there are references to `AssetBundleManager`,
`AssetBundleCreateRequest`, `AssetBundleRequest`, loading/unloading, CRC and
bundle incompatibility. `ScriptingAssemblies.json` also lists
`UnityEngine.AssetBundleModule.dll` and
`UnityEngine.UnityWebRequestAssetBundleModule.dll`.

However, the `AssetBundle` class was not found in the dump, nor the exact
strings for the `UnityEngine.AssetBundle::LoadFromFile_Internal`,
`LoadFromMemory_Internal` and `LoadAsset_Internal` calls in the two binaries
consulted.

That is consistent with binding stripping, but does not prove it. Nor does it
prove the native functions are entirely absent: code can remain without the
names searched for. The remaining references do not guarantee an accessible,
working loader.

**Open question:** find a loading path usable by Bunny, check its real
signature and its link to the IL2CPP objects. Do not assume
`il2cpp_resolve_icall` will resolve a call just because it appears in Unity's
documentation.

Unity 2021.3's official code documents the AssetBundle bindings, but it
serves as an investigation reference, not as proof that those bindings were
kept in the game. See the external sources at the end.

### Format and compilation

In tModLoader, `FxcReader` reads a compiled effect and calls `Effect`'s
constructor with `GraphicsDevice` and the bytes. That constructor does not
appear in the `Effect` class of the mobile dump analyzed.

A Unity route would require shader assets compiled for Android, with
compatible version, variants and graphics APIs. Initially using the same Unity
2021.3.56f2 removes one variable from the investigation, without guaranteeing
compatibility. Do not build the bundle with a newer version assuming automatic
compatibility.

`Shader.Find` looks up an existing shader; it does not compile a `.fx`'s text.
The dump marks the `Material` constructor from shader text as obsolete and
unsupported. Loading PNG through `bl.loadTexture` does not offer an equivalent
path for loading shaders either.

An effect's math can be ported, but vertex inputs, textures, properties,
transparency, sprite coordinates and the Unity shader's structure will still
need adapting. Do not treat `.fx`, `.fxc` and a Unity shader asset as
interchangeable formats.

## Approaches and provisional choice

| Approach | Benefits | Limitations and maintenance |
|---|---|---|
| Expose existing shaders | Leverages the drawing and resources already loaded; smaller integration surface. | Limits mods to the existing programs; still requires checking availability, parameter isolation and deactivation. |
| Load Unity assets and attach materials to effects | Allows custom programs using the game's graphics structure. | Loader not proven yet; requires compatibility of assets, parameters, batches and unloading. |
| Custom native rendering | An alternative if asset loading proves unworkable. | Greater dependence on OpenGL ES/Vulkan, contexts, synchronization and preserving graphics state; entry into the pipeline not verified yet. |

Provisional priority: prove reuse of an existing effect, investigate asset
loading and only then define an API for custom shaders. Do not start by
intercepting the graphics API directly without demonstrating the need.

GPU cost depends on the area drawn, the number of passes and texture samples,
not just on the loading approach. Intermediate targets for screen filters use
memory proportional to resolution, format and count. No time, memory or FPS
cost was measured in this research.

## Investigation plan and completion criteria

Every step below is **pending**; none are test results.

| Step | Question | Evidence needed to conclude |
|---|---|---|
| P1 — native effect | Can a mod apply and remove an existing shader? | The shader/material's real name, a visual result compared to the control and no replacement by Default. |
| P2 — loader | Is there a usable path to load an external Shader asset? | Signature and thread verified; asset loaded as a valid object; controlled failure for a missing/incompatible file. |
| P3 — custom shader | Does a program that does not ship with the game take part in drawing? | A shader with a unique name, an identified external origin and a visual effect Default does not produce. |
| P4 — parameters and sprites | Do the mod's parameters and textures reach the right material? | Predictable change of color/intensity/time, two instances with no state leaking, correct animation and transparency. |
| P5 — screen filter | Does the existing capture accept the custom effect? | Affected layers recorded, correct activation/deactivation, resolution and zoom handled. |
| P6 — coexistence and lifecycle | Do several mods and restarts preserve state? | Deterministic filter order, removal of custom resources, restart and pause/resume with no leftover effects. |
| P7 — cost and compatibility | Does the effect work on the chosen devices? | Real graphics backend recorded; CPU/GPU and memory compared to the control on a physical device, besides the emulator. |

In the future test, record in particular:

- Two mods using the same shader with different parameters; registration names
  must avoid collisions and materials need an ownership rule.
- Filter order: applying A then B can give a different result than B then A.
  Do not confuse this order with the priority of music/backgrounds.
- Deactivating a mod/biome, switching worlds, returning to the menu and
  restarting the process.
- Missing asset, unsupported variant, invalid texture index and lost
  references to Unity/IL2CPP objects.
- Atlas, animated frames, cropping, rotation, flip, zoom and transparency.
- Settings that disable filters, capture or render targets.
- Separation between the game thread and the render thread: the existing
  mechanism for creating textures on the game thread does not authorize
  arbitrary GL commands on that same thread. See [threads](threads-e-motor-js.md).

Before setting a performance budget, choose the device, backend, resolution
and control scene. Do not estimate FPS from the shader's name.

## Local references to continue

RVA addresses for the build identified above; look them up in `refs/dump.cs`
and resolve calls and strings with `refs/script.json`. Do not copy these
addresses to another version without checking again.

| Symbol | RVA | Result of this research |
|---|---|---|
| `Effect.ApplyValuesToMaterial` | `0xEBB0F0` | Only a return in the native code. |
| `EffectPass..ctor(Effect, int, string, Material)` | `0xEBB884` | Receives a Unity material. |
| `PixelShader.LoadPass` | `0xEBD404` | Resources → Shader → Material → EffectPass; Default fallback. |
| `ScreenShader.get_Effect` | `0xEBFA24` | Registers the candidate screen passes. |
| `ScreenShader.LoadPass` | `0xEBFF44` | Same loading mechanism, in the screen family. |
| `ScreenShader.ApplyValuesToMaterial` | `0xEC01AC` | Sends values through SetColor/SetFloat/SetVector. |
| `ScreenShader..cctor` | `0xEC052C` | Registers the Unity property IDs. |
| `MaterialBuffer.GetMaterial` | `0xEC1B70` | Material selection and instance control; full cycle pending. |
| `MaterialBuffer.MaterialPartition.GetMaterial` | `0xEE391C` | Unity texture binding; investigate isolation. |

To reproduce the initial inspection, with tools already available in the
environment:

```powershell
Get-FileHash refs/libil2cpp.so, app/src/main/jniLibs/arm64-v8a/libil2cpp.so -Algorithm SHA256
Get-FileHash refs/libunity.so, app/src/main/jniLibs/arm64-v8a/libunity.so -Algorithm SHA256
Get-FileHash refs/global-metadata.dat, terraria1456_assets/src/main/assets/bin/Data/Managed/Metadata/global-metadata.dat -Algorithm SHA256
rg -n 'class EffectPass|class ScreenShader|class FilterManager|class AssetBundle' refs/dump.cs
```

The NDK's `llvm-objdump` lets you check `ScreenShader.LoadPass`'s body:

```powershell
llvm-objdump --disassemble --no-show-raw-insn --start-address=0xEBFF44 --stop-address=0xEC01AC refs/libil2cpp.so
```

That command assumes `llvm-objdump` is on the PATH. The calls need to be
correlated with `ScriptMethod` in `refs/script.json`; the generic labels the
disassembler prints are not reliable names for the game's methods.

## External sources

Consulted on 2026-10-03. Prefer documentation for the version the game uses.

- [tModLoader — FxcReader, branch 1.4.4](https://github.com/tModLoader/tModLoader/blob/1.4.4/patches/tModLoader/Terraria/ModLoader/Assets/Readers/FxcReader.cs): loading the compiled effect; a more specific current reference than old XNB examples.
- [tModLoader — Expert Shader Guide](https://github.com/tModLoader/tModLoader/wiki/Expert-Shader-Guide): parameters, passes, sprite and screen shaders. The compilation section has XNA/XNB examples; do not assume every instruction applies to the current FXC flow.
- [tModLoader — ScreenShaderData](https://docs.tmodloader.net/docs/stable/class_screen_shader_data.html): a reference for a possible API experience, not a mobile implementation.
- [Unity 2021.3 — AssetBundle bindings](https://github.com/Unity-Technologies/UnityCsReference/blob/2021.3/Modules/AssetBundle/Managed/AssetBundle.bindings.cs): declarations of the loading calls; availability in the game pending.
- [Unity 2021.3 — AssetBundles](https://docs.unity3d.com/2021.3/Documentation/Manual/AssetBundlesIntro.html): platform-specific assets and runtime loading.
- [Unity 2021.3 — Building AssetBundles](https://docs.unity3d.com/2021.3/Documentation/Manual/AssetBundles-Building.html): build target, compression and manifests.
- [Unity 2021.3 — AssetBundle troubleshooting](https://docs.unity3d.com/2021.3/Documentation/Manual/AssetBundles-Troubleshooting.html): shader variant selection when building bundles.
- [Unity — UUM-12895](https://issuetracker-mig.prd.it.unity3d.com/issues/assetbundles-dont-rebuild-in-older-versions-when-they-were-built-with-a-newer-version-of-unity): compatibility limits for bundles built with newer versions, including the Unity 2021.3 context.
- [Unity 2021.3 — Shader.Find](https://docs.unity3d.com/2021.3/Documentation/ScriptReference/Shader.Find.html): looking up an existing shader and the absence of shaders stripped from the build.
- [Unity 2021.3 — Native plugin interface](https://docs.unity3d.com/2021.3/Documentation/Manual/NativePluginInterface.html): callbacks on the render thread and OpenGL contexts. Availability of that entry point in the game not verified.

## Logging the next rounds

When updating this document, record the date, build, hashes, hypothesis
investigated, inspection/test method, result, location of the evidence and
limitation. A negative test should also stay recorded, to avoid repeating a
route already ruled out without changed assumptions.

Only promote a step to validated at runtime when the shader's origin is
identified, the result is compared to the control and there is enough data to
repeat the experiment. Do not replace a hypothesis with a promise of
implementation.
