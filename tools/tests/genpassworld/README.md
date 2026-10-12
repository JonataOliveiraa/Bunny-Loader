# GenPass and PassLegacy: full generation

This fixture creates a small world through the normal Terraria mobile 301720 menu. The game starts `WorldGen.worldGenCallback` through `ThreadManager.RunForUserCritical`. No thread check is replaced and no native terrain step is removed or disabled.

`BeforePass` and `StructurePass` extend `GenPass` directly and override `ApplyPass(progress, configuration)`. The first step receives the native objects before the terrain. The second runs after Final Cleanup and places a 7 × 5 structure of copper and tin blocks. A later PassLegacy confirms the 35 blocks. A third subclass is disabled and must stay without calls.

The test checks the managed generation thread, the order, the native arguments, a changed weight, Enable/Disable, the exact call count, completed native steps, PostWorldGen, validation of the `.wld` and the persistence of the blocks after restarting the process and loading the saved file. The callbacks' proof lives in the system's auxiliary `.wld.bl.json` file.

## Running

```powershell
python tools/tests/moditemhooks/device.py prepare --fixture genpassworld --run-id my-test --world '-' --isolate
```

Start the game, pick a test character and create a world from the menu. The fixture sets a unique name `BL_GenPass_my-test`, small size and classic difficulty. The helper prepares `config.js` with the same name across retries. Record `GENPASS_GENERATED` and `GENPASS_SAVED` from the log.

Close the process and reopen the generated world with the same fixture enabled. The loaded proof and the blocks must produce `GENPASS_RELOADED` with `generationCallbacksThisProcess: 0`, showing that the structure came from the saved file. Validate the log by gathering both runs:

```powershell
python tools/tests/genpassworld/verify.py path-to-the-log.txt
```

Clean up the preferences and the temporary package:

```powershell
python tools/tests/moditemhooks/device.py cleanup --fixture genpassworld --run-id my-test
```

That command keeps the generated world. If you need to remove it, confirm the new file's exact path and remove only its auxiliary files. Do not reuse an existing world name.

Also check that the loaded proof stays intact in a later save:

```powershell
node tools/tests/genpassworld/check.mjs
```

## Native result

Run on **2026-10-06**, on MuMu/Android, with the APK from `out/bunny-loader.apk` and Terraria mobile **301720**. The [results log](native-results.log) passed `verify.py`:

- World **BL_GenPass_full-20261006-a**, **4200 × 1200**.
- Creating thread **1**; generation on thread **5**, identified as `worldGenCallback`.
- **106 native steps run**, with every terrain step preserved.
- Order: PreWorldGen, the BeforePass subclass, the StructurePass subclass, PassLegacy and PostWorldGen. Each active custom step ran once; the disabled subclass ran zero times.
- A structure of **35 blocks** present when generation finished, after saving and after restarting the process. On reload, the new process's generation callbacks stayed at zero.
- A `.wld` file validated by the game, **2,989,962 bytes**, native format **326**. Evidence copy in `build/genpassworld/full-20261006-a/world/`.

No fix to the production code was needed. The ModSystem suite passed in **32 groups of checks**; the extra test of saving the loaded proof also passed. The temporary worlds and the fixture package were removed from the emulator, with the preferences restored.

## Coverage

The test is single player. It does not run a full Hardmode conversion nor syncing between devices. The [ModSystem fixture](../modsystemhooks/README.md) covers the additional contracts and the automated tests include subclasses with their own state and isolating an exception in ApplyPass.
