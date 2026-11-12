# Subworld plan

Written 2026-11-09. Goal: dimensions (subworlds) for C# mods, with Bunny
Loader's own API, written from the PC source and the phone dump.

## Status (2026-11-10)

Phases 1 to 3 are written and pass on MuMu and on the phone. The guide for
modders is [24-dimensoes.md](../mods-em-csharp/24-dimensoes.md).

| Pack | Covers | Result |
|---|---|---|
| `cssubworld` | Trip, generation, file, carried data, persistent reload, loading screen, Exit Dimension by a real tap | 24/24 on MuMu and on the phone |
| `cssubworld2` | `Update`, random tile updates off, hidden underworld, temporary file deleted, `SavePlayerChanges` | 8/8 on MuMu and on the phone |
| `cssubmp` | Host on MuMu, client on the phone: accept (by a real tap), Exit with no vote, decline, no answer | host 6/6, client 8/8 |

What the code taught (beyond §2 to §5):

- A small world showed a black sky: `Main.DrawBackground` draws a full-screen
  black box once the "magma layer" (`maxTilesY - 330`) is on screen. While a
  dimension hides the underworld, `NativeApi.HideUnderworldBackground` swaps
  that `sub #330` for `add #1024`, and puts it back on the way out.
- New worlds get random backgrounds, trees and moon in vanilla's "Reset" pass;
  generation calls `RandomizeTreeStyle`, `RandomizeCaveBackgrounds`,
  `RandomizeBackgrounds` and `RandomizeMoonState` first.
- `WorldGen.genRand` is the thread's `Main.rand` on the phone; generation
  seeds it per pass.
- The touch controls prompt shows on every spawn while the player chose
  "Later"; a trip sets `GUIConfigureTouchControlsPopup.DisableCheck` around it.
- Joining a server from code needs the direct session the join menu creates;
  see the memory note on joining by code.
- `UIText` (the native stamp) cut changing texts to the first letter of each
  word; the vote window draws its labels with `Utils.DrawBorderString`.

Open: players in different worlds at the same time are out of scope on the
phone.

### Second pass (2026-11-10)

| Pack | Covers | Result |
|---|---|---|
| `cssubtrans` | Fade out, covered loading, fade in, the underworld layer, water and light at the bottom with a control dimension that keeps the underworld, the two Example Mod dimensions | 23/23 on MuMu |

- **The bottom rows.** `Main.UnderworldLayer` is a getter that 156 places in
  the game call (lighting, liquids, spawns, music, map colours, biome zones),
  never inlined. While a dimension hides the underworld,
  `NativeApi.HideUnderworld` swaps its `sub w0, w8, #200` for `add w0, w8, #0`,
  so the layer is `Main.maxTilesY` and no tile is in the underworld. The
  control in `cssubtrans` (same basin, `HideUnderworld` off) loses 41% of its
  water in 4 s and lights the bottom; with the swap, no water is lost and the
  light is 0. The `UpdateBiomes` hook went away.
- **Transitions.** `ShowLoadingScreen`, `TransitionOutSeconds`,
  `TransitionInSeconds` and `DrawTransition` on `ModSubworld`;
  `Core/SubworldTransition` runs the stages (leaving, covered, arriving) in
  real time. In multiplayer the host and the clients fade out together, the
  clients leave at the end of theirs, the host changes world after it plus the
  usual 0.5 s, and each one fades in once it is back.
- **Drawing over the touch controls.** The phone sorts every batch with
  `renderQueue = 3000 + offset`, the offset counting every batch of the frame
  (about 6000 in a world on MuMu), so everything late in the frame ties at
  Unity's 5000 and the order between batches is lost. Neither drawing after
  `Main.DoDraw` in the interface buffer (`XNAUnityRunner.SetupRenderingUI`),
  nor the magnifier's render layer 10 (drawn by the world camera), nor a z
  offset covered the HUD. The last interface layer does, so the transition
  draws there in the world and after `Main.DoDraw` only in the menu, where
  there are few batches.
- **Vote window.** The portraits are square boxes drawn with `MagicPixel`,
  with a dark fill, and the head's outline is white: the phone's
  `LegacyPlayerRenderer.RenderOutlines` always draws a black ring outside the
  ring of the given colour, so `Color.Transparent` left only the black ring.

