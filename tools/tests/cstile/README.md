# C# tiles integration test

Run on the reserved device:

```sh
DEVICE=127.0.0.1:16416 bash tools/tests/run-cs.sh cstile
DEVICE=127.0.0.1:16416 KEEPSAVES=1 bash tools/tests/run-cs.sh cstile
```

The first run checks 31 cases. A preserved world containing the Block sidecar entry adds a strict reload check, for 32 cases. The persistence marker is at tile 64,64 with frame 18,36. The test also exercises a solid block and a 2x2 object, whole-object destruction, default and explicit drops, vetoes, dust, pick power, wiring, direct mobile right click, cursor icon, animation, native draw-data callbacks, drawing/special drawing, emitted light reaching Lighting.AddLight, nearby effects, danger/spelunker coordinates and global hooks on vanilla dirt.

The test runner waits for the complete mod load before opening the world. Opening it as soon as csauto first logs the menu can happen before the native save backend is installed. The touch-controls dialog pauses the world; dismiss it on the visible emulator with the normal Later button.

## JS save compatibility

```sh
QJS=true DEVICE=127.0.0.1:16416 bash tools/tests/run-cs.sh cstilelegacy
QJS=false DEVICE=127.0.0.1:16416 KEEPSAVES=1 bash tools/tests/run-cs.sh cstile
```

`cstilelegacy` intentionally shares this pack's mod UID and Block name. It registers ABefore first, so JS saves Block as numeric ID 755. C# registers Block as 754. Only one of these two packs should be enabled at a time. The C# reload assertion verifies the UID/name mapping and both frame coordinates.

## Recorded validation

On 2026-10-31, MuMu 127.0.0.1:16416:

| Check | Result |
| --- | --- |
| Fresh C# world | 31/31 |
| Reopened C# world | 32/32 |
| JS writer | 1/1 |
| JS world reopened with C# | 32/32 |
| csitems | 9/9 |
| csnpc | 19/19 |
| csnpcloot | 41/41 |
| cstownnpc, NOENV=1 | 33/33 |
| NativeApi order | 99 entries, pass |
| Debug APK | Build passes; packaged runtime matches the built DLL |
| ExampleModCSharp | Complete sample compiles |

The tile guide and phase-5 plan record the mobile rendering/light/map limitations and the deferred systems. This suite does not validate multiplayer tile synchronization.
