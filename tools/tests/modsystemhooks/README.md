# ModSystem: implementation and validation

## Analysis

The reference is the local copy of `exmod_tmod/tModLoader/patches/tModLoader/Terraria/ModLoader/ModSystem.cs`, together with the calls from `SystemLoader`, `Main`, `WorldGen`, `MessageBuffer` and `NetMessage`. The mobile signatures were checked in `refs/dump.cs`; the internal points were checked in `refs/script.json` and `refs/libil2cpp.so` from version 301720.

Inputs: registered systems, the native list of steps, the world state and drawing/network arguments. Outputs: callbacks at the groups' edges, edited steps, values through Ref and vetoes. Cases covered: no overrides, later registrations, a cancelled generation, disabled steps, invalid weights, fractional and non-finite rates, menus, exceptions, a missing header, replaced readers and repeated unloading.

## Design

| Approach | Time per update | Space | Maintenance |
| --- | --- | --- | --- |
| Intercept each entity and count the edges | Proportional to the entities, on top of the interested systems | Counters and intermediate states | Depends on the number and update paths of each entity |
| Intercept the groups' edges directly | Proportional to the systems interested per callback | Cached override lists | Nine internal points with instruction checks, plus the public methods |

The implementation uses the second approach. No group point is installed without a matching override. The lists are invalidated when a system registers, without looking for overrides or creating a function per listener every tick. Editing the steps costs O(n), where n is the number of steps, and happens during generation.

JS `GenPass` and `PassLegacy` have a native PassLegacy; the ApplyPass hook forwards the custom steps to their callbacks. The existing native steps keep their implementation. The JS list is referenced during the generator's call.

## Automated tests

```powershell
node tools/tests/modsystemhooks/check.mjs
python tools/tests/modsystemhooks/verify_native.py
```

Result: **32 groups of checks**, **27 native method hooks** exercised by the suite and **9 internal points** with instructions checked in the binary. The test also checks signatures and parameter names against the dump.

The checks include execution order, Ref values, three independent rates, generation lists, success/cancellation, Hardmode, recipes, the listener cache, headers, world rejection, sending/receiving and exception isolation. Direct GenPass subclasses keep the instance's state, receive ApplyPass's arguments and respect Enable/Disable; an exception in a subclass lets the next PassLegacy run. The camera uses a single hook and keeps ModPlayer before ModSystem in both registration orders. A veto remains valid even if the rejection message throws or if a callback that cancelled a packet provides an invalid Ref.

Regressions: ModPlayer **55**, ModItem **138**, localization **95**, PlayerDrawLayer **28** and hair **11** checks passed. These tests use a controlled JS environment; they do not replace running in the game.

## Native single player fixture

```powershell
python tools/tests/moditemhooks/device.py prepare --fixture modsystemhooks --run-id my-run --isolate
```

Start the game with a test character and world. The fixture records the calls and emits `MODSYSTEM_RESULT` after 240 ticks. The cleanup uses the same identifier:

```powershell
python tools/tests/moditemhooks/device.py cleanup --fixture modsystemhooks --run-id my-run
```

The helper keeps and restores the changed preferences, installs the fixture's unique UID and checks the paths before removing them. The backups live in `build/modsystemhooks/<run-id>`.

The fixture validates in the native generator inserting two steps, disabling the original step, the before/after order and PostWorldGen. Since the synthetic call happens on the update thread, only during this test the fixture skips `ThreadManager.CheckThreadTerminiate`: the normal generator requires a managed generation thread. The production code keeps that check. The synthetic steps generate no terrain; it is not a test of full world creation nor of generation on a worker thread.

The default Hardmode step is disabled only by the fixture before running a temporary step. That proves the list editing, without converting the test world. Receiving uses the real MessageBuffer, cancels a temporary packet and checks the output through Ref. The rates test uses four real ticks with clock 0.25, tiles 0.5 and events 0.75, checking the clock's total advance and the independent accumulation of the integer fields. A temporary ModPlayer checks the order of the shared camera in the game.

The run's result is in [native-singleplayer.log](native-singleplayer.log). This round's network tests do not prove a multiplayer session between devices. Header persistence, menu blocking and unloading were validated in the automated suite; they are not claimed as complete UI/save tests on the device.

## Full generation with GenPass and PassLegacy

The [genpassworld fixture](../genpassworld/README.md) was run separately from the normal menu, on the managed `worldGenCallback` thread, without replacing thread checks. It generated a **4200 × 1200** world, completed the **106 native steps**, ran two direct GenPass subclasses and a PassLegacy in the expected order and kept a disabled step without calls. The **35-block** structure persisted in the valid `.wld` and was checked after restarting the process and loading the world. The [native results](../genpassworld/native-results.log) passed the fixture's independent verifier. That run complements the synthetic generator described above; it does not run the full Hardmode conversion.

## Dropped method

`RequiresScreenTarget`: the mobile renderer uses Unity, does not call `FilterManager.CanCapture` and has no `Main.finalScreenTarget`. Main.screenTarget is not the final capture the method asks for. Returning true would not have the effect tModLoader expects.

## Adaptations

`ModifyWorldGenTasks(tasks)` has one argument, following the current local reference. PreWorldGen runs after the mobile list is built and before it runs. Hardmode exposes the single **Hardmode Conversion** step, instead of the Good/Evil/Walls steps. Headers use the local auxiliary file `.wld.bl.header.json`; there is no cloud persistence. Contracts and examples: [modsystem-hooks.md](../../../docs/referencia/modsystem-hooks.md).
