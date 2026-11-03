# Cost of C# per frame

Measured on MuMu `127.0.0.1:16384` on 2026-10-31, not on a phone yet. MuMu
translates ARM to x86 (houdini): microsecond numbers swing up to 2x between
runs, so every comparison below that matters was made inside one run.

## How it is measured

C# hooks do not go through the JS dispatcher, so `bl.hookStats` cannot see
them. The native C# hook profiler fills that gap:

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

Same run, QuickJS on, `jsentry` (JS) and `csperf` (C#) on the same three game
methods, empty hooks that only call the original. JS is `bl.hookStats` (JS time
without the original); C# is the profiler (the whole C# stub without the
original).

| Entry | JS | C#, 1 hook | C#, 3 hooks | C#, hooks removed |
|-------|---:|-----------:|------------:|------------------:|
| Isolated, 1/frame (`Main.DoUpdate_AnimateCursorColors`) | 21.3 µs | 29.3 µs | 34.2 µs | 26.3 µs |
| Right after, 1/frame (`Main.DoUpdate_AnimateVisualPlayerAura`) | 2.8 µs | 9.1 µs | 11.5 µs | 10.4 µs |
| Burst, 400/frame (`WorldItem.UpdateItem`) | 1.31 µs | 1.45 µs | 1.95 µs | 1.47 µs |
| Burst, ms/frame | 0.53 | 0.58 | 0.83 | 0.60 |

- Entering C# costs about what entering JS costs: a little more in a burst,
  more when cold. The body of a hook is where C# wins (fields 15-64x, math
  3-29x faster than JS, from the earlier `benchcs`).
- The cost is the transition, not `HookChain`: with every hook removed the
  native stub stays and costs the same. Each extra hook in a chain adds about
  0.25 µs.
- Cold entries are expensive: the first C# entries of a frame cost 50-120 µs
  each (`Main.DoUpdate` 113-126 µs, `Player.Update` 59-121 µs with trivial
  bodies).

## A world with ExampleModCSharp

QuickJS off, ExampleModCSharp plus `csperfmod` (which adds a `ModSystem` with
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

JS for reference, from another run and other content (2026-10-09, JS
ExampleMod, `perfloader`): 1.8 ms/frame idle, 7.2 ms with 60 projectiles and
15 slimes.

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
   enter when a mod type or flag needs it, as the JS hooks do) and fewer,
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
asset name. The JS `bl.assetOfTexture` uses the same native lookup. Same
scenario, one run:

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