### Third pass (2026-11-10)

| Pack | Covers | Result |
|---|---|---|
| `cssubmap` | Beach at the edges with and without `HideOcean`, the fullscreen map of dimensions of 1000×500 to 250×200, a 120×100 dimension refused | 12/12 on MuMu |
| `cssubwld` | The Example Mod arena from `ExampleArena.wld`: size, Example Block and Wall from the `.bl` files, torches, name, difficulty, spawn, no dungeon, death mark, a fresh copy on every trip | 14/14 on MuMu |

- **`.wld` files.** `ModSubworld.WorldFile` names a `.wld` in the mod. The trip
  copies it (through `FileUtilities`, the game's file cache) and every file
  beside it that starts with its name (Bunny Loader's `.tiles.bl`,
  `.walls.bl`, `.npcs.bl`, `.bl.json`...) to the dimension's path, loads it,
  then gives it the dimension's name, an id derived from the main world and
  the dimension, and the main world's difficulty. `cswldmake` built the arena
  with Example Mod blocks and walls; the `.bl` files name mod content as
  `<mod uid>/<name>`, so they load on any device with the same mod.
- **The fullscreen map** on the phone is `GUIMap.DrawMapFullscreenBackground`
  (`Main`'s copy is never called). Its background was the underworld whenever
  the screen was below `maxTilesY - 232` (always, in a world under ~300 tiles
  tall) and the ocean within 380 columns of an edge. Both constants are
  swapped with the rest of the hidden underworld and with `HideOcean`.
- **The map frame.** The fullscreen map draws `TextureAssets.Map` (a backdrop
  with a wooden frame) behind the world, scaled by `maxTilesX / 840` with
  constants only for widths 4200, 6300, 6400 and 8400; any other width draws
  it at its own size, pinned to the map's corner, and its brown border shows
  beside a small dimension. While a dimension is active, that one
  `SpriteBatch.Draw(Texture2D, Rectangle, Color)` call in `GUIMap.DrawMap`
  (found by its target, `NativeApi.HideMapFrame`) is a `nop`.
- **`HideOcean`.** `WorldGen.oceanDepths` (beach zone, ocean music and spawns,
  water settling) counted the 380 columns next to each edge as beach: most of
  a small dimension. With `HideOcean` (on by default) its `b.pl` after the
  height check becomes a `nop`, so it answers `false`.
- **Minimum size 200×160.** The camera stays 40 tiles inside every edge and
  sees up to 120×75 tiles (`Main.MaxWorldViewSize`). In a 120×100 world it
  left the world and `TileLightScanner.ExportTo` read tiles at -81: the game
  crashed. `Enter` refuses smaller dimensions, and a smaller `.wld` sends the
  trip back to the main world.
- **No dungeon.** Generated dimensions kept the main world's `dungeonX/Y`; a
  file kept `0,0`. They start at `-1` now, which keeps the Old Man away. The
  skull in the arena's map was the Example Mod's `ExampleMapLayer`, which drew
  the dungeon icon even with no dungeon; it checks for `-1` now.
- **Death mark.** Each world keeps its own `showLastDeath`/`lastDeathPostion`
  across trips; a temporary dimension forgets its own.

## 1. API

Named like the rest of the C# API (`ModBiome`, `ModSystem`, loaders):

```csharp
public abstract class ModSubworld : ModType
{
    public abstract int Width { get; }
    public abstract int Height { get; }
    public abstract List<GenPass> GenerationTasks { get; }

    public virtual bool Persistent => false;
    public virtual string LoadingText => "Loading Dimension";

    public virtual void OnEnter() { }
    public virtual void OnExit() { }
    public virtual void PostWorldReady() { }
    public virtual void SaveCarriedData(TagCompound tag) { }
    public virtual void LoadCarriedData(TagCompound tag) { }
}

public static class SubworldLoader
{
    public static IReadOnlyList<ModSubworld> Subworlds { get; }
    public static ModSubworld? Current { get; }
    public static bool InSubworld { get; }
    public static bool Traveling { get; }
    public static bool IsActive<T>() where T : ModSubworld;
    public static bool Enter<T>() where T : ModSubworld;
    public static bool Enter(ModSubworld subworld);
    public static bool Exit();
}
```

That is phase 1. Phase 2 adds `SavePlayerChanges`, `VanillaWorldUpdates`,
`HideUnderworld`, `Music`, `Update`, `ModifyGravity` and `ModifyLight`.

`SaveCarriedData`/`LoadCarriedData` carry state between worlds (defeated
bosses, for example) in the `TagCompound` the rest of the API already uses.
Whenever a world is left, every subworld saves its tag; once the next world is
loaded, every subworld gets its tag back. Order on a trip: `SaveCarriedData`,
`OnExit` of the world left, `OnEnter` of the subworld entered, the load or
generation, `LoadCarriedData`, then `PostWorldReady` after the player spawns.

## 2. Mechanism

One world per process: `Main.tile` (`TileData` static pointers), `Main.npc`,
`Main.projectile`, time and events are all static. A subworld is a world swap in
the same process:

1. `gameMenu = true`, loading screen, background thread.
2. Old world: `SaveCarriedData`, `OnExit`. `Player.SavePlayer`,
   `WorldFile.SaveWorld()` (main world, or the subworld if `Persistent`),
   `SystemLoader.OnWorldUnload`.
3. `Main.ActiveWorldFileData` → `<WorldPath>/<main UniqueId>/<Mod>_<Name>.subworld`.
   - File exists: `WorldFile.LoadWorld(false)`. Every Bunny Loader save follows
     the active world path (`.tiles.bl`, `.walls.bl`, `.npcs.bl`, `.bl.json`,
     header), so mod content restores with no extra work.
   - Otherwise: `maxTilesX/Y`, `WorldGen.setWorldSize`, `clearWorld` (on the
     phone it calls `SetWorldSizeOnMainThread` → `TileData.Allocate(x, y)`), then
     `GenerationTasks` with a `GenerationProgress`, as in
     `WorldGenHooks.Hardmode`. `SaveWorld` if `Persistent`, `OnWorldLoad`.
   - `LoadCarriedData`, `PostWorldReady`.
4. Map (`Main.Map.Load`), then the main thread does what the phone's
   `WorldGen.FinaliseOnMainThread` does: `gameMenu = false`, `Player.Spawn`,
   `WorldFile.SetOngoingToTemps`.

The phone's own flow, from the dump: `playWorld` → thread `playWorldCallBack`
(`WorldFile.LoadWorld` + map) → `FinaliseOnMainThread`.

## 3. Hooks

| Behaviour | Hook |
|---|---|
| `Update`, `VanillaWorldUpdates` off | `On_WorldGen.UpdateWorld`, `On_Main.DoUpdateInWorld` |
| `ModifyGravity` | `On_NPC.UpdateNPC_UpdateGravity` + player gravity |
| `HideUnderworld` | `On_Main.DrawUnderworldBackground`, `On_Player.UpdateBiomes` |
| `Music` | `On_Main.UpdateAudio` |
| `ModifyLight` | `On_TileLightScanner.GetTileLight`, only when a mod overrides it (per tile, per frame) |
| Loading screen | `On_Main.DoDraw` during the swap |
| "Exit Dimension" | pause menu (`GUISettingsPauseMenu`), see §5 |
| `SavePlayerChanges` off | `On_Player.SavePlayer` + reload the `.plr` on exit |
| Save & Exit, app killed, quick start | the next Play opens the main world, never a subworld path |
| Delete a world | `On_Main.EraseWorld` deletes `<UniqueId>/` too |

The phone reads and writes world files through `CacheFileManager`: writes go to
memory and reach the disk later from an upload thread, and `Exists` only looks
at the cache. So the `.subworld` file and its `.bak` go through `FileUtilities`
(`Exists`, `Delete`, `CreateDirectory`), and only Bunny Loader's own `.bl`
sidecars, written straight to disk, go through `System.IO`.

The world list (`Main.LoadWorlds`) asks the cache for every file under
`WorldPath` ending in `.wld`, subfolders included. That is why subworld files
end in `.subworld`: they never show up in the list.

## 4. Multiplayer: the group travels together

A server process per subworld does not fit the phone: the host plays in the same
process (`netMode` 3), so the whole session moves together. Rules set by the user
on 2026-11-09:

- **Only the host starts.** A client calling `SubworldLoader.Enter` gets a
  message that only the host opens dimensions.
- **Request to every client.** A dialog with the dimension name, the portrait of
  every player in the session, **Accept** / **Decline**, and a 30 s countdown.
- **Portraits.** The receiving player's own portrait has a yellow border and the
  label "You". Everyone sees each answer live: green border for Accept, red for
  Decline. Our reading: the host's portrait starts green, and one's own border
  turns green or red after answering, keeping the "You" label.
- **Any Decline cancels for everyone.** No answer within 30 s counts as Decline.
- **Nobody accepted:** cancelled, and only the host sees a "Nobody accepted"
  message. The host can send the request again right away (no cooldown).
- **Everyone accepted:** the host saves and sends a travel packet; clients show
  "Loading Dimension", disconnect and reconnect on their own (reusing
  `joinServer` from `boot/QuickStart.cpp`: same IP, port and password); the host
  stops accepting connections until the world is ready, then the clients join
  through the normal join flow. Clients retry for a while, then fall back to the
  menu with an error.
- **Return: the host decides, no vote.** Everyone goes back to the main world.
- A player who joins while the session is in a subworld lands in the subworld.
- A client who leaves during a request is dropped from the list (not a Decline).

## 5. Pause menu and loading screen

- While in a subworld, **"Save & Exit" becomes "Exit Dimension"** with the planet
  icon, for single player and for the host. Clients keep "Save & Exit", which
  leaves the server.
- Icon: `_icons/menu/planeta.png`, 32x32, the user's art. Embedded in libbunny
  with `tools/bin2header.py`, like the bunny head (`BunnyHeadIcon.h`,
  `NativeApi.BunnyHeadTexture`).
- Loading screen text "Loading Dimension" for single player, host and clients:
  the phone's own status screen (`GUIStatusMenu`, menu mode 10) with our text in
  `Main.statusText`. A subworld can change the text with `LoadingText`.
  The save and load threads write `Main.statusText` too ("Saving world",
  "Drawing map"), so the status screen draws our text itself during a trip
  instead of reading the field (`SubworldScreens.StatusDraw`, through
  `StatusMenu_Layout`).
- The phone's `SpriteBatch.Begin` ignores the transform matrix (it only keeps
  `_hasMatrix`), and the UI camera puts (0, 0) at the corner of the safe area.
  `DrawTransition` is stretched over the whole display by moving the vertices
  queued in `SpriteBatcher._batchItemList` after it draws (the full screen comes
  from `XNAUnityRunner.AdjustRealScreenForUI`, whose input has Y going up).
- The "Save & Exit" label is `Lang.mobile[79]` in this build (read by
  `GUISettingsPauseMenu.Draw`); it is swapped only while that menu draws.
- Texts in `assets/localization/game/<culture>.json`, `BunnyLoader.` keys: Loading
  Dimension, Exit Dimension, Accept, Decline, You, Nobody accepted, Only the host
  opens dimensions.

## 6. Phases

1. **Single player core.** API, swap, generate/load/save, loading screen, Exit
   Dimension. Test pack `cssubworld`: a small flat subworld entered by an item;
   save, exit, enter again (loads from file); mod tile, wall, NPC and `ModSystem`
   data survive the round trip.
2. **World hooks.** §3 table, `SavePlayerChanges`, delete with the main world.
3. **Multiplayer.** §4 dialog, packets, reconnect; `tools/tests/run-mp.sh`.
4. **Docs and sample.** tML's ExampleMod has no subworld, so the sample is ours.

## 7. Check first on MuMu

- Small sizes: `TileData`, lighting buffers, `WorldMap.Allocate`, sections of
  200x150, a world smaller than the screen.
- The swap from our own thread versus the game's `ThreadManager.RunForUserCritical`.
- Background autosave (`BackgroundSuspensionCheck`) saving the right world.
- Player portraits on the phone: which renderer draws a head outside the map.
