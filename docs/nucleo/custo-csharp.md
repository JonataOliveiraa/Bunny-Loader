# Cost of C# per frame

Measured on MuMu `127.0.0.1:16384` on 2026-10-31, not on a phone yet. MuMu
translates ARM to x86 (houdini): microsecond numbers swing up to 2x between
runs, so every comparison below that matters was made inside one run.

## How it is measured

The native C# hook profiler measures them:

- `adb shell setprop debug.bl.cshookprofile 1` before the game starts turns it
  on (the log says `C# hook profiler: on`). Off, nothing changes.
- Every C# hook installed through the `NativeApi` gets an assembly link around
  it and around its `orig` (`script/dotnet/HookProfileThunks.S`,
  `HookProfile.cpp`), and so do the per-frame C# callbacks the native content
  calls (`ResourceDraw`, `NPCEvents`, `Dust.*`, `SystemStage`, `TileEvents`,
  `ItemDraw`...). Per hook it counts calls, total time and self time: the time
  in C# without the game's original and without other hooks called inside.
  Only the game thread is timed.
- The profiler's own cost per entry (`bias`, 136-162 ns) is measured at the
  first read and subtracted; `clock_gettime` costs 57-67 ns.
- `BunnyLoader.HookProfile.Snapshot()` reads the table from C#.
- `tools/tests/csperf` measures entering a hook; `tools/tests/csperfmod`
  measures a world with ExampleModCSharp. Both log frame times, managed
  allocations (`GC.GetAllocatedBytesForCurrentThread`;
  `GC.GetTotalAllocatedBytes(false)` always returns 0 on Mono) and Mono GCs.

## Entering a hook

