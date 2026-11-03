# C# tiles integration test

Run on the reserved device:

```sh
DEVICE=127.0.0.1:16416 bash tools/tests/run-cs.sh cstile
DEVICE=127.0.0.1:16416 KEEPSAVES=1 bash tools/tests/run-cs.sh cstile
```

The first run checks 38 cases. A preserved world containing the Block sidecar entry adds a strict reload check, for 39 cases. The persistence marker is at tile 64,64 with frame 18,36. The test also exercises a solid block and a 2x2 object, whole-object destruction, default and explicit drops, vetoes, dust, pick power, wiring, direct mobile right click, cursor icon, animation, native draw-data callbacks, drawing/special drawing, emitted light reaching Lighting.AddLight, nearby effects, danger/spelunker coordinates and global hooks on vanilla dirt. King/queen statues are checked in both actual hook installation orders, including PreHitWire vetoes before teleportation. Crafting station adjacency checks the precomputed aliases.

The test runner waits for the complete mod load before opening the world. Opening it as soon as csauto first logs the menu can happen before the native save backend is installed. The touch-controls dialog pauses the world; dismiss it on the visible emulator with the normal Later button.

## Recorded validation

On 2026-10-31, MuMu 127.0.0.1:16416:

| Check | Result |
| --- | --- |
| Fresh C# world | 31/31 |
| Reopened C# world | 32/32 |
| csitems | 9/9 |
| csnpc | 19/19 |
| csnpcloot | 41/41 |
| cstownnpc, NOENV=1 | 33/33 |
| NativeApi order | 99 entries, pass |
| Debug APK | Build passes; packaged runtime matches the built DLL |
| ExampleModCSharp | Complete sample compiles |

The tile guide and phase-5 plan record the mobile rendering/light/map limitations and the deferred systems. This suite does not validate multiplayer tile synchronization.

## Tile review follow-up

On 2026-10-31, the reformatted runtime passes 38/38 fresh and 39/39 reopened on MuMu 16416. The additional checks cover cached station adjacency, both king/queen statue hook orders and vetoes before NPC teleportation.
