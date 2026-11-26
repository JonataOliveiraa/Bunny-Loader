# Plan: generic game methods, and the texture methods the phone lacks

Written 2026-11-26, after a developer asked how to use `Texture2D.SetData`. Two
parts, in this order: first calling and hooking the game's **generic methods**
from C#, then writing by hand the **XNA methods the mobile game does not have**
(`SetData(Color[])`, `GetData`, `FromStream`).

## Status (2026-11-26)

Part A was built with a simpler design than section 3 describes:

- `tools/proxygen/generic_instances.py` reads `refs/dump.cs` and writes
  `ProxyGen/generic-instances.txt`: one line per compiled instance (owner,
  method, parameter names, type arguments). 173 lines for Assembly-CSharp.
- ProxyGen closes each instance at generation time (`MakeGenericMethod` on the
  dummy DLL, `System.Object` for the shared body) and plans it with the
  existing `Model()`, so the ABI is decided at build time, as for any other
  method. No `IGameType` and no runtime `Abi<T>`.
- The public method (`Request<T>`, `Get<T>`...) checks `T` against the
  instances (`typeof(T) == typeof(int)`, or `GameGeneric.IsReference<T>()` for
  the shared body) and calls the private closed method, which resolves the
  inflated `MethodInfo` once per `T` (`GameInterop.ResolveGeneric` +
  NativeApi `InflateMethod`). No instance → `NotSupportedException` naming the
  ones that exist.
- 93 generic methods generated. Skipped, with the reason in `skipped.txt`:
  `Func<T>`/`List<T>`/`Task<T>` shapes, generic virtual methods, the shared
  enum instances (`ByteEnum`, `Int32Enum`), `Nullable<T>` arguments, and the
  ones written by hand (`!Type.Method<>` in `types.txt`: `Utils.Clamp`,
  `Utils.Swap`, the two `BindShader`, `GameConfiguration.Get`).
- Found on the way: `AssetRequestMode` has different numbers in the game
  (`ImmediateLoad = 1`, `AsyncLoad = 2`) and in the tML-style enum. Parameters
  and returns of that type now convert (`AssetRequestModes`), hooks included.

Parts B and C are not started.

### Cost measured (2026-11-26, MuMu 16384, beta, 3 runs)

Pack `tools/tests/csgenericsperf` (52 cases: every compiled instance of the
read-only generic methods, plus non-generic baselines), table from
`tools/tests/csgenericsperf/report.py` over the three logs. Medians:

| Call | Hot | First call | Managed alloc |
|---|---|---|---|
| Non-generic game call (`Utils.ToInt`, `CountBuffs`) | 342 / 473 ns | 0.27 ms | 0 |
| Generic, any instance (`SelectRandom`, `NextFromList`, `IndexInRange`, `GetModule`, `GetPower`, `TryGet`...) | 316–411 ns | 1.5–4.9 ms | 0 |
| `Preferences.Get<T>` (string argument) | 990–1093 ns, same as `Contains(string)` 956 ns | ~2 ms | 0 |
| `AssetRepository.Request<Texture2D>` | 1.56 µs (`ModContent.Request` 1.03, `TextureAssets.Item[1]` 0.66) | 12.8 ms | 0 |
| `int[]` passed where `GameArray<int>` is expected | +2.6 µs | | 48 B (a new game array per call) |
| Type the game lacks (`Get<double>`) | 8.7 µs per throw | | 712 B |

- When hot, choosing the instance costs nothing: the first and sixth
  `typeof` branch take the same time, and `IsReference<T>()` is 0.3 ns. A
  generic call costs as much as any non-generic call into the game.
- The first call per `T` is 1.5–5 ms (Mono JIT of the wrapper and the closed
  method, plus `MakeGenericMethod`). A second `T` of an already-called method
  costs 70–85 µs.
- The cost to avoid is the C# array conversion: keep a `GameArray<T>`.
- Pure logic stays faster in managed code: `Utils.Clamp` 4.8 ns against
  ~330 ns for any game call, and picking from an array by hand is 53 ns
  against 330 ns for `SelectRandom`.
