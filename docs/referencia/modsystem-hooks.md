# ModSystem: generation, updates and mobile integration

Implementation checked against `exmod_tmod/tModLoader/patches/tModLoader/Terraria/ModLoader/ModSystem.cs` and `refs/dump.cs`. The current local signature of `ModifyWorldGenTasks` only receives `tasks`.

## Generation steps

```javascript
export class OreSystem extends ModSystem {
    ModifyWorldGenTasks(tasks) {
        const index = tasks.findIndex(pass => pass.Name === 'Shinies');
        if (index < 0) return;
        tasks.splice(index + 1, 0, new PassLegacy('My ore', (progress, configuration) => {
            progress.Message = 'Generating ore';
            progress['void Set(double value)'](1);
        }, 1));
    }
}
```

The example illustrates the insertion. Add the generation inside the callback, using the block type, the native generation API and the world's bounds.

`tasks` is a JavaScript array of `GenPass`. The native steps are also represented by this class. You can insert, delete, reorder, change `Weight` and call `Disable()`/`Enable()`. The loader validates the types and weights before replacing the native list. An invalid list is reported and the native list remains available.

`PreWorldGen()` runs before the steps execute, after the game builds the list. `PostWorldGen()` runs when `WorldGenerator.GenerateWorld()` returns `true`, including the execution of the extra steps. A cancelled generation does not call the post-hook.

`GenPass` accepts a name and a weight; subclasses implement `ApplyPass(progress, configuration)`. `PassLegacy` accepts a name, a function and an optional weight, defaulting to 1. The callback's arguments are the native `GenerationProgress` and `Terraria.IO.GameConfiguration` objects. A disabled step does not run. JavaScript exceptions from a step are reported without stopping the other steps.

## Hardmode

On mobile, the conversion is concentrated in `WorldGen.initializeHardMode()`. The initial list of `ModifyHardmodeTasks(tasks)` contains **Hardmode Conversion**, which runs that native routine. Add steps before or after it, or disable it. tModLoader's separate `Hardmode Good`, `Hardmode Evil` and `Hardmode Walls` names do not exist in this adaptation; look for the mobile step defensively. Their callbacks receive `null` for progress and configuration, like the local Hardmode steps.

## Updates and drawing

| Method | Contract / mobile point |
| --- | --- |
| `UpdateUI(gameTime)` | After `Main.UpdateUIStates`, inside the world; includes the UI update while paused. |
| `PostUpdateInput()` | After `Main.DoUpdate_HandleInput`. |
| `PreUpdateEntities()` | Entry of `Main.DoUpdateInWorld`, before the groups. |
| `PreUpdatePlayers()`, `PostUpdatePlayers()` | Edges of the player loop. |
| `PreUpdateNPCs()`, `PostUpdateNPCs()` | Edges of the NPC group. |
| `PreUpdateGores()`, `PostUpdateGores()` | Edges of the gore loop. |
| `PreUpdateProjectiles()`, `PostUpdateProjectiles()` | Before and after the projectile group. |
| `PreUpdateItems()`, `PostUpdateItems()` | Edges of the `WorldItem` loop. |
| `PreUpdateDusts()`, `PostUpdateDusts()` | Before and after `Dust.UpdateDust`. |
| `PreUpdateInvasions()`, `PostUpdateInvasions()` | Before and after `Main.UpdateInvasion`, when the native routine runs. |
| `ModifyTimeRate(timeRate, tileUpdateRate, eventUpdateRate)` | Three independent `Ref`s. The clock uses `double`; tiles and events accumulate fractions between ticks to feed mobile's integer fields. Non-finite, negative values or values above `Int32.MaxValue` keep the game's value. |
| `ModifySunLightColor(tileColor, backgroundColor)` | Two `Ref<Color>`s after the native calculation of the sky and tile colors. |
| `ModifyLightingBrightness(scale)` | `Ref<float>` after `Lighting.UpdateGlobalBrightness`; non-finite and negative values are ignored. |
| `ModifyScreenPosition()` | After the native camera calculation and ModPlayer, inside the world. Change `Main.screenPosition`. The hook is shared and the order does not depend on registration. |
| `ModifyTransformMatrix(transform)` | `Ref<SpriteViewMatrix>` after the native zoom, before the matrix is consumed in drawing. |

The callbacks walk override lists computed when systems are registered. The native points inside the loops preserve the registers and check four of the binary's instructions before installing. A system with no update overrides does not install those points. The group callbacks run once per group, not once per entity.

## Content, world and network

| Method | Contract |
| --- | --- |
| `ResizeArrays()` | Native content ready, before the mods' setup phases. |
| `OnLocalizationsLoaded()` | After applying the mods' JSONs, initially and when the language reloads. |
| `PostSetupRecipes()` | After every recipe, `PostAddRecipes` and rebuilding the caches. |
| `OnModUnload()` | Once on a normal exit or on the JS engine's shutdown. A forced termination by the system does not run callbacks. |
| `SaveWorldHeader(tag)` | A `TagCompound` separate from the full data, in `<world>.wld.bl.header.json`. Keeps entries from missing mods. No cloud persistence. |
| `CanWorldBePlayed(playerData, worldData)` | Any `false` prevents selection/loading. Receives `PlayerFileData` and `WorldFileData`. |
| `WorldCanBePlayedRejectionMessage(playerData, worldData)` | The message from the first system that blocked. Empty text gets a default message. |
| `HijackGetData(messageType, reader, playerNumber)` | Native `Ref<byte>` and `Ref<BinaryReader>`. The reader's position goes back to 1 between systems. Any `true` cancels the whole packet. `playerNumber` is `MessageBuffer.whoAmI`. Swapping the reader copies its payload into the mobile routine. |
| `HijackSendData(whoAmI, msgType, remoteClient, ignoreClient, text, number, number2, number3, number4, number5, number6, number7)` | Any `true` cancels `SendData` and the entity data envelope. `whoAmI` is the buffer index: `remoteClient` on the server's targeted send and 256 in the other cases. `TrySendData` also goes through `SendData`. |

Read the header without loading the world's full data:

```javascript
const header = new Ref();
if (worldData.TryGetHeaderData(MySystem, header)) {
    const enabled = header.value.GetBool('enabled');
}
```

`TryGetHeaderData` also accepts the system's instance. When moving a world by hand, take both auxiliary files, `.bl.json` and `.bl.header.json`.

## Dropped method

`RequiresScreenTarget` was not exposed. The mobile renderer uses Unity, does not call `FilterManager.CanCapture` and has no `Main.finalScreenTarget`; returning `true` from this method would not enable a compatible screen capture.

## Validation

See the [test suite and its limits](../../tools/tests/modsystemhooks/README.md). Besides the synthetic generator, the [full generation test](../../tools/tests/genpassworld/README.md) created a 4200 × 1200 world through the normal `worldGenCallback` thread, keeping the 106 native steps. Direct GenPass subclasses and a PassLegacy ran in the expected order; the structure's 35 blocks persisted after saving, restarting the process and loading the world. The full Hardmode conversion was not run. This round's network tests are not equivalent to a multiplayer session between devices.