`csperf` on three game methods, empty hooks that only call the original,
timed by the profiler (the whole C# stub without the original).

| Entry | 1 hook | 3 hooks | hooks removed |
|-------|-------:|--------:|--------------:|
| Isolated, 1/frame (`Main.DoUpdate_AnimateCursorColors`) | 29.3 µs | 34.2 µs | 26.3 µs |
| Right after, 1/frame (`Main.DoUpdate_AnimateVisualPlayerAura`) | 9.1 µs | 11.5 µs | 10.4 µs |
| Burst, 400/frame (`WorldItem.UpdateItem`) | 1.45 µs | 1.95 µs | 1.47 µs |
| Burst, ms/frame | 0.58 | 0.83 | 0.60 |

- The cost is the transition, not `HookChain`: with every hook removed the
  native stub stays and costs the same. Each extra hook in a chain adds about
  0.25 µs.
- Cold entries are expensive: the first C# entries of a frame cost 50-120 µs
  each (`Main.DoUpdate` 113-126 µs, `Player.Update` 59-121 µs with trivial
  bodies).

## A world with ExampleModCSharp

ExampleModCSharp plus `csperfmod` (which adds a `ModSystem` with
all 22 update hooks, empty), two runs. Frame values are averages over 5 s;
DoUpdate runs about twice per drawn frame at these frame rates, so per-frame
call counts of update hooks are about 2x the entity count.

| | Idle (run 1 / run 2) | 60 `SparklingBall` + 15 `ExampleCustomAISlimeNPC` |
|-|---:|---:|
| Frame | 32.4 / 35.5 ms | 38.3 / 42.8 ms |
| DoUpdate | 3.0 / 2.8 ms | 5.4 / 5.7 ms |
| DoDraw | 23.2 / 25.7 ms | 21.6 / 24.0 ms |
| C# self time | 17.5 / 20.2 ms | 22.5 / 25.3 ms |
| of which `ResourceDraw` | 14.2 / 15.9 ms | 13.2 / 14.7 ms |
| C# without `ResourceDraw` | 3.3 / 4.3 ms | 9.3 / 10.6 ms |
| C# entries per frame | 150 / 161 | 871 / 944 |
| Managed allocation, game thread | 9.4 / 9.8 KB/frame | 20.5 / 22.1 KB/frame |
| Mono GCs in 5 s | 0 / 1 (gen0) | 1 / 1 (gen0) |

The biggest entries with entities:

| Hook | Calls/frame | µs/call | ms/frame |
|------|------------:|--------:|---------:|
| `(callback) ResourceDraw` | 24 | 550-660 | 13.2-14.7 |
| `Projectile.AI` | 134-149 | 13.0 | 1.75-1.95 |
| `(callback) Dust.update` (about 2000 `Sparkle` dusts) | 2.2-2.5 | 600-650 | 1.35-1.61 |
| `(callback) NPCEvents` | 105-117 | 6.5 | 0.68-0.76 |
| `Projectile.HandleMovement` | 134 | 5.0 | 0.67 |
| `Projectile.UpdatePosition` | 134 | 4.4 | 0.59 |
| `Main.DrawNPCDirect` | 16 | 20 | 0.32 |
| `Main.DrawProjDirect` | 60 | 5.1 | 0.30 |
| `Projectile.GetAlpha` | 60 | 2.7 | 0.16 |
| `(callback) SystemStage` (the `ModSystem` update stages) | 14-16 | 8-9 | 0.11-0.15 |

A run with the profiler off gave 58 ms (idle) and 48 ms (entities) per frame:
MuMu's run-to-run noise is larger than anything the profiler adds, so frame
times only compare within a run.

## Where the time goes

1. `ResourceDraw` (`ModResourceOverlay`): 14-16 ms/frame, most of the C# time.
   Each heart and star draw makes an `Asset<Texture2D>` with
   `Asset.ForTexture`, which calls the native `AssetOfTexture`; that rebuilds
   the list of every texture asset of the game (`content::textureAssets()`,
   thousands of entries) on every call. A texture-to-asset cache would remove
   almost all of it.
2. Cold entries: ExampleModCSharp enters C# about 150 times per frame idle,
   many of them once per frame and cold (30-120 µs each): about 3-4 ms. Some
   run for nothing: `NetMessage.SendData` enters C# about 6 times per frame in
   singleplayer; `Player.GetWeaponDamageMultiplier`, `RecalculateLuck`,
   `SetMatch` and other `Player` hooks run every update. Native filters (only
   enter when a mod type or flag needs it) and fewer,
   shared per-frame entries are the lever.
3. Per projectile, about 22 µs per update spread over `AI`, `HandleMovement`,
   `UpdatePosition`, `GetAlpha` and `DrawProjDirect`; per NPC event 6.5 µs.
   Filtering vanilla entities natively and merging the per-projectile hooks
   would cut most of it.
4. Allocations of 10-22 KB per frame make a gen0 GC every 5-7 s; the frames
   with a GC were not slower than the average here. Low priority.

## After the texture asset cache (2026-11-01)

`AssetOfTexture` now keeps a texture-to-asset map: a hit is checked against
the asset's current texture, a miss scans again at most every 300 updates
for that texture. `Asset.ForTexture` looks up its wrapper before reading the
asset name. Same scenario, one run:

| | Idle | 60 projectiles + 15 NPCs |
|-|---:|---:|
| `ResourceDraw` | 13-18 µs/call, 0.3-0.4 ms/frame | 13 µs/call, 0.3 ms/frame |
| C# self time | 4.2 ms/frame (was 17.5-20.2) | 12.7 ms/frame (was 22.5-25.3) |
| DoDraw | 12.6 ms (was 23-26) | 12.2 ms (was 22-24) |
| Managed allocation | 6.6 KB/frame | 17.2 KB/frame |

Items 2 to 4 above are still open.

## On a phone (2026-11-03)

Samsung Galaxy A15 (SM-A156M), reached through ZeroTier and wireless
debugging; `csperf` with `debug.bl.cshookprofile 1`, ExampleModCSharp on,
standing in the world. Four windows of 300 frames:

| | Phone | MuMu (above) |
|-|---:|---:|
| Frame | 16.7-16.9 ms (60 fps) | |
| DoUpdate / DoDraw | 2.7-3.3 ms / 5.7-6.4 ms | |
| C# self time | 2.07-2.23 ms/frame, ~90 hooks, 209-611 entries/frame | 4.2 ms/frame |
| Hot hook entry (`WorldItem.UpdateItem`, 400/frame) | 0.52-0.60 µs/call | 1.45 µs (burst) |
| Cold hook entry, once per frame | 2.0-6.1 µs/call | 26-34 µs (isolated) |
| Managed allocation | 6.4 KB/frame, one gen0 GC per ~5 s, no frame spike | 6.6 KB/frame |
| Profiler bias / clock | 204 ns / 97 ns | |

Entering a hook costs about a third of MuMu's on the phone, where the
emulator's translation is not in the way. The largest single costs are
handlers, not entries: `TeleportPylonsSystem.IsPlayerNearAPylon` takes
320-330 µs in its one call per frame (0.33 ms/frame, the mod pylon check),
`LegacyPlayerRenderer.DrawPlayer_UseNormalLayers` 150 µs, the `ResourceDraw`
callback 24 × 5.6 µs, `Lighting.LightTiles` 90 µs and `Player.UpdateBiomes`
65-95 µs.

The mod pylon check scanned the whole 121x123-tile pylon reach once per mod
pylon type every frame, even with no mod pylon in the world. It now looks at
the `TEModdedPylon` entities first and scans only when one is inside that
region: on MuMu, with ExampleModCSharp, 150-180 µs per frame before and
21-24 µs after (`csperf` now reports it as a focus hook).

## Per-frame pass (2026-11-07)

Measured on MuMu `127.0.0.1:16384` with `csperfmod` and ExampleModCSharp, old
and new `BunnyLoader.dll` alternating on the same APK (two rounds each,
averages). Without mods nothing changed: only the test's own hooks enter C#.

| | Idle, before | Idle, after | 60 projectiles + 15 NPCs, before | after |
|-|--:|--:|--:|--:|
| Frame | 18.3 ms | 17.3 ms | 26.1 ms | 21.2 ms |
| C# self time | 6.62 ms | 4.92 ms | 12.69 ms | 8.56 ms |
| C# entries per frame | 1425 | 521 | 2594 | 1270 |
| Managed allocation | 12.3 KB/frame | 11.0 KB/frame | 20.7 KB/frame | 11.8 KB/frame |

What changed, most expensive first:

1. `Main.DrawItem` entered C# for all 400 item slots every frame (the game
   calls it for every slot and returns early for inactive ones): 0.71 ms/frame.
   The world drawing stage now takes its item from a hook on
   `Main.DrawItem_GetBasics`, which only runs for items that are drawn.
