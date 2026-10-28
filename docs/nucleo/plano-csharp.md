# The C# plan: every loader in C#, then no more JavaScript

This is the plan Bunny Loader follows until **every mod loader runs in C#** and
**QuickJS, the JS bridge and the JS loaders are removed**. It is a working
document: each phase has a checklist that gets ticked as it lands, and the
decisions it records are the ones every phase must respect.

Status on 2026-10-28: the C# runtime (Mono, generated proxies, `On_<Type>`
hooks, mods compiled from sources) and a basic `ModItem` loader are done
(commit `9f60ef4e`), and phase 0 has started: the API is in
`Terraria.ModLoader`, with `Mod.Logger`, and C# mods load in tModLoader's
order up to `AddRecipes`, with `ModContent` (`Find`, `Request<Texture2D>`, files)
and `ModLoader` (`TryGetMod`, `Call`, C# dependencies), translations in
tModLoader's hjson, `Item.ModItem`, `GlobalItem`, saved item data and `ModPacket`. Everything else below is still to do.

## 1. The rule: look like tModLoader

A mod written for Bunny Loader in C# must read like a tModLoader mod. Porting
from tModLoader should mean copying the class, fixing what the mobile game
really lacks, and nothing else.

- **Same names, same places.** Public classes live in tModLoader's namespaces
  with tModLoader's names and member signatures: `Terraria.ModLoader.ModItem`,
  `Terraria.ModLoader.IO.TagCompound`, `Terraria.ModLoader.ModContent`,
  `Terraria.Localization.LocalizedText`... When the mobile game already has the
  type (`TextureAssets`, `LocalizedText`, `Language`, `Filters`, `SkyManager`,
  `Asset<T>`), the mod uses the game's own type through the generated proxy.
- **No `bl` object.** tModLoader has no global helper object, and the C# API
  will not have one either. What the JS `bl.*` does today splits in two:
  1. **Plumbing** (registering types, growing tables, hook flags, `typeOf`,
     `addressOf`, `classOf`...). In tModLoader this is the `internal` part of
     each loader (`ItemLoader.Register` and friends). In C# it is `internal`
     code in `BunnyLoader.dll` plus the native table (`NativeApi`). Mods never
     see it.
  2. **What mods call.** Each piece maps to where tModLoader puts it:

     | JS today | tModLoader / C# |
     |---|---|
     | `bl.log` | `Mod.Logger.Info/Warn/Error/Debug` |
     | `bl.loadTexture`, `bl.loadTextureAsset`, `bl.textureSize` | `ModContent.Request<Texture2D>(path)`, `Mod.Assets.Request`, `asset.Value.Width` |
     | `bl.vanillaTexture`, `bl.builtinTexture` | `TextureAssets.Item[type]` and the other `TextureAssets` arrays |
     | `bl.readJson`, `bl.file` | `Mod.GetFileBytes`, `Mod.GetFileStream`, `Mod.FileExists` |
     | `bl.tiles.typeAt` | `Main.tile[x, y].TileType`, `Framing.GetTileSafely` |
     | `bl.items/npcs/projectiles/... .typeOf` | `ModContent.ItemType<T>()`, `ModContent.Find<T>("Mod/Name")`, `Mod.Find<T>` |
     | `bl.sounds`, `bl.music` | `SoundEngine.PlaySound`, `MusicLoader.GetMusicSlot`, `MusicLoader.AddMusic` |
     | `bl.screen` | `Filters.Scene`, `SkyManager`, `ScreenShaderData` (section 4, phase 7) |
     | `bl.chat` | `Main.NewText`, `ChatHelper.BroadcastChatMessage` |
     | `bl.onContentReady` | `ModSystem.PostSetupContent`, `Mod.PostSetupContent` |
     | `bl.info.terrariaVersionCode` | `BuildInfo`, `Main.versionNumber` |
     | `bl.addressOf`, `bl.box/unbox`, `bl.gc`, `bl.yield`, `bl.hook*` | Nothing: they exist only because of the JS bridge |
     | `bl.menu.*` (Mod Menu folders, hiding) | Bunny Loader only (section 1.1) |

### 1.1 What tModLoader does not have

Some features are Bunny Loader's own: the Mod Menu (cheat catalog), touch
buttons, screen effects, texture and font packs, the mod page. They keep
working, with three constraints:

- When tModLoader has a close concept, use its name: touch buttons are
  `ModKeybind` (with the mobile layout as extra members), not a new class.
- What has no counterpart lives in the `BunnyLoader` namespace, so a reader
  always knows it is not tModLoader: `BunnyLoader.ModMenu`, `BunnyLoader.Screen*`.
- Extra members on a tModLoader class are allowed when they only add (for
  example `ModItem.HideFromModMenu`), never when they change what an existing
  member means.

### 1.2 Where the game itself differs

The proxies are the mobile game (1.4.5.8.6), not tModLoader's patched Terraria.
Where tModLoader added members to vanilla types (`Item.ModItem`,
`Item.DamageType`, `Item.ResearchUnlockCount`, `NPC.ModNPC`, `Player.GetModPlayer<T>()`,
`Item.CloneDefaults`...), Bunny Loader adds them to the proxy structs, which are
`partial`, with the same name and behavior. Where the mobile game lacks a type
tModLoader assumes (`ItemRarityID`, for example), Bunny Loader provides it
with tModLoader's name.

## 2. Decisions every phase follows

- **Native stays native.** The C++ content runtime (`content/*`: type tables,
  textures, saves, tiles, walls, NPCs, dusts...) is not JS and is not rewritten.
  C# reaches it through `NativeApi` entries, as `ModItem` does. The ~80 `bl.*`
  functions in `script/api` become `NativeApi` entries where C# needs them.
- **Synchronous, on the game thread.** C# hooks run where the game calls them,
  with the real objects, like tModLoader. No worker thread, snapshots or
  sampled AI.
- **One class per file**, identifiers in English, comments in pt-BR, logs and
  errors in English, tModLoader's member order and XML doc summaries kept short.
- **A feature is done when** (all four):
  1. the C# API matches tModLoader's signatures;
  2. the ExampleMod example for it is ported into `samples/ExampleModCSharp`,
     with tModLoader's textures and the translations;
  3. a test pack in `tools/tests/cs<name>` passes on the MuMu with QuickJS off;
  4. the guide in `docs/mods-em-csharp` covers it.
  Hooks that run every frame are also measured on a real phone.
- **Both engines coexist until phase 9.** Each C# loader is written beside the
  JS one, sharing only the native runtime. Hooks that both install must chain
  (as `Item.SetDefaults` does today).

## 3. Decisions (settled on 2026-10-27)

| Decision | Settled |
|---|---|
| Namespace of the API | Everything public moves from `BunnyLoader` to `Terraria.ModLoader` (and `.IO`, `.Config`, `.UI`...) in phase 0, while only the test packs and `ExampleModCSharp` use it. `BunnyLoader` keeps only what tModLoader does not have (section 1.1). |
| Localization files | tModLoader's layout: `Localization/en-US.hjson` with `Mods.<Mod>.Items.<Name>.DisplayName`. The `ItemName`/`ItemTooltip` JSON added for the first `ModItem` stays readable until phase 9, and `ExampleModCSharp` switches to hjson in phase 0. |
| Mod identity in `FullName` | tModLoader uses the internal mod name (`ExampleMod/ExampleItem`). Saves keep the uid key (`<uid>/<Name>`), since that is what existing saves store. |
| What happens to the 47 JS mods in the catalog | All three options of section 6. |

## 4. Phases

Sizes are the JS lines each phase replaces, to compare phases, not to promise
dates. The JS core folders (Core 1.5k, UI 1.5k, Effects 2.3k, Enums 0.1k) are
split among the phases that need them.

### Phase 0: the foundation (base for everything)

- [x] Public API moved to tModLoader's namespaces; `Mod.Logger`; `Log` becomes internal.
- [x] `ModType<TEntity, TModType>`, `ModTexturedType`, `ILoadable`, `[Autoload]`, `IsLoadingEnabled`, load and unload order (`Mod.Load`, `SetStaticDefaults`, `PostSetupContent`, `AddRecipes`). `Unload` exists but is never called: mods are not unloaded during a session (changing mods restarts the game). Test pack: `tools/tests/csload`.
- [x] `ModContent`: `Request<T>`, `Find`/`TryFind`, `GetInstance`, `<X>Type<T>()`, `Mod.Find`, `Mod.Assets`, `Mod.GetFileBytes`. `Asset<T>` is written by hand in `ReLogic.Content` (only `Texture2D` for now) until ProxyGen generates generics; it loads on first `Value` on the game thread. `Mod.Assets` is a `BunnyLoader.ModAssets` (the name `AssetRepository` is the game's `Main.Assets`). Test pack: `tools/tests/csassets`.
- [x] `ModLoader.TryGetMod`, `Mod.Call`, dependencies and load order between C# mods. The order is the native one (manifest `dependencies` first); a mod finds the ones created before it, and every mod from `PostSetupContent` on. A mod with sources compiles on the phone against its C# dependencies' `.dll`; the catalog and the PC tools cannot compile such a mod yet. C# and JS mods cannot call each other. Test packs: `tools/tests/csmodtarget`, `csmodref`, `cssourceref`.
- [x] Localization: `LocalizedText`, `Language.GetTextValue`, the hjson layout (section 3), `this.GetLocalization`, `ExampleModCSharp` moved to hjson. `Language` and `LocalizedText` are the game's proxies, with `GetOrRegister`, `GetTextValue(key, args)` and `Format(args)` added in partials; the native `content/common/ModLanguage` keeps the mods' texts, puts them in the game's table on the game thread and re-applies them when the language changes. Test pack: `tools/tests/csloc`.
- [x] Per-entity instances: `Item.ModItem`, `IEntityWithInstances`, `NewInstance`/`Clone`, `CloneNewInstances`, the per-object data that `script/bridge/ExtraFields` gives JS today. `BunnyLoader.EntityInstances<T>` is the C# side of ExtraFields: a table keyed by the game object's address, with a weak handle (NativeApi `GcHandleNewWeak`) to tell it from a new object at the same address, swept as it grows. `Item.ModItem` is a partial on the proxy; `Item.Clone` clones the instance. `IEntityWithInstances` is left for the globals item, where tModLoader's entity-side lists (`EntityGlobals`) live. Test pack: `tools/tests/csinstances`.
- [x] Globals: `GlobalType`, `GlobalItem`/`GlobalNPC`/... lists, `InstancePerEntity`, `AppliesToEntity`. `GlobalType<TEntity, TGlobal>` (`AppliesToEntity` early and late, `NewInstance`, `Clone`), `IEntityWithGlobals<TGlobal>` (`EntityGlobals`, the entity side; this is where `IEntityWithInstances` ended up), and `BunnyLoader.GlobalList<TEntity, TGlobal>`, the plumbing each entity loader reuses (per-entity slots in an `EntityInstances`). `GlobalItem` has `SetDefaults` and `SetStaticDefaults`, plus `Item.GetGlobalItem`/`TryGetGlobalItem`; its other hooks are phase 1, and `GlobalNPC`/`GlobalProjectile`... come with their loaders. A global without `InstancePerEntity` that has instance fields is refused, as in tModLoader. Test pack: `tools/tests/csglobals`.
- [x] Saves: `TagCompound`, `TagIO`, `SaveData`/`LoadData`, wired to the native save files (`ModItemSave` and friends call C# instead of `script::saveModItemData`). `content/items/ModItemData` sends each item's data to the engine that owns its type (C# types are marked at registration). The data in the `.bl` files is the JSON the JS mods already write, so ported mods keep their saves; `TagIO` is tModLoader's binary NBT for mods' own files. `Item.DeepClone` and `clientClone` carry the instances too. Still missing: `GlobalItem.SaveData`/`LoadData` (vanilla items have no entry in the `.bl` files yet) and `ModPlayer`/`ModSystem` data (their phases). Test pack: `tools/tests/cssave`.
- [x] Networking: `ModPacket`, `Mod.GetPacket`, `HandlePacket`, `NetSend`/`NetReceive`, the join handshake (message 68). The transport is native (`content/common/ModNet`): C# packets go as NetModules (message 82) with their own module id, `0xB17F`, next to the JS one (`0xB17E`), so each engine takes only its own from `NetManager.Read`. The handshake rides message 68 like the JS one, with its own prefix (`bunnyloader-cs:`): the client sends its C# mods (one `name<TAB>version` line each), and the host refuses a client whose list differs, in the host's language, naming the mods the client is missing and the ones only the client has. Moved: `NetSend`/`NetReceive` go with the item network hooks in phase 1 (they ride messages 5, 21, 32 and 90, as `ItemNetworkHooks.js` does), and the sync offer for C# mods goes with `ServerSync*` in phase 6. Test pack: `tools/tests/csnet`.
- [ ] ProxyGen: grow `types.txt` to every type the 175 JS hooks touch; generics (`Asset<T>`, `List<T>`), delegates, events, struct returns over 16 bytes. Each type that does not generate is recorded with the reason.
  - [x] Struct returns over 16 bytes: already generated (`RetBuf`, `x8`; `hookcs` checks `Matrix`).
  - [x] Each type and member that does not generate goes to `skipped.txt` with the reason; a listed type that is not in the game is skipped instead of failing the build; nested types can be written with a dot. ProxyGen takes several `--types` files.
  - [x] `tools/proxygen/js_types.py` lists in `ProxyGen/types-js.txt` the game types the JS calls by signature that are not in `types.txt` (114 today).
  - [ ] With the dump (local): run ProxyGen with `types-js.txt`, move the types that generate into `types.txt`, build `BunnyLoader.dll`. First run: 772 types, 80 members skipped, and 16 compile errors in `Terraria.UI.ItemSlot.g.cs`, all from `Terraria.ID.PlayerItemSlotID.SlotReference`: a listed nested type did not bring its outer type, so it was never written. Fixed: a listed nested type registers the types around it, every struct gets its layout before the files are written, and a member (or mirror field) whose type is not generated is skipped with `type X is not generated`. `js_types.py` leaves out names that are members (`Terraria.Main.ItemDropsDB`) by checking `refs/dump.cs`. The 110 types of `types-js.txt` are now in `types.txt` (waiting for the local build).
  - [ ] With the dump (local): generic types (`Asset<T>`, `List<T>`; then retire the hand-written `ReLogic.Content.Asset<T>`), delegates and events in the generator.
- [ ] Hook cost on the phone for per-frame paths (`CallFast` where it is a leaf).

JS replaced: Mod, ModContent, ModLoader, ModPacket, NetReader/Writer, TagCompound, Core/ (Entities, ExtraFields use, GlobalList/Registry, ModRegistry, ModNet, ModFiles, Lang, Safe, Hooks).

### Phase 1: items (6.1k JS lines)

- [ ] `ModItem` complete: use, combat, healing, drawing, pickup, networking, data hooks.
  - [x] Use: `CanUseItem`, `UseItem` (with the use time when it returns `true`), `UseAnimation`, `UseStyle`, `HoldStyle`, `HoldItem`, `HoldoutOffset`, `UpdateInventory`, on `ModItem` and `GlobalItem`. `ModLoader/ItemHooks.cs` hooks the same `Player` methods as the JS (`ItemCheck_CheckCanUse_Inner`, `ItemCheck_StartActualUse`, `ItemCheck_OwnerOnlyCode`, `ApplyItemAnimation`, `ItemCheck_ApplyUseStyle`/`ApplyHoldStyle`, `UpdateEquips`), by signature through `BunnyLoader.Hook` (the generated `On_` names of overloaded methods are not known without the dump), and only when a loaded class overrides the hook. Test pack: `tools/tests/csuse`.
  - [ ] Combat and ammo: `ModifyWeaponDamage`/`Crit`/`Knockback`, `ModifyItemScale`, `CanShoot`, `ModifyShootStats`, `Shoot`, the ammo hooks, `CanHitNPC`/`ModifyHitNPC`/`OnHitNPC`, PvP, `MeleeEffects`, `UseItemHitbox` (needs `HitInfo`/`HitModifiers`/`StatModifier`, phase 2).
  - [x] Healing: `GetHealLife`/`GetHealMana`, potion delay. `ModLoader/ItemHealHooks.cs`, on the JS hook points (`QuickHeal`/`QuickMana`, `ApplyLifeAndOrMana`, the quick heal choice, `ApplyPotionDelay` with `AddBuff` and `TryToResetHungerToNeutral` inside it); `ModPlayer`'s side comes with phase 2. Every raw hook now calls the game through `GameInterop.Call` (`BunnyLoader.Interop.GameCall`), so an exception in the game reaches C# as a `GameException` instead of unwinding through Mono. `Player.HasBuff(int)` is hand-written (`Terraria/Player.cs`, as tModLoader's `Player.TML.cs`: the phone's game has only `FindBuffIndex`). Test pack: `tools/tests/csheal`.
  - [x] Drawing: `PreDrawInWorld`/`PostDrawInWorld`, `PreDrawInInventory`/`PostDrawInInventory`, `GetAlpha`. `ModLoader/ItemDrawHooks.cs`. The stages inside `Main.DrawItem` and `ItemSlot.DrawItemIcon` (the hooks in the middle of the method that the JS `ItemDrawLoader` uses) moved out of the QuickJS code into `content/items/ItemDrawStages`, which both engines listen to (`NativeApi.ItemDrawListen`; each engine has its own on/off flag, set inside its own `DrawItem`/`DrawItemIcon` hook). `GetAlpha` on `Item.GetAlpha(Color)`, with the `GlobalItem`s too (the JS has only `ModItem`). Test pack: `tools/tests/csdraw`.
  - [x] Pickup: `CanPickup`, `OnPickup`, `GrabRange`, `GrabStyle`, `ItemSpace`. `ModLoader/ItemPickupHooks.cs`, on the JS hook points (`CanAcceptItemIntoInventory(Item)` inside a `GrabItems` scope for the veto and the range, with the pull branch for a range above the game's; `GetItemGrabRange(WorldItem)` for the owner search; `PullItem_Common`/`PullItem_Pickup`; `PickupItem(WorldItem)`; both `ItemSpace(Item)` overloads). `Item.GetWorldItem()` finds the item's `WorldItem` (an index of `Main.item` rebuilt on a miss). The pull for a range above the game's runs outside the `GrabItems` scope, since the game's `CanPullItem` calls `CanAcceptItemIntoInventory` again. Test pack: `tools/tests/cspickup`.
  - [x] Network: `NetSend`/`NetReceive` on `ModItem` and `GlobalItem`, with the item sync messages 5, 21, 32 and 90. `ModLoader/ItemNetHooks.cs`, as the JS `ItemNetworkHooks`: a C# packet (kind 2) goes right before the game's message (`NetMessage.SendData`), is queued per sender and applied after `MessageBuffer.ProcessData` reads the message, or inside it when the server passes the message on; a `ModItem` without `NetSend` sends its `SaveData` text. Chest slots through `NativeApi.ExpandChestItem`/`StoreChestItem` (the phone keeps chest items compact; the data goes into the chest's mod data and `InventoryStorage.SyncChestItemUpdateToStorage`). Not ported yet: the JS's resync when only the mod data changed (`IsNetStateDifferent`) and the data that a dropped item carries to the ground. Test pack: `tools/tests/csnetitem` (two devices).
  - [x] Right click and opening: `CanRightClick`, `RightClick`, `ConsumeItem`, on `ModItem` and `GlobalItem`. `ModLoader/ItemOpenHooks.cs`, on the JS hook points (`ItemSlot.TryOpenContainer_GrantItems`, the touch "Open" button `GUIPageOptions.CanBeOpened`, `VirtualControllerInputState.GetItemCategory`); the types whose `CanRightClick` is true on a fresh item go into `ItemID.Sets.OpenableBag` when the content is ready. The developer set for boss bags, as in tModLoader, with tModLoader's `ItemID.Sets.PreHardmodeLikeBossBag` hand-written (`Terraria/ID/ItemID.Sets.cs`: the game has no such set; the seven pre-Hardmode bags the PC's `OpenBossBag` leaves out are marked). Test pack: `tools/tests/csopen`.
  - [ ] `ModifyItemLoot` (with `ItemLoot`/`IItemDropRule`, together with the NPC loot).
- [ ] `GlobalItem`, tooltips (`TooltipLine`, `ModifyTooltips`, `DrawableTooltipLine`, vanilla tooltips).
  - [x] `TooltipLine` and `ModifyTooltips` on `ModItem` and `GlobalItem`. `ModLoader/ItemTooltipHooks.cs`, on `Main.MouseText_DrawItemTooltip_GetLinesInfo`: the game's lines become `TooltipLine`s named by their text (the `Lang.tip`/`Language` texts the game builds them from, and the item's own tooltip lines for `Tooltip0..n`), the mods change the list, and it is written back into the game's arrays (with the prefix colors and the special lines found again by role). `GameArray<T>.New`. Test pack: `tools/tests/cstooltip`.
  - [ ] The tooltip drawing in C# (the JS `TooltipLoader.#Draw`, `VanillaTooltips`): `OverrideColor`, the `[c/RRGGBB:text]` tags, `PreDrawTooltip`/`PostDrawTooltip`/`PreDrawTooltipLine`/`PostDrawTooltipLine` with `DrawableTooltipLine`, the price lines and the rarity color.
- [ ] `ModPrefix`, `ModRarity`, `DamageClass` (with `Item.DamageType`).
- [ ] Recipes: `Recipe`, `CreateRecipe`, `Condition`, `RecipeGroup`, `GlobalRecipe`.
- [ ] Equipment: `EquipLoader`, `[AutoloadEquip]`, armor sets, wings, PvP item sync.

Ports: ItemLoader, Item*Hooks, ItemDataLoader, ItemDrawLoader, ItemPickupLoader, GlobalItemLoader, TooltipLoader, VanillaTooltips, PrefixLoader, RarityLoader, DamageClassLoader, RecipeLoader, EquipLoader, ArmorSetLoader, WingLoader, PvpItemNetwork.

### Phase 2: player and buffs (4.0k)

- [ ] `ModPlayer` with every Player hook group (update, items, jumps, drawing, network, world).
- [ ] Combat: `HitInfo`, `HitModifiers`, `HurtInfo`, `HurtModifiers`, `StatModifier`.
- [ ] `ModBuff`, `GlobalBuff`.
- [ ] `ModResourceOverlay`, `ModHair`, `ModMount`.

Ports: PlayerLoader, Player*Hooks, CombatLoader, BuffLoader, ResourceOverlayLoader, HairLoader, MountLoader.

### Phase 3: projectiles, dusts, gores (1.2k)

- [ ] `ModProjectile`, `GlobalProjectile`.
- [ ] `ModDust`, `ModGore`.

Ports: ProjectileLoader, GlobalProjectileLoader, DustLoader, GoreLoader.

### Phase 4: NPCs (3.0k)

- [ ] `ModNPC`, `GlobalNPC`; loot (`ItemLoot`, `NPCLoot`, `GlobalLoot`, drop rules).
- [ ] Spawning (`NPCSpawnInfo`, `SpawnPool`, `SpawnCondition`); town NPCs, shops (`NPCShop`), happiness, bestiary.
- [ ] Banners (`ModBannerTile`, needs phase 5 tiles), boss bars (`ModBossBar`, `GlobalBossBar`), `ModEmoteBubble`.

Ports: NPCLoader, GlobalNPCLoader, GlobalLootLoader, SpawnLoader, TownNPCLoader, BestiaryLoader, BannerLoader, BossBarLoader, EmoteBubbleLoader.

### Phase 5: tiles and the world (3.9k)

- [ ] `ModTile`, `GlobalTile`, drawing, use, world hooks; `TileRestingInfo`, `TileObjectData`.
- [ ] `ModWall`, `ModTree`, `ModTileEntity`, `ModPylon`/`TEModdedPylon`.
- [ ] World generation (`GenPass`), chests and their sync, page and sign icons.

Ports: TileLoader, TileDrawLoader, TileUseLoader, TileWorldLoader, GlobalTileLoader, WallLoader, TreeLoader, TileEntityLoader, TileEntityItemHooks, PylonLoader, ChestNetworkHooks, PageIconLoader.

### Phase 6: systems and the environment (7.0k)

- [ ] `ModSystem` with every hook group (update, draw, network, world).
- [ ] `ModBiome`, `ModSceneEffect`, water and waterfall styles, surface and underground backgrounds, map backgrounds, `ModCloud`.
- [ ] `ModMenu` (title menu and theme setting), music (`MusicLoader`) and sounds (`SoundEngine`, `SoundStyle`).
- [ ] `ModAchievement`, angler quests, `ModCommand`.
- [ ] `ModConfig` with the config menu, host settings and server sync.

Ports: SystemLoader, System*Hooks, BiomeLoader, Biome*Loader, SceneEffectLoader, WaterStyleLoader, WaterfallStyleLoader, Surface/Underground/BackgroundTexture/MapBackground loaders, CloudLoader, MenuLoader, MenuThemeSetting, ModMusic, VanillaMusic, SoundLoader, AchievementLoader, AnglerQuestLoader, CommandLoader, ConfigLoader, ModConfigMenu, HostSettingsMenu, ServerSync*, LocalizationLoader.

### Phase 7: UI, touch buttons, screen effects (0.9k + UI 1.5k + Effects 2.3k)

- [ ] `Terraria.UI`: `UIState`, `UserInterface`, `UIElement` and the elements, `GameInterfaceLayer`, `ModSystem.ModifyInterfaceLayers`.
- [ ] `ModKeybind` with the touch button layout (today's `ModTouchButton`).
- [ ] Screen effects: `Filters`/`ScreenShaderData`/`SkyManager` where the game has them; the Bunny Loader layers (`BunnyLoader.Screen*`) for the rest.
- [ ] Texture packs and font packs (today partly JS).

Ports: InterfaceLayerLoader, KeybindLoader, TouchButton*, ScreenEffectsSetting, UI/, Effects/.

### Phase 8: the mods

- [ ] `samples/ExampleModCSharp` covers everything `samples/ExampleMod` (232 JS files, 9.6k lines) covers. This happens phase by phase; here only the leftovers are closed.
- [ ] The other samples rewritten in C#: DobroDeDano, HelloMod, SemQueda, TemploPiramide, VidaCheia.
- [ ] The 190 JS test packs: each one either gets a C# counterpart or is retired with a note on what covers it now.
- [ ] Catalog: section 6.

### Phase 9: removing JavaScript

Only after phases 0–8 are ticked and a release with both engines has been out
long enough for authors to port.

- [ ] Native: `third_party/quickjs`, `script/bridge`, `script/api`, `script/js`, `JsHook`, the `ScriptEngine`, the QuickJS branches in `mods/ModLoader.cpp` and `boot/Probe.cpp`, `bl.*` installation, `BL_HAVE_QUICKJS`.
- [ ] `content/*` callbacks that call into JS (`script::saveModItemData` and the like) point only to C#.
- [ ] App: the QuickJS setting (`Prefs.quickJs`, Settings screen, `GameActivity`), `.js` entries in `ModManifest`, `Catalog`, `ModRepository`, `ModSources`, `NativeBridge`; the launcher refuses JS packs with a clear message.
- [ ] BL Editor and Mod Sources: C# only.
- [ ] Tools: `tools/mods` (`pack.py`, `publish.py`, `port_textures.py`, tests) without `main.js`; the catalog app.
- [ ] Docs: `docs/mods` (the JS guides) archived to `docs/historico`; `docs/mods-em-csharp` becomes the mod guide; README, wiki and `docs/nucleo` updated.
- [ ] The APK and `libbunny.so` size before and after, in the changelog.

## 5. Order and parallel work

Phase 0 blocks everything. After it, phases 1 and 3 can run side by side, 2
after 1 (the player uses items), 4 after 3, 5 after 1, 6 and 7 after 0. Phase 8
moves along with each phase; phase 9 is last.

## 6. The JS mods that exist today

The catalog has 61 packs: 47 JS mods and 14 texture or font packs (these have
no code and keep working). Removing QuickJS breaks the 47. All three of these
are done:

1. **Port the ones we own** (Example Mod and the samples) and contact the
   authors of the rest with the C# guide and a deadline.
2. **Keep the last release with QuickJS downloadable** for anyone who needs a
   JS mod, with the catalog showing which app version each mod needs.
3. **Flag JS mods in the catalog** as soon as phase 1 lands, so new mods are
   written in C#.

When: (3) right away, (1) during phase 8, (2) at phase 9.

- [ ] JS mods flagged in the catalog.
- [ ] Authors of the other JS mods contacted, with the C# guide and a deadline.
- [ ] The last QuickJS release kept downloadable, and the catalog says which app version each mod needs.
