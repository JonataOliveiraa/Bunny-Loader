# Systems: `ModSystem`

A `ModSystem` holds code that belongs to the mod as a whole, not to one item
or NPC: world-wide data, counters, events, things that run every frame. The
class is created once and loaded like any other content: `Load()`, then the
hooks below. The names, parameters and order are tModLoader's.

```csharp
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace MyMod.Common.Systems;

public class MeteorCounter : ModSystem
{
    public static int Meteors;

    public override void ClearWorld() => Meteors = 0;

    public override void SaveWorldData(TagCompound tag)
    {
        if (Meteors > 0) tag["meteors"] = Meteors;
    }

    public override void LoadWorldData(TagCompound tag) => Meteors = tag.GetInt("meteors");

    public override void PostUpdateWorld()
    {
        if (Main.rand.NextBool(100000)) Meteors++;
    }
}
```

Each hook installs its game hook the first time a loaded system overrides
it. A mod with no `ModSystem`, or one that only overrides `AddRecipes`, adds
nothing to the frame.

## Loading

| Hook | When |
| --- | --- |
| `OnModLoad` | right after `Mod.Load()`, still while the mod loads |
| `ResizeArrays` | when every type is in the game, before `SetStaticDefaults` |
| `OnLocalizationsLoaded` | at the same point, and again whenever the player changes the language |
| `SetStaticDefaults`, `PostSetupContent`, `AddRecipeGroups`, `AddRecipes`, `PostAddRecipes` | see [the load order](09-conteudo.md#the-order) |
| `PostSetupRecipes` | after every mod's recipes |
| `OnModUnload` | when the game closes (`Main.QuitGame`) |

## The world

| Hook | When |
| --- | --- |
| `ClearWorld` | the game clears the world: on entering one, on generating one, and on a multiplayer client |
| `CanWorldBePlayed` / `WorldCanBePlayedRejectionMessage` | in the world list and when the world opens; `false` refuses this character and world, and the message goes to the log |
| `OnWorldLoad` | after the world loads, **before** `LoadWorldData`; on a multiplayer client, after `ClearWorld` (the world comes from the server, there is no load) |
| `LoadWorldData` | the data saved by `SaveWorldData`; an empty `TagCompound` when there is none |
| `SaveWorldData` | at every world save, after the game's |
| `SaveWorldHeader` | at the same moment: small data read without opening the world, through `worldFileData.TryGetHeaderData<MySystem>(out TagCompound tag)` |
| `PreSaveAndQuit` | before "save and quit" |
| `OnWorldUnload` | after leaving the world |

The data goes into `<world>.wld.bl.json` and the header into
`<world>.wld.bl.header.json`, next to the world and never inside the `.wld`,
each under `<mod uid>/<class name>`. This is the same file and key as the JS
`ModSystem`: a mod ported from JS keeps its worlds' data.

Cloud worlds aren't saved there. World generation (`PreWorldGen`,
`ModifyWorldGenTasks`, `PostWorldGen`, `ModifyHardmodeTasks`) comes with the
tiles phase.

## Every frame

In one frame of the world, in this order:

1. `PreUpdateEntities`
2. `PreUpdatePlayers`, the players, `PostUpdatePlayers`
3. `PreUpdateNPCs`, the NPCs, `PostUpdateNPCs`
4. `PreUpdateGores`, the gores, `PostUpdateGores`
5. `PreUpdateProjectiles`, the projectiles, `PostUpdateProjectiles`
6. `PreUpdateItems`, the dropped items, `PostUpdateItems`
7. `PreUpdateDusts`, the dust, `PostUpdateDusts`
8. `PreUpdateTime`/`PostUpdateTime`, `PreUpdateWorld`/`PostUpdateWorld`, `PreUpdateInvasions`/`PostUpdateInvasions`
9. `PostUpdateEverything`

`UpdateTime` and `UpdateWorld` run only in singleplayer and on the server, as
in the game. `UpdateUI(GameTime)` runs at the end of each update, with the
frame's `GameTime`, outside the title menu. `PostUpdateInput` runs after the game reads the input.

The player, NPC, gore and item stages are native hooks in the middle of
`Main.DoUpdateInWorld`, checked against the phone build's instructions; on a
different build they are left out and the log says so.

## Time and nearby tiles

`ModifyTimeRate(ref double timeRate, ref double tileUpdateRate, ref double eventUpdateRate)`
changes how fast the clock, random tile updates and events go, in this
frame. `timeRate` affects only the clock; fractions of the other two
accumulate over frames.

`ResetNearbyTileEffects()` and `TileCountsAvailable(ReadOnlySpan<int> tileCounts)`
come with the game's scan around the local player: the reset first, then the
counts per tile type. Keep the numbers you need: the span is only valid
during the call. The reset also comes when leaving the world and when
entering another.

The `csmodsystem` pack checks these hooks, including a save and a second
load (`KEEPSAVES=1`). Drawing (`ModifySunLightColor`,
`ModifyLightingBrightness`, `ModifyScreenPosition`, `ModifyTransformMatrix`,
`PostDrawTiles`...) and networking (`NetSend`, `NetReceive`, `HijackGetData`,
`HijackSendData`) come in the next step; `ModifyInterfaceLayers` comes with
the UI.