2. `WorldItem.UpdateItem` (`PostUpdate`) and
   `Player.QuickHeal_GetItemToUse_TryChoosingItem` (`GetHealLife`) entered C#
   for every slot: 434-624 and 58 entries per frame, 0.45-0.70 and 0.13 ms.
   They are now gated natively (`script/dotnet/HookGate.S`,
   `NativeApi.InstallGatedHook`): the link reads a field of an argument and goes
   straight to the next link when it is zero (`WorldItem.inner.type`,
   `Item.healLife`). The gated `UpdateItem` link is installed at
   `ContentReady`, after the mods' `Load`, so an `On_WorldItem.UpdateItem`
   added during load still runs outside it. `On_` hooks of mods are never
   gated. After: 0 and 2 entries per frame.
3. `EntityInstances.Get` (every `ModItem`, `ModProjectile`, `ModNPC`,
   `ModPlayer` and global lookup) checked the weak handle in native code on
   every call, a C# to native transition each time. It now checks once per
   game GC cycle, using the game's `System.GC.GetCollectionCount`
   (`GC_get_gc_no`, a plain read, called without the GC transition). Per call:
   `Projectile.AI` 13.7 → 8.6 µs, `HandleMovement` 5.6 → 3.3, `Damage`
   5.0 → 2.9, `UpdatePosition` 3.4 → 2.2, `CutTiles`/`CanCutTiles` 3.1 → 1.9,
   `DrawProjDirect` 4.7 → 2.4, `GetAlpha` 2.5 → 1.7, `DrawNPCExtras` 46 → 31,
   `ResourceDraw` 7.9 → 5.5.
4. `HandleMovement` allocated a `Movement` per projectile per update and
   `RefreshInfoAccs` a dictionary and one queue per item type per update: both
   are reused now. That is most of the 8.9 KB/frame less with projectiles.
5. `Asset<T>.FromGame` (every `TextureAssets.X[i]` of a game asset array) read
   the asset name through native field lookups and a new string before looking
   in its cache: it looks in the cache first. The ExampleMod map layer that
   draws one icon went from about 34 to 20 µs.
