# tools/: PC tools

None of them run by themselves in the build; they are all called by hand
(except `wiki.py`, which GitHub runs on every push to `main`).

## Development

| Script | What it does |
|---|---|
| [`ui.sh`](ui.sh) | A short UI cycle: builds without the game's assets and `.so` files, installs, opens and takes a screenshot (~6 s). |
| [`dump.sh`](dump.sh) | Regenerates `refs/` from the installed game (pull + extraction + Il2CppDumper). |
| [`dumpgrep.sh`](dumpgrep.sh) | Extracts a class, struct or enum block from `refs/dump.cs`. |
| [`gen-cheatdata.py`](gen-cheatdata.py) | Generates `cheatbridge/bunny/CheatData.java` (the hand-picked NPCs) from `refs/dump.cs`. Items do not go through here: the menu sorts them by the game's own fields. |
| [`build-cheatbridge-dex.sh`](build-cheatbridge-dex.sh) | Compiles the cheat menu to dex and embeds it as a C header. |
| [`bin2header.py`](bin2header.py) | Binary → C array. Used by the script above. |
| [`ui-sprites.py`](ui-sprites.py) | Crops the game's UI textures the menu uses (Journey hours, arrows) and the "?" for a missing mod item (`content/items/UnloadedIcon.h`). Receives the game's `Images` folder. |
| [`mod-art.py`](mod-art.py) | The example mods' covers and icons. |
| [`wiki.py`](wiki.py) | Generates the [wiki](https://github.com/JonataOliveiraa/Bunny-Loader/wiki) from `README.md` and `docs/`. Runs by itself in the `.github/workflows/wiki.yml` workflow on every push to `main`; by hand, only to check. |

```bash
tools/ui.sh                                     # iterate on screen
tools/dump.sh                                   # full pipeline
tools/dump.sh --skip-pull                       # reuses refs/base.apk
tools/dumpgrep.sh Projectile Terraria           # inspect a class
```

There will be no `.bmod` packer: the package is assembled by hand by the
author, with the documentation as a reference.

## Downloading and extracting tModLoader mods

Double-click [`mods/baixar-tmod.bat`](mods/baixar-tmod.bat), paste a Workshop
link and wait. It also accepts IDs and several links on the command line:

```powershell
python tools/mods/tmod.py "https://steamcommunity.com/sharedfiles/filedetails/?id=3810452158"
python tools/mods/tmod.py 3810452158 2824688072
python tools/mods/tmod.py --local "C:/Mods/MyMod.tmod"
```

Requires **Windows and Python 3.10+**. The script sets up portable versions of
the [official SteamCMD](https://developer.valvesoftware.com/wiki/SteamCMD),
[tModUnpacker 1.3](https://github.com/IVogel/tModUnpacker/releases/tag/1.3) and
[standalone 7-Zip](https://www.7-zip.org/download.html) in `build/tmod-tools/`.
The unpacker uses Windows' .NET Framework 4.6.1 or later; it does not need the
.NET SDK. The unpacker release has its SHA-256 pinned in the script. On first
use, SteamCMD's own update can take a few minutes; the script continues
automatically when the bootstrap finishes updating.

Each run creates a new folder in `out/tmod/<ID>/<date-time>-<suffix>/`:

- `tmod/`: the original `.tmod` files, keeping the Workshop's version folders.
- `extraido/`: one folder per package/version; inside it, the unpacker creates
  the folder with the mod's internal name, resources and DLLs. `.rawimg`
  becomes `.png`.
- `steamcmd.log` and `logs/`: download and extraction diagnostics.

Every `.tmod` of the item is extracted. Earlier results are kept. `--local`
only extracts; `--out FOLDER` changes the output; `--cache FOLDER` changes the
cache; `--steamcmd PATH.exe` and `--unpacker PATH.exe` use already installed
tools. `--timeout 900` is the default limit, in seconds, per call. In batches,
a failure is logged and the other items continue; any failure results in exit
code 1.

The download uses anonymous access, like
[tModLoader's server script](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/release_extras/DedicatedServerUtils/manage-tModLoaderServer.sh).
Private, removed, other-game or restricted items may not download. The script
checks SteamCMD's confirmation, the `.tmod`'s structure and hash, and refuses
internal names that would escape the extraction folder.

Extracting does not decompile the C# DLLs and does not convert the mod to
Bunny Loader. The extracted content depends on what the author put in the
package.

Validation tests, without network:

```powershell
python -m unittest discover -s tools/mods -p test_tmod.py -v
```

## Online catalog

The mods the Explore tab downloads live in a separate repository,
[Bunny-Loader-Mods](https://github.com/JonataOliveiraa/Bunny-Loader-Mods), with
no server. Separate from the app's code on purpose: a takedown request against
a mod only takes down the catalog, and the publishing token cannot write to the
code.

| Where | What |
|---|---|
| `main` | `index.json` (the list), `mods/<uid>/` (each mod's showcase: manifest, icon, cover, `description.md`, the authors' photos) and `ports/` (the sources of the ported texture packs). |
| Releases `mod-<id>-v<version>` | The `.bl` of each version. Never marked as *Latest*. |

The app reads everything without logging in (`raw.githubusercontent.com` and
the Release download link) and checks the index's sha256 before installing
([`RemoteCatalog.kt`](../app/src/main/kotlin/dev/bunnyloader/mods/RemoteCatalog.kt)).
There is no token in the APK. Only publishing needs one, on the PC.

When the launcher opens or comes back, the app checks the catalog. During the
fetch, Home and Explore show the spinning icon and the showcases' progress;
the last downloaded list remains available. On Home and Explore, pull down with
the scroll at the top and release to refresh the store. The showcases are
synced with up to four simultaneous operations.

A newer online version of an installed mod opens a popup with **Update**,
**Update all** and **Not now**. Each UID/version is announced once, when the
popup becomes visible; dismissing it keeps the update button on the page. A
later version produces another notice. Downloads continue after closing the
popup, with up to three simultaneous packages and serialized import. Offline,
new versions already known to the cache can also be announced.

**The easy way:** double-click [`mods/gerenciar.bat`](mods/gerenciar.bat) (or
`python tools/mods/manager.py`). It opens a page in the browser
(`http://127.0.0.1:8770`) that shows the mods in `samples/` with each one's
status (up to date, new version ready, changed without bumping the version),
publishes with one click, bumps the version, checks and publishes a dragged
`.bl`, and removes mods from the catalog. The token is pasted into the page and
stays only in the manager's memory. The page only serves this computer and
requires a key randomized on every run.

Underneath, it calls the same command-line scripts.
[`mods/publish.py`](mods/publish.py) checks the package with the app's import
rules, creates the Release, builds the showcase in `build/mods-repo/` (a clone
of the mods repository) and pushes the index:

Only ExampleMod goes inside the APK (`BUNDLED_SAMPLES` in
`app/build.gradle.kts`). The others in `samples/` are published here.
[`mods/pack.py`](mods/pack.py) builds a folder's `.bl`, always with the same
bytes, in `build/mods-packs/`.

[`mods/port_textures.py`](mods/port_textures.py) copies the images of resource
packs from the local Workshop to `build/mods-repo/ports/textures/`, creates
`manifest.json` with authorship and builds the `.bl` files, without requiring
`main.js`. Example: `python tools/mods/port_textures.py 2439853873 2796830227`.
Each port has a README in `ports/textures/<name>/` of the mods repository; the
finished `.bl` files sit in `ports/textures/in bl/`.

The token can come from `BL_GITHUB_TOKEN`; without it, the script asks for it
on the spot, without showing it on screen.

```bash
python tools/mods/pack.py samples/VidaCheia      # -> build/mods-packs/vidacheia-1.0.0.bl
python tools/mods/publish.py --dry-run Mod.bl    # checks and builds, no network
python tools/mods/publish.py Mod.bl Other.bl     # publishes
python tools/mods/publish.py --remove <uid>      # removes from the catalog (the Release stays)
```

Updating a mod means publishing the `.bl` with a higher `version` in
`manifest.json`: the app compares it with the installed one and shows
"Update". Republishing the same version with different content is refused.
`raw.githubusercontent.com` caches for a few minutes, so the change may take a
moment to show up in the app.

## Tests

The tests are **mods**: each folder in [`tests/`](tests) installs into the
game, does what it tests for real (uses the item, summons the boss, saves and
reloads the world) and writes one log line per check, ending in
`<test> FIM: tudo ok` or `<test> FIM: N falha(s)`.

[`bench/run.sh`](bench/run.sh) runs a cycle on the emulator: installs the
requested mods, opens the game, enters the **first** world in the list, waits
for each test's `FIM` and saves the result lines.

```bash
tools/bench/run.sh output.txt samples/ExampleMod tools/tests/boss
BL_DEVICE=127.0.0.1:16416 tools/bench/run.sh output.txt tools/tests/refs   # another emulator
```

[`bench/repeat.sh`](bench/repeat.sh) repeats a round until the game freezes,
for intermittent hangs: it watches for a test's `FIM` in logcat and keeps the
logcat of the round that froze. Delete the test packages left on the device
first.

```bash
tools/bench/repeat.sh 12 modfurniture samples/ExampleMod tools/tests/modfurniture tools/tests/prefix
```

- Preconditions: the APK installed, a world and the test character ("Bench")
  first in the list.
- **One test at a time.** `run.sh` does not remove the mods from earlier
  rounds, and together they get in each other's way (the `hooks` shot kills the
  `npcs` slime): delete the `test-*` ones from `bunny_packs/` between runs.
- The Mod Menu's powers stay saved; `run.sh` clears them before opening.
- **Tombstones:** a player who dies in a test leaves one, and with a few nearby
  the game turns on the graveyard (music, light, fog and spawning change).
  `run.sh` adds `tests/cleanworld` to every round: on entering the world, it
  removes the tombstones (standing and falling) within 200 tiles of the spawn
  and the player and saves the world. `BL_KEEP_GRAVES=1` turns it off (and
  removes its package from the device).
- With more than one emulator, `BL_DEVICE` picks which. `run.sh` only taps the
  screen with Bunny Loader in front (a blind tap on an emulator's home screen
  once installed a game from the store).

| Test | What it checks | Needs the Example Mod |
|---|---|---|
| **The bridge** | | |
| `nullable` | Types, structs, arrays, `Nullable<T>`, hooks with structs. | |
| `optarg` | A loose method of an object (`const f = Main.spriteBatch['...']; f()`) calls on its owner; `null` in a struct with a default value becomes `default(T)` (call and `original()`), required structs and numbers refuse it. | |
| `wrappers` | An object held only by JS survives the collector; identity (`===`). | |
| `structindex` | `proj.ai[0]`, `localAI.get_Item`, `oldPos[i].X`, `hideMisc[i]` and the bounds; a whole 6 KB log line in the file. | |
| `extrafields` | `bl.defineField`, `bl.defineMethod`. | |
| `strictnames` | A member the class does not have is an error (with a suggestion), `in`, engine queries without errors; signatures only with the exact parameter names. | |
| `refs` | `ref`/`out` in calls and hooks, structs by `ref`, a `Ref` passed on and come loose. | |
| `files` | `bl.file`, `bl.directory`, `bl.path`. | |
| `hookslots` | Hundreds of hooks at once, and the depth of chained hooks on the same method. With 25 hooks on the names, the menu lists are slow to accept taps: if it stops at world selection, tap Play. | yes |
| `ifbusy` | A hook that does not wait for the JS engine held by another thread (`ifBusy`). | **no**: run without it |
| `enginethreads` | The game thread and the save thread in `original()` at the same time, each with its own JS stack. | |
| `wrappermap` | Fuzzing `WrapperMap` against `std::unordered_map` (runs as a binary, `wrappermap/run.sh`). | |
| `tilesettled` | Mod tile readiness, including zero registered tiles: keeps quick start from waiting forever without the Example Mod. A standalone binary (`tilesettled/run.sh`), without opening the game or touching saves. | no |
| **Content** | | |
| `moditems` | The mod item tables. | |
| `modsave` | Saving mod items with the mod on and off (run more than once). | |
| `projectiles`, `npcs`, `buffs`, `tiles` | Each kind of mod content. | |
| `trailcache` | Mod projectile trails (`TrailingMode` 0 and 2, `TrailCacheLength`): `oldPos`, `oldRot` and `oldSpriteDirection` at the requested size and filled in, next to vanilla projectiles. | |
| `tilesave` | Three rounds: with the mod, without it and with it again. | |
| `tileautosave` | With the Example Mod: triggers the autosave (`WorldGen.saveAndPlay`, on a thread) and checks every frame that mod tiles do not vanish while it saves. Run together with `tilesave`. | |
| `bossbag` | With the Example Mod: `ExampleBossBag` is openable, opening it through `ItemSlot.TryOpenContainer` spends one and gives the `ItemLoot` (ExampleItem and the boss's coins), and the boss's rules have `NotExpert` and the bag. | |
| `modmount` | `ModMount`: the type after the game's (`MountID.Count`, `Mount.mounts` and `MountID.Sets` grown), the `_Back` texture, and the hooks on a test mount (`SetMount`/`Dismount` with `skipDust` and per-player data, `UpdateEffects`, `UpdateFrame`, `JumpHeight`, `JumpSpeed`, `Draw`). With the Example Mod: the car (two layers, the buff, the balloons) and the minecart (`Cart`, `SetAsMinecart`, the copied `delegations`, mounted by the buff via `BuffID.Sets.MountType`) and the touch mount button (`Player.QuickMount`, through `miscEquips[3]`). At the end it leaves the player mounted on the car, to look at on screen. | |
| `hookchain` | With the Example Mod (`samples/ExampleMod`): the example hook in the grapple slot, launched every 90 frames at the sky; `PreDrawExtras` called. On screen: only the mod's chain, without the game's underneath. From frame 400 on, `PreDrawExtras` true and `PreDraw` false: the game's chain without the hook's head, and `PostDraw` runs. | yes |
| `frametime` | Frame time: the game's `DoUpdate` and `DoDraw` and the interval between frames, at zoom 1, 2 and 0.75, and the most expensive hooks (`bl.hookStats`: calls and JS time per frame), QuickJS collections and the cost of a collection. The summary also goes to the game chat: packaged as a `.bl` (`out/frametime.bl`), it measures on the phone without adb. For comparing the app with and without mods, and versions of it (see `bisect/`). Run with `BL_KEEP_GRAVES=1` and no other packages on the device. | no |
| `bgreset` | With the Example Mod: an always-active scene sets a mod surface background (the example's textures) and logs, every second, the style, the texture and transparency arrays and `LocalUserGameState`. For comparing before and after an event (background, user switch). | no |
| `tooltipdraw` | The mod tooltip and the mod rarity on screen: puts a test item (mod rarity and prefix) in `HoverItem` and asks for `MouseText` for a few frames; checks tModLoader's line names, `ModifyTooltips` (new line, `Hide`, `OverrideColor`), `Pre/PostDrawTooltip(Line)`, `yOffset`, the name color and `GetPrefixedRarity`. With the Example Mod, it then shows `ExampleTooltipItem`; the tooltip stays on screen for about 15 s for a screenshot. | no |
| `oldpos` | The projectile trail read in two ways: `oldPos[i]` and `oldPos.get_Item(i)` (which on mobile returns `ref Vector2`), with `oldRot` and `oldSpriteDirection`. | |
| `hookcost` | The cost of a hook that only forwards to `original()`, on methods called once per frame, measured through `bl.hookStats` and from inside JS. | no |
| `perfloader` | The mod loader's performance: during loading, the loaders' patterns in pure JS; in the world, with the Example Mod, the per-entity cost of each hook with 60 projectiles and 15 mod slimes, and the frame time with and without them. See `docs/historico/DESEMPENHO-LOADER.md`. | no |
| `basecls` | The documentation's base class example (`04-conteudo-novo.md`): the base with `static Autoload = false` and the base outside `Content/` and `Common/` are not registered; the subclasses get in with the base's `SetDefaults` through `super`, the inherited fields and methods, and `instanceof` through the base. | |
| `getcontent` | `Mod.GetContent(Base)` and `ModContent.GetContent(Base)`: the mod's own `ModItem`s in registration order and with `Type`, another base (`ModBuff`, `ModSystem`), nothing from another mod, all mods together and an error with a base that is not a class. | |
| `dupnamesa`, `dupnamesb`, `dupnamesc` | Run together: A and B have a `ModPlayer` and a `ModItem` with the same class (`Abc`). Both register; by name each finds its own; `'mod/Abc'` finds the other's; the save writes one key per mod and the load gives each its own back (A does the round trip and cleans up). C, without `Abc`, gets `undefined`/0 by name and finds both through `'mod/Abc'`. | |
| `itempostupdate` | `ModItem.PostUpdate`: the Example Soul dropped on the ground calls `PostUpdate` with the `WorldItem` (with `Center`), and the hotbar one (slot 48) draws without errors in `GetAlpha`. Cleans both up at the end. | yes |
| `modwall` | `ModWall` with ExampleWall: the type after the game's, `WallID.Count` increased, texture, `SetStaticDefaults` (`Main.wallHouse`), `wallBlend`, the item that places it; `PlaceWall`, `KillWall` with a drop; the map (`wallLookup` and the color); corruption conversion with `WallID.Sets.Conversion.Stone`; with ExampleWallAdvanced, `AnimateWall` switching `Main.wallFrame` and `ModifyLight`; the save takes the wall and the paint into `.walls.bl`. Run twice: the second checks the wall and the paint came back on load and cleans up. | yes |
| `achievements` | The game's achievements with mod ones registered: each one's `_completedCount` equal to the completed conditions (rereading the file doubled it, and already-completed vanilla ones showed up locked), and TIMBER completing through the wood pickup notice. Resets the device's TIMBER. | |
| `menusky` | With the Example Mod: the mod theme's `DrawSunAndMoon` (`flag: 'menu.sky'` filter) out of JS in the world and turned back on when returning to the menus (`SaveAndQuit`). | no |
| `tilename` | With the Example Mod: the mod workbench in the crafting guide, with the name (`Recipe.GetRequiredTileName`, the map's, and the text of `GUICraftGuidePopup.UpdateText` with the `ExampleLamp` recipe) and the icon (`TileID.Sets.CraftingStationItemId`, a mobile-only table). | no |
| `bgwatch` | Diagnostic, changes nothing: logs the surface background state every 10 s and whenever it changes (style, sum of the layers' transparencies — zero is the screen with only the sky —, arrays, `LocalUserGameState`, scene). Made to run on the user's phone with logcat capturing. | no |
| `cloudforce` | Diagnostic: 5 s after entering the world, and every 30 s, half of the clouds become the first mod cloud type, and it logs whether that type's horizon mask (`TextureMaskManager.CloudMasks`) exists. For mobile's horizon drawing, which broke the whole background (see `docs/historico/FUNDO-SUMINDO-NUVEM-DE-MOD.md`). | no |
| `soltos` | Step 13, with its own content and the Example Mod's: `ModCommand` through the chat processor (arguments, alias, `UsageException`, non-mod commands go on, `/help`; the example's `/heal` and `/addtime`), `ModHair` (type and texture, the character creation list, saving by name with 0 in the game's file), `ModCloud` (common in `addCloud` with `OnSpawn`, weight 0, rare in `RollRareCloud`), `ModEmoteBubble` (`NewBubble` with `OnSpawn`, the menu by category, drawing the bubble), `ModAchievement` (registration with a name, a completed condition with `OnCompleted`, `OnNPCKilled`; the example's ManyWormsKilled) and the quest fish (in the list, the Angler's line, outside hard mode it does not roll, `CatchFish` upside down). **Saves the character** (the hair test). | yes |
| `tileframes` | Two rounds: the frame of a mod 3x3 and of a stone one when reopening the world. | |
| `localization` | `ModLocalization.Translate` returns the text (deep key, English fallback), `Language.GetText('Mods.<id>.…')` without an extra step, `Key`/`GetText`/`Exists`, the `{$key}`s (full, relative, from the game, `@n`, circular, in `Register`), the game's categories (`RandomFromCategory`), the `Key$Variant` variant and switching languages (there and back, in the same `LocalizedText`; with the Example Mod, also NPC and item names). | optional |
| `multitile` | Two rounds: the Example Sink (2x2, `TileObjectData`) placed, broken by one cell and by the floor (a single drop), saved and reopened. | yes |
| `modfurniture` | The Example Mod furniture through the game's paths: the house with a mod door, table, chair and torch passes `RoomNeeds`; the door opens and closes (by tap, by itself with the player walking into it, with a town NPC passing, by wire; the goblin warrior breaks it in and the peon knocks it down, dropping the mod door's item); the torch's steady light, campfire, chandelier and lamp; a music box on the floor and equipped; tapping the chair sits; a wire turns the lamp off; the campfire buff. Does not save. | yes |
| `teitems` | Two rounds: a mod item with `SaveData` in the item frame, weapon rack, food platter and jar (through the place-by-hand function), on the mannequin and the hat rack; the frame that drops the item (it falls with the data); the 1st saves the world, the 2nd checks it came back, dismantles and saves again. | |
| `globalhooks` | GlobalNPC, GlobalItem and GlobalProjectile on vanilla NPCs, items and projectiles: `FindFrame`, `CheckDead`, `CanChat`, `GetChat`, `GetAlpha`, boss icon; the shops (`ModifyShop`, the old `SetupShop`, `ModifyActiveShop`, Traveling Merchant); GlobalItem crit and knockback; `CombatLoader`'s hits (item, projectile and NPC against an NPC; hostile projectile and PvP on the player) and `HitModifiers` (`FinalDamage`, `ArmorPenetration`, `SetMaxDamage`, `SetInstantKill`). Does not save. | |
| `opcost` | The cost, in ns, of each bridge access pattern the per-frame hooks use (field, array, nested class, `Vector2.new`, `bl.tiles.typeAt`...), measured in the world. Does not touch the world. | |
| `tileperf` | Frame time with 150 vanilla torches and with 150 mod torches and 10 mod campfires on screen, and the cost of each `ModTile` method per frame. Does not save. | yes |
| `wallperf` | Frame time with 2130 walls on screen (71x30): no wall, vanilla wall, ExampleWall and ExampleWallAdvanced (light, animation, `WallFrame`); the time to place, the cost of each `ModWall` method and of the save with 2130 vanilla and mod walls. Saves the world at the end already without the walls (the dug-out area stays, the same as `tileperf`'s). | yes |
| `armor` | Worn textures (slots, `Count` and tables grown, `AddEquipTexture`, custom `EquipTexture`, `AutoloadEquip`), sets per item and per `GlobalItem`, vanity and shadows, `FrameEffects` (dry and wet costume, Pre/EquipTexture/Update order), `SetMatch` (robe), wings (`WingStats`, the three speeds and `WingUpdate`), mannequin. Puts on and takes off; does not save. | yes |
| `loadorder/A`, `B`, `C`, `D` | Four mods on the same `Player.Update`: they log loading, entering and leaving the chain on the first frame. Reorder with the arrows in Packages and check with `python tools/tests/loadorder/check.py --device SERIAL --expected ACBD`. | no |
| `loadordersort` | Sorting by `dependencies` (`mods/LoadOrder.cpp`) and reading the field from the manifest (`mods/ManifestText.cpp`): a dependency before whoever asks for it, the launcher's order for the rest, missing/cycle/ambiguous id disables the mod, an already sorted list does not change (the sync's). Runs on the PC: `wsl bash tools/tests/loadordersort/run.sh`. | no |
| `armorsave` | Two rounds: puts on and saves the character; then checks the pieces came back, with the drawn slots, and cleans up. | yes |
| `mparmor` | Multiplayer (host and client, `mpa ...`): each side puts on the set, the wings and the beard and sees the other's mod slots and bonus. | no (mp-session) |
| `prefix` | Two rounds: stats, name, tooltip, rolling and categories of the Example Mod prefixes; saves items with a mod prefix in the inventory and in a chest. In the 2nd an extra prefix shifts the numbers, and the save returns them by name. Cleans up at the end. | yes |
| `mpbiome` | Multiplayer (`mpb ...`, ModBiome step B4): the host's biome reaches whoever joins, the client's reaches the host on and off, the host's change reaches the client, and nobody receives `OnEnter`/`OnLeave` for a remote player. | no (mp-session) |
| `mpprefix` | Multiplayer (`mpp ...`): each side places items with a mod prefix and the other checks prefix and stats; the host checks the client's accessory defense. | no (mp-session) |
| `hooks` | The classes' hooks: AI, natural spawning, Bestiary, tooltip, recipe, item use. | |
| `recipes` | Recipes and groups. | |
| `globals` | `GlobalItem`, `GlobalNPC`, `GlobalProjectile` (filter, per-entity instance, `Clone`), per-type drops and globals with the Bestiary, and `ModSystem` (world, update, saved data). Run twice: the second loads what the first saved. | yes |
| `modplayer` | `ModPlayer` (frame, damage, death, saved data). | |
| `modplayerhooks` | Registers every player hook; checks references, mana, times, vetoes, clone, crafting and update and drawing callbacks. `node tools/tests/modplayerhooks/check.mjs` runs the behavior tests and checks signatures using the local dump. | no |
| `npcaitype` | ModNPC `AIType`: a temporary type in the native AI, instances, inheritance, vetoes and GlobalNPC order. `node tools/tests/npcaitype/check.mjs` tests the dispatch; its own runner and the Android rounds are in the [README](tests/npcaitype/README.md). | save copies only |
| `projectilekill` | `OnTileCollide false` regression: death by time and hitting an NPC on the ground still call `OnKill`; `Kill` inside the callback does not recurse. `node tools/tests/projectilekill/check.mjs` checks the lifecycle without the game. | no |
| `pylonmap` | Tapping a mod pylon opens the map in mobile's pylon mode, like the game's: `MapPylonTile`, the framing of the revealed pylons (checked against the game's in 2000 cases), the lines to the mod ones, the progression of the old failure (only the pylon itself was tappable) and the request made in the middle of drawing, which sees the mod ones in the list. `node tools/tests/pylonmap/check.mjs` runs without the game. | no |
| `pylonprobe` | A probe (only logs) of the pylon teleport request: the `_pylons` list in the request and the two "near a pylon" checks. It found that a tap on a vanilla pylon, handled inside the map drawing, did not see the mod ones. Needs the Example Mod. | no |
| `menutheme` | A 2nd title theme (long name, the classic title music) for the Menu Theme row in Settings; logs the title's music and fades in the menus. The taps come from outside. | no |
| `nonfinite` | NaN and infinity in fields: the progression of Ocram's error in the phantom dust (`Main.dust[6000]`). A field that already holds infinity accepts another, as in C#; a new NaN or infinity in a field holding a number is still refused; a `float[]` element the same; a method's `float` argument already accepted it. | no |
| `exmodnew` | The new Example Mod examples in the game (needs it enabled): pylon, tile entity, tree, boss bar, pickup, hook, shortsword and the buff's tip. | no |
| `hairsettings` | `GetHairSettings` uses the `ArmorIDs` and `HairID` Sets: a hat set up in `SetStaticDefaults`, mod hair, masks and negative slots. `node tools/tests/hairsettings/check.mjs` runs the tests without the game. | no |
| `modcontent` | `ModContent` and what stays out of the Mod Menu. | yes |
| `exmod1`, `exmod2` | The whole Example Mod, used. | yes |
| `summons` | Pets, minion and sentry used the way a finger would. | yes |
| `townnpc` | A mod town NPC in four rounds: with the mod, with, without and with again. | yes |
| `townshop` | Chat, shop, happiness, portrait and profile. | yes |
| `townfight` | The town NPC's attack and gore. | yes |
| `boss` | The boss spawning through the summon item. | yes |
| `drops` | Mod item drops through the game's rules; 400 slots on the ground, the first creation and recreation of EmergencyStacking.GroupLookup, and 120 kills in the same frame. Use a test world: it clears the existing drops. | yes |
| `fishline` | The mod rod's line. | yes |
| `biomescan` | The biomes' block count (ModBiome step B0): every game `SceneMetrics._tileCounts` with the new size, the scan's order and frequency, and the exact `ExampleTile` count with 0/39/40/41 blocks. Also logs the measurements (`biomescan medida ...`). | yes |
| `modbiome` (+ `modbiomeb`) | `ModBiome` and `ModSceneEffect` (step B1): `Type` and `ModContent.GetInstance`, `player.InModBiome`, `OnEnter`/`OnInBiome`/`OnLeave` once per change, two biomes at the same time, the scene's music by priority, an `IsBiomeActive` that throws, the same class in two mods and the biome from 40 `ExampleTile`s (`TileCountsAvailable`). Run both folders together. | yes |
| `arrays` | Game arrays through JS: `list.makeGeneric(type)` (C# names, full name, class, helper), `Uint8Array` in a single copy, `Class.newArray(n \| list)`, `System.Array.Reverse` on one of them, `fill`/`empty`/`find` (also on a game field), and the errors. | yes |
| `bgscan` | Measuring the backgrounds (step B5.0): the arrays' sizes (`TextureAssets.Background`, `bgAlphaFrontLayer`...), the order and thread of each background drawing step, and three bands drawn by JS on the far, middle and close layers (check in the screenshot). | yes |
| `modbg` | Mod backgrounds (step B5): `BackgroundTextureLoader`, the style numbers, `Priority` `None` without effect, `BiomeLow` beating the forest (surface and underground, with the 4 textures swapped in `backTexture`/`backTextureValues`) and going back to the game's. Run during the day, in a forest. | yes |
| `modwater` | Mod water (step B6): `ModWaterStyle`/`ModWaterfallStyle` with the numbers after the game's and the tables grown, the water switch through the scene with the fade, `CalculateWaterStyle`, the splash, the rain and the light through the water, and the mod waterfall requested from the game. Makes a pond in front of the player and logs `modwater: tela ...` for the screenshot (the test texture is red). Does not save. | no |
| `modmap` | Mod map background (step B6): a scene's `MapBackground` on the full-screen map (the texture covers the screen instead of the game's background, with the sky color at the surface), `MapBackgroundFullbright`, `MapBackgroundColor` and going back to the game's background. Opens the map zoomed out (the world's edge in view) and logs `modmap: tela ...` for the screenshot (the test texture is red). Does not save. | no |
| `modspawn` | Natural spawning as in tModLoader: `GlobalNPC.EditSpawnRate` (the target player, the game's rate, and the forced one applies), `EditSpawnInfo` before `EditSpawnPool` at the same point, `SpawnChance` with the point, the ground, the block and the `NPC.Spawner` fields, the pool with only the mod one, empty and with a vanilla type (`GlobalNPC.SpawnNPC` after), `EditSpawnRange` in `GetSpawnArea`'s area, and the game's spawn unchanged. Clears the nearby enemies: **do not run together with `hooks`** (it removes the NPC that one watches). Does not save. | no |
| `biomecontent` | The rest of ModBiome: `DisplayName`/`TownNPCDialogueName` (default, from the class per culture and from the Example Mod's `Localization`), `SetBiomeAffection` with a mod biome (the Guide, with a house in place, charges less and the line mentions the biome), the Bestiary (the `SpawnModBiomes` biome in the entry, the 30 x 30 icon, the 115 x 65 background and the filter; and those of ExampleSlimeNPC and ExamplePerson, who also loves the example biome), `GlobalNPC.EditSpawnFlags` and `SpawnCondition` (they add up to 1 at the point). Needs the Example Mod. Does not save. | no |
| `cleanworld` | Not a test: removes the tombstones (block 85 and the ones still falling) within 200 tiles of the spawn and the player, on the 2nd frame inside the world, and saves the world. `run.sh` adds it to every round (`BL_KEEP_GRAVES=1` turns it off). | yes |
| `biomeworld` | The flags' lifecycle (step B2): the block-based biome turns on and off on `Teleport` itself, and leaving the world (`WorldGen.SaveAndQuit`) calls `OnLeave`, clears the flags and the scene and calls `ResetNearbyTileEffects`. **Leaves the world at the end**: run it alone. | yes |
| `biomemusic` | The scene's music against the game's, tier by tier (step B3): surface, silence, `Music -1`, `Priority None`, slime rain, a vanilla boss (Queen Slime), the game's music box, Otherworld, and a mod track playing and stopping. With "No enemies" on in the Mod Menu, the boss disappears and the test fails. | yes |
| `exmodfixes` | Example Mod fixes through the game's path: the grapple button (`QuickGrapple`), `Item.material`, the gem block's light, living fire on living fire, the origin and the **real tap** on the clock (against the game's), the 1.4.5 set (`ArmorSetBonuses`), shield/boots of the same type and the swap by tap, and the whip (a randomized curve, and a hit off the line of sight against the thorn one). The clock step waits for an `adb shell input tap` at the coordinates it logs. | yes |
| `sounds` | `SoundStyle`, `SoundEngine.PlaySound`, `MaxInstances`, a used item's `UseSound`. | yes |
| `soundfilter` | SoundLoader's native filter: JS regressions and an A/B comparison on Android, with vanilla, mixed and custom loads, music and 32 NPCs. Uses its own runner; see the [README](tests/soundfilter/README.md) and the [results](tests/soundfilter/RESULTADOS.md). | save copies only |
| `soundasync` | A bounded SFX queue outside QuickJS: states/failures, cancellation, concurrent pause, retention and an Android A/B with normal and extreme load and bursts. Shares soundfilter's runner with `--suite soundasync`; see the [README](tests/soundasync/README.md). | save copies only |
| `launcherperf` | Launcher loading: cache/concurrency, GIFs and per-uid invalidation, with an A/B comparison of Explore, Settings and the page on Android. See the [README](tests/launcherperf/README.md) and the [results](tests/launcherperf/RESULTADOS.md). | restores preferences/cache; does not start the game |
| `music` | The boss swapping the game's music for its own, each switch checked in the middle of the fade. | yes |
| `crossmod` (+ `crossmodtarget`) | `ModLoader.TryGetMod` and `Call`, in both load orders. | |
| `autoload` (+ `nomodclass`) | The mod's structure: the `export default` `Mod` class (`Load`, `bl.mod`), automatic registration of `Content/` and `Common/` (order, `Autoload = false`, what is not mod content), the mirror texture and by name in `Assets/Textures/`, gore, localization, and a mod without the `Mod` class that does not load. | |
| **Multiplayer** | | |
| `serversync` | The mods' exact version between host and client: the JS handshake, native `manifest.json`/`mod.json` metadata, Kotlin planning/admission/session and two Android instances. Its own runner; see the [README](tests/serversync/README.md) and the [results](tests/serversync/RESULTADOS.md). | save copies only |
| `mprecipes`, `mpplayer`, `mpitems`, `mptiles`, `mpglobals`, `mpnet` | A client and a host (see `mp-session.sh`): mod items used by the client and checked on the host, the dash with a real tap. | yes |
| `mpshop` | A mod town NPC's shop in multiplayer: the host places the Person next to the client; the client chats, taps Shop and buys with coins and with a custom currency (and drops the item into the inventory, like a finger); the host checks the chat, the items and the payment over the network, and that the Person keeps its max life. | yes |

[`mp-session.sh`](mp-session.sh) starts a multiplayer session between two
MuMu instances: one hosts, the other joins, with no new enemies and during the
day.

## Measurement and investigation

| Folder | What it is |
|---|---|
| [`bench/`](bench) | The JS → IL2CPP bridge benchmark mod (each operation 20 thousand times, best of 7) and `compare.py`, which compares rounds. The numbers are in the [cost guide](../docs/mods/03-custo-e-desempenho.md) and in the [optimization history](../docs/historico/PONTE-OTIMIZACAO.md). |
| [`bisect/`](bisect) | `buildat.sh <commit>` builds an old commit's APK in a worktree outside the repository (QuickJS, assets and the game's `.so` files linked through junctions and undone before deleting) and leaves it at `out/bisect/app-<commit>.apk`: the binary search for a regression. One build at a time. Careful: MuMu only has a 60 Hz screen, and another instance running on the PC distorts the measurement. |
| [`crash-trials/`](crash-trials) | Creates worlds in a row on MuMu, restarting the app on each attempt, and counts crashes ([history](../docs/historico/AVALIACAO-PONTE-E-CRASH.md)). |
| [`disasm/`](disasm) | Annotated disassembly of `libil2cpp.so` (`prep.sh` generates the caches from `refs/`). Compiled limits: `scan_limits.py` (`cmp #N`), `scan_loops.py` (end of a loop in bytes), `scan_movs.py` (`new T[N]`) and `scan_halved.py` (the drop's `0 < x < N` done by halves). See [content under the hood](../docs/nucleo/conteudo.md#the-compiled-limits). |
