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
each under `<mod uid>/<class name>`.

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
load (`KEEPSAVES=1`).

## Drawing

| Hook | When it runs on the phone |
|------|---------------------------|
| `ModifySunLightColor(ref Color tileColor, ref Color backgroundColor)` | After the game picks the sky and tile colors (`Main.ApplyColorOfTheSkiesToTiles`), outside the title menu. What you leave in the two colors is what the frame uses. |
| `ModifyLightingBrightness(ref float scale)` | After `Lighting.UpdateGlobalBrightness`. `scale` starts at 1 and multiplies the global brightness: 0.5 halves it. |
| `ModifyScreenPosition()` | After the camera moves (`Main.DoDraw_UpdateCameraPosition`), after every `ModPlayer.ModifyScreenPosition`. Change `Main.screenPosition` here. |
| `ModifyTransformMatrix(ref SpriteViewMatrix Transform)` | In `Main.DoDraw`, with `Main.GameViewMatrix`. Replacing the matrix replaces the game's. |
| `PostDrawTiles()` | After walls, tiles and NPCs are drawn and the sprite batch is ended, before the projectiles. Begin and end your own batch. |
| `PostDrawInterface(SpriteBatch spriteBatch)` | At the start of the mouse text layer, the last interface layer, with the batch already begun. |

`ModifyInterfaceLayers` comes with the UI.

## The map

`PreDrawMapIconOverlay(IReadOnlyList<IMapLayer> layers, MapOverlayDrawContext mapOverlayDrawContext)`
runs before the map icons are drawn, on the minimap, the overlay and the
fullscreen map. Every layer starts visible; set `layer.Visible = false` to
skip it in this draw. The game's list is left as it was afterwards.

`PostDrawFullscreenMap(ref string mouseText)` runs after the icons of the
fullscreen map, with the batch begun. Set `mouseText` to show a hover text.

## Networking

`NetSend(BinaryWriter writer)` and `NetReceive(BinaryReader reader)` carry a
system's data with the world data (message 7), from the server to each
client, as in tModLoader. The data goes in a C# packet right before the
message, and the client reads it right after the game applies the world
data. Override both. `writer.WriteFlags(...)` and `reader.ReadFlags(out ...)`
(`Terraria.ModLoader.IO.BinaryIO`) pack up to eight `bool`s in one byte:

```csharp
public override void NetSend(BinaryWriter writer)
{
    writer.WriteFlags(downedMinionBoss);
}

public override void NetReceive(BinaryReader reader)
{
    reader.ReadFlags(out downedMinionBoss);
}
```

`HijackSendData(...)` runs before every message the game sends in
multiplayer; return `true` to stop it. `HijackGetData(ref byte messageType, ref BinaryReader reader, int playerNumber)`
runs before every message the game reads: the reader starts after the type
byte; return `true` to keep the game from reading it, or change
`messageType` or give another `reader` to make the game read something else.
`HijackSendData` is skipped in singleplayer, as in tModLoader.

The `csmodsystem2` pack checks the drawing and map hooks, the world data
round trip and the hijacks on one device. Two devices (a client really
receiving `NetReceive`) are not tested yet.
