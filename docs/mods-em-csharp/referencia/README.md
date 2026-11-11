# API reference

Every public class a mod can use in Bunny Loader, member by member, side by side
with tModLoader. Use the [guides](../README.md) to learn how something works and
this reference to look up what exists.

## The classes you build a mod with

Create a class that derives from one of these and Bunny Loader finds it by itself. Click a name to see all its hooks.

### The mod itself

| Class | What you use it for |
|---|---|
| [Mod](sistemas.md#mod) | The mod's main class. Every mod needs one. It holds `Load`, `Call` and `HandlePacket`. |
| [ModSystem](sistemas.md#modsystem) | Code for the whole mod: world data, recipes, interface layers, world generation. |
| [ModPlayer](jogadores.md#modplayer) | Data and hooks attached to each player. |
| [ModConfig](interface.md#modconfig) | Options the player can change in the game. |
| [ModCommand](sistemas.md#modcommand) | A chat command. |

### Items

| Class | What you use it for |
|---|---|
| [ModItem](itens.md#moditem) | An item: weapon, tool, armor, accessory, material, potion... |
| [ModPrefix](itens.md#modprefix) | A prefix, such as Legendary. |
| [ModRarity](itens.md#modrarity) | A rarity, with its own color. |

### NPCs and enemies

| Class | What you use it for |
|---|---|
| [ModNPC](npcs.md#modnpc) | An enemy, a boss or a town NPC. |
| [ModInvasion](npcs.md#modinvasion) | A wave-based event. Only in Bunny Loader. |
| [ModBossBar](npcs.md#modbossbar) | The health bar of a boss. |
| [ModBossBarStyle](npcs.md#modbossbarstyle) | Replaces the whole boss bar display. |
| [ModEmoteBubble](npcs.md#modemotebubble) | An emote. |

### Projectiles and buffs

| Class | What you use it for |
|---|---|
| [ModProjectile](projeteis.md#modprojectile) | A projectile: arrow, magic bolt, minion, whip, hook... |
| [ModBuff](jogadores.md#modbuff) | A buff or debuff. |

### Player

| Class | What you use it for |
|---|---|
| [ModMount](jogadores.md#modmount) | A mount or minecart. |
| [ModHair](jogadores.md#modhair) | A hairstyle. |
| [ModResourceOverlay](jogadores.md#modresourceoverlay) | Draws over the hearts and the mana stars. |

### Blocks and the world

| Class | What you use it for |
|---|---|
| [ModTile](tiles.md#modtile) | A block or piece of furniture. |
| [ModWall](tiles.md#modwall) | A wall. |
| [ModBannerTile](tiles.md#modbannertile) | An enemy banner tile. |
| [ModPylon](tiles.md#modpylon) | A pylon tile. |
| [ModTileEntity](tiles.md#modtileentity) | Data and updates attached to a placed tile. |
| [ModTree](tiles.md#modtree) | A kind of tree. |
| [ModCactus](tiles.md#modcactus) | A kind of cactus. |
| [ModBiome](tiles.md#modbiome) | A biome. |
| [ModBiomeConversion](tiles.md#modbiomeconversion) | A conversion, such as the one the Clentaminator does. |

### Scenery and effects

| Class | What you use it for |
|---|---|
| [ModSceneEffect](tiles.md#modsceneeffect) | Music and scenery that apply under a condition. |
| [ModSurfaceBackgroundStyle](tiles.md#modsurfacebackgroundstyle) | A surface background. |
| [ModUndergroundBackgroundStyle](tiles.md#modundergroundbackgroundstyle) | An underground background. |
| [ModWaterStyle](tiles.md#modwaterstyle) | A water style. |
| [ModWaterfallStyle](tiles.md#modwaterfallstyle) | A waterfall style. |
| [ModCloud](tiles.md#modcloud) | A cloud. |
| [ModDust](tiles.md#moddust) | A dust particle. |
| [ModGore](tiles.md#modgore) | A gore piece. |

### Interface and input

| Class | What you use it for |
|---|---|
| [ModMenu](interface.md#modmenu) | A title menu theme. |
| [ModMapLayer](tiles.md#modmaplayer) | Draws icons over the map. |
| [ModAchievement](interface.md#modachievement) | An achievement. |
| [ModKeybind](interface.md#modkeybind) | An input binding, a touch button on the phone. Not derived: created with `KeybindLoader.RegisterKeybind`. |
| [ModTouchButton](interface.md#modtouchbutton) | A button on the screen. Only in Bunny Loader. Not derived: created with `TouchButtonLoader.RegisterButton`. |
| [ModTouchJoystick](interface.md#modtouchjoystick) | A button the player drags to aim. Only in Bunny Loader. Not derived: created with `TouchButtonLoader.RegisterJoystick`. |

### Change what already exists

| Class | What you use it for |
|---|---|
| [GlobalItem](itens.md#globalitem) | Every item, from the game or from any mod. |
| [GlobalNPC](npcs.md#globalnpc) | Every NPC. |
| [GlobalProjectile](projeteis.md#globalprojectile) | Every projectile. |
| [GlobalBuff](jogadores.md#globalbuff) | Every buff. |
| [GlobalTile](tiles.md#globaltile) | Every tile. |
| [GlobalWall](tiles.md#globalwall) | Every wall. |
| [GlobalPylon](tiles.md#globalpylon) | Every pylon. |
| [GlobalBossBar](npcs.md#globalbossbar) | Every boss bar. |
| [GlobalEmoteBubble](npcs.md#globalemotebubble) | Every emote. |
| [GlobalBackgroundStyle](outros.md#globalbackgroundstyle) | Every background. |

### Also content, without Mod in the name

| Class | What you use it for |
|---|---|
| [DamageClass](itens.md#damageclass) | A damage class. |
| [ExtraJump](outros.md#extrajump) | An extra jump, like the Cloud in a Bottle. |
| [InfoDisplay](outros.md#infodisplay) | A line of information accessories. |
| [BuilderToggle](outros.md#buildertoggle) | A builder accessory switch. |
| [PlayerDrawLayer](jogadores.md#playerdrawlayer) | A layer of the player's drawing. |


## How to read the tables

| Column | Meaning |
|---|---|
| **Member** | The signature, with short type names |
| **Kind** | `hook` is a `virtual` you override. Otherwise `method`, `property`, `field`, `event`, `const`, `constructor`, `indexer`, with `static` when it applies |
| **tModLoader** | `same`: same name and parameters, in tModLoader or in the PC game it runs on. `differs`: same name, other signature, which the row shows. **only here**: exists only in Bunny Loader. **not here**: exists in tModLoader, a mod that uses it does not compile here. `XNA` and `ReLogic`: from a library tModLoader also uses, not compared |
| **What it does** | What the member is for. **Phone:** marks a behavior different from the PC |

The game's own members, such as `Item.damage` or `Main.dayTime`, come from the
mobile game and are not listed: only what Bunny Loader adds to those classes is.
The `On_` classes are generated for each game method, as
[guide 2](../02-hooks.md) explains.

These pages are generated from the code by `tools/docs/ApiReference`. Descriptions
of members that tModLoader also has come from its documentation (MIT license); the
rest are in `tools/docs/ApiReference/notes.txt`.

The `tModLoader` column looks at signatures only. A member can be `same` and still
act differently on the phone, and an `only here` member can be a wrapper around
something the PC game has under another shape, such as `GameList` for a `List<T>`.

**601 classes, 5351 members:** 4311 the same as tModLoader or the PC game, 146 with a different signature, 566 only here, 328 not here. tModLoader's loaders have 297 more methods that only run hooks: each page counts them under its loader.

## [Items](itens.md)

| Class | Kind | In tModLoader | What it is |
|---|---|---|---|
| [AmmoID](itens.md#ammoid) | static class | yes | Ammo types: the value of `Item.ammo` and `Item.useAmmo`. |
| [AmmoID.Sets](itens.md#ammoidsets) | struct | yes | Tables indexed by ammo. |
| [AmmoID.Sets.LauncherAmmoMatches](itens.md#ammoidsetslauncherammomatches) | class | **no** | Which rocket each launcher fires for a given ammo. |
| [ArmorIDs](itens.md#armorids) | struct | yes | Equipment texture slots: the numbers in `Item.headSlot`, `bodySlot` and `legSlot`. |
| [ArmorIDs.Body](itens.md#armoridsbody) | struct | yes | Body slots. |
| [ArmorIDs.Body.Sets](itens.md#armoridsbodysets) | struct | yes | Tables indexed by body slot. |
| [ArmorIDs.Head](itens.md#armoridshead) | struct | yes | Head slots. |
| [ArmorIDs.Head.Sets](itens.md#armoridsheadsets) | struct | yes | Tables indexed by head slot. |
| [ArmorIDs.Head.Sets.DrawHeadSet](itens.md#armoridsheadsetsdrawheadset) | class | **no** | Whether the player's head shows under a given helmet. |
| [ArmorIDs.Legs](itens.md#armoridslegs) | struct | yes | Leg slots. |
| [ArmorIDs.Legs.Sets](itens.md#armoridslegssets) | struct | yes | Tables indexed by leg slot. |
| [Condition](itens.md#condition) | record | yes | A named check with text, used by recipes and shops. |
| [Conditions](itens.md#conditions) | static class | yes | The game's drop conditions, each as a nested class. |
| [Conditions.BeatAnyMechBoss](itens.md#conditionsbeatanymechboss) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.CorruptKeyCondition](itens.md#conditionscorruptkeycondition) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.CrimsonKeyCondition](itens.md#conditionscrimsonkeycondition) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.DesertKeyCondition](itens.md#conditionsdesertkeycondition) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.DontStarveIsNotUp](itens.md#conditionsdontstarveisnotup) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.DontStarveIsUp](itens.md#conditionsdontstarveisup) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.DownedAllMechBosses](itens.md#conditionsdownedallmechbosses) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.DownedPlantera](itens.md#conditionsdownedplantera) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.DropExtraGel](itens.md#conditionsdropextragel) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.Easymode](itens.md#conditionseasymode) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.EmpressOfLightIsGenuinelyEnraged](itens.md#conditionsempressoflightisgenuinelyenraged) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.EyeOfCthulhuDefeatedAndNoAltarsInWorld](itens.md#conditionseyeofcthulhudefeatedandnoaltarsinworld) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.FirstTimeKillingPlantera](itens.md#conditionsfirsttimekillingplantera) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.FromCertainWaveAndAbove](itens.md#conditionsfromcertainwaveandabove) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.FrostMoonDropGateForTrophies](itens.md#conditionsfrostmoondropgatefortrophies) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.FrostMoonDropGatingChance](itens.md#conditionsfrostmoondropgatingchance) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.FrozenKeyCondition](itens.md#conditionsfrozenkeycondition) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.HallowKeyCondition](itens.md#conditionshallowkeycondition) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.HalloweenGoodieBagDrop](itens.md#conditionshalloweengoodiebagdrop) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.HalloweenWeapons](itens.md#conditionshalloweenweapons) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.IsBloodMoonAndNotFromStatue](itens.md#conditionsisbloodmoonandnotfromstatue) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.IsChristmas](itens.md#conditionsischristmas) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.IsCorruption](itens.md#conditionsiscorruption) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.IsCorruptionAndNotExpert](itens.md#conditionsiscorruptionandnotexpert) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.IsCrimson](itens.md#conditionsiscrimson) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.IsCrimsonAndNotExpert](itens.md#conditionsiscrimsonandnotexpert) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.IsExpert](itens.md#conditionsisexpert) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.IsHardmode](itens.md#conditionsishardmode) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.IsMasterMode](itens.md#conditionsismastermode) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.IsPumpkinMoon](itens.md#conditionsispumpkinmoon) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.IsUsingSpecificAIValues](itens.md#conditionsisusingspecificaivalues) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.JungleKeyCondition](itens.md#conditionsjunglekeycondition) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.LegacyHack_IsABoss](itens.md#conditionslegacyhack_isaboss) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.LegacyHack_IsBossAndExpert](itens.md#conditionslegacyhack_isbossandexpert) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.LegacyHack_IsBossAndNotExpert](itens.md#conditionslegacyhack_isbossandnotexpert) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.LivingFlames](itens.md#conditionslivingflames) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.MechanicalBossesDummyCondition](itens.md#conditionsmechanicalbossesdummycondition) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.MechdusaKill](itens.md#conditionsmechdusakill) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.MissingTwin](itens.md#conditionsmissingtwin) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.NamedNPC](itens.md#conditionsnamednpc) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.NeverTrue](itens.md#conditionsnevertrue) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.NotDropExtraGel](itens.md#conditionsnotdropextragel) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.NotExpert](itens.md#conditionsnotexpert) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.NotFromStatue](itens.md#conditionsnotfromstatue) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.NotMasterMode](itens.md#conditionsnotmastermode) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.NotRemixSeed](itens.md#conditionsnotremixseed) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.NotRemixSeedEasymode](itens.md#conditionsnotremixseedeasymode) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.NotRemixSeedHardmode](itens.md#conditionsnotremixseedhardmode) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.PirateMap](itens.md#conditionspiratemap) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.PlayerNeedsHealing](itens.md#conditionsplayerneedshealing) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.PumpkinMoonDropGateForTrophies](itens.md#conditionspumpkinmoondropgatefortrophies) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.PumpkinMoonDropGatingChance](itens.md#conditionspumpkinmoondropgatingchance) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.RedHatSkeletron](itens.md#conditionsredhatskeletron) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.RemixSeed](itens.md#conditionsremixseed) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.RemixSeedEasymode](itens.md#conditionsremixseedeasymode) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.RemixSeedHardmode](itens.md#conditionsremixseedhardmode) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.SkyblockIsNotUp](itens.md#conditionsskyblockisnotup) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.SkyblockIsUp](itens.md#conditionsskyblockisup) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.SkyblockIsUpNoSickle](itens.md#conditionsskyblockisupnosickle) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.SoulOfLight](itens.md#conditionssouloflight) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.SoulOfNight](itens.md#conditionssoulofnight) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.TenthAnniversaryIsNotUp](itens.md#conditionstenthanniversaryisnotup) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.TenthAnniversaryIsUp](itens.md#conditionstenthanniversaryisup) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.WindyEnoughForKiteDrops](itens.md#conditionswindyenoughforkitedrops) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.XmasPresentDrop](itens.md#conditionsxmaspresentdrop) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.YoyoCascade](itens.md#conditionsyoyocascade) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.YoyosAmarok](itens.md#conditionsyoyosamarok) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.YoyosHelFire](itens.md#conditionsyoyoshelfire) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.YoyosKraken](itens.md#conditionsyoyoskraken) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [Conditions.YoyosYelets](itens.md#conditionsyoyosyelets) | class | yes | A condition from the game for `ItemDropRule.ByCondition`. |
| [DamageClass](itens.md#damageclass) | abstract class | yes | `DamageClass` is used to determine the application of item effects, damage/stat scaling, and class bonuses. |
| [DamageClassData](itens.md#damageclassdata) | class | yes | The stats a player keeps per damage class: damage, crit, knockback, attack speed, armor penetration. |
| [DamageClassLoader](itens.md#damageclassloader) | static class | yes | Registers damage classes and reads their stats. |
| [DrawableTooltipLine](itens.md#drawabletooltipline) | class | yes | This class serves as a way to store information about a line that will be drawn of tooltip for an item. |
| [EquipLoader](itens.md#equiploader) | static class | yes | This serves as a central place to store equipment slots and their corresponding textures. |
| [GlobalItem](itens.md#globalitem) | abstract class | yes | This class allows you to modify and use hooks for all items, both vanilla and modded. |
| [Item](itens.md#item) | struct | yes | What Bunny Loader adds to the game's item. |
| [Item.NewItemModifier](itens.md#itemnewitemmodifier) | struct | **no** | Options for how a new item is created in the world, as the phone's `Item.NewItem` expects them. |
| [ItemCreationContext](itens.md#itemcreationcontext) | abstract class | yes | Why an item was created. |
| [ItemDefinition](itens.md#itemdefinition) | class | yes | ItemDefinition represents an Item identity. |
| [ItemDropAttemptResult](itens.md#itemdropattemptresult) | struct | yes | The outcome of one attempt to run a drop rule. |
| [ItemDropAttemptResultState](itens.md#itemdropattemptresultstate) | enum | yes | How a drop attempt ended. |
| [ItemDropDatabase](itens.md#itemdropdatabase) | struct | yes | The game's table of rules for each NPC and bag. |
| [ItemDropResolver](itens.md#itemdropresolver) | struct | yes | Runs the rules when something dies or a bag opens. |
| [ItemDropRule](itens.md#itemdroprule) | static class | yes | Shortcuts that build the common rules, such as `ItemDropRule.Common(type, 3)`. |
| [ItemDropRuleResolveAction](itens.md#itemdropruleresolveaction) | delegate | yes | The method a rule chain uses to run the next rule. |
| [ItemDropWithConditionRule](itens.md#itemdropwithconditionrule) | class | yes | A `CommonDrop` that also needs a condition. |
| [ItemHoldStyleID](itens.md#itemholdstyleid) | struct | yes | Values of `Item.holdStyle`. |
| [ItemID](itens.md#itemid) | struct | yes | Item types. |
| [ItemID.Sets](itens.md#itemidsets) | struct | yes | Tables indexed by item type. |
| [ItemID.Sets.SandgunAmmoInfo](itens.md#itemidsetssandgunammoinfo) | class | yes | Used in `SandgunAmmoProjectileData`. |
| [ItemIO](itens.md#itemio) | static class | yes | Writes and reads items: `Save` and `Load` for a `TagCompound`, `Send` and `Receive` for packets. |
| [ItemLoader](itens.md#itemloader) | static class | yes | This serves as the central class from which item-related functions are carried out. |
| [ItemLoot](itens.md#itemloot) | struct | yes | Provides access to `ItemDropDatabase`'s methods specific to this item type. |
| [ItemRarityID](itens.md#itemrarityid) | static class | yes | Enumerates the values used with Item.rare |
| [ItemSlot](itens.md#itemslot) | struct | yes |  |
| [ItemSlot.Context](itens.md#itemslotcontext) | struct | yes |  |
| [ItemUseStyleID](itens.md#itemusestyleid) | struct | yes | Values of `Item.useStyle`. |
| [ModItem](itens.md#moditem) | abstract class | yes | This class serves as a place for you to place all your properties and hooks for each item. |
| [ModPrefix](itens.md#modprefix) | abstract class | yes | Represents a modded prefix (or modifier). |
| [ModRarity](itens.md#modrarity) | abstract class | yes | A rarity of your own: its color and where it sits in the order. |
| [PrefixID](itens.md#prefixid) | struct | yes | The game's prefixes. |
| [PrefixLoader](itens.md#prefixloader) | static class | yes | Finds mod prefixes and runs their hooks. |
| [RarityLoader](itens.md#rarityloader) | static class | yes | Finds mod rarities. |
| [Recipe](itens.md#recipe) | struct | yes | A Recipe is a collection of ingredients, tiles, and a resulting Item. |
| [Recipe.IngredientQuantityRules](itens.md#recipeingredientquantityrules) | static class | yes | Reduces how many ingredients a recipe consumes, for `AddConsumeIngredientCallback`. |
| [RecipeException](itens.md#recipeexception) | class | yes | Thrown when a recipe is built wrongly, such as an ingredient added twice. |
| [RecipeGroup](itens.md#recipegroup) | struct | yes | An ingredient that accepts any item in the group, such as any wood. |
| [RecipeItemCreationContext](itens.md#recipeitemcreationcontext) | class | yes | The items used and the recipe, passed to `OnCreated` when an item is crafted. |
| [RecipeLoader](itens.md#recipeloader) | static class | yes | This is where all Recipe hooks are gathered and called. |
| [StatModifier](itens.md#statmodifier) | struct | yes | A stat change made of an additive part, a multiplier and flat amounts, as in `player.GetDamage(DamageClass.Melee) += 0.1f`. |
| [TooltipLine](itens.md#tooltipline) | class | yes | This class serves as a way to store information about a line of tooltip for an item. |

## [NPCs](npcs.md)

| Class | Kind | In tModLoader | What it is |
|---|---|---|---|
| [AbstractNPCShop](npcs.md#abstractnpcshop) | abstract class | yes | A shop of any shape. |
| [AbstractNPCShop.Entry](npcs.md#abstractnpcshopentry) | class | yes | One line of a shop: the item and its conditions. |
| [AutoloadBossHead](npcs.md#autoloadbosshead) | class | yes | This attribute annotates a `ModNPC` class to indicate that the game should autoload the boss head texture found at `BossHeadTexture`. |
| [AutoloadHead](npcs.md#autoloadhead) | class | yes | This attribute annotates a `ModNPC` class to indicate that the game should autoload the head texture found at `HeadTexture`. |
| [BestiaryEntry](npcs.md#bestiaryentry) | struct | yes | An NPC's page in the bestiary. |
| [BestiaryInfoList](npcs.md#bestiaryinfolist) | struct | **no** | The list behind `BestiaryEntry.Info`, with `Add` and `AddRange`. |
| [BossBarDrawParams](npcs.md#bossbardrawparams) | struct | yes | Holds data required for boss bar drawing. |
| [BossBarLoader](npcs.md#bossbarloader) | static class | yes | Runs the hooks of mod boss bars. |
| [EmoteBubble](npcs.md#emotebubble) | struct | yes | The emote bubble above a player or NPC. |
| [EmoteBubbleLoader](npcs.md#emotebubbleloader) | static class | yes | Registers emotes and runs their hooks. |
| [EmoteID](npcs.md#emoteid) | static class | yes | Emote types, and the categories of the emote menu in `EmoteID.Category`. |
| [EmoteID.Category](npcs.md#emoteidcategory) | static class | yes | This class is added by TML for easily adding mod emotes to a specific category. |
| [GlobalBossBar](npcs.md#globalbossbar) | abstract class | yes | A class that is used to modify existing boss health bars. |
| [GlobalEmoteBubble](npcs.md#globalemotebubble) | abstract class | yes | Changes emote bubbles of the game and of every mod. |
| [GlobalNPC](npcs.md#globalnpc) | abstract class | yes | This class allows you to modify and use hooks for all NPCs, both vanilla and modded. |
| [IItemDropRule](npcs.md#iitemdroprule) | interface | yes | A drop rule. |
| [IItemDropRuleChainAttempt](npcs.md#iitemdroprulechainattempt) | interface | yes | A rule chained to another, run only if the first one meets its test. |
| [IItemDropRuleCondition](npcs.md#iitemdroprulecondition) | interface | yes | A condition for a drop rule. |
| [ITownNPCProfile](npcs.md#itownnpcprofile) | interface | yes | Chooses a town NPC's texture, head, name and variant. |
| [InvasionEnemy](npcs.md#invasionenemy) | record | **no** | An enemy of an invasion wave: its NPC type, how often it spawns and the points it gives. |
| [InvasionLoader](npcs.md#invasionloader) | static class | **no** | Runs invasions: `Start`, `Stop`, `AddPoints`, and reads the wave and progress. |
| [InvasionWave](npcs.md#invasionwave) | record | **no** | One wave of an invasion: the points to clear it and its enemies. |
| [ModBossBar](npcs.md#modbossbar) | abstract class | yes | A class that is used to create custom boss health bars for modded and vanilla NPCs. |
| [ModBossBarStyle](npcs.md#modbossbarstyle) | abstract class | yes | A class that is used to swap out the entire boss bar display system with your own implementation |
| [ModEmoteBubble](npcs.md#modemotebubble) | abstract class | yes | Represents an emote. |
| [ModInvasion](npcs.md#modinvasion) | abstract class | **no** | A wave-based event that uses the game's progress bar. |
| [ModNPC](npcs.md#modnpc) | abstract class | yes | This class serves as a place for you to place all your properties and hooks for each NPC. |
| [NPC](npcs.md#npc) | struct | yes | What Bunny Loader adds to the game's NPC. |
| [NPC.HitInfo](npcs.md#npchitinfo) | struct | yes | Represents a finalized damage calculation for damage about to be applied to an NPC. |
| [NPC.HitModifiers](npcs.md#npchitmodifiers) | struct | yes | Represents a damage calculation in the process of being calculated for damage to be applied to an NPC. |
| [NPC.Spawner](npcs.md#npcspawner) | struct | yes | Spawns a boss or event the way the game does, through the phone's `NPC.Spawner`. |
| [NPCAIStyleID](npcs.md#npcaistyleid) | class | yes | Values of `NPC.aiStyle`. |
| [NPCDefinition](npcs.md#npcdefinition) | class | yes | NPCDefinition represents an NPC identity. |
| [NPCHappiness](npcs.md#npchappiness) | struct | yes | This struct provides access to an NPC type's NPC & Biome relationships. |
| [NPCHeadLoader](npcs.md#npcheadloader) | static class | yes | This class serves as a central place from which NPC head slots are stored and NPC head textures are assigned. |
| [NPCID](npcs.md#npcid) | struct | yes | NPC types. |
| [NPCID.BestiaryDrawOffsets](npcs.md#npcidbestiarydrawoffsets) | class | **no** | `NPCID.Sets.NPCBestiaryDrawOffset`: how an NPC is drawn in the bestiary. |
| [NPCID.PortraitOffsetTable](npcs.md#npcidportraitoffsettable) | class | **no** | `NPCID.Sets.NPCPortraitsCloseUpOffsets`: the position of a portrait in chat. |
| [NPCID.PortraitTable](npcs.md#npcidportraittable) | class | **no** | `NPCID.Sets.NPCPortraits`: the portrait shown in chat, per NPC. |
| [NPCID.Sets](npcs.md#npcidsets) | static class | yes | Tables indexed by NPC type. |
| [NPCID.Sets.NPCBestiaryDrawModifiers](npcs.md#npcidsetsnpcbestiarydrawmodifiers) | struct | yes | How to draw an NPC in the bestiary: position, velocity, scale, frame. |
| [NPCInteraction](npcs.md#npcinteraction) | abstract class | yes | A button of a town NPC's chat. |
| [NPCInteractionDatabase](npcs.md#npcinteractiondatabase) | class | yes | This holds and registers the `NPCInteractionList` data for each NPC. |
| [NPCInteractionDatabase.DedicatedButton](npcs.md#npcinteractiondatabasededicatedbutton) | class | **no** | A chat button with a fixed place: Close, Happiness, Housing. |
| [NPCInteractionList](npcs.md#npcinteractionlist) | class | yes | Allows for `NPCInteraction` chat buttons to be assigned to an NPC. |
| [NPCInteractionList.Entry](npcs.md#npcinteractionlistentry) | class | yes | This contains the NPCInteraction as well as if the interaction is enabled. |
| [NPCInteractions](npcs.md#npcinteractions) | static class | yes | Ready-made chat buttons. |
| [NPCInteractions.Actions](npcs.md#npcinteractionsactions) | static class | yes | The game's chat buttons as `NPCInteraction` objects. |
| [NPCInteractions.Actions.CloseChat](npcs.md#npcinteractionsactionsclosechat) | class | yes | The Close button. |
| [NPCInteractions.Actions.GuideReverseCrafting](npcs.md#npcinteractionsactionsguidereversecrafting) | class | yes | The Guide's crafting button. |
| [NPCInteractions.Actions.GuideTip](npcs.md#npcinteractionsactionsguidetip) | class | yes | The Guide's help button. |
| [NPCInteractions.Actions.OpenShop](npcs.md#npcinteractionsactionsopenshop) | class | yes | A button that opens a shop by name. |
| [NPCInteractions.Actions.VanillaOption](npcs.md#npcinteractionsactionsvanillaoption) | abstract class | **no** | One of the two buttons the game gives an NPC. |
| [NPCLoader](npcs.md#npcloader) | static class | yes | This serves as the central class from which NPC-related functions are carried out. |
| [NPCLoot](npcs.md#npcloot) | struct | yes | Provides access to `ItemDropDatabase`'s methods specific to this NPC type. |
| [NPCShop](npcs.md#npcshop) | class | yes | A town NPC's shop: `new NPCShop(Type).Add<MyItem>().Register()`. |
| [NPCShop.Entry](npcs.md#npcshopentry) | class | yes | One line of an `NPCShop`. |
| [NPCShopDatabase](npcs.md#npcshopdatabase) | static class | yes | Every shop by name. |
| [NPCSpawnInfo](npcs.md#npcspawninfo) | struct | **no** | Where and how an NPC would spawn, passed to `SpawnChance`. |
| [Profiles](npcs.md#profiles) | class | yes | Ready-made `ITownNPCProfile` classes. |
| [Profiles.DefaultNPCProfile](npcs.md#profilesdefaultnpcprofile) | class | yes | Class that is some-what identical to that allows for modded texture usage. |
| [Profiles.StackedNPCProfile](npcs.md#profilesstackednpcprofile) | class | yes | A profile that picks from others, usually normal and shimmered. |
| [SpawnCondition](npcs.md#spawncondition) | class | yes | This serves as a central class to help modders spawn their NPCs. |

## [Projectiles](projeteis.md)

| Class | Kind | In tModLoader | What it is |
|---|---|---|---|
| [GlobalProjectile](projeteis.md#globalprojectile) | abstract class | yes | This class allows you to modify and use hooks for all projectiles, both vanilla and modded. |
| [ModProjectile](projeteis.md#modprojectile) | abstract class | yes | This class serves as a place for you to place all your properties and hooks for each projectile. |
| [Projectile](projeteis.md#projectile) | struct | yes | What Bunny Loader adds to the game's projectile. |
| [ProjectileDefinition](projeteis.md#projectiledefinition) | class | yes | ProjectileDefinition represents a Projectile identity. |
| [ProjectileID](projeteis.md#projectileid) | struct | yes | Projectile types. |
| [ProjectileID.Sets](projeteis.md#projectileidsets) | static class | yes | Tables indexed by projectile type. |
| [ProjectileID.Sets.FallingBlockTileItemInfo](projeteis.md#projectileidsetsfallingblocktileiteminfo) | class | yes | Used in `FallingBlockTileItem`. |
| [ProjectileLoader](projeteis.md#projectileloader) | static class | yes | This serves as the central class from which projectile-related functions are carried out. |

## [Players, buffs, hair and mounts](jogadores.md)

| Class | Kind | In tModLoader | What it is |
|---|---|---|---|
| [BuffLoader](jogadores.md#buffloader) | static class | yes | This serves as the central class from which buff-related functions are supported and carried out. |
| [EquipTexture](jogadores.md#equiptexture) | class | yes | This serves as a place for you to program behaviors of equipment textures. |
| [Gender](jogadores.md#gender) | enum | yes | The gender a mod hair picks when the player randomizes. |
| [GlobalBuff](jogadores.md#globalbuff) | abstract class | yes | This class allows you to modify the behavior of any buff in the game. |
| [HairLoader](jogadores.md#hairloader) | static class | yes | Registers mod hairs. |
| [ModAccessorySlot](jogadores.md#modaccessoryslot) | abstract class | yes | A ModAccessorySlot instance represents a net new accessory slot instance. |
| [ModAccessorySlotPlayer](jogadores.md#modaccessoryslotplayer) | class | yes |  |
| [ModBuff](jogadores.md#modbuff) | abstract class | yes | This class serves as a place for you to define a new buff and how that buff behaves. |
| [ModHair](jogadores.md#modhair) | abstract class | yes | This class allows you to define custom hair styles for a player, controlling the associated gender and unlock conditions. |
| [ModMount](jogadores.md#modmount) | abstract class | yes | This class serves as a place for you to place all your properties and hooks for each mount. |
| [ModPlayer](jogadores.md#modplayer) | abstract class | yes | A ModPlayer instance represents an extension of a Player instance. |
| [ModResourceOverlay](jogadores.md#modresourceoverlay) | abstract class | yes | This class serves as a place for you to customize how the vanilla resource displays (Classic, Fancy and Bars) are drawn. |
| [Mount](jogadores.md#mount) | struct | yes | What Bunny Loader adds to the game's mount. |
| [Mount.MountData](jogadores.md#mountmountdata) | struct | yes | What Bunny Loader adds to a mount's data. |
| [Mount.MountDelegatesData](jogadores.md#mountmountdelegatesdata) | struct | yes | Replaceable methods of a mount, such as `MinecartDust`. |
| [MountLoader](jogadores.md#mountloader) | static class | yes | This serves as the central place from which mounts are stored and mount-related functions are carried out. |
| [MountTextureType](jogadores.md#mounttexturetype) | enum | yes | This is an enum of all possible types of extra mount textures for custom mounts. |
| [Player](jogadores.md#player) | struct | yes | What Bunny Loader adds to the game's player. |
| [Player.HurtInfo](jogadores.md#playerhurtinfo) | struct | yes | Represents a finalized damage calculation for damage about to be applied to a Player. |
| [Player.HurtModifiers](jogadores.md#playerhurtmodifiers) | struct | yes | Represents a damage calculation in the process of being calculated for damage to be applied to a Player. |
| [PlayerDrawLayer](jogadores.md#playerdrawlayer) | abstract class | yes | This class represents a DrawLayer for the player. |
| [PlayerDrawLayer.AfterParent](jogadores.md#playerdrawlayerafterparent) | class | yes | Places this layer immediately after (over) the provided parent layer. |
| [PlayerDrawLayer.BeforeParent](jogadores.md#playerdrawlayerbeforeparent) | class | yes | Places this layer immediately before (under) the provided parent layer. |
| [PlayerDrawLayer.Between](jogadores.md#playerdrawlayerbetween) | class | yes | Places this layer between the two provided layers. |
| [PlayerDrawLayer.Multiple](jogadores.md#playerdrawlayermultiple) | class | yes | Places this layer into multiple `Position`s. |
| [PlayerDrawLayer.Position](jogadores.md#playerdrawlayerposition) | abstract class | yes | A PlayerDrawLayer's position in the player rendering draw order. |
| [PlayerDrawLayer.Transformation](jogadores.md#playerdrawlayertransformation) | abstract class | yes | A change applied to every layer in a group, as `PlayerDrawLayer` positions it. |
| [PlayerDrawLayerLoader](jogadores.md#playerdrawlayerloader) | static class | yes | Orders the layers and draws them. |
| [PlayerDrawLayerSlot](jogadores.md#playerdrawlayerslot) | class | yes | A layer that stands for a slot where another is drawn. |
| [PlayerDrawLayers](jogadores.md#playerdrawlayers) | struct | yes | The game's layers by name, as `PlayerDrawLayers.Head`, to position yours against. |
| [PlayerDrawSet](jogadores.md#playerdrawset) | struct | yes | Contains all the parameters defining how a particular player will be rendered in the current lighting, copied from the `drawPlayer`. |
| [PlayerEyeHelper](jogadores.md#playereyehelper) | struct | yes | The blink state of the player's eyes. |
| [PlayerLoader](jogadores.md#playerloader) | static class | yes | This is where all ModPlayer hooks are gathered and called. |
| [PlayerStatsSnapshot](jogadores.md#playerstatssnapshot) | struct | yes | The player's life and mana at the moment the resource bars are drawn. |
| [ResourceOverlayDrawContext](jogadores.md#resourceoverlaydrawcontext) | struct | yes | What a `ModResourceOverlay` is drawing now: texture, position, color. |
| [ResourceOverlayLoader](jogadores.md#resourceoverlayloader) | static class | yes | Runs the hooks of mod resource overlays. |

## [Tiles, walls and world content](tiles.md)

| Class | Kind | In tModLoader | What it is |
|---|---|---|---|
| [BackgroundColorAttribute](tiles.md#backgroundcolorattribute) | class | yes | Specifies a background color to be used for the property, field, or class in the ModConfig UI. |
| [BackgroundTextureLoader](tiles.md#backgroundtextureloader) | static class | yes | This is the class that keeps track of all modded background textures and their slots/IDs. |
| [BiomeConversionLoader](tiles.md#biomeconversionloader) | static class | yes | Registers conversion types and runs the conversions of tiles and walls. |
| [BiomeLoader](tiles.md#biomeloader) | static class | yes | Runs the hooks of mod biomes. |
| [Chest](tiles.md#chest) | struct | yes | What Bunny Loader adds to the game's chest. |
| [CloudInABottleJump](tiles.md#cloudinabottlejump) | class | yes | One of the game's extra jumps, as an `ExtraJump` you can position against or read from `Player.GetJumpState`. |
| [CloudLoader](tiles.md#cloudloader) | static class | yes | Registers mod clouds. |
| [ContentBoolTable](tiles.md#contentbooltable) | class | **no** | A yes-or-no table by content type. |
| [ContentIntTable](tiles.md#contentinttable) | class | **no** | A number table by content type. |
| [ContentTable](tiles.md#contenttable) | class | **no** | A table by content type that holds objects. |
| [DustID](tiles.md#dustid) | static class | yes | The game's dust types. |
| [DustLoader](tiles.md#dustloader) | static class | yes | Registers mod dusts. |
| [FlexibleTileWand](tiles.md#flexibletilewand) | struct | yes | Lets an item place several tile styles, as the Rubble Maker does. |
| [GlobalPylon](tiles.md#globalpylon) | abstract class | yes | Global Type that exists for pylons that allows for modification of teleportation steps, map drawing, and other functionality for any pylon that exists, whether it be vanilla or modded. |
| [GlobalTile](tiles.md#globaltile) | abstract class | yes | This class allows you to modify the behavior of any tile in the game, both vanilla and modded. |
| [GlobalWall](tiles.md#globalwall) | abstract class | yes | This class allows you to modify the behavior of any wall in the game (although admittedly walls don't have much behavior). |
| [Gore](tiles.md#gore) | struct | yes | What Bunny Loader adds to the game's gore. |
| [GoreLoader](tiles.md#goreloader) | static class | yes | Registers mod gores. |
| [MapLayerLoader](tiles.md#maplayerloader) | static class | yes | Runs the mod map layers. |
| [ModBannerTile](tiles.md#modbannertile) | abstract class | yes | Extension to that streamlines the process of creating an enemy banner tile. |
| [ModBiome](tiles.md#modbiome) | abstract class | yes | This class represents a biome added by a mod. |
| [ModBiomeBestiaryInfoElement](tiles.md#modbiomebestiaryinfoelement) | struct | yes | The icon and name of a mod biome in a bestiary page. |
| [ModBiomeConversion](tiles.md#modbiomeconversion) | abstract class | yes | This class allows you to define a modded conversion to work with `Convert` and `RegisterConversion` / `RegisterConversion` For the moment, this class does not provide any functional hooks, but it can still be used by mods in combination with the aforementioned hooks to create custom conversion behavior |
| [ModBlockType](tiles.md#modblocktype) | abstract class | yes | This is the superclass for ModTile and ModWall, combining common code |
| [ModCactus](tiles.md#modcactus) | abstract class | yes | This class represents a type of modded cactus. |
| [ModCloud](tiles.md#modcloud) | abstract class | yes | This class allows you to customize the behavior of a custom cloud. |
| [ModDust](tiles.md#moddust) | abstract class | yes | This class represents a type of dust that is added by a mod. |
| [ModGore](tiles.md#modgore) | abstract class | yes | This class allows you to customize the behavior of a custom gore. |
| [ModMapLayer](tiles.md#modmaplayer) | abstract class | yes | This class is used to facilitate easily drawing icons and other things over the map. |
| [ModMapLayer.After](tiles.md#modmaplayerafter) | class | yes | Draws a map layer after another one. |
| [ModMapLayer.Before](tiles.md#modmaplayerbefore) | class | yes | Draws a map layer before another one. |
| [ModMapLayer.Position](tiles.md#modmaplayerposition) | abstract class | yes | Where a map layer goes in the draw order. |
| [ModPylon](tiles.md#modpylon) | abstract class | yes | Extension to that streamlines the process of creating a modded Pylon. |
| [ModSceneEffect](tiles.md#modsceneeffect) | abstract class | yes | ModSceneEffect is an abstract class that your classes can derive from. |
| [ModSubworld](tiles.md#modsubworld) | abstract class | **no** | A dimension: a separate world the player travels to, with its own size, generation and save file. |
| [ModSurfaceBackgroundStyle](tiles.md#modsurfacebackgroundstyle) | abstract class | yes | Each background style determines in its own way how exactly the background is drawn. |
| [ModTile](tiles.md#modtile) | abstract class | yes | This class represents a type of tile that can be added by a mod. |
| [ModTileEntity](tiles.md#modtileentity) | abstract class | yes | Tile Entities are Entities tightly coupled with tiles, allowing the possibility of tiles to exhibit cool behavior. |
| [ModTree](tiles.md#modtree) | abstract class | yes | This class represents a type of modded tree. |
| [ModUndergroundBackgroundStyle](tiles.md#modundergroundbackgroundstyle) | abstract class | yes | Each background style determines in its own way how exactly the background is drawn. |
| [ModWall](tiles.md#modwall) | abstract class | yes | This class represents a type of wall that can be added by a mod. |
| [ModWaterStyle](tiles.md#modwaterstyle) | abstract class | yes | Represents a style of water that gets drawn, based on factors such as the background. |
| [ModWaterfallStyle](tiles.md#modwaterfallstyle) | abstract class | yes | Represents a style of waterfalls that gets drawn. |
| [PlantLoader](tiles.md#plantloader) | static class | yes | Finds which tree, palm tree or cactus of a mod grows on a ground. |
| [PressurePlateHelper](tiles.md#pressureplatehelper) | struct | yes | Tells which pressure plates are being stepped on. |
| [PylonLoader](tiles.md#pylonloader) | static class | yes | Runs the hooks of `GlobalPylon` and of mod pylons. |
| [SceneEffectLoader](tiles.md#sceneeffectloader) | static class | yes | This serves as the central class from which SceneEffect functions are supported and carried out. |
| [SceneEffectLoader.SceneEffectInstance](tiles.md#sceneeffectloadersceneeffectinstance) | class | yes | The scene the player is in now: music, water, backgrounds, each with a priority. |
| [SceneEffectLoader.SceneEffectInstance.PrioritizedPair](tiles.md#sceneeffectloadersceneeffectinstanceprioritizedpair) | struct | yes | A value and the priority that chose it. |
| [SceneEffectPriority](tiles.md#sceneeffectpriority) | enum | yes | This enum dictates from low to high which SceneEffect selections take priority. |
| [SubworldLoader](tiles.md#subworldloader) | static class | **no** | Travels between the main world and the dimensions: `Enter`, `Exit`, `Current`. |
| [TEModdedPylon](tiles.md#temoddedpylon) | abstract class | yes | This is a TML provided Tile Entity that acts extremely similar to vanilla's pylon TEs by default. |
| [TeleportPylonInfo](tiles.md#teleportpyloninfo) | struct | yes | A pylon in the world: its position and type. |
| [Tile](tiles.md#tile) | struct | yes | A data structure used for accessing information about tiles, walls, wires, and liquids at a single position in the world. |
| [TileData](tiles.md#tiledata) | struct | **no** | The tile data of the world. |
| [TileDefinition](tiles.md#tiledefinition) | class | yes | TileDefinition represents a Tile identity. |
| [TileDrawing](tiles.md#tiledrawing) | struct | yes | What Bunny Loader adds to the game's tile drawing. |
| [TileEntity](tiles.md#tileentity) | struct | yes | A tile with its own data. |
| [TileID](tiles.md#tileid) | struct | yes | Tile types. |
| [TileID.Sets](tiles.md#tileidsets) | static class | yes | Tables indexed by tile type. |
| [TileID.Sets.FallingBlockProjectileInfo](tiles.md#tileidsetsfallingblockprojectileinfo) | class | yes | Used in `FallingBlockProjectile`. |
| [TileLoader](tiles.md#tileloader) | static class | yes | This serves as the central class from which tile-related functions are supported and carried out. |
| [TileObjectData](tiles.md#tileobjectdata) | struct | yes | The layout of a multi-tile object: size, anchors, styles. |
| [TileRestingInfo](tiles.md#tilerestinginfo) | struct | yes | Holds data required for offsetting an entity when it rests on a tile (sitting/sleeping). |
| [WallLoader](tiles.md#wallloader) | static class | yes | This serves as the central class from which wall-related functions are supported and carried out. |
| [WaterFallStylesLoader](tiles.md#waterfallstylesloader) | static class | yes | Registers mod waterfall styles. |
| [WaterStylesLoader](tiles.md#waterstylesloader) | static class | yes | Registers mod water styles. |
| [WorldGen](tiles.md#worldgen) | struct | yes | What Bunny Loader adds to the game's world generation. |
| [WorldGenLegacyMethod](tiles.md#worldgenlegacymethod) | delegate | yes | The body of a `PassLegacy`. |

## [Mods, systems and network](sistemas.md)

| Class | Kind | In tModLoader | What it is |
|---|---|---|---|
| [Asset](sistemas.md#asset) | class | **no** | A texture, sound or effect that may still be loading. |
| [AssetArray](sistemas.md#assetarray) | struct | **no** | A game table of assets, such as `TextureAssets.Item`. |
| [AssetRequestMode](sistemas.md#assetrequestmode) | enum | **no** | Whether a request loads in the background or now. |
| [AssetState](sistemas.md#assetstate) | enum | **no** | Where an asset is in its loading. |
| [AutoloadAttribute](sistemas.md#autoloadattribute) | class | yes | Allows for customizing if the annotated Type should be automatically loaded. |
| [AutoloadEquip](sistemas.md#autoloadequip) | class | yes | This attribute annotates a `ModItem` class to indicate that the game should autoload the specified equipment texture or textures and assign it to this item. |
| [BinaryIO](sistemas.md#binaryio) | static class | yes | Reads and writes colors, vectors, items and compact numbers in a packet. |
| [BitReader](sistemas.md#bitreader) | class | yes | Reads back what a `BitWriter` wrote. |
| [BitWriter](sistemas.md#bitwriter) | class | yes | Packs many `bool` into a few bytes. |
| [CommandCaller](sistemas.md#commandcaller) | interface | yes | Who ran a chat command: the player and where to reply. |
| [CommandType](sistemas.md#commandtype) | enum | yes | A flag enum representing context where this command operates. |
| [ILoadable](sistemas.md#iloadable) | interface | yes | Works with `AddContent` to provide Load and Unload callbacks. |
| [ILocalizedModType](sistemas.md#ilocalizedmodtype) | interface | yes | Content with a place in the localization files. |
| [ILocalizedModTypeExtensions](sistemas.md#ilocalizedmodtypeextensions) | static class | yes | Finds the key and the text of a content's localization. |
| [Lang](sistemas.md#lang) | struct | yes | What Bunny Loader adds to the game's language tables. |
| [Language](sistemas.md#language) | static class | yes | Contains methods to access or retrieve localization values. |
| [LocalizedText](sistemas.md#localizedtext) | struct | yes | Contains the localization value corresponding to a key for the current game language. |
| [Mod](sistemas.md#mod) | abstract class | yes | Mod is an abstract class that you will override. |
| [ModCommand](sistemas.md#modcommand) | abstract class | yes | This class represents a chat or console command. |
| [ModContent](sistemas.md#modcontent) | static class | yes | Manages content added by mods. |
| [ModLoader](sistemas.md#modloader) | static class | yes | This serves as the central class which loads mods. |
| [ModPacket](sistemas.md#modpacket) | class | yes | Used to send data between the server and client. |
| [ModSystem](sistemas.md#modsystem) | abstract class | yes | ModSystem is an abstract class that your classes can derive from. |
| [ModTexturedType](sistemas.md#modtexturedtype) | abstract class | yes | The base type for most modded things with textures. |
| [ModType](sistemas.md#modtype) | abstract class | yes | The base type for most modded things. |
| [ModTypeLookup](sistemas.md#modtypelookup) | static class | yes | Enables content instances to register with `Register` towards retrieval via and similar methods. |
| [NetworkText](sistemas.md#networktext) | struct | yes | Text sent over the network, translated on each client. |
| [SystemLoader](sistemas.md#systemloader) | static class | yes | This is where all `ModSystem` hooks are gathered and called. |
| [TagCompound](sistemas.md#tagcompound) | class | yes | Tag compounds contained named values, serializable as per the NBT spec: NBT spec wiki page All primitive data types are supported as well as byte[], int[] and Lists of other supported data types Lists of Lists are internally stored as IList<IList> Modification of lists stored in a TagCompound will only work if there were no type conversions involved and is not advised bool is supported using TagConverter, serialized as a byte. |
| [TagIO](sistemas.md#tagio) | static class | yes | Writes and reads a `TagCompound` in binary. |
| [TagSerializable](sistemas.md#tagserializable) | interface | yes | An object that saves itself to a `TagCompound`. |
| [UsageException](sistemas.md#usageexception) | class | yes | Throw it in a command to show the player how to use the command. |

## [Interface, input and config](interface.md)

| Class | Kind | In tModLoader | What it is |
|---|---|---|---|
| [AchievementLoader](interface.md#achievementloader) | static class | **no** | Registers mod achievements. |
| [AchievementsHelper](interface.md#achievementshelper) | struct | yes | Reports achievement progress. |
| [ArmorShaderData](interface.md#armorshaderdata) | struct | yes | A shader for a dye. |
| [ArmorShaderDataSet](interface.md#armorshaderdataset) | struct | yes | `GameShaders.Armor`: binds a dye item to its shader. |
| [Color](interface.md#color) | struct | **no** | What Bunny Loader adds to the color. |
| [ColorHSLSliderAttribute](interface.md#colorhslsliderattribute) | class | yes | Add this attribute to a Color item and the UI will present a Hue, Saturation, and Lightness sliders rather than Red, Green, and Blue sliders. |
| [ColorNoAlphaAttribute](interface.md#colornoalphaattribute) | class | yes | Add this attribute to a Color item and Alpha will not be presented in the UI and will remain as 255 unless manually edited. |
| [Colors](interface.md#colors) | static class | yes | The game's named colors. |
| [ConfigArgsAttribute](interface.md#configargsattribute) | abstract class | yes | Base of config attributes with arguments. |
| [ConfigKeyAttribute](interface.md#configkeyattribute) | abstract class | yes | Base of config attributes that take a localization key. |
| [ConfigManager](interface.md#configmanager) | static class | yes | Finds configs by type. |
| [ConfigSaveResult](interface.md#configsaveresult) | enum | yes | Contains potential results for `SaveChanges`. |
| [ConfigScope](interface.md#configscope) | enum | yes | Each ModConfig class has a different scope. |
| [DefaultDamageClass](interface.md#defaultdamageclass) | class | yes | One of the game's damage classes. |
| [DefaultDictionaryKeyValueAttribute](interface.md#defaultdictionarykeyvalueattribute) | class | yes | Defines the default key value to be added when using the ModConfig UI to add elements to a Dictionary. |
| [DefaultListValueAttribute](interface.md#defaultlistvalueattribute) | class | yes | Defines the default value to be added when using the ModConfig UI to add elements to a Collection (List, Set, or Dictionary value). |
| [DrawTicksAttribute](interface.md#drawticksattribute) | class | yes | Add this attribute and the sliders will show white tick marks at each increment. |
| [GameInterfaceDrawMethod](interface.md#gameinterfacedrawmethod) | delegate | yes | The body of a `LegacyGameInterfaceLayer`. |
| [GameInterfaceLayer](interface.md#gameinterfacelayer) | class | yes | One layer of the game's interface, from `ModSystem.ModifyInterfaceLayers`. |
| [HairColorMethod](interface.md#haircolormethod) | delegate | **no** | The color a hair dye gives. |
| [HairID](interface.md#hairid) | class | yes | Hair types. |
| [HairID.Sets](interface.md#hairidsets) | static class | yes | Tables indexed by hair type. |
| [HairShaderDataSet](interface.md#hairshaderdataset) | struct | yes | `GameShaders.Hair`: binds a hair dye to its shader. |
| [HeaderAttribute](interface.md#headerattribute) | class | yes | This attribute adds a label above this property or field in the ModConfig UI that acts as a header. |
| [IncrementAttribute](interface.md#incrementattribute) | class | yes | Use this to set an increment for sliders (if using `SliderAttribute`) or the +/- buttons. |
| [JsonDefaultDictionaryKeyValueAttribute](interface.md#jsondefaultdictionarykeyvalueattribute) | class | yes | Similar to JsonDefaultListValueAttribute, but for assigning to the Dictionary Key rather than the Value. |
| [JsonDefaultListValueAttribute](interface.md#jsondefaultlistvalueattribute) | class | yes | Similar to DefaultListValueAttribute but for reference types. |
| [JsonDefaultValueAttribute](interface.md#jsondefaultvalueattribute) | class | yes | Similar to DefaultValueAttribute but for reference types. |
| [KeybindLoader](interface.md#keybindloader) | static class | yes | Registers a `ModKeybind`. |
| [LabelArgsAttribute](interface.md#labelargsattribute) | class | yes | Use to provide values to be interpolated into the label of the annotated property or field. |
| [LabelKeyAttribute](interface.md#labelkeyattribute) | class | yes | A label is the text shown to the user in the ModConfig UI. |
| [LegacyGameInterfaceLayer](interface.md#legacygameinterfacelayer) | class | yes | A layer that draws with a method of yours. |
| [MenuLoader](interface.md#menuloader) | static class | yes | Runs the mod title menus. |
| [ModAchievement](interface.md#modachievement) | abstract class | yes | Defines a custom achievement and implements how it should act upon completion. |
| [ModAchievement.After](interface.md#modachievementafter) | class | yes | Places an achievement after another one. |
| [ModAchievement.Before](interface.md#modachievementbefore) | class | yes | Places an achievement before another one. |
| [ModAchievement.Default](interface.md#modachievementdefault) | class | yes | Places an achievement in its default spot, at the end. |
| [ModAchievement.Position](interface.md#modachievementposition) | abstract class | yes | Where an achievement goes in the list. |
| [ModConfig](interface.md#modconfig) | abstract class | yes | ModConfig provides a way for mods to be configurable. |
| [ModKeybind](interface.md#modkeybind) | class | yes | Represents a loaded input binding. |
| [ModMenu](interface.md#modmenu) | abstract class | yes | A class that is used to customize aesthetic features of the main menu, such as the logo, background and music. |
| [ModTouchButton](interface.md#modtouchbutton) | class | **no** | A button on the screen, placed by the player in the game's controls editor. |
| [ModTouchJoystick](interface.md#modtouchjoystick) | class | **no** | A button the player drags to aim. |
| [MusicLoader](interface.md#musicloader) | static class | yes | Registers mod music and music boxes. |
| [OptionStringsAttribute](interface.md#optionstringsattribute) | class | yes | By default, string fields will provide the user with a text input field. |
| [RangeAttribute](interface.md#rangeattribute) | class | yes | Specifies a range for primitive data values. |
| [RangedDamageClass](interface.md#rangeddamageclass) | class | yes | One of the game's damage classes. |
| [ReloadRequiredAttribute](interface.md#reloadrequiredattribute) | class | yes | This attribute hints that changing the value of the annotated property or field will put the config in a state that requires a reload. |
| [SeparatePageAttribute](interface.md#separatepageattribute) | class | yes | This specifies that the annotated item will appear as a button that leads to a separate page in the UI. |
| [ShowDespiteJsonIgnoreAttribute](interface.md#showdespitejsonignoreattribute) | class | yes | This attribute indicates that the field or property should be shown in the ModConfig UI despite having a `JsonIgnoreAttribute` annotation. |
| [ShowItemDropInUI](interface.md#showitemdropinui) | enum | yes | Whether a drop shows in the bestiary. |
| [SliderAttribute](interface.md#sliderattribute) | class | yes | Affects whether this data will be presented as a slider or an input field. |
| [SliderColorAttribute](interface.md#slidercolorattribute) | class | yes | Specifies a slider color for ModConfig elements that use a slider. |
| [SoundEngine](interface.md#soundengine) | static class | yes | Plays sounds. |
| [SoundID](interface.md#soundid) | struct | yes |  |
| [SoundLimitBehavior](interface.md#soundlimitbehavior) | enum | yes | What happens when too many of a sound play together. |
| [SoundStyle](interface.md#soundstyle) | struct | yes | This data type describes in detail how a sound should be played. |
| [SoundUpdateCallback](interface.md#soundupdatecallback) | delegate | **no** | Runs each frame for a playing sound, to change it or stop it. |
| [TooltipArgsAttribute](interface.md#tooltipargsattribute) | class | yes | Use to provide values to be interpolated into the tooltip of the annotated property or field. |
| [TooltipKeyAttribute](interface.md#tooltipkeyattribute) | class | yes | A tooltip is the text shown to the user in the ModConfig UI next to the cursor when they hover over the annotated member (property, field, or class). |
| [TouchButtonLoader](interface.md#touchbuttonloader) | static class | **no** | Registers touch buttons and joysticks: `RegisterButton`, `RegisterJoystick`. |
| [UICommon](interface.md#uicommon) | static class | yes | Common colors and text of the mod menus. |
| [UIElement](interface.md#uielement) | class | yes | The base of every UI piece: position and size in `StyleDimension`, children, events, drawing. |
| [UIEvent](interface.md#uievent) | class | yes | An event of an element. |
| [UIImage](interface.md#uiimage) | class | yes | Shows a texture. |
| [UIImageButton](interface.md#uiimagebutton) | class | yes | A texture that responds to a touch. |
| [UIImageFramed](interface.md#uiimageframed) | class | yes | Shows one frame of a texture. |
| [UIList](interface.md#uilist) | class | yes | A scrollable list element. |
| [UIMouseEvent](interface.md#uimouseevent) | class | yes | A touch on an element. |
| [UIPanel](interface.md#uipanel) | class | yes | The game's blue panel. |
| [UIScrollWheelEvent](interface.md#uiscrollwheelevent) | class | yes | A scroll on an element. |
| [UIScrollbar](interface.md#uiscrollbar) | class | yes | The game's scrollbar. |
| [UIState](interface.md#uistate) | class | yes | A full screen of UI: the root element you give a `UserInterface`. |
| [UIText](interface.md#uitext) | class | yes | Text. |
| [UITextPanel](interface.md#uitextpanel) | class | yes | A panel with text in it. |
| [UserInterface](interface.md#userinterface) | class | yes | Runs one `UIState`: update, draw and touch. |

## [Talking to the game](interop.md)

| Class | Kind | In tModLoader | What it is |
|---|---|---|---|
| [GameArray](interop.md#gamearray) | struct | **no** | An array of the game, such as `Main.npc`. |
| [GameArray2D](interop.md#gamearray2d) | struct | **no** | A two-dimensional array of the game. |
| [GameClass](interop.md#gameclass) | class | **no** | A class of the game by namespace and name, to create arrays of it. |
| [GameConfiguration](interop.md#gameconfiguration) | struct | yes | What Bunny Loader adds to the game's settings file. |
| [GameDictionary](interop.md#gamedictionary) | struct | **no** | A `Dictionary` of the game. |
| [GameException](interop.md#gameexception) | class | **no** | An exception thrown by the game's code, carried into C#. |
| [GameHandle](interop.md#gamehandle) | class | **no** | Holds a game object so the game's garbage collector keeps it. |
| [GameList](interop.md#gamelist) | struct | **no** | A `List` of the game. |
| [GameListExtensions](interop.md#gamelistextensions) | static class | **no** | Helper for `List<int>` fields of the game, through `GameObject`. |
| [GameNullable](interop.md#gamenullable) | struct | **no** | `int?` and friends of the game. |
| [GameObject](interop.md#gameobject) | struct | **no** | Any object of the game, as a pointer. |
| [GameString](interop.md#gamestring) | struct | **no** | A string of the game. |
| [GameTipData](interop.md#gametipdata) | class | yes | Wrapper class for a LocalizedText and visibility field that has intended use with modification of Game Tips. |
| [GameTipID](interop.md#gametipid) | static class | yes | Numbers of the game's tips. |
| [IGameObject](interop.md#igameobject) | interface | **no** | Every type that wraps a game object, with its `Pointer`. |

## [Everything else](outros.md)

| Class | Kind | In tModLoader | What it is |
|---|---|---|---|
| [AccessorySlotLoader](outros.md#accessoryslotloader) | class | yes | This serves as a central place to store equipment slots and their corresponding textures. |
| [AccessorySlotType](outros.md#accessoryslottype) | enum | yes |  |
| [AddableFloat](outros.md#addablefloat) | struct | yes | A value that only adds, as in `StatModifier.Base`. |
| [AnchorData](outros.md#anchordata) | struct | yes | How a tile object attaches to what is around it. |
| [Animation](outros.md#animation) | struct | yes | What Bunny Loader adds to the game's tile animations. |
| [BasiliskMountJump](outros.md#basiliskmountjump) | class | yes | One of the game's extra jumps, as an `ExtraJump` you can position against or read from `Player.GetJumpState`. |
| [BigEndianReader](outros.md#bigendianreader) | class | yes | A `BinaryReader` that reads big-endian numbers, for NBT files. |
| [BigEndianWriter](outros.md#bigendianwriter) | class | yes | A `BinaryWriter` that writes big-endian numbers, for NBT files. |
| [BigProgressBarSystem](outros.md#bigprogressbarsystem) | struct | yes |  |
| [BiomeConversionID](outros.md#biomeconversionid) | static class | yes | The game's conversion types, as `WorldGen.Convert` takes them. |
| [BlizzardInABottleJump](outros.md#blizzardinabottlejump) | class | yes | One of the game's extra jumps, as an `ExtraJump` you can position against or read from `Player.GetJumpState`. |
| [BlockType](outros.md#blocktype) | enum | yes | The shape of a tile: solid, half brick, slopes. |
| [BuffDefinition](outros.md#buffdefinition) | class | yes | BuffDefinition represents an Buff identity. |
| [BuffDrawParams](outros.md#buffdrawparams) | struct | yes | Holds data required for buff drawing. |
| [BuffID](outros.md#buffid) | struct | yes | Buff types. |
| [BuffID.Sets](outros.md#buffidsets) | struct | yes | Tables indexed by buff type. |
| [BuffID.Sets.BuffTextHandlerTable](outros.md#buffidsetsbufftexthandlertable) | class | **no** | Custom buff time text, per buff. |
| [BuilderToggle](outros.md#buildertoggle) | abstract class | yes | Represents a builder toggle button shown in the top left corner of the screen while the inventory is shown. |
| [BuilderToggle.After](outros.md#buildertoggleafter) | class | yes | Places a builder toggle after another one. |
| [BuilderToggle.Before](outros.md#buildertogglebefore) | class | yes | Places a builder toggle before another one. |
| [BuilderToggle.Default](outros.md#buildertoggledefault) | class | yes | Places a builder toggle in its default spot. |
| [BuilderToggle.Position](outros.md#buildertoggleposition) | abstract class | yes | Where a builder toggle goes in the row. |
| [BuilderToggleDrawParams](outros.md#buildertoggledrawparams) | struct | yes | Holds data required for builder toggle drawing. |
| [BuyItemCreationContext](outros.md#buyitemcreationcontext) | class | yes | Created in the context of a player buying an item from a shop. |
| [CachedProjectileCounterBuffTextHandler](outros.md#cachedprojectilecounterbufftexthandler) | struct | yes | A buff text that counts a projectile, as minion buffs do. |
| [CalculatedStyle](outros.md#calculatedstyle) | struct | yes | The final position and size of an element, in pixels. |
| [CameraModifierStack](outros.md#cameramodifierstack) | struct | yes | `Main.instance.CameraModifiers`: adds a screen shake or camera move. |
| [Chains](outros.md#chains) | static class | yes | Chains one rule to another with `OnSuccess`, `OnFailedRoll`, `OnFailedConditions`. |
| [Chains.TryIfDoesntFillConditions](outros.md#chainstryifdoesntfillconditions) | class | yes | Runs the next rule when the conditions fail. |
| [Chains.TryIfFailedRandomRoll](outros.md#chainstryiffailedrandomroll) | class | yes | Runs the next rule when the roll fails. |
| [Chains.TryIfSucceeded](outros.md#chainstryifsucceeded) | class | yes | Runs the next rule when this one drops. |
| [Collision](outros.md#collision) | struct | yes | What Bunny Loader adds to the game's collision. |
| [CommandLoader](outros.md#commandloader) | static class | yes | This serves as the central class from which ModCommand functions are supported and carried out. |
| [CommonCode](outros.md#commoncode) | static class | yes | Helpers that drop items, used by rules. |
| [CommonDrop](outros.md#commondrop) | class | yes | Drops an item with a chance. |
| [CommonDropNotScalingWithLuck](outros.md#commondropnotscalingwithluck) | class | yes | A `CommonDrop` that ignores luck. |
| [CommonDropScalingWithOnlyBadLuck](outros.md#commondropscalingwithonlybadluck) | class | yes | A `CommonDrop` that only bad luck changes. |
| [CommonDropWithRerolls](outros.md#commondropwithrerolls) | class | yes | A `CommonDrop` with more tries. |
| [ContentSamples](outros.md#contentsamples) | static class | yes | A ready sample of each item, NPC and projectile, set up with defaults. |
| [ContentSamples.BestiaryRarityStars](outros.md#contentsamplesbestiaryraritystars) | class | **no** | Stars each NPC gets in the bestiary. |
| [ContentSamples.ItemSamples](outros.md#contentsamplesitemsamples) | class | **no** | `ContentSamples.ItemsByType`. |
| [ContentSamples.ProjectileSamples](outros.md#contentsamplesprojectilesamples) | class | **no** | `ContentSamples.ProjectilesByType`. |
| [CursorOverrideID](outros.md#cursoroverrideid) | class | yes | Cursor icons in the inventory. |
| [CustomModConfigItemAttribute](outros.md#custommodconfigitemattribute) | class | yes | Use this attribute to specify a custom UI element to be used for the annotated property, field, or class in the ModConfig UI. |
| [CycleAttribute](outros.md#cycleattribute) | class | yes | If present, this data will be presented as a cylc button. |
| [DashID](outros.md#dashid) | static class | yes | Corresponds to values assigned to `dashType`. |
| [DrawAnimationVertical](outros.md#drawanimationvertical) | struct | yes | An animation for `Main.RegisterItemAnimation`: frames stacked vertically. |
| [DrawData](outros.md#drawdata) | struct | yes | A sprite waiting to be drawn, as the player draw layers use it. |
| [DrawDataList](outros.md#drawdatalist) | struct | **no** | The list a player draw layer adds its `DrawData` to. |
| [DropAttemptInfo](outros.md#dropattemptinfo) | struct | yes | What is happening when a rule runs: the NPC, player, item and the random source. |
| [DropBasedOnExpertMode](outros.md#dropbasedonexpertmode) | class | yes | Runs one rule in Expert and another in Normal. |
| [DropBasedOnExtraGel](outros.md#dropbasedonextragel) | class | yes | Runs a rule by the extra gel of a slime. |
| [DropBasedOnMasterAndExpertMode](outros.md#dropbasedonmasterandexpertmode) | class | yes | Runs one of three rules by difficulty. |
| [DropBasedOnMasterMode](outros.md#dropbasedonmastermode) | class | yes | Runs one rule in Master and another below it. |
| [DropLocalPerClientAndResetsNPCMoneyTo0](outros.md#droplocalperclientandresetsnpcmoneyto0) | class | yes | Drops for each client, and takes the NPC's money away. |
| [DropNothing](outros.md#dropnothing) | class | yes | Drops nothing. |
| [DropOneByOne](outros.md#droponebyone) | class | yes | Drops items one at a time, each with its chance. |
| [DropOneByOne.Parameters](outros.md#droponebyoneparameters) | struct | yes | The amounts and chances of a `DropOneByOne`. |
| [DropPerPlayerOnThePlayer](outros.md#dropperplayerontheplayer) | class | yes | Drops one for each player, at the player. |
| [DropRateInfo](outros.md#droprateinfo) | struct | yes | What the bestiary shows for a drop. |
| [DropRateInfoChainFeed](outros.md#droprateinfochainfeed) | struct | yes | The chance carried along a rule chain, for the bestiary. |
| [DropdownAttribute](outros.md#dropdownattribute) | class | yes | If present, this data will be presented as a drop down. |
| [Entity](outros.md#entity) | struct | yes | What Bunny Loader adds to the game's entity. |
| [EntityDefinition](outros.md#entitydefinition) | abstract class | yes | Classes implementing EntityDefinition serve to function as a way to save and load the identities of various Terraria objects. |
| [EntitySource_ShakeTree](outros.md#entitysource_shaketree) | struct | yes | The source of what falls from a shaken tree. |
| [EntitySource_TileBreak](outros.md#entitysource_tilebreak) | struct | yes | The source of what a broken tile drops. |
| [EntitySource_TileUpdate](outros.md#entitysource_tileupdate) | struct | yes | Intended for mods to use when a tile spawns an entity due to periodic/random updating, rather than in response to a specific trigger. |
| [EquipType](outros.md#equiptype) | enum | yes | This is an enum of all the types of equipment that exist. |
| [ExpandAttribute](outros.md#expandattribute) | class | yes | Affects whether this data will be expanded by default. |
| [Extensions](outros.md#extensions) | static class | yes | Chain methods as extensions of a rule. |
| [ExtraJump](outros.md#extrajump) | abstract class | yes | `ExtraJump` is a singleton, defining the properties and behaviour of midair extra jumps. |
| [ExtraJump.After](outros.md#extrajumpafter) | class | yes | Places an extra jump after another one. |
| [ExtraJump.Before](outros.md#extrajumpbefore) | class | yes | Places an extra jump before another one. |
| [ExtraJump.Position](outros.md#extrajumpposition) | abstract class | yes | Where an extra jump goes in the order the game tries them. |
| [ExtraJumpLoader](outros.md#extrajumploader) | static class | yes | Runs the extra jumps. |
| [ExtraJumpState](outros.md#extrajumpstate) | struct | yes | A structure containing fields used to manage extra jumps Valid states for an extra jump are as follows: Enabled = `false` \| The extra jump cannot be used. |
| [FartInAJarJump](outros.md#fartinajarjump) | class | yes | One of the game's extra jumps, as an `ExtraJump` you can position against or read from `Player.GetJumpState`. |
| [FilterManager](outros.md#filtermanager) | struct | yes | `Filters.Scene`: the full-screen filters. |
| [FlavorTextBestiaryInfoElement](outros.md#flavortextbestiaryinfoelement) | struct | yes | The description of a bestiary page, by localization key. |
| [FlipperJump](outros.md#flipperjump) | class | yes | One of the game's extra jumps, as an `ExtraJump` you can position against or read from `Player.GetJumpState`. |
| [FromOptionsWithoutRepeatsDropRule](outros.md#fromoptionswithoutrepeatsdroprule) | class | yes | Drops some items from a list, with no repeats. |
| [GenBase](outros.md#genbase) | abstract class | yes | The base of a world generation step. |
| [GenPass](outros.md#genpass) | abstract class | yes | One step of world generation, added in `ModSystem.ModifyWorldGenTasks`. |
| [GenerationProgress](outros.md#generationprogress) | struct | yes | The progress text and value of a step. |
| [GenericDamageClass](outros.md#genericdamageclass) | class | yes | One of the game's damage classes. |
| [GlobalBackgroundStyle](outros.md#globalbackgroundstyle) | abstract class | yes | This class serves to collect functions that operate on any kind of background style, without being specific to one single background style. |
| [GlobalLoot](outros.md#globalloot) | struct | yes | This readonly struct is a simple shortcut for modifying global drop rules in an `ItemDropDatabase`. |
| [GlobalType](outros.md#globaltype) | abstract class | yes | The base of every `GlobalX`. |
| [GoatMountJump](outros.md#goatmountjump) | class | yes | One of the game's extra jumps, as an `ExtraJump` you can position against or read from `Player.GetJumpState`. |
| [ICustomAutoload](outros.md#icustomautoload) | interface | yes | Provides a static abstract `Autoload` method for mods to implement their own autoloading for types where `ILoadable` does not suffice. |
| [IEntityWithGlobals](outros.md#ientitywithglobals) | interface | yes | An entity with `GlobalX` instances. |
| [ILoot](outros.md#iloot) | interface | yes | A list of drop rules: `npcLoot` and `itemLoot` in `ModifyNPCLoot` and `ModifyItemLoot`. |
| [IMapLayer](outros.md#imaplayer) | struct | yes | A layer of the full map. |
| [IModCameraModifier](outros.md#imodcameramodifier) | interface | **no** | A change to the camera, per frame. |
| [IModParticle](outros.md#imodparticle) | interface | **no** | A particle you draw yourself. |
| [IModType](outros.md#imodtype) | interface | yes | Content with a mod and a name. |
| [INestedItemDropRule](outros.md#inesteditemdroprule) | interface | yes | A rule that runs another rule. |
| [IPlant](outros.md#iplant) | interface | yes | Common to mod trees, palm trees and cactus. |
| [IProvideItemConditionDescription](outros.md#iprovideitemconditiondescription) | interface | yes | A condition with text for the bestiary. |
| [ITree](outros.md#itree) | interface | yes | Common to mod trees and palm trees. |
| [ImmunityCooldownID](outros.md#immunitycooldownid) | static class | yes | The kinds of immunity after a hit. |
| [InfoDisplay](outros.md#infodisplay) | abstract class | yes | Represents an informational display toggle typically provided by informational accessories. |
| [LeadingConditionRule](outros.md#leadingconditionrule) | class | yes | A condition with no drop, that leads a chain. |
| [LegacyHairShaderData](outros.md#legacyhairshaderdata) | struct | yes | A hair dye made of a color method. |
| [LegacyNameAttribute](outros.md#legacynameattribute) | class | yes | Allows for types to be registered with legacy/alias names for lookup via and similar methods. |
| [LegacySoundStyle](outros.md#legacysoundstyle) | struct | yes | The game's sound by number. |
| [LoaderManager](outros.md#loadermanager) | static class | yes |  |
| [MagicDamageClass](outros.md#magicdamageclass) | class | yes | One of the game's damage classes. |
| [MagicSummonHybridDamageClass](outros.md#magicsummonhybriddamageclass) | class | yes | One of the game's damage classes. |
| [Main](outros.md#main) | struct | yes | What Bunny Loader adds to the game's main class. |
| [MechBossSpawnersDropRule](outros.md#mechbossspawnersdroprule) | class | yes | The spawners of the mechanical bosses. |
| [MeleeDamageClass](outros.md#meleedamageclass) | class | yes | One of the game's damage classes. |
| [MeleeNoSpeedDamageClass](outros.md#meleenospeeddamageclass) | class | yes | One of the game's damage classes. |
| [ModBackgroundStyle](outros.md#modbackgroundstyle) | abstract class | yes | Base of the mod backgrounds. |
| [ModSide](outros.md#modside) | enum | yes | A ModSide enum defines how mods are synced between clients and servers. |
| [MultipliableFloat](outros.md#multipliablefloat) | struct | yes | A value that only multiplies, as in `StatModifier`. |
| [NativeDropCondition](outros.md#nativedropcondition) | class | **no** | A condition of the mobile game, with a C# face. |
| [NativeDropRule](outros.md#nativedroprule) | class | **no** | A rule of the mobile game, with a C# face. |
| [NetmodeID](outros.md#netmodeid) | static class | yes | Correspond to `netMode` values. |
| [NullAllowedAttribute](outros.md#nullallowedattribute) | class | yes | This attribute means the annotated item can possibly be null. |
| [OneFromOptionsDropRule](outros.md#onefromoptionsdroprule) | class | yes | Drops one item from a list. |
| [OneFromOptionsNotScaledWithLuckDropRule](outros.md#onefromoptionsnotscaledwithluckdroprule) | class | yes | Drops one item from a list, ignoring luck. |
| [OneFromRulesRule](outros.md#onefromrulesrule) | class | yes | Runs one rule from a list. |
| [OverlayManager](outros.md#overlaymanager) | struct | yes | `Overlays.Scene`: the screen overlays. |
| [ParticleRenderer](outros.md#particlerenderer) | struct | yes | Draws `IParticle` objects. |
| [PassLegacy](outros.md#passlegacy) | class | yes | A `GenPass` from a method. |
| [PlacementHook](outros.md#placementhook) | struct | yes | A method called when a tile object is placed, for `TileObjectData.newTile`. |
| [Point](outros.md#point) | struct | **no** | What Bunny Loader adds to the point. |
| [Point16](outros.md#point16) | struct | yes | A tile position in two `short`. |
| [PosData](outros.md#posdata) | struct | yes | A value at a position, in a sorted array, for sparse data by tile. |
| [PosData.OrderedSparseLookupBuilder](outros.md#posdataorderedsparselookupbuilder) | class | yes | Efficient builder for [] lookups covering the whole world. |
| [PosData.OrderedSparseLookupReader](outros.md#posdataorderedsparselookupreader) | class | yes | Reads a `PosData` array walking forward. |
| [PrefixCategory](outros.md#prefixcategory) | enum | yes | The kind of item a prefix fits. |
| [PrefixDefinition](outros.md#prefixdefinition) | class | yes | PrefixDefinition represents a Prefix identity. |
| [ProjAIStyleID](outros.md#projaistyleid) | static class | yes | Values of `Projectile.aiStyle`. |
| [Rectangle](outros.md#rectangle) | struct | **no** | What Bunny Loader adds to the rectangle. |
| [ReinitializeDuringResizeArraysAttribute](outros.md#reinitializeduringresizearraysattribute) | class | yes | Classes annotated with this attribute will have their static constructor called again during the ResizeArrays stage of mod loading. |
| [SandstormInABottleJump](outros.md#sandstorminabottlejump) | class | yes | One of the game's extra jumps, as an `ExtraJump` you can position against or read from `Player.GetJumpState`. |
| [SantankMountJump](outros.md#santankmountjump) | class | yes | One of the game's extra jumps, as an `ExtraJump` you can position against or read from `Player.GetJumpState`. |
| [SetFactory](outros.md#setfactory) | struct | yes | SetFactory is responsible for creating "custom ID sets" for content. |
| [SetFactory.NamedSetKey](outros.md#setfactorynamedsetkey) | class | yes | Used to construct the key for this "named ID set". |
| [SimpleItemDropRuleCondition](outros.md#simpleitemdroprulecondition) | record | yes | A condition made of a method and a text. |
| [SimpleModCloud](outros.md#simplemodcloud) | class | yes | A `ModCloud` made by `Autoload` from a texture in `Clouds/`. |
| [SimpleModGore](outros.md#simplemodgore) | class | yes | A `ModGore` made by `Autoload` from a texture in `Gores/`. |
| [SkyManager](outros.md#skymanager) | struct | yes | `SkyManager.Instance`: the custom skies. |
| [SlimeBodyItemDropRule](outros.md#slimebodyitemdroprule) | class | yes | What a slime carries inside. |
| [SlopeType](outros.md#slopetype) | enum | yes | The slope of a tile. |
| [SpriteFrame](outros.md#spriteframe) | struct | yes | One frame of a sprite sheet. |
| [StatInheritanceData](outros.md#statinheritancedata) | struct | yes | How much one damage class takes from another. |
| [StatueMimicItemDropRule](outros.md#statuemimicitemdroprule) | class | yes | The drop of a statue mimic. |
| [StyleDimension](outros.md#styledimension) | struct | yes | A position or size made of pixels plus a share of the parent. |
| [SummonDamageClass](outros.md#summondamageclass) | class | yes | One of the game's damage classes. |
| [SummonMeleeSpeedDamageClass](outros.md#summonmeleespeeddamageclass) | class | yes | One of the game's damage classes. |
| [SurfaceBackgroundStylesLoader](outros.md#surfacebackgroundstylesloader) | static class | yes | Registers the mod surface backgrounds. |
| [TextureAssetArray](outros.md#textureassetarray) | class | **no** | A table of the game's textures by type. |
| [TextureAssets](outros.md#textureassets) | static class | yes | The game's textures by type: `TextureAssets.Item[type]`. |
| [ThrowingDamageClass](outros.md#throwingdamageclass) | class | yes | One of the game's damage classes. |
| [ToFromStringConverter](outros.md#tofromstringconverter) | class | yes | This TypeConverter facilitates converting to and from the string Type. |
| [TreePaintingSettings](outros.md#treepaintingsettings) | struct | yes | How a tree takes paint. |
| [TsunamiInABottleJump](outros.md#tsunamiinabottlejump) | class | yes | One of the game's extra jumps, as an `ExtraJump` you can position against or read from `Player.GetJumpState`. |
| [UndergroundBackgroundStylesLoader](outros.md#undergroundbackgroundstylesloader) | static class | yes | This serves as the central class from which ModUndergroundBackgroundStyle functions are supported and carried out. |
| [UnicornMountJump](outros.md#unicornmountjump) | class | yes | One of the game's extra jumps, as an `ExtraJump` you can position against or read from `Player.GetJumpState`. |
| [UnloadedAccessorySlot](outros.md#unloadedaccessoryslot) | class | yes |  |
| [Utils](outros.md#utils) | static class | yes | What Bunny Loader adds to the game's helpers. |
| [UtilsExtensions](outros.md#utilsextensions) | static class | yes | The extension methods tModLoader has in `Utils`, written here as extensions of the mobile game's types. |
| [VanillaDamageClass](outros.md#vanilladamageclass) | abstract class | yes | One of the game's damage classes. |
| [VanillaExtraJump](outros.md#vanillaextrajump) | abstract class | yes | One of the game's extra jumps, as an `ExtraJump` you can position against or read from `Player.GetJumpState`. |
| [Vector2](outros.md#vector2) | struct | **no** | A 2D vector of the game: position, velocity, scale. |
| [Vector3](outros.md#vector3) | struct | **no** | A 3D vector, also used for light and color values. |
| [Vector4](outros.md#vector4) | struct | **no** | A 4D vector, used for shader values and colors. |
| [WallDefinition](outros.md#walldefinition) | class | yes | WallDefinition represents a Wall identity. |
| [WeightedRandom](outros.md#weightedrandom) | class | yes | Picks an item by weight. |
| [WhipSettings](outros.md#whipsettings) | struct | yes | What Bunny Loader adds to the whip settings. |
| [WhipTagEffect](outros.md#whiptageffect) | struct | yes | What a whip's tag does to the minions' hits. |
| [WingStats](outros.md#wingstats) | struct | yes | The flight time and speed of a pair of wings. |
| [Wiring](outros.md#wiring) | static class | yes | What Bunny Loader adds to the game's wiring. |
| [WorldFileData](outros.md#worldfiledata) | struct | yes | What Bunny Loader adds to the file data of a world. |

## In tModLoader, not here

Public classes of tModLoader's mod API that Bunny Loader does not have. Names are under `Terraria.ModLoader`.

| Classes | Why |
|---|---|
| `ActiveItemIterator`, `ActiveItemIterator.Enumerator`, `ActiveNPCIterator`, `ActiveNPCIterator.Enumerator`, `ActivePlayerIterator`, `ActivePlayerIterator.Enumerator`, `ActiveProjectileIterator`, `ActiveProjectileIterator.Enumerator` | `Main.ActiveItems`, `ActiveNPCs`, `ActivePlayers` and `ActiveProjectiles` exist and return a plain `IEnumerable`. |
| `ActuatorsVisibilityBuilderToggle`, `AutoActuateBuilderToggle`, `AutoPaintBuilderToggle`, `BlockSwapBuilderToggle`, `BlueWireVisibilityBuilderToggle`, `GreenWireVisibilityBuilderToggle`, `HideAllWiresBuilderToggle`, `RedWireVisibilityBuilderToggle`, `RulerGridBuilderToggle`, `RulerLineBuilderToggle`, `TorchBiomeBuilderToggle`, `VanillaBuilderToggle`, `WireVisibilityBuilderToggle`, `YellowWireVisibilityBuilderToggle` | The game's own toggles are not classes here. `BuilderToggle.TorchBiome` is the one exposed, and mods add theirs with `BuilderToggle`. |
| `Assets.AssemblyResourcesContentSource`, `Assets.FxcReader`, `Assets.MP3Reader`, `Assets.OggReader`, `Assets.RawImgReader`, `Assets.TModContentSource`, `Assets.WavReader` | File readers of tModLoader's asset system. Here assets go through `ModContent.Request` and Bunny Loader's own readers. |
| `AsyncProvider`, `AsyncProviderState`, `AsyncProviderStateExtensions`, `ICustomAutoload.AutoloadImpl`, `ILoadable.AutoloadImpl`, `ILoader`, `Loader`, `ModContent.TrackCurrentlyLoadingMod` | Internal to tModLoader's loading. |
| `BuildInfo`, `BuildInfo.BuildPurpose`, `Framework`, `FrameworkVersion` | Describes tModLoader's own build. |
| `BuilderToggleLoader` | Internal here. Mods subclass `BuilderToggle`. |
| `CloneByReference`, `DamageClass.Sets`, `InitializationItemCreationContext` | Not implemented. |
| `CombinedHooks` | Internal here. It runs the `ModPlayer` and `GlobalItem` hooks of a use together, and those hooks exist. |
| `CompassInfoDisplay`, `DPSMeterInfoDisplay`, `DepthMeterInfoDisplay`, `DummyInfoDisplay`, `FishFinderInfoDisplay`, `LifeformAnalyzerInfoDisplay`, `MetalDetectorInfoDisplay`, `RadarInfoDisplay`, `SextantInfoDisplay`, `StopwatchInfoDisplay`, `TallyCounterInfoDisplay`, `VanillaInfoDisplay`, `WatchesInfoDisplay`, `WeatherRadioInfoDisplay` | The game's own displays are not classes here. Mods add theirs with `InfoDisplay`. |
| `Config.LabelAttribute` | Old in tModLoader too. Labels come from the localization files. |
| `Config.TooltipAttribute` | Old in tModLoader too. Tooltips come from the localization files. |
| `ContentInstance` | Internal to tModLoader. Use `ModContent.GetInstance<T>()`. |
| `Default.ModLoaderModItem` | Base of tModLoader's own items. |
| `Default.PylonShopNPC` | tModLoader's rule for selling pylons in shops. Here the same rule is internal, and `NPCShopDatabase.NoPylons` lists the exceptions. |
| `Default.StartBag` | tModLoader's bag of starting items. `ModPlayer.AddStartingItems` puts them straight in the inventory. |
| `Default.UnloadedChest`, `Default.UnloadedDresser`, `Default.UnloadedGlobalItem`, `Default.UnloadedGlobalNPC`, `Default.UnloadedItem`, `Default.UnloadedNonSolidTile`, `Default.UnloadedPlayer`, `Default.UnloadedPrefix`, `Default.UnloadedSemiSolidTile`, `Default.UnloadedSolidTile`, `Default.UnloadedSupremeFurniture`, `Default.UnloadedSystem`, `Default.UnloadedTile`, `Default.UnloadedTileEntity`, `Default.UnloadedWall` | tModLoader keeps content of a removed mod as a placeholder. There are no placeholders here: such an item loads empty. |
| `ExtendsFromModAttribute` | Weak references to other mods are not part of Bunny Loader. |
| `GlobalBlockType` | `GlobalTile` and `GlobalWall` derive directly from `ModType` here. |
| `GlobalInfoDisplay` | Not implemented: mods cannot change the displays of other mods or of the game. |
| `GlobalList` | Internal to tModLoader. Here the lists of globals live in `Terraria.ModLoader.Core`. |
| `GlobalRecipe` | Not implemented. Change recipes with `ModSystem.PostAddRecipes`. |
| `IAutoload`, `IAutoloader` | Internal to tModLoader's loading. `ILoadable` and `ICustomAutoload` are the ones that exist. |
| `IEntityWithInstances` | Internal to tModLoader. Instances live in `Terraria.ModLoader.Core`. |
| `IIndexed`, `ModSideExtensions`, `RefReadOnlyArray`, `TopoSort`, `TopoSort.SortingException` | Internal to tModLoader. |
| `ILPatchFailureException`, `MonoModHooks` | IL editing cannot exist here: the game is compiled to machine code. Use the `On_X` hooks. |
| `IO.BoolTagSerializer`, `IO.ColorSerializer`, `IO.MultiDimArraySerializer`, `IO.Point16Serializer`, `IO.PointSerializer`, `IO.RectangleSerializer`, `IO.TagSerializer`, `IO.UIntTagSerializer`, `IO.ULongTagSerializer`, `IO.UShortTagSerializer`, `IO.Vector2TagSerializer`, `IO.Vector3TagSerializer`, `IO.VersionSerializer` | Not implemented. `TagCompound` takes numbers, `bool`, strings, lists, enums, `Item` and `TagSerializable`. A `Vector2` or `Color` must be saved as its own fields. |
| `IO.ImageIO` | tModLoader's `.rawimg` format. Mods here ship PNG. |
| `IO.TagPrinter` | Debug helper of tModLoader. |
| `IO.TileIO.IOImpl`, `IO.TileIO.TileIOImpl`, `IO.TileIO.WallIOImpl` | tModLoader's save of tiles. Here tiles and walls go to Bunny Loader's own `.bl` files. |
| `IO.UploadFile` | Part of tModLoader's mod browser. |
| `IPylonTileEntity` | Mod pylons use `TEModdedPylon` directly. |
| `InfoDisplayLoader` | Internal here. Mods subclass `InfoDisplay`. |
| `JITWhenModsEnabledAttribute`, `MemberJitAttribute`, `NoJITAttribute`, `PreJITFilter` | tModLoader compiles mods at load time with the JIT. Here a mod's DLL runs on Mono with no such filters. |
| `LocalizationLoader`, `LocalizationLoader.CommentedWscJsonObject`, `LocalizationLoader.LocalizationEntry`, `LocalizationLoader.LocalizationFile` | Internal here. Localization files load by themselves. |
| `Logging`, `Logging.LogFile`, `Logging.QuietExceptionHandle` | tModLoader's log. Use `Mod.Logger`. |
| `MapLegend` | Not implemented. Use `ModTile.AddMapEntry`. |
| `ModNet` | Internal here. Use `Mod.GetPacket` and `Mod.HandlePacket`. |
| `ModPalmTree` | Not implemented. `ModTree` and `ModCactus` exist. |
| `ModResourceDisplaySet`, `ResourceDisplaySetLoader` | Not implemented. `ModResourceOverlay` exists. |
| `TravelingMerchantShop`, `TravelingMerchantShop.InfoEntry` | Not implemented as a shop class. The game's merchant still works, and `GlobalNPC.SetupTravelShop` runs. |
