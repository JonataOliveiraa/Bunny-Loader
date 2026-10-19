# Changelog

Every Bunny Loader version, newest first. New changes go under
**Unreleased** as they land; when a build is published, that section gets the
version number and the date.

## Unreleased

## 0.2.0 — 2026-10-19

The first version after `v0.1.0-dev.20261006`: 75 commits, from Oct 7 to Oct 19, 2026.

### For players

- **Languages:** the launcher and the Mod Menu now speak English, Russian, Spanish and Simplified Chinese, besides Portuguese. Go to Settings > Appearance > Language; Automatic follows the phone's language.
- **Mod names and descriptions in your language:** a mod can ship a translated name, description and changelog, and the launcher shows the one for your language (contributed by bilimoing, PR #1).
- **Menu Theme:** a new Settings option to pick the title screen theme.
- **Screen effects:** mods can apply full-screen visual effects (distortion, bloom, blur, chromatic aberration...). Settings has an option to turn them off.
- **Mod touch buttons:** mods can add their own buttons to the touch controls, and you place them in the game's touch controls editor (Mod Buttons technique by Animatak_).
- **Font packs:** a new pack type that replaces the game's fonts (tooltips, menus, item stacks, damage text). Example: `FonteComicNeue`.
- **Packages tab:** enabled and newly imported mods go to the top; importing shows progress and unzips faster; the app asks before updating an installed mod; cards slide when they move.
- **Explore (catalog):** loads 10 packages at a time. The catalog and the texture ports now live in their own repository (Bunny-Loader-Mods).
- **Mod page:** can have a theme (button colors, an image or animated GIF background), and images now accept GIF, WebP, JPG and BMP, with adjustable size and alignment.
- **Mod Configuration:** "Apply" keeps the screen open, with Back/Discard.
- **Multiplayer:** mods are synced with the server when you join, PvP works with mod items, and a host playing on their own phone now runs the mods' server-side logic.
- **License:** the project now has a license (PolyForm Noncommercial 1.0.0), with License and Third-party screens under Settings > About.
- **New app icon.**

### Fixes

- Mod chests, dressers and shops showed a generic icon (wooden chest or bag) on the inventory page; they now show the furniture's item or the town NPC's head. Mod signs opened with an empty frame and no name; they now show the item and its name.
- Screen flickering, with parts of the previous frame showing, on phones with a Mali-G52 GPU (e.g. Vivo Y5s), even on the main menu.
- Mod teleportation pylons crashed the host in multiplayer; they now open the pylon map and teleport like the game's own.
- Multi-cell mod objects (tables, chests...) come back whole when the world loads.
- During world generation, the "Tile Cleanup" step turned mod chests into wooden chests.
- Bestiary: hidden NPCs no longer leave black silhouettes with repeated numbers, and mods can have their own bestiary icon.
- Mod item, NPC and buff names in the Mod Menu follow in-game language changes.
- Mod boss health bars draw correctly in the mobile layout.
- Vanilla tooltip lines are named Tooltip0, Tooltip1... again.
- Melee PvP veto with a stale player index.
- Each entity gets its own ModNPC/ModProjectile/ModItem instance (two copies could share the same state before).
- ModPlayer sync in multiplayer.
- Game exceptions raised from mod calls now become an error with a message instead of crashing the app.

### Performance

- Lower per-frame cost, measured on a real phone: interface layers are reused (two leaked every frame before), QuickJS hashing fixed (Maps/Sets with number keys were slow), mod dusts no longer scan all 6,000 dusts every frame, and the minimap boss head is cached per frame.
- Projectile hits no longer enter JS once per NPC (minions and lasers lagged with many targets).
- Background, cloud, lighting and tile mouse-over hooks only run when some mod uses them.

### For modders

- **New content:** ModTileEntity, ModTree, ModBossBar, ModPylon, ModDust, ModTouchButton (with the ModKeybind alias), ModResourceOverlay and ModScreenEffect.
- **Mod UI (as in tModLoader):** UIState, UserInterface, UIPanel, UIText, UIImage, UIList, UIScrollbar and interface layers (ModifyInterfaceLayers).
- **Globals:** GlobalTile, GlobalBuff, GlobalBossBar and GlobalRecipe; GlobalNPC gained shops (SetupShop, ModifyShop...), drawing and more hooks; GlobalItem gained crit, knockback, ammo and healing hooks.
- **tModLoader 1.4.4 hit pipeline:** CanHit/ModifyHit/OnHit with HitModifiers, HitInfo, HurtModifiers and HurtInfo, for items, projectiles, NPCs, players and globals (NPC vs NPC and PvP included).
- **Items:** per-instance SaveData/LoadData, ResearchUnlockCount (Journey mode), PreHardmodeLikeBossBag, pickups (OnPickup), grapple range, ShouldUpdatePosition and ModifyBuffText.
- **Other:** mod banners (ModBannerTile), a second boss head (AddBossHeadTexture), mod fonts (`ModContent.Request('Assets/Fonts/X')`), recipe conditions and callbacks, ModSystem.Load.
- **Example Mod:** new examples for pylons, tile entities, trees, boss bars, dusts, UI (/coins), a resource bar, life and mana fruits, touch buttons, screen effects (a Last Prism port), a castle generated in the world (/castle) and a custom font.
- **Documentation** translated to English, with new guides: hits, dusts, interface, screen effects, touch buttons and font packs.

### Installing and updating

- Official APKs keep the same signature as the previous Release: the new version installs over it, no uninstall needed.
- `versionCode` is no longer stuck at 1 and goes up with every build (some phones, like Samsung and Xiaomi, refused updates with the same number).
- If you installed an APK built by someone else (a different signature), back up `Android/data/com.bunnyloader`, uninstall once and install the official one.

## 0.1.0-dev.20261006 — 2026-10-06

Development build for Android ARM64 (commit `1cae6181`).

- ModSystem: 35 new hooks, GenPass and PassLegacy with editable world generation steps.
- ModItem: combat, use, ammo, healing and potion delay hooks.
- Custom player draw layers and hook optimizations.
- Texture packs, animated icons, update management and save import.
- Documentation, the method spreadsheet, Workshop tools and 12 texture ports.
