# World generation

`ModSystem` can edit the mobile game's generation passes without replacing its terrain generator. The callbacks run on the game's generation worker. Use `WorldGen.genRand` for world generation randomness and avoid UI or rendering operations in a pass.

## Insert a pass

```csharp
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.Generation;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

public sealed class OreSystem : ModSystem
{
    public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
    {
        int index = tasks.FindIndex(pass => pass.Name == "Shinies");
        if (index < 0)
            return;

        tasks.Insert(index + 1, new PassLegacy("My ore", (progress, configuration) =>
        {
            progress.Message = "Generating my ore";
            int count = (int)(Main.maxTilesX * Main.maxTilesY * 0.00006);
            for (int i = 0; i < count; i++)
            {
                int x = WorldGen.genRand.Next(Main.maxTilesX);
                int y = WorldGen.genRand.Next((int)GenVars.worldSurfaceLow, Main.maxTilesY);
                WorldGen.TileRunner(x, y, 4, 4, ModContent.TileType<MyOre>());
                progress.Set((i + 1d) / count);
            }
        }, 100));
        totalWeight += 100;
    }
}
```

`MyOre` is a registered `ModTile`. The complete `ExampleModCSharp` example includes `ExampleOreSystem`, an `ExampleOrePass` subclass, the ore tile, its placing item and the original ExampleMod textures. It inserts the ore after the native `Shinies` pass using the ExampleMod distribution and weight.

The `ref double totalWeight` overload follows the requested tModLoader 1.4.4 contract. The local tModLoader checkout also uses `ModifyWorldGenTasks(List<GenPass> tasks)` without the weight parameter. Both are available: the default implementation of the weight overload calls the list-only overload and adjusts the total by the difference in pass weights. Override one overload per system. When overriding the weight overload, update the weight explicitly after adding or removing passes.

## Passes and progress

Subclass `Terraria.WorldBuilding.GenPass` and implement `ApplyPass(GenerationProgress progress, GameConfiguration configuration)` for a pass with its own state. `Terraria.GameContent.Generation.PassLegacy` takes a `WorldGenLegacyMethod` delegate instead. Both use the native generation pipeline.

`PassLegacy(name, method)` uses the mobile game's default weight table for that name. Use `PassLegacy(name, method, weight)` to choose an explicit weight.

`Name` and `Weight` can be changed. Weights must be finite and non-negative. `Disable()` preserves the pass in the list while preventing its execution; `Enable()` reverses it. Removing or moving entries edits the actual native pass list. Check for a pass's existence before using its index because other mods can change the list.

`GenerationProgress` exposes the native `Message`, `Value`, `TotalProgress`, `TotalWeight`, `CurrentPassWeight`, `Set`, `Start` and `End`. Normal world generation starts and ends each pass; a custom pass normally changes only its message and calls `Set` with its fraction complete.

`GameConfiguration` wraps the game's native per-pass configuration. `Get<T>(string entry)` reads JSON-compatible values through the native configuration root and deserializes them into C# values. A missing entry raises `KeyNotFoundException`. This cold generation API creates a JSON snapshot for each lookup; read settings once before an inner generation loop.

`PreWorldGen()` runs before pass editing. `PostWorldGen()` runs only when the native generator reports completion. Exceptions from an individual system or custom pass are logged with an English hook label. A rejected task list restores its previous entries and total weight; mutations to properties on an existing pass are not rolled back.

## Hardmode

```csharp
public override void ModifyHardmodeTasks(List<GenPass> list)
{
    list.Add(new PassLegacy("My Hardmode step", (progress, configuration) =>
    {
        progress.Message = "Updating my ore";
    }));
}
```

The phone has one `WorldGen.initializeHardMode` entry point rather than the tModLoader conversion task list. The initial list therefore contains a single `Hardmode Conversion` pass that executes the original mobile method. Insert passes before or after it, or disable it. This API does not expose the internal good/evil/wall conversion stages as separate passes.

Hardmode custom passes receive a native `GenerationProgress` and a default, null `GameConfiguration`; check `configuration.IsNull` before accessing configuration there. The integration test disables the original conversion and verifies list editing and execution order. A complete Hardmode conversion is outside that test.

## Mod chests during cleanup

The native `Tile Cleanup` pass rewrites `TileID.Sets.BasicChest` tiles into vanilla chest types. The world generation adapter snapshots mod chest types from `Main.chest` before that pass and restores their active 2x2 cells afterward. Its name, weight and enabled state are preserved. A mod replacing the cleanup pass is responsible for its replacement's chest behavior.

This preserves chest tile types during generation; interactive mod chest behavior, chest networking and tile entities belong to the following phase-5 steps.

## Validation

Build the runtime and test pack, install the matching APK on the reserved device, then run:

```sh
bash tools/tests/build-cs.sh csworldgen
DEVICE=127.0.0.1:16416 bash tools/tests/csworldgen/run.sh
```

The runner enables `csworldgen` and `csauto` without `csenv`, selects the test character and creates a uniquely named small world through the normal mobile callback. It preserves the terrain passes and waits for generation and saving. Keep the MuMu window visible throughout the run.

The test covers subclass and delegate passes, pass editing, disabling a pass, native arguments and worker execution, total weight, callback order, `PostWorldGen`, Hardmode list editing, mod chest cleanup and a marker in a validated saved world. See [the fixture](../../tools/tests/csworldgen/README.md) for recorded results and limits.
