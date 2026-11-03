# C# world generation integration test

```sh
bash tools/tests/build-cs.sh csworldgen
DEVICE=127.0.0.1:16416 bash tools/tests/csworldgen/run.sh
```

Install the APK containing the matching runtime before running. The runner uses only MuMu 16416, enables this pack and `csauto` without `csenv`, and waits up to 900 seconds for the final `csworldgen END` line. Keep the emulator visible.

The fixture creates a unique `BL_CSWorldGen_<time>` small, classic world with seed `73377337` through `GUIWorldCreateMenu.CreateWorld`. The game's normal generation worker and native terrain passes remain enabled. Existing test saves are restored by the shared runner unless `KEEPSAVES=1` is set; the newly generated world has a distinct name.

It inserts a `GenPass` subclass before the terrain and a `PassLegacy` marker after it. Another subclass is disabled. A mod chest is placed immediately before `Tile Cleanup` and checked immediately afterward. The marker is twelve copper blocks at `maxTilesX / 2 + 80..83`, y `100..102`.

Checks include native pass wrappers, real progress/configuration objects, the worker thread, an explicit `ref totalWeight`, missing configuration lookup, disabled execution, ordering and `PostWorldGen`, terrain pass execution, chest type preservation, the Hardmode list, world validation after the normal save and the marker at save time.

The Hardmode test disables `Hardmode Conversion` and runs passes on either side. It does not perform a full conversion. The save checks validate the produced file and in-memory marker; reopening the generated world and multiplayer generation are not covered by this fixture.

## Recorded validation

On 2026-10-31, MuMu `127.0.0.1:16416`:

| Check | Result |
| --- | --- |
| Native world generation and saving | 30/30 |
| Preserved native passes | 106 |
| csmodsystem save / reopen regressions | 11/11 and 5/5 |
| NativeApi instruction/order checks | 7/7, 105 entries |
| Runtime and ARM64 APK | Build passes; packaged runtime matches the built DLL |
| Test fixture | Compiles without warnings |
| Complete ExampleModCSharp | Compiles with its 19 existing nullable warnings |

Integer, double and string configuration reads are checked against a native `JObject` created by the fixture. The native per-pass objects also receive missing-key checks. The chest fixture checks tile type preservation, rather than chest inventory or networking.

The full ExampleMod sample compiles, but this fixture does not measure the sample ore quantity. The guide and phase-5 plan record the remaining mobile limits.