- `Count<WorldItem>` (10.5 µs) is the game walking 400 items, not overhead.
- Not covered: the phone (MuMu exaggerates the cost of entering the game, see
  the C# cost notes).

**Methods with side effects** (31 more cases in the same pack, 83 in all, 3
runs, no errors). Each one runs on a throwaway target, or in pairs that leave
nothing behind:

| Call | Hot | Against |
|---|---|---|
| `SetFactory.CreateCustomSet<T>` on a 16-slot factory, each instance (`Color`, `Vector2`, `float`, `PlacementDetails`, `IncludeCapeFrontAndBackInfo`, `BannerEffect`, object) | 405–495 ns | `SetFactory.New(16)` 1.03 µs |
| `CreateCustomSet<object>` + `Recycle<object>` | 1.59 µs | |
| `Utils.Shift<object>` on a scratch array | 340 ns | |
| `ParticlePool.RequestParticle<T>` + `ReleaseParticle<T>` | 713 ns | two calls |
| `NetModule.CreatePacket<T>(64)` + `Recycle` | 1.35 µs | |
| `ChatCommandId.FromType<T>` | 1.96 µs | the game reads an attribute |
| new `ChatCommandProcessor` + `AddCommand<T>` / `AddDefaultCommand<T>` | 2.47 / 3.12 µs | `New()` 0.98 µs |
| new `ChatMessage` + `SetCommand<T>` | 1.45 µs | `New("bl")` 1.08 µs |
| `Invariant.Assert` with 1, 2 and 3 type arguments | 661–687 ns | same for any count |
| `DeepCloning.ShallowClone<T>`, `AddImmutableType<T>` | 408 / 966 ns | |
| new `DeepCloneContext` + `AddClone<T>`, `TryGetClone<T>(out)` | 1.29 µs / 362 ns | `New()` 0.82 µs |
| `AssetRepository.EnsureReloadActionExistsForType<T>` | 900 ns | |
| `IdDictionary.Create<ItemID, short>` / `<BuffID, int>` / `<TileID, ushort>` | 6.6 / 0.49 / 1.0 ms | the game reflects over every constant |
| `Lang.FillNameCacheArray<ItemID, short>` | 8.8 ms | the game looks up every name |

- Same picture: each generic call costs as much as a plain call into the game.
  The rest is the game's own work. A struct passed in float registers
  (`Vector2`) or several type arguments (`Assert`, `IdDictionary.Create`)
  add nothing measurable.
- The first run crashed in `DeepCloneContext.TryGetClone`: the context made at
  setup was held only by C#, and the game's collector freed it. The pack now
  pins its setup objects with `GameHandle`. This is the rule in "Two garbage
  collectors" on the Under-the-Hood page, not a problem of the generics.
- Left out on purpose, because they change game state that a test cannot
  undo: global registries (`NetManager.Register`,
  `CreativePowerManager.Register`, `WorldGenerationOptions.Register`,
  `ChatManager.Register`, `LeashedEntity.Registry.Register`,
  `SocialAPI.LoadModule`), asset loaders and reloaders (`ForceReloadAsset`,
  `LoadAsset`, `TryLoadingAsset`, `TryLoad`, `ContentManager.Load`,
  `ConstructOnMainThread`, `LoadUnityResource`, `DeserializeEmbeddedResource`),
  Unity object finders (`FindInChildren`...), `Texture2D.SetData<object>`
  (unknown behavior on the phone, part C), and the deep clone and restore of
  `DeepCloning` (debug code).

## 1. Where we stand

ProxyGen has never generated generic methods. `Emitter.Model` returns
`generic methods are not generated yet` for any method with
`IsGenericMethodDefinition` or `ContainsGenericParameters`, and the same
`Model` builds both the call proxy and the `On_` hook. So a generic method of the
game can be neither called nor hooked.

What already works and stays as it is:

- Generic methods of Bunny Loader itself (`ModContent.Request<T>`,
  `GetInstance<T>`, `TagCompound.Get<T>`) and of the mods. They are managed code
  on Mono.
- Pure-logic generics that tML exposes, written by hand in managed C#:
  `Utils.Swap<T>`, `Utils.Clamp<T>` (`Terraria/Utils.TML.cs`),
  `TileEntity.TryGet<T>` (`TileEntity.TML.cs`). Calling the game for these would
  only add a bridge crossing.
- The native side inflating `AssetRepository.Request<Texture2D>` by
  `MakeGenericMethod` (`ContentAssets.cpp`, `requestTextureMethod`). This is the
  pattern part A generalizes.

### Inventory (from `skipped.txt` and `refs/dump.cs`)

| | Count |
|---|---|
| Generic methods skipped | 108 |
| With a declaration in the dump | 105 |
| With at least one compiled instance | 93 |
| Compiled instances | 161 |
| ... shared body (`<object>`) | 113 |
| ... value-type body | 48 |
| No compiled instance at all | 12 (7 of them `Terraria.Testing.Cloning`) |

The useful ones for mods are the ones that touch game state:
`AssetRepository.Request<T>` (tML's `Main.Assets.Request<T>`),
`NetManager.GetModule<T>`/`GetId<T>`, `CreativePowerManager.GetPower<T>`,
`ParticlePool.RequestParticle<T>`/`ReleaseParticle<T>`,
`ChatManager.Register<T>`, `Main.RunOnMainThread<T>`,
`WorldGenerationOptions.Get<T>`, `IdDictionary.Create<T, V>`,
`SetFactory.CreateCustomSet<T>`, `Preferences.Get<T>`,
`GameConfiguration.Get<T>`, `ItemSlot.HandleItemPickupAction<T>`. The `Utils.*`
helpers are better served by managed copies (see above).

Out of scope here: generic **types** (`List<T>` proxies, which go through
`GameList`/`GameDictionary`), methods of generic types, and generic virtual
methods. None of the 108 needs them first.

## 2. How the game runs a generic method (Unity 2021.3.56f2, IL2CPP)

- There is no single body. IL2CPP compiles each instance it saw at build time.
- **Reference-type arguments share one body**, the `<object>` instance in the
  dump (`Texture2D.SetData<object>` at `0x1C04250`). The body learns the real `T`
  from the hidden last argument, the **inflated** `MethodInfo*`, through its
  `rgctx_data`/`genericMethod` fields (`refs/il2cpp.h`, `struct MethodInfo`).
  Passing the definition's `MethodInfo` there, as today's proxies pass
  `__m.Info`, would give the body the wrong context.
- **Value-type arguments get one body each**, only for the `T` the game used.
  `Utils.SelectRandom<int>` exists, `SelectRandom<Vector2>` does not.
  `SetData<Color>` does not exist either. Unity 2021 has no full generic
  sharing (it came in 2022.1), so a missing value-type instance cannot be run at
  all: calling it ends in `ExecutionEngineException` (or a null method pointer).
- `MakeGenericMethod` through reflection gives the inflated `MethodInfo` for any
  reference type (the shared body plus its context) and for the compiled value
  types. `ContentAssets.cpp` already does it with `runtime_invoke` and
  `method_get_from_reflection`.
- A hook patches **code** (`hook::install` patches at `methodPointer`), so a
  hook on the shared body catches every reference-type instance at once. A
  value-type instance is its own address and needs its own hook.

## 3. Part A: calling generic game methods

### A0. Prove the mechanics on MuMu (before ProxyGen)

A throwaway native test, in the style of `requestTextureMethod`, logging:

1. Inflate `AssetRepository.Request<Texture2D>` and call it directly with the
   inflated `MethodInfo` as the last argument. Compare with what
   `ModContent.Request` returns for the same vanilla path.
2. Inflate `Utils.SelectRandom<int>` (compiled value type) and call it.
3. Inflate `Utils.SelectRandom<Vector2>` (not compiled). Record what comes back:
   an exception from `MakeGenericMethod`, a null `methodPointer`, or a stub that
   throws when called. Part A's error message depends on it.
4. Check that `FindMethodBySignature` finds a generic definition by the
   signature ProxyGen would write (`Asset<T> Request<T>(string assetName,
   AssetRequestMode mode)`), that is, what `il2cpp_type_get_name` gives for a
   generic parameter. Adjust `Signature.cpp` if not.

### A1. Runtime: one cache slot per instance

- New NativeApi entry `InflateMethod(definition, classes, count)`: does the
  `MakeGenericMethod`, returns the inflated `MethodInfo*`, or null with the
  reason in the log. It goes at the end of both tables (a new APK, like every
  NativeApi entry).
- `GameInterop.GenericMethod(ref MethodRef slot, GameClass owner, string
  signature, layout, GameClass[] args)`: same contract as `Method`, but resolves
  the definition and then inflates it. If `methodPointer` is null it throws
  `NotSupportedException("Utils.SelectRandom<Vector2> is not compiled into the
  game; compiled: int, short, ushort, float, Color, Point, object")`.
- The cache is a nested `static class __G<T>` per generic method, generated by
  ProxyGen. Every `T` gets its own static `MethodRef`, with no dictionary lookup
  per call.
- The list of compiled instances comes from `refs/script.json` (Il2CppDumper
  writes the instances there), read by ProxyGen at build time and used only for
  the message.

### A2. Mapping `T` to the game

- A new interface `IGameType` with `static abstract GameClass GameClass { get; }`,
  generated on every proxy (reference) and every layout mirror (value). The
  primitives (`int`, `float`, `bool`...) come from a fixed table in
  `GameTypes.Of<T>()`.
- Constraint emitted: `where T : unmanaged, IGameType`. Proxies are already
  `readonly struct`s holding a pointer, so one constraint covers both kinds.
  `GameTypes.IsReference<T>()` tells whether the argument travels as a pointer or
  by value.
- Parameter and return shapes supported in the first round: `T`, `T[]`
  (`GameArray<T>`), `ref T`/`out T`, `Asset<T>` (already typed by hand). Others
  (`Func<T>`, `List<T>`, `IEnumerable<T>`) are skipped with their own reason.
- ABI: a reference `T` is a pointer (`x` register). A value `T` depends on its
  layout. Struct ≤ 16 bytes without floats goes in one or two `x` registers.
  Floats or an HFA (`Vector2`, `Color` is not one) go to `v` registers. Bigger
  structs go by pointer. ProxyGen cannot decide this ahead of time, so a small
  `Abi<T>` computed once per `T` packs the `CallFrame`. First round: reference,
  primitives and non-float structs. HFA after A0 shows it is needed.

### A3. ProxyGen

- `Model()` accepts generic definitions whose shapes A2 covers, and emits
  `public static Asset<T> Request<T>(...) where T : unmanaged, IGameType`.
- A name written by hand wins, as today (`!` in `types.txt`). That keeps
  `Utils.Clamp`/`Swap` and `TileEntity.TryGet` managed. Pure-logic `Utils.*`
  that tML has and we do not yet (`SelectRandom`, `NextFromList`,
  `IndexInRange`) get managed copies too, not game calls.
- `skipped.txt` keeps the reason for what is still out (`generic method:
  parameter Func<T> not mapped`).

### A4. Test and docs

- Pack `tools/tests/csgenerics`: `Main.Assets.Request<Texture2D>` against
  `ModContent.Request`, `NetManager.GetModule<T>`, a compiled value instance,
  the missing instance (expects the message), `T[]` and `out T`. MuMu first,
  then the phone.
- The wiki `Under-the-Hood` page loses "generic methods are not generated yet"
  and gains how instances work. The API reference is regenerated.

## 4. Part B: hooking generic game methods

### B0. Check the hidden argument at real call sites

Native logger hooks on `AssetRepository.Request<object>` and on
`Utils.SelectRandom<int>`, logging the last register. Confirm that the game
passes the inflated `MethodInfo` from shared code and from specialized code, and
never null. If a specialized site passes null, the value-type hooks must not
read it.

### B1. Shape of the hooks

tML has no `On_` for generic methods, so the names are ours:

- **Shared body**: `On_AssetRepository.Request`. The handler receives
  `GenericArgs args` (the `T` classes read from the hidden `MethodInfo`) besides
  the normal parameters. `On_AssetRepository.Request.For<Texture2D>(handler)`
  filters in the dispatcher, so the handler only sees that `T`.
- **Value-type body**: one event per compiled instance,
  `On_Utils.SelectRandom_Int32`, with concrete types and no `GenericArgs`.

### B2. Forward the caller's `MethodInfo` to `orig`

Today the native entry (`__n...`) receives the hidden argument in its last
register and drops it, and `__b...` calls the original with the static
`__m.Info`. For a shared body that is wrong (see section 2). The entry stores the
received pointer in a `[ThreadStatic]` slot (saved and restored around the call,
for recursion), and `__b...` passes it on. Non-generic hooks stay as they are.

### B3. Gate and cost

`Request` and `ParticlePool.RequestParticle` run often. A HookGate mode "hidden
argument is one of N pointers" lets a `For<T>` handler stay out of the C# entry
for every other `T`. Measure with `csperfmod` before and after, as in the earlier
hook audits.

### B4. Test

Pack `tools/tests/csgenerichooks`: counts `Request<Texture2D>` against
`Request<SoundEffect>` under `For<T>`, changes the return of a value instance,
and checks that `orig` with a forwarded context returns the same asset as
without a hook.

## 5. Part C: XNA methods the mobile game lacks, by hand

The mobile `Texture2D` (`refs/dump.cs`, TypeDefIndex 1062) wraps a Unity
texture and has only `SetData<T>(T[] data, int start, int length)`, compiled as
`<object>`. It has no `GetData`, no `FromStream` and no `SetData` overload
without `start`/`length` or with a `Rectangle`. Even after part A, `SetData`
with `Color[]` is not possible because there is no `<Color>` body. These are
written by hand in a `Texture2D` partial, backed by the native code that already
builds mod textures from pixels (`gameTextureFromPixels` in `ContentAssets.cpp`:
writable raw data, `memcpy`, `Apply`).

### C0. Read what the game does

- Disassemble `SetData<object>` (`0x1C04250`) and `CreateEmptyTexture`. Find
  what `new Texture2D(device, w, h)` creates on the Unity side (format,
  readable or not).
- List the texture kinds a mod can reach: its own Unity texture (RGBA32), a
  region of an atlas (`PackedEntry`, `_textureAtlas`), palettized
  (`_unityPalTexture` + `_unityAlphaTexture`), `RenderTarget2D`
  (`_unityRenderTexture`). Each method below states what it does with each.

### C1. `SetData`

`SetData<T>(T[] data)`, `SetData<T>(T[] data, int start, int count)`,
`SetData<T>(int level, Rectangle? rect, T[] data, int start, int count)`, for
`T` = `Color`, `uint`, `byte`. A new NativeApi entry `TextureWrite(texture,
x, y, w, h, rgba)` writes into the Unity texture and calls `Apply`. A texture
that does not own its pixels (atlas, palette) is first copied to its own RGBA32
texture (copy on write), so the edit never changes the shared atlas.

### C2. `GetData`

Same overloads. A readable texture is read from its raw data. Otherwise a GPU
read (`RenderTexture` + `ReadPixels`, on the game thread, slow, documented as
such). An atlas region is cropped by `PackedEntry`. A palette is decoded.

### C3. `Texture2D.FromStream` and `new Texture2D(...)`

`FromStream(GraphicsDevice, Stream)` decodes with the native `PngDecode` and
builds with `gameTextureFromPixels`. tML's `new Texture2D(device, w, h)` gets the
partial constructor `: this(New(...).Pointer)`, as the other
hand-written constructors do.

### C4. `RenderTarget2D.GetData`

Through the C2 path for the render texture.

### C5. Inventory of the rest

A script compares FNA's public graphics API (the one tML builds on) with the
mobile dump and lists what is missing. Each item is then decided one by one:
by hand, part A, or left out. The 37 skips for `argument does not fit the
supported registers` are a separate ABI item and do not go in here.

### C6. Test and example

Pack `tools/tests/cstexdata`: `SetData` then `GetData` round trip on a
mod-created texture, on a mod texture and on a vanilla atlas texture (which must
stay intact), `FromStream` from a mod file, and the drawn result in a capture.
As with every new API feature, it closes with the tML ExampleMod piece
that uses it, if there is one. Otherwise with the test pack and a wiki page.

## 6. Order

1. A0 (it decides the A1 error message and the B0 premise).
2. A1–A4: calls.
3. B0–B4: hooks.
4. C0–C6: hand-written texture methods. C1 does not depend on A or B. If the
   developer who asked is waiting, it can be moved ahead without changing the
   rest.

## 7. Risks

- **`T` from a mod's own type.** `Request<MyModType>` has no game class.
  `IGameType` only exists on proxies and mirrors, so this does not compile. Good.
- **Generic methods with constraints** (`where T : ModuleBase`) that
  `MakeGenericMethod` rejects at runtime. The constraint is copied into the C#
  signature where it maps to a proxy, so the compiler catches it.
- **DLL size.** About 100 extra methods plus a nested class each: small next to
  the 20,918 hooks (since `*` in `types.txt`).
- **Copy on write in C1** costs memory per edited vanilla texture. It is
  documented, and only texture data that a mod actually edits is copied.