6. Smaller ones: `ModContent.GetInstance<T>` (and so `ItemType<T>`,
   `GetGlobalItem<T>`...) reads a static generic cache instead of a lock and a
   dictionary; `Main.ActiveItems` and the `PostUpdate` check read
   `inner.type` (what `WorldItem.get_active` does) instead of calling the
   getter per slot; `ProjectileHooks.Wants` and `ProjectileLoader.HasHooks` use
   the template instead of binding an instance; no closure per call in
   `BuffHooks.Over` and `PlayerLoader.All`; the interface layer list is reused.

Still open: `MapIconOverlay.Draw` (about 0.36 ms, almost all of it the game's
own map layers drawn from C# around the mod layers),
`DrawPlayer_UseNormalLayers` (about 0.27 ms, the vanilla layers called one by
one), `Dust.update` (the mod's own `Lighting.AddLight` per dust), and the
remaining per-slot entries that a field check cannot decide
(`RefreshInfoAccsFromItemType`, 64-75 per frame).

## Hosting a world (2026-11-11)

Players reported lag when hosting, and none with the JS engine. Hosting
(`netMode` 3) runs `Main.TrySyncingMyPlayer`, which calls `Player.clientClone`
once per update, and `PlayerNetHooks` takes a second copy for
`ModPlayer.SendClientChanges`. Each `Player.clientClone` calls
`Item.clientClone` for about 380 items (inventory, armor, dyes, banks,
loadouts). With any `GlobalItem` loaded, `ItemLoader` hooked
`Item.clientClone` to copy the `ModItem` and `GlobalItem` instances, so every
one of those copies entered C# and allocated. tModLoader copies only the net
state there (`Item.CopyNetStateTo`) and forbids `Item.Clone` inside
`Player.clientClone`.

Only the copy shown in the player's hand needs the instances:
`lastVisualizedSelectedItem`, refreshed with `clientClone` by `ItemCheck` and
at the end of `ItemCheckWrapped`. `content/items/VisualItemClone.cpp` already
watched those calls; it now also covers `ItemCheckWrapped` and calls C#
(`NativeApi.OnVisualItemCopy`, `ItemLoader.VisualItemCopied`) only when the
destination is that copy. `ItemLoader` no longer hooks `Item.clientClone`.
`tools/tests/csvisualclone` checks that the hand copy still follows the held
item's `ModItem` and `GlobalItem` (3/3 before and after).

`tools/tests/csmpperf` measures the host alone, with a client, and with 60
`SparklingBall` projectiles and 15 `ExampleCustomAISlimeNPC`; the client
measures its own frames. With ExampleModCSharp, the phone (SM-A156M) hosting
`Limite_Petrificado` and MuMu joining, old and new `BunnyLoader.dll` on the
same APK:

| Phone hosting | Alone, before | Alone, after | Client, before | after | Entities, before | after |
|-|--:|--:|--:|--:|--:|--:|
| Frame | 16.6 ms | 16.6 ms | 16.8 ms | 16.7 ms | 33.9 ms | 21.9 ms |
| DoUpdate | 6.9 ms | 4.5 ms | 7.7 ms | 5.1 ms | 11.5 ms | 9.1 ms |
| C# self time | 5.7 ms | 3.5 ms | 6.5 ms | 3.9 ms | 17.9 ms | 9.1 ms |
| C# entries per frame | 1369 | 606 | 1531 | 753 | 4259 | 1958 |
| `Item.clientClone` entries | 761 | 0 | 763 | 0 | 1547 | 0 |
| Managed allocation | 125 KB | 40 KB | 132 KB | 46 KB | 264 KB | 60 KB |
| Mono GCs in 5 s | 9 | 3 | 9 | 4 | 9 | 4 |

The cost fed itself: a slower frame runs more updates per drawn frame, and
each update copies the player twice. On MuMu hosting alone the copies took
10.5 ms of the 19.9 ms of C# per frame (1532 entries), with 186 KB allocated per
frame and a gen0 GC every 0.8 s; after the change, 553 entries, 14 KB and no
GC in 5 s.

`NPC.TrySyncingUniqueTownNPCData` (hooked when a mod NPC has
`ActsLikeTownNPC`) entered C# for every NPC on every update. The game only
acts there on a client, and only while `needsUniqueInfoUpdate` is set: the hook
is now gated on that field, and outside a client it clears the field, so each
NPC enters once.

Still open: with a second player, the host and the client allocate about
46-63 KB per frame against 14-40 KB alone (a gen0 GC every 1-2 s, no frame
spike on the phone), and `PlayerNetHooks` could compare against
`Main.clientPlayer` instead of taking its own copy every update.

## Time to the title screen (2026-11-04)

Measured on MuMu `127.0.0.1:16384`, from the tap on Play to `startup: title on
screen`, with the mod already compiled. Old and new `BunnyLoader.dll` swapped
in place on the same device, in alternating rounds: six boots each with
ExampleModCSharp, four each with a generated mod of 1500 `ModItem` classes
with one texture file each. Both sets also have `csauto`. Medians, in seconds.
The phone is further down: there, the opening animation hides most of the gain.

| Stage | ExampleMod before | ExampleMod after | 1500 items before | 1500 items after |
|---|--:|--:|--:|--:|
| Game content loaded | 6.6 | 6.5 | 9.8 | 9.4 |
| `Host.LoadMod`, all mods | 19.4 | 8.4 | 51.6 | 10.3 |
| `Initialize_AlmostEverything` and fade | 3.6 | 3.6 | 3.3 | 3.0 |
| Native install (`tickMod*`) | 1.9 | 2.0 | 3.2 | 3.4 |
| `ContentReady` | 4.8 | 2.5 | 1.7 | 1.3 |
| **Title on screen** | **36.3** | **22.9** | **69.5** | **28.1** |

MuMu drifts: some boots ran 10 to 30 s slower than their neighbours, with the
old DLL and with the new one. Medians and the alternating rounds are what make
the columns comparable.

### What `Host.LoadMod` spent

With ExampleModCSharp the load thread spends about 80% of its time in the JIT.
JIT time here is self time per method from a Mono profiler callback, starting
at `Host.Initialize`. It includes class loading done during compilation and
excludes nested compilations. `System.Runtime.JitInfo.GetCompilationTime`
counts nested compilations twice and reads higher: 10.0 s against the 7.9 s
below.

| JIT, whole boot | Before | After |
|---|--:|--:|
| Self time | 20.1 s | 7.9 s |
| of which `On_*` hook classes | 10.8 s | 1.4 s |
| of which `On_*` static constructors | 6.6 s | 0 |
| Methods compiled | 10393 | 10787 |

Before, the generated `On_Main` created the `HookChain` and the layout array of
all its 1373 methods in one static constructor, 2.1 s for that method alone,
and `On_Player` 1.2 s. The first touch of an `On_*` class also loaded a generic
type per hook. A mod uses few of them: ExampleModCSharp installs 372 of the
10626 hooks.

Three changes in the generated code and the load path:

1. Each hook now lives in its own nested class (`On_Main.__H12345`), loaded on
   first use. A hook actually installed costs about 4.8 ms of JIT on MuMu.
2. Method layouts are string literals read only on the first resolve, not
   `int[]` fields filled by each proxy's static constructor. Together with the
   nested classes this adds about 0.4 MB to the API (30.8 MB to 31.3 MB, built
   the same way).
3. Reading the API version no longer loads `System.Reflection.Metadata`
   (360 methods), and the tooltip tag regex is no longer `RegexOptions.Compiled`
   (about 200 ms of `DynamicMethod` at load).

The rest is real work, and the part that scaled badly was one function.
`ItemHooks.Overrides` called `GetMethods` on every question, and registering an
item asks more than 100 of them: 23 ms per `ModItem`, 20 of them outside the
JIT. That is most of the 41 s the 1500-item column lost. It now builds the set
of overridden names once per type: about 2.3 ms per item, of which 0.3 ms is
JIT.

### `ContentReady`

The 2.1 s step was `ItemLootHooks.ContentReady`, with 47 ms of JIT. It ran
`SetDefaults` on every item and reflection per item per `GlobalItem`, in one
slice. It now skips vanilla items unless a `GlobalItem` overrides
`ModifyItemLoot`, and yields after each item. `csnpcloot` covers that case.

| | Before | After |
|---|--:|--:|
| Longest step, three boots | 2539 ms (item loot) | 316 ms (`ServerSyncHooks.Install`) |
| Next longest | 506 ms | 290 ms (`BestiaryHooks`) |

### Native install with many items

`tickModItems` is one block in one frame: 1.9 s for 1564 items on MuMu (1500
plus the 64 reserve items), about 1.2 ms per item, and only 0.2 s more with one
texture file per item than with one shared texture. The duplicate check in
`registerModItem` is quadratic but does not show up at this size. The screen
holds the flower for those 2 s, so it is not split.

The Calamity pack on the phone is a texture pack and adds no items, so it says
nothing about this path. It was not used as load.

### On a phone

Samsung Galaxy A15 (SM-A156M), `com.bunnyloader.beta`, one mod at a time, without
`csauto`, already compiled: ExampleModCSharp, then the generated mod of 1500
items. Four boots each, old and new DLL alternating. Medians, in seconds.

| Stage | ExampleMod before | ExampleMod after | 1500 items before | 1500 items after |
|---|--:|--:|--:|--:|
| Game content loaded | 3.5 | 3.5 | 3.5 | 3.6 |
| `Host.LoadMod` | 8.3 | 2.5 | 11.3 | 2.5 |
| From the end of `LoadMod` to the end of the splash | 2.9 | 8.4 | 1.7 | 7.7 |
| Native install (`tickMod*`) | 1.0 | 1.0 | 1.4 | 1.5 |
| `ContentReady` | 2.5 | 1.3 | 0.7 | 0.4 |
| **Title on screen** | **18.1** | **17.0** | **18.6** | **15.8** |

`Host.LoadMod` is 3.3 to 4.5 times faster, but the title moved by 1.1 s and 2.8 s,
because the game's own splash timer now decides. Mods are ready at about 6 s, and the
screen then waits for the animation to reach its end, at about 15 s. On MuMu
the mods were the slower of the two, so the gain shows there. Cutting the splash
once mods are ready already exists, under Quick start
(`config().fastBoot`, `advanceSplashTimer`). Doing it on every boot would remove
most of that 8.4 s wait, but the logo screen stays by decision (2026-11-04).

The longest `ContentReady` step on the phone is 163 ms. `startup: C# compiled`
reports about 10700 methods and 3.2 to 3.8 s of JIT.

### Still open

- 7.9 s of JIT remain on MuMu, 2.9 s of it in the base library, and about 2.5 s
  on the phone by the same `JitInfo` count at the end of `LoadMod`. Only
  precompiling would remove it: Mono AOT, with the package
  `Microsoft.NETCore.App.Runtime.AOT.win-x64.Cross.android-arm64` 10.0.14.
  Not tried; it adds a build step and grows the APK.
- `Layouts.Verify` runs on every boot, about 0.2 s in `StructLayouts..cctor`.
- The first time a mod compiles on the device costs about 77 s on MuMu on top
  of this (332 files, 21294 lines). The title came up at 109 s in that run. On
  the Galaxy A15 it costs 12.8 s in the `:compile` process, before the game
  opens, and the game then compiles about 10700 methods with the JIT instead of
  33700. The time to the title is about the same; what changes is that the
  errors come first and the game process no longer runs Roslyn.

### How to measure

The log line `startup: C# compiled N methods, K KB of IL, T ms of JIT` comes at
the end of `ContentReady` on every boot, phone included. `T` is the `JitInfo`
number.

`tools/startup/` has the rest:

- `bootrun.sh <out dir> <label> <runs> <uid>...` makes cold boots and keeps the
  logs. On a phone, set `DEVICE`, `BL_PKG` and `TAP` (the Play button: `1211 842`
  in landscape, `538 2118` in portrait; the launcher follows the phone). It needs
  the `csauto` pack on the device. `table.py <glob>...` prints the stages above.
- `genmany.py <out dir> [N]` writes the pack of many items.
- `jitprof.c` is the profiler callback, built with the NDK clang into a `.so`.
  Copy it to `files/libjitprof.so`, add `StartupProfile.cs` to the API, and call
  `JitStart()` in `Host.Initialize` and `JitDump()` at the end of
  `ContentReady`. `jitreport.py` reads the `files/jitprof.txt` it writes.

## Against the engine of 0.3.3

Measured on the same phone on 2026-11-03: see
[Migration to C#](../mods-em-csharp/07-migracao-para-csharp.md#measured-on-the-same-phone).
