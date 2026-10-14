# Methods by class

A catalog generated from the public classes in `Exports.js`, with one row per method, constructor or getter/setter declared in JavaScript. It includes inherited methods and the `NetWriter`/`NetReader` aliases. Fields, enums and descriptor objects are left out of the method count. The methods of the native `Terraria.*` classes, of `bl.*` and the private loaders are looked up through the [bridge](ponte-e-bl.md).

Open the [Excel spreadsheet](metodos.xlsx) to filter the table by class, method, kind or status. The [UTF-8 CSV](metodos.csv) has the same records and the [JSON](metodos.json) allows queries from tools.

`Declared` confirms the member exists in the current JavaScript API. It does not mean all of its flows were tested in game. The return column shows the base implementation's expression, not a type declaration nor the override's required return. `See source` marks implementations with more than one statement. The signatures are Bunny Loader's and may differ from tModLoader's.

Contracts and mutable parameters: [class reference](classes.md), [ModPlayer](modplayer-hooks.md) and [ref/out](../mods/02-ref-e-out.md). The ten ModNPC methods without a complete adaptation show up with the `Not implemented` status and no made-up signature.

Install the dependency with `npm --prefix tools/docs ci` and update with `node tools/docs/generate.mjs`. Check for differences with `node tools/docs/generate.mjs --check`. The [documentation tool](../../tools/docs/README.md) explains how to update the spreadsheet.

## Index

| Class | Own members | Inherited | Contracts |
|---|---:|---:|---|
| [`BackgroundTextureLoader`](#backgroundtextureloader) | 6 | 0 | [Reference](classes.md#mod-backgrounds) |
| [`CloudLoader`](#cloudloader) | 4 | 0 | [Reference](classes.md#modcloud) |
| [`CommandLoader`](#commandloader) | 4 | 0 | [Reference](classes.md#modcommand) |
| [`Condition`](#condition) | 4 | 0 | [Reference](classes.md#condition) |
| [`DamageClass`](#damageclass) | 20 | 0 | [Reference](classes.md#damageclass) |
| [`DamageClassLoader`](#damageclassloader) | 19 | 0 | [Reference](classes.md#damageclass) |
| [`DrawableTooltipLine`](#drawabletooltipline) | 1 | 2 | [Reference](classes.md#tooltipline) |
| [`EquipLoader`](#equiploader) | 8 | 0 | [Reference](classes.md#worn-textures-equiploader-and-equiptexture) |
| [`EquipTexture`](#equiptexture) | 9 | 0 | [Reference](classes.md#worn-textures-equiploader-and-equiptexture) |
| [`ExtraJump`](#extrajump) | 1 | 0 | [Reference](classes.md#modplayer) |
| [`GenPass`](#genpass) | 10 | 0 | [Reference](classes.md#genpass-and-passlegacy) |
| [`GlobalBossBar`](#globalbossbar) | 5 | 0 | [Reference](classes.md#globalbossbar) |
| [`GlobalBuff`](#globalbuff) | 11 | 0 | [Reference](classes.md#globalbuff) |
| [`GlobalItem`](#globalitem) | 73 | 8 | [Reference](classes.md#globalitem) |
| [`GlobalLoot`](#globalloot) | 11 | 0 | [Reference](classes.md#globalloot) |
| [`GlobalNPC`](#globalnpc) | 52 | 8 | [Reference](classes.md#globalnpc) |
| [`GlobalProjectile`](#globalprojectile) | 32 | 8 | [Reference](classes.md#globalprojectile) |
| [`GlobalRecipe`](#globalrecipe) | 6 | 0 | [Reference](classes.md#globalrecipe) |
| [`GlobalTile`](#globaltile) | 25 | 0 | [Reference](classes.md#globaltile) |
| [`HitInfo`](#hitinfo) | 1 | 0 | [Reference](classes.md#hits) |
| [`HitModifiers`](#hitmodifiers) | 18 | 0 | [Reference](classes.md#hits) |
| [`HurtInfo`](#hurtinfo) | 1 | 0 | [Reference](classes.md#hits) |
| [`HurtModifiers`](#hurtmodifiers) | 23 | 0 | [Reference](classes.md#hits) |
| [`ItemLoot`](#itemloot) | 6 | 0 | [Reference](classes.md#npc-helpers) |
| [`KeybindLoader`](#keybindloader) | 1 | 0 | [Reference](classes.md#modtouchbutton) |
| [`Mod`](#mod) | 19 | 0 | [Reference](classes.md#mod-and-modloader) |
| [`ModAchievement`](#modachievement) | 18 | 0 | [Reference](classes.md#modachievement) |
| [`ModBannerTile`](#modbannertile) | 4 | 43 | [Reference](classes.md#modbannertile) |
| [`ModBiome`](#modbiome) | 5 | 5 | [Reference](classes.md#modbiome-and-modsceneeffect) |
| [`ModBossBar`](#modbossbar) | 11 | 0 | [Reference](classes.md#modbossbar) |
| [`ModBuff`](#modbuff) | 20 | 0 | [Reference](classes.md#modbuff) |
| [`ModCloud`](#modcloud) | 5 | 0 | [Reference](classes.md#modcloud) |
| [`ModCommand`](#modcommand) | 10 | 0 | [Reference](classes.md#modcommand) |
| [`ModConfig`](#modconfig) | 15 | 0 | [Reference](classes.md#modconfig) |
| [`ModContent`](#modcontent) | 27 | 0 | [Reference](classes.md#modcontent) |
| [`ModEmoteBubble`](#modemotebubble) | 10 | 0 | [Reference](classes.md#modemotebubble) |
| [`ModGore`](#modgore) | 1 | 0 | [Reference](classes.md#npc-helpers) |
| [`ModHair`](#modhair) | 6 | 0 | [Reference](classes.md#modhair) |
| [`ModItem`](#moditem) | 133 | 0 | [Reference](classes.md#moditem) |
| [`ModLoader`](#modloader) | 4 | 0 | [Reference](classes.md#mod-and-modloader) |
| [`ModLocalization`](#modlocalization) | 8 | 0 | [Reference](classes.md#modlocalization) |
| [`ModMenu`](#modmenu) | 8 | 0 | [Reference](classes.md#modmenu) |
| [`ModMount`](#modmount) | 15 | 0 | [Reference](classes.md#modmount) |
| [`ModNPC`](#modnpc) | 74 | 0 | [Reference](classes.md#modnpc) |
| [`ModPacket`](#modpacket) | 2 | 15 | [Reference](classes.md#network) |
| [`ModPlayer`](#modplayer) | 134 | 0 | [Reference](classes.md#modplayer) |
| [`ModPrefix`](#modprefix) | 16 | 0 | [Reference](classes.md#modprefix) |
| [`ModProjectile`](#modprojectile) | 59 | 0 | [Reference](classes.md#modprojectile) |
| [`ModPylon`](#modpylon) | 12 | 43 | [Reference](classes.md#modpylon-and-temoddedpylon) |
| [`ModRarity`](#modrarity) | 4 | 0 | [Reference](classes.md#modrarity) |
| [`ModRecipe`](#modrecipe) | 16 | 0 | [Reference](classes.md#modrecipe) |
| [`ModSceneEffect`](#modsceneeffect) | 6 | 0 | [Reference](classes.md#modbiome-and-modsceneeffect) |
| [`ModScreenEffect`](#modscreeneffect) | 15 | 0 | [Reference](classes.md#screen-effects) |
| [`ModSurfaceBackgroundStyle`](#modsurfacebackgroundstyle) | 7 | 0 | [Reference](classes.md#mod-backgrounds) |
| [`ModSystem`](#modsystem) | 58 | 0 | [Reference](classes.md#modsystem) |
| [`ModTile`](#modtile) | 47 | 0 | [Reference](classes.md#modtile) |
| [`ModTileEntity`](#modtileentity) | 25 | 0 | [Reference](classes.md#modtileentity) |
| [`ModTouchButton`](#modtouchbutton) | 13 | 0 | [Reference](classes.md#modtouchbutton) |
| [`ModTree`](#modtree) | 12 | 0 | [Reference](classes.md#modtree) |
| [`ModUndergroundBackgroundStyle`](#modundergroundbackgroundstyle) | 3 | 0 | [Reference](classes.md#mod-backgrounds) |
| [`ModWall`](#modwall) | 17 | 0 | [Reference](classes.md#modwall) |
| [`ModWaterStyle`](#modwaterstyle) | 9 | 0 | [Reference](classes.md#mod-water) |
| [`ModWaterfallStyle`](#modwaterfallstyle) | 4 | 0 | [Reference](classes.md#mod-water) |
| [`MusicLoader`](#musicloader) | 5 | 0 | [Reference](classes.md#sound-and-music) |
| [`NPCHappiness`](#npchappiness) | 3 | 0 | [Reference](classes.md#npc-helpers) |
| [`NPCLoot`](#npcloot) | 5 | 0 | [Reference](classes.md#npc-helpers) |
| [`NPCShop`](#npcshop) | 8 | 0 | [Reference](classes.md#npc-helpers) |
| [`NPCSpawnInfo`](#npcspawninfo) | 44 | 0 | [Reference](classes.md#npc-helpers) |
| [`NetReader`](#netreader) | 17 | 0 | [Reference](classes.md#network) |
| [`NetWriter`](#netwriter) | 16 | 0 | [Reference](classes.md#network) |
| [`PassLegacy`](#passlegacy) | 2 | 8 | [Reference](classes.md#genpass-and-passlegacy) |
| [`PlayerDrawLayer`](#playerdrawlayer) | 11 | 0 | [Reference](classes.md#modplayer) |
| [`PrefixLoader`](#prefixloader) | 13 | 0 | [Reference](classes.md#modprefix) |
| [`RarityLoader`](#rarityloader) | 4 | 0 | [Reference](classes.md#modrarity) |
| [`ScreenEffects`](#screeneffects) | 11 | 0 | [Reference](classes.md#screen-effects) |
| [`SoundEngine`](#soundengine) | 4 | 0 | [Reference](classes.md#sound-and-music) |
| [`SoundStyle`](#soundstyle) | 1 | 0 | [Reference](classes.md#sound-and-music) |
| [`SpawnCondition`](#spawncondition) | 7 | 0 | [Reference](classes.md#npc-helpers) |
| [`SpawnPool`](#spawnpool) | 7 | 0 | [Reference](classes.md#npc-helpers) |
| [`StatInheritanceData`](#statinheritancedata) | 4 | 0 | [Reference](classes.md#damageclass) |
| [`StatModifier`](#statmodifier) | 10 | 0 | [Reference](classes.md#damageclass) |
| [`TEModdedPylon`](#temoddedpylon) | 3 | 23 | [Reference](classes.md#modpylon-and-temoddedpylon) |
| [`TagCompound`](#tagcompound) | 13 | 0 | [Reference](classes.md#tagcompound) |
| [`TooltipLine`](#tooltipline) | 3 | 0 | [Reference](classes.md#tooltipline) |
| [`TouchButtonLoader`](#touchbuttonloader) | 22 | 0 | [Reference](classes.md#modtouchbutton) |
| [`UsageException`](#usageexception) | 1 | 0 | [Reference](classes.md#modcommand) |

86 public classes, 1580 declared records and 10 methods not implemented. The count includes the same member in each class that inherits it.

## Objects and enums

`AffectionLevel`, `CommandType`, `EquipType`, `ModKeybind`, `MountTextureType`, `PlayerDrawLayers`, `PrefixCategory`, `SceneEffectPriority`, `ScreenGraphics`, `ScreenPresets`, `ScreenUtils`, `SoundLimitBehavior`, `TooltipNames`, `TreeTypes`. They are public exports with no JavaScript class declaration in this module.

## BackgroundTextureLoader

[Contract and fields](classes.md#mod-backgrounds).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `AddBackgroundTexture(mod, texture)` | Static | `BackgroundTextureLoader` | `See source` | Um fundo fora de uma pasta Backgrounds/; só durante a carga do mod. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/BackgroundTextureLoader.js#L32) |
| `Autoload()` | Static | `BackgroundTextureLoader` | `See source` | Na carga de cada mod, com o mod na pilha. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/BackgroundTextureLoader.js#L21) |
| `GetBackgroundSlot(modOrKey, path)` | Static | `BackgroundTextureLoader` | `See source` | GetBackgroundSlot(mod, 'Assets/Textures/Backgrounds/Nome') ou GetBackgroundSlot('examplemod/Assets/Textures/Backgrounds/Nome'). Lança se não existe, como no tModLoader. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/BackgroundTextureLoader.js#L52) |
| `get TotalCount` | Static getter | `BackgroundTextureLoader` | `BackgroundTextureLoader.VanillaCount + BackgroundTextureLoader.#count` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/BackgroundTextureLoader.js#L18) |
| `TryGetBackgroundSlot(modOrKey, path, result)` | Static | `BackgroundTextureLoader` | `See source` | TryGetBackgroundSlot(chave, ref) ou (mod, caminho, ref): o número em ref.value. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/BackgroundTextureLoader.js#L61) |
| `get VanillaCount` | Static getter | `BackgroundTextureLoader` | `Terraria.Main.maxBackgrounds` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/BackgroundTextureLoader.js#L17) |

## CloudLoader

[Contract and fields](classes.md#modcloud).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `Add(inst)` | Static | `CloudLoader` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/CloudLoader.js#L18) |
| `AddCloudFromTexture(texture, spawnChance = 1, rareCloud = false)` | Static | `CloudLoader` | `See source` | Uma nuvem só de textura (o caminho no mod, sem extensão), no Mod.Load. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/CloudLoader.js#L30) |
| `Autoload()` | Static | `CloudLoader` | `See source` | Todo PNG numa pasta Clouds/ do mod sem nuvem com o mesmo nome (classe ou AddCloudFromTexture) vira nuvem comum de peso 1, como no tModLoader. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/CloudLoader.js#L42) |
| `ChooseCloud(vanillaPool, rare)` | Static | `CloudLoader` | `CloudLoader.#Roll(CloudLoader.#Pool(vanillaPool, rare))` | O ChooseCloud do tModLoader: 0 é "uma do jogo". | [Code](../../app/src/main/cpp/script/js/mod/Loaders/CloudLoader.js#L104) |

## CommandLoader

[Contract and fields](classes.md#modcommand).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `Add(cmd)` | Static | `CommandLoader` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/CommandLoader.js#L10) |
| `GetHelp(callerType)` | Static | `CommandLoader` | `See source` | "/nome descrição" de cada comando que vale para este tipo de chamada (o GetHelp do tModLoader); o mod na frente quando o nome se repete. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/CommandLoader.js#L135) |
| `Handle(input, caller)` | Static | `CommandLoader` | `See source` | true: era um comando de mod (rodou, ou respondeu o erro). | [Code](../../app/src/main/cpp/script/js/mod/Loaders/CommandLoader.js#L107) |
| `Matches(commandType, callerType)` | Static | `CommandLoader` | `See source` | O CommandType.World vale como Chat sozinho e como Server no servidor (o netMode é campo de bits: quem hospeda e joga fica com 3). | [Code](../../app/src/main/cpp/script/js/mod/Loaders/CommandLoader.js#L28) |

## Condition

[Contract and fields](classes.md#condition).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `constructor(description, predicate)` | Constructor | `Condition` | `instance` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Condition.js#L4) |
| `get Description` | Getter | `Condition` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Condition.js#L22) |
| `IsMet()` | Instance | `Condition` | `!!this.Predicate()` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Condition.js#L30) |
| `toString()` | Instance | `Condition` | `this.Description` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Condition.js#L34) |

## DamageClass

[Contract and fields](classes.md#damageclass).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `CountsAsClass(damageClass)` | Instance | `DamageClass` | `DamageClassLoader.CountsAs(this, DamageClassLoader.Resolve(damageClass))` | Query the relations between registered classes. | [Code](../../app/src/main/cpp/script/js/mod/DamageClass.js#L38) |
| `get Default` | Static getter | `DamageClass` | `DamageClassLoader.Vanilla.Default` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/DamageClass.js#L10) |
| `get Generic` | Static getter | `DamageClass` | `DamageClassLoader.Vanilla.Generic` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/DamageClass.js#L11) |
| `GetEffectInheritance(damageClass)` | Instance | `DamageClass` | `false` | Defines whether it inherits the other class's effects; default false. | [Code](../../app/src/main/cpp/script/js/mod/DamageClass.js#L25) |
| `GetModifierInheritance(damageClass)` | Instance | `DamageClass` | `damageClass === DamageClass.Generic ? StatInheritanceData.Full : StatInheritanceData.None` | Returns StatInheritanceData; by default, it inherits all of Generic's bonuses and none of the others'. | [Code](../../app/src/main/cpp/script/js/mod/DamageClass.js#L21) |
| `GetPrefixInheritance(damageClass)` | Instance | `DamageClass` | `this.GetEffectInheritance(damageClass)` | Defines prefix inheritance; the default delegates to GetEffectInheritance. | [Code](../../app/src/main/cpp/script/js/mod/DamageClass.js#L27) |
| `GetsPrefixesFor(damageClass)` | Instance | `DamageClass` | `See source` | Query the relations between registered classes. | [Code](../../app/src/main/cpp/script/js/mod/DamageClass.js#L42) |
| `get Magic` | Static getter | `DamageClass` | `DamageClassLoader.Vanilla.Magic` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/DamageClass.js#L15) |
| `get MagicSummonHybrid` | Static getter | `DamageClass` | `DamageClassLoader.Vanilla.MagicSummonHybrid` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/DamageClass.js#L18) |
| `get Melee` | Static getter | `DamageClass` | `DamageClassLoader.Vanilla.Melee` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/DamageClass.js#L12) |
| `get MeleeNoSpeed` | Static getter | `DamageClass` | `DamageClassLoader.Vanilla.MeleeNoSpeed` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/DamageClass.js#L13) |
| `get Ranged` | Static getter | `DamageClass` | `DamageClassLoader.Vanilla.Ranged` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/DamageClass.js#L14) |
| `register(cls)` | Static | `DamageClass` | `See source` | Registers by hand and returns the type. | [Code](../../app/src/main/cpp/script/js/mod/DamageClass.js#L47) |
| `SetDefaultStats(player)` | Instance | `DamageClass` | `undefined` | Initializing the type and the player's stats. | [Code](../../app/src/main/cpp/script/js/mod/DamageClass.js#L31) |
| `SetStaticDefaults()` | Instance | `DamageClass` | `undefined` | Initializing the type and the player's stats. | [Code](../../app/src/main/cpp/script/js/mod/DamageClass.js#L29) |
| `ShowStatTooltipLine(player, lineName)` | Instance | `DamageClass` | `true` | Controls the stat line in the tooltip; default true. | [Code](../../app/src/main/cpp/script/js/mod/DamageClass.js#L36) |
| `get Summon` | Static getter | `DamageClass` | `DamageClassLoader.Vanilla.Summon` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/DamageClass.js#L16) |
| `get SummonMeleeSpeed` | Static getter | `DamageClass` | `DamageClassLoader.Vanilla.SummonMeleeSpeed` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/DamageClass.js#L17) |
| `get Throwing` | Static getter | `DamageClass` | `DamageClassLoader.Vanilla.Throwing` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/DamageClass.js#L19) |
| `get UseStandardCritCalcs` | Getter | `DamageClass` | `true` | A getter defaulting to true. | [Code](../../app/src/main/cpp/script/js/mod/DamageClass.js#L33) |

## DamageClassLoader

[Contract and fields](classes.md#damageclass).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `Activate()` | Static | `DamageClassLoader` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L328) |
| `Add(inst)` | Static | `DamageClassLoader` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L37) |
| `CountsAs(cls, other)` | Static | `DamageClassLoader` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L64) |
| `get DamageClassCount` | Static getter | `DamageClassLoader` | `DamageClassLoader.Vanilla && DamageClassLoader.#classes.length` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L31) |
| `Data(player)` | Static | `DamageClassLoader` | `See source` | Os bônus por classe deste jogador, zerados todo quadro (ResetEffects). | [Code](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L174) |
| `Defaulting(entity, fn)` | Static | `DamageClassLoader` | `See source` | O SetDefaults de um item ou projétil: o DamageType escrito nele vale para o tipo inteiro (sobrevive ao Clone, ao NewItem e à rede). | [Code](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L78) |
| `GetDamageClass(type)` | Static | `DamageClassLoader` | `(DamageClassLoader.Vanilla && DamageClassLoader.#classes[type]) \|\| null` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L33) |
| `Install()` | Static | `DamageClassLoader` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L557) |
| `IsHooked(cls)` | Static | `DamageClassLoader` | `!DamageClassLoader.#native.has(cls)` | De gancho: o jogo não a entende pelas flags. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L62) |
| `ItemClass(item)` | Static | `DamageClassLoader` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L88) |
| `NameOf(cls)` | Static | `DamageClassLoader` | `cls.Name \|\| cls.constructor.name` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L35) |
| `ProjectileClass(proj)` | Static | `DamageClassLoader` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L113) |
| `Resolve(which)` | Static | `DamageClassLoader` | `See source` | A classe pedida como instância, classe registrada ou Type. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L50) |
| `SetItemClass(item, which)` | Static | `DamageClassLoader` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L94) |
| `SetProjectileClass(proj, which)` | Static | `DamageClassLoader` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L119) |
| `Stats(player, which)` | Static | `DamageClassLoader` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L184) |
| `Total(player, which)` | Static | `DamageClassLoader` | `See source` | O total de uma classe: o dela mais o de cada classe herdada, como o GetTotalDamage do tModLoader. As do jogo são lidas dos campos do jogador. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L193) |
| `UsesCrit(cls)` | Static | `DamageClassLoader` | `!!Safe.Run(DamageClassLoader.NameOf(cls) + '.UseStandardCritCalcs', () => cls.UseStandardCritCalcs)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L468) |
| `get Vanilla` | Static getter | `DamageClassLoader` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L26) |

## DrawableTooltipLine

[Contract and fields](classes.md#tooltipline). Inherits from `TooltipLine`.

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `colorTag(text, color)` | Static | `TooltipLine` (inherited) | `'[c/' + TooltipLoader.Hex(color) + ':' + text + ']'` | '[c/RRGGBB:text]' to paint a span. | [Code](../../app/src/main/cpp/script/js/mod/TooltipLine.js#L20) |
| `constructor(parent, index, x, y, color)` | Constructor | `DrawableTooltipLine` | `instance` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/DrawableTooltipLine.js#L7) |
| `Hide()` | Instance | `TooltipLine` (inherited) | `See source` | Hide() removes the line from the tooltip. | [Code](../../app/src/main/cpp/script/js/mod/TooltipLine.js#L18) |

## EquipLoader

[Contract and fields](classes.md#worn-textures-equiploader-and-equiptexture).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `AddEquipTexture(...args)` | Static | `EquipLoader` | `See source` | Registers by hand, in Load(). | [Code](../../app/src/main/cpp/script/js/mod/Loaders/EquipLoader.js#L59) |
| `Apply(item, type)` | Static | `EquipLoader` | `See source` | No SetDefaults de cada Item, antes do do mod (que pode trocar). | [Code](../../app/src/main/cpp/script/js/mod/Loaders/EquipLoader.js#L89) |
| `Autoload(inst, cls, name)` | Static | `EquipLoader` | `See source` | No registro do item (bl.mod ainda é o dele): as texturas <Texture>_<tipo>, ou só as de static AutoloadEquip = [EquipType.Head, ...] (o atributo do tModLoader; [] desliga, para quem registra à mão com AddEquipTexture). | [Code](../../app/src/main/cpp/script/js/mod/Loaders/EquipLoader.js#L46) |
| `GetEquipSlot(...args)` | Static | `EquipLoader` | `See source` | The slot; -1 if there is none. From SetStaticDefaults on. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/EquipLoader.js#L76) |
| `GetEquipTexture(kind, slot)` | Static | `EquipLoader` | `EquipLoader.#textures.get(kind + ':' + slot)` | The slot's EquipTexture. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/EquipLoader.js#L73) |
| `Install()` | Static | `EquipLoader` | `See source` | Uma vez, na primeira vez que um item de mod precisa do slot (a amostra do jogo ou o SetStaticDefaults): nessa hora o jogo já carregou as texturas dele. Antes disso, subir o Count faria o jogo procurar Armor_Head_293. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/EquipLoader.js#L99) |
| `InstalledKinds()` | Static | `EquipLoader` | `EquipLoader.#kinds` | Os tipos de equipamento que ganharam textura de mod. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/EquipLoader.js#L85) |
| `VanillaCount(kind)` | Static | `EquipLoader` | `EquipLoader.#vanilla.get(kind) \|\| 0` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/EquipLoader.js#L82) |

## EquipTexture

[Contract and fields](classes.md#worn-textures-equiploader-and-equiptexture).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `ArmorSetShadows(player)` | Instance | `EquipTexture` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/EquipTexture.js#L28) |
| `FrameEffects(player, type)` | Instance | `EquipTexture` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/EquipTexture.js#L11) |
| `HorizontalWingSpeeds(player, speed, acceleration)` | Instance | `EquipTexture` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/EquipTexture.js#L45) |
| `IsVanitySet(head, body, legs)` | Instance | `EquipTexture` | `this.Item ? this.Item.IsVanitySet(head, body, legs) : false` | head, body, legs: os SLOTS vestidos (player.head...), não itens. | [Code](../../app/src/main/cpp/script/js/mod/EquipTexture.js#L16) |
| `PreUpdateVanitySet(player)` | Instance | `EquipTexture` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/EquipTexture.js#L20) |
| `SetMatch(male, equipSlot, robes)` | Instance | `EquipTexture` | `See source` | equipSlot e robes são Ref (.value). | [Code](../../app/src/main/cpp/script/js/mod/EquipTexture.js#L33) |
| `UpdateVanitySet(player)` | Instance | `EquipTexture` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/EquipTexture.js#L24) |
| `VerticalWingSpeeds(player, ascentWhenFalling, ascentWhenRising, maxCanAscendMultiplier, maxAscentMultiplier, constantAscend)` | Instance | `EquipTexture` | `See source` | Para asas sem item vestido (a textura posta pelo FrameEffects). | [Code](../../app/src/main/cpp/script/js/mod/EquipTexture.js#L38) |
| `WingUpdate(player, inUse)` | Instance | `EquipTexture` | `this.Item ? this.Item.WingUpdate(player, inUse) === true : false` | true: o mod anima as asas (o WingFrame do jogo não roda). | [Code](../../app/src/main/cpp/script/js/mod/EquipTexture.js#L50) |

## ExtraJump

[Contract and fields](classes.md#modplayer).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `constructor(name, field)` | Constructor | `ExtraJump` | `instance` | See the implementation in the source. Descriptor of the native jumps. Registering new jump types is not part of the current API. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/PlayerJumpHooks.js#L2) |

## GenPass

[Contract and fields](classes.md#genpass-and-passlegacy).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `Apply(progress, configuration)` | Instance | `GenPass` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GenPass.js#L21) |
| `ApplyPass(progress, configuration)` | Instance | `GenPass` | `See source` | See the implementation in the source. Implement it in a JS subclass; a callback run by the native generator through PassLegacy. Callback failures are isolated. | [Code](../../app/src/main/cpp/script/js/mod/GenPass.js#L24) |
| `constructor(name, weight = 1)` | Constructor | `GenPass` | `instance` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GenPass.js#L2) |
| `Disable()` | Instance | `GenPass` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GenPass.js#L19) |
| `Enable()` | Instance | `GenPass` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GenPass.js#L20) |
| `get Enabled` | Getter | `GenPass` | `this.Native.Enabled` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GenPass.js#L18) |
| `get Name` | Getter | `GenPass` | `this.Native.Name` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GenPass.js#L11) |
| `set Name(value)` | Setter | `GenPass` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GenPass.js#L12) |
| `get Weight` | Getter | `GenPass` | `this.Native.Weight` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GenPass.js#L13) |
| `set Weight(value)` | Setter | `GenPass` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GenPass.js#L14) |

## GlobalBossBar

[Contract and fields](classes.md#globalbossbar).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `PostDraw(spriteBatch, npc, drawParams)` | Instance | `GlobalBossBar` | `undefined` | After. | [Code](../../app/src/main/cpp/script/js/mod/GlobalBossBar.js#L14) |
| `PostSetupContent()` | Instance | `GlobalBossBar` | `undefined` | The base implementation does nothing. | [Code](../../app/src/main/cpp/script/js/mod/GlobalBossBar.js#L10) |
| `PreDraw(spriteBatch, npc, drawParams)` | Instance | `GlobalBossBar` | `true` | Before each bar. false: no bar (PostDraw runs anyway). | [Code](../../app/src/main/cpp/script/js/mod/GlobalBossBar.js#L13) |
| `register(cls)` | Static | `GlobalBossBar` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GlobalBossBar.js#L16) |
| `SetStaticDefaults()` | Instance | `GlobalBossBar` | `undefined` | With TileObjectData unlocked: changing a vanilla tile's shape or anchors works here. | [Code](../../app/src/main/cpp/script/js/mod/GlobalBossBar.js#L9) |

## GlobalBuff

[Contract and fields](classes.md#globalbuff).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `ModifyBuffText(type, buffName, tip, rare)` | Instance | `GlobalBuff` | `undefined` | After the ModBuff's own. | [Code](../../app/src/main/cpp/script/js/mod/GlobalBuff.js#L19) |
| `PostDraw(spriteBatch, type, buffIndex, drawParams)` | Instance | `GlobalBuff` | `undefined` | After. | [Code](../../app/src/main/cpp/script/js/mod/GlobalBuff.js#L25) |
| `PostSetupContent()` | Instance | `GlobalBuff` | `undefined` | The base implementation does nothing. | [Code](../../app/src/main/cpp/script/js/mod/GlobalBuff.js#L6) |
| `PreDraw(spriteBatch, type, buffIndex, drawParams)` | Instance | `GlobalBuff` | `true` | Before each bar. false: no bar (PostDraw runs anyway). | [Code](../../app/src/main/cpp/script/js/mod/GlobalBuff.js#L24) |
| `ReApplyNPC(type, npc, time, buffIndex)` | Instance | `GlobalBuff` | `false` | The buff was there and got another AddBuff. true: the game does not renew the time (as in tModLoader; ModBuff's is the other way round). | [Code](../../app/src/main/cpp/script/js/mod/GlobalBuff.js#L15) |
| `ReApplyPlayer(type, player, time, buffIndex)` | Instance | `GlobalBuff` | `false` | The buff was there and got another AddBuff. true: the game does not renew the time (as in tModLoader; ModBuff's is the other way round). | [Code](../../app/src/main/cpp/script/js/mod/GlobalBuff.js#L14) |
| `register(cls)` | Static | `GlobalBuff` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GlobalBuff.js#L30) |
| `RightClick(type, buffIndex)` | Instance | `GlobalBuff` | `true` | The tap that cancels the buff; false keeps it. | [Code](../../app/src/main/cpp/script/js/mod/GlobalBuff.js#L28) |
| `SetStaticDefaults()` | Instance | `GlobalBuff` | `undefined` | With TileObjectData unlocked: changing a vanilla tile's shape or anchors works here. | [Code](../../app/src/main/cpp/script/js/mod/GlobalBuff.js#L5) |
| `UpdateNPC(type, npc, buffIndex)` | Instance | `GlobalBuff` | `undefined` | Every frame, each active buff (tModLoader's two Update). buffIndex is Ref: whoever removes the buff lowers it by 1. | [Code](../../app/src/main/cpp/script/js/mod/GlobalBuff.js#L10) |
| `UpdatePlayer(type, player, buffIndex)` | Instance | `GlobalBuff` | `undefined` | Every frame, each active buff (tModLoader's two Update). buffIndex is Ref: whoever removes the buff lowers it by 1. | [Code](../../app/src/main/cpp/script/js/mod/GlobalBuff.js#L9) |

## GlobalItem

[Contract and fields](classes.md#globalitem). Inherits from `GlobalType`.

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `AddRecipeGroups()` | Instance | `GlobalType` (inherited) | `undefined` | Once, with content ready. | [Code](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L14) |
| `AddRecipes()` | Instance | `GlobalType` (inherited) | `undefined` | Once, with content ready. | [Code](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L15) |
| `AllowPrefix(item, pre)` | Instance | `GlobalItem` | `true` | ModItem's prefix ones, for any item (the global comes first in ChoosePrefix). | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L76) |
| `AppliesToEntity(entity, lateInstantiation)` | Instance | `GlobalType` (inherited) | `true` | false: the Global's methods do not run for it. lateInstantiation is false on the ModifyNPCLoot sample. | [Code](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L8) |
| `ApplyPotionDelay(item, player, potionDelay)` | Instance | `GlobalItem` | `true` | Healing and potion delay. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L36) |
| `ApplyPrefix(item, pre)` | Instance | `GlobalItem` | `undefined` | ModItem's prefix ones, for any item (the global comes first in ChoosePrefix). | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L77) |
| `ArmorSetShadows(player, set)` | Instance | `GlobalItem` | `undefined` | Vanity by the drawn slots. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L92) |
| `CanAccessoryBeEquippedWith(equippedItem, incomingItem, player)` | Instance | `GlobalItem` | `true` | Conjuntos de qualquer item: o nome do conjunto ('' = nenhum), e o efeito por ele. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L80) |
| `CanBeChosenAsAmmo(ammo, weapon, player)` | Instance | `GlobalItem` | `null` | Ammo; a false vetoes, in the null ones a true forces. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L24) |
| `CanBeConsumedAsAmmo(ammo, weapon, player)` | Instance | `GlobalItem` | `true` | Ammo; a false vetoes, in the null ones a true forces. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L26) |
| `CanChooseAmmo(weapon, ammo, player)` | Instance | `GlobalItem` | `null` | Ammo; a false vetoes, in the null ones a true forces. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L23) |
| `CanConsumeAmmo(weapon, ammo, player)` | Instance | `GlobalItem` | `true` | Ammo; a false vetoes, in the null ones a true forces. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L25) |
| `CanHitNPC(item, player, target)` | Instance | `GlobalItem` | `null` | The melee hit, as in ModItem (Hits). | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L42) |
| `CanHitPvp(item, player, target)` | Instance | `GlobalItem` | `true` | The PvP melee hit. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L46) |
| `CanMeleeAttackCollideWithNPC(item, player, hitbox, target)` | Instance | `GlobalItem` | `null` | The melee hit, as in ModItem (Hits). | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L43) |
| `CanPickup(worldItem, player)` | Instance | `GlobalItem` | `true` | Picking up from the ground, as in ModItem, for any item. The Globals come before ModItem (in GrabRange, after), as in tModLoader. No native filter: every item in range enters JS. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L56) |
| `CanShoot(item, player)` | Instance | `GlobalItem` | `true` | Before the shot; false does not shoot. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L37) |
| `CanUseItem(item, player)` | Instance | `GlobalItem` | `true` | Before use; false prevents it. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L3) |
| `ChoosePrefix(item, rand)` | Instance | `GlobalItem` | `-1` | ModItem's prefix ones, for any item (the global comes first in ChoosePrefix). | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L74) |
| `Clone(from, to)` | Instance | `GlobalType` (inherited) | `Entities.Clone(this)` | Returns a copy of the global instance's fields. In the item flow, it is called by Item.Clone. | [Code](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L9) |
| `GetHealLife(item, player, quickHeal, healValue)` | Instance | `GlobalItem` | `undefined` | Healing and potion delay. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L33) |
| `GetHealMana(item, player, quickHeal, healValue)` | Instance | `GlobalItem` | `undefined` | Healing and potion delay. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L34) |
| `GrabRange(worldItem, player, grabRange)` | Instance | `GlobalItem` | `undefined` | Picking up from the ground, as in ModItem, for any item. The Globals come before ModItem (in GrabRange, after), as in tModLoader. No native filter: every item in range enters JS. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L59) |
| `GrabStyle(worldItem, player)` | Instance | `GlobalItem` | `false` | Picking up from the ground, as in ModItem, for any item. The Globals come before ModItem (in GrabRange, after), as in tModLoader. No native filter: every item in range enters JS. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L60) |
| `HoldItem(item, player)` | Instance | `GlobalItem` | `undefined` | Every frame with the item in hand. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L8) |
| `HoldItemFrame(item, player)` | Instance | `GlobalItem` | `undefined` | As in ModItem, after it, for any item. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L19) |
| `HoldStyle(item, player, mountOffset, heldItemFrame)` | Instance | `GlobalItem` | `undefined` | Every frame with the item in hand. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L7) |
| `HorizontalWingSpeeds(item, player, speed, acceleration)` | Instance | `GlobalItem` | `undefined` | Wings, including vanilla ones. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L97) |
| `get InstancePerEntity` | Getter | `GlobalType` (inherited) | `false` | true (field or getter): each entity gets its own copy, created in SetDefaults. | [Code](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L5) |
| `IsArmorSet(head, body, legs)` | Instance | `GlobalItem` | `''` | A set of any items ('' = none). | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L81) |
| `IsVanitySet(head, body, legs)` | Instance | `GlobalItem` | `See source` | Vanity by the drawn slots. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L85) |
| `ItemSpace(item, player)` | Instance | `GlobalItem` | `false` | Picking up from the ground, as in ModItem, for any item. The Globals come before ModItem (in GrabRange, after), as in tModLoader. No native filter: every item in range enters JS. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L58) |
| `MeleeEffects(item, player, hitbox)` | Instance | `GlobalItem` | `undefined` | As in ModItem, after it, for any item. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L16) |
| `ModifyHitNPC(item, player, target, modifiers)` | Instance | `GlobalItem` | `undefined` | The melee hit, as in ModItem (Hits). | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L44) |
| `ModifyHitPvp(item, player, target, modifiers)` | Instance | `GlobalItem` | `undefined` | The PvP melee hit. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L47) |
| `ModifyItemScale(item, player, scale)` | Instance | `GlobalItem` | `undefined` | As in ModItem, after it, for any item. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L13) |
| `ModifyPotionDelay(item, player, baseDelay)` | Instance | `GlobalItem` | `undefined` | Healing and potion delay. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L35) |
| `ModifyShootStats(item, player, stats)` | Instance | `GlobalItem` | `undefined` | stats = { position, velocity, type, damage, knockBack }. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L38) |
| `ModifyTooltips(item, tooltips)` | Instance | `GlobalItem` | `undefined` | The tooltip; after ModItem. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L66) |
| `ModifyWeaponCrit(item, player, crit)` | Instance | `GlobalItem` | `undefined` | As in ModItem, after it, for any item. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L11) |
| `ModifyWeaponDamage(item, player, damage)` | Instance | `GlobalItem` | `damage` | The weapon's damage; return the new one. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L9) |
| `ModifyWeaponKnockback(item, player, knockback)` | Instance | `GlobalItem` | `undefined` | As in ModItem, after it, for any item. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L12) |
| `NeedsAmmo(item, player)` | Instance | `GlobalItem` | `true` | Ammo; a false vetoes, in the null ones a true forces. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L22) |
| `NewInstance(target)` | Instance | `GlobalType` (inherited) | `Entities.NewInstance(this)` | A new entity's copy. Default: the template's fields. | [Code](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L10) |
| `OnConsumeAmmo(weapon, ammo, player)` | Instance | `GlobalItem` | `undefined` | Ammo; a false vetoes, in the null ones a true forces. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L27) |
| `OnConsumedAsAmmo(ammo, weapon, player)` | Instance | `GlobalItem` | `undefined` | Ammo; a false vetoes, in the null ones a true forces. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L28) |
| `OnCraft(item, player, recipe)` | Instance | `GlobalItem` | `undefined` | Crafted in a recipe. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L61) |
| `OnHitNPC(item, player, target, hit, damageDone)` | Instance | `GlobalItem` | `undefined` | The melee hit, as in ModItem (Hits). | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L45) |
| `OnHitPvp(item, player, target, hurtInfo)` | Instance | `GlobalItem` | `undefined` | The PvP melee hit. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L48) |
| `OnPickup(worldItem, player)` | Instance | `GlobalItem` | `true` | Picking up from the ground, as in ModItem, for any item. The Globals come before ModItem (in GrabRange, after), as in tModLoader. No native filter: every item in range enters JS. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L57) |
| `PickAmmo(weapon, ammo, player, type, speed, damage, knockback)` | Instance | `GlobalItem` | `undefined` | Ammo; a false vetoes, in the null ones a true forces. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L30) |
| `PostDrawInInventory(item, spriteBatch, position, frame, drawColor, itemColor, origin, scale)` | Instance | `GlobalItem` | `undefined` | Same arguments as ModItem. The global Pre runs before the local one; the local Post runs before the global. Post remains after a veto. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L65) |
| `PostDrawInWorld(item, spriteBatch, lightColor, alphaColor, rotation, scale, whoAmI)` | Instance | `GlobalItem` | `undefined` | Same arguments as ModItem. Local Pre and Post run before the globals; every applicable Pre runs even after a veto. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L63) |
| `PostDrawTooltip(item, lines)` | Instance | `GlobalItem` | `undefined` | ModItem's, with item first, for any item; after ModItem. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L70) |
| `PostDrawTooltipLine(item, line)` | Instance | `GlobalItem` | `undefined` | ModItem's, with item first, for any item; after ModItem. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L72) |
| `PostSetupContent()` | Instance | `GlobalType` (inherited) | `undefined` | Once, with content ready. | [Code](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L16) |
| `PostUpdateInWorld(item, worldItem)` | Instance | `GlobalItem` | `undefined` | The base implementation does nothing. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L54) |
| `PreDrawInInventory(item, spriteBatch, position, frame, drawColor, itemColor, origin, scale)` | Instance | `GlobalItem` | `true` | Same arguments as ModItem. The global Pre runs before the local one; the local Post runs before the global. Post remains after a veto. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L64) |
| `PreDrawInWorld(item, spriteBatch, lightColor, alphaColor, rotation, scale, whoAmI)` | Instance | `GlobalItem` | `true` | Same arguments as ModItem. Local Pre and Post run before the globals; every applicable Pre runs even after a veto. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L62) |
| `PreDrawTooltip(item, lines, x, y)` | Instance | `GlobalItem` | `true` | ModItem's, with item first, for any item; after ModItem. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L69) |
| `PreDrawTooltipLine(item, line, yOffset)` | Instance | `GlobalItem` | `true` | ModItem's, with item first, for any item; after ModItem. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L71) |
| `PrefixChance(item, pre, rand)` | Instance | `GlobalItem` | `null` | ModItem's prefix ones, for any item (the global comes first in ChoosePrefix). | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L75) |
| `PreUpdateInWorld(item, worldItem)` | Instance | `GlobalItem` | `true` | O item no chão, todo quadro (ver ModItem.PreUpdateInWorld). | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L53) |
| `PreUpdateVanitySet(player, set)` | Instance | `GlobalItem` | `undefined` | Vanity by the drawn slots. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L90) |
| `register(cls)` | Static | `GlobalItem` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L100) |
| `SetDefaults(item)` | Instance | `GlobalItem` | `undefined` | The item spawns or changes type. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L2) |
| `SetMatch(armorSlot, type, male, equipSlot, robes)` | Instance | `GlobalItem` | `undefined` | A part's drawn slot (0 head, 1 body, 2 legs). | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L94) |
| `SetStaticDefaults()` | Instance | `GlobalType` (inherited) | `undefined` | Once, with content ready. | [Code](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L12) |
| `Shoot(item, player, position, velocity, type, damage, knockBack, source)` | Instance | `GlobalItem` | `true` | false: the game's projectile does not come out. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L39) |
| `UpdateAccessory(item, player, vanity, hideVisual)` | Instance | `GlobalItem` | `undefined` | Accessory equipped (also vanity). | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L51) |
| `UpdateArmorSet(player, set)` | Instance | `GlobalItem` | `undefined` | A set of any items ('' = none). | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L82) |
| `UpdateEquip(item, player)` | Instance | `GlobalItem` | `undefined` | Equipped (accessory or armor). | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L50) |
| `UpdateInventory(item, player)` | Instance | `GlobalItem` | `undefined` | Every frame, for the 58 inventory slots. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L49) |
| `UpdateVanitySet(player, set)` | Instance | `GlobalItem` | `undefined` | Vanity by the drawn slots. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L91) |
| `UseAnimation(item, player)` | Instance | `GlobalItem` | `undefined` | As in ModItem, after it, for any item. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L17) |
| `UseItem(item, player)` | Instance | `GlobalItem` | `undefined` | The use starts; true as in ModItem. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L5) |
| `UseItemFrame(item, player)` | Instance | `GlobalItem` | `undefined` | As in ModItem, after it, for any item. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L18) |
| `UseItemHitbox(item, player, hitbox, noHitbox)` | Instance | `GlobalItem` | `undefined` | As in ModItem, after it, for any item. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L15) |
| `UseStyle(item, player, mountOffset, heldItemFrame)` | Instance | `GlobalItem` | `undefined` | Every frame with the item in hand. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L6) |
| `VerticalWingSpeeds(item, player, ascentWhenFalling, ascentWhenRising, maxCanAscendMultiplier, maxAscentMultiplier, constantAscend)` | Instance | `GlobalItem` | `undefined` | Wings, including vanilla ones. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L96) |
| `WingUpdate(wings, player, inUse)` | Instance | `GlobalItem` | `false` | Wings, including vanilla ones. | [Code](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L98) |

## GlobalLoot

[Contract and fields](classes.md#globalloot).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `Add(rule)` | Instance | `GlobalLoot` | `See source` | Adds or removes a global rule. | [Code](../../app/src/main/cpp/script/js/mod/GlobalLoot.js#L15) |
| `constructor()` | Constructor | `GlobalLoot` | `instance` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GlobalLoot.js#L5) |
| `Get()` | Instance | `GlobalLoot` | `NPCLoader.Rules(Terraria.Main.ItemDropsDB._globalEntries)` | The global rules, in an array. | [Code](../../app/src/main/cpp/script/js/mod/GlobalLoot.js#L11) |
| `GetRulesForNPCID(id)` | Instance | `GlobalLoot` | `Terraria.Main.ItemDropsDB['GetRulesForNPCID(int npcNetId, bool includeGlobalDrops)'](id, false)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GlobalLoot.js#L38) |
| `get itemDropDatabase` | Getter | `GlobalLoot` | `Terraria.Main.ItemDropsDB` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GlobalLoot.js#L9) |
| `ModifyGlobalLoot()` | Instance | `GlobalLoot` | `undefined` | The base implementation does nothing. | [Code](../../app/src/main/cpp/script/js/mod/GlobalLoot.js#L36) |
| `register(cls)` | Static | `GlobalLoot` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GlobalLoot.js#L54) |
| `RegisterToNPC(id, rule)` | Instance | `GlobalLoot` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GlobalLoot.js#L42) |
| `Remove(rule)` | Instance | `GlobalLoot` | `See source` | Adds or removes a global rule. | [Code](../../app/src/main/cpp/script/js/mod/GlobalLoot.js#L20) |
| `RemoveFromNPC(id, rule)` | Instance | `GlobalLoot` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GlobalLoot.js#L48) |
| `RemoveWhere(predicate)` | Instance | `GlobalLoot` | `See source` | Removes the matching ones; returns how many. | [Code](../../app/src/main/cpp/script/js/mod/GlobalLoot.js#L25) |

## GlobalNPC

[Contract and fields](classes.md#globalnpc). Inherits from `GlobalType`.

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `AddRecipeGroups()` | Instance | `GlobalType` (inherited) | `undefined` | Once, with content ready. | [Code](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L14) |
| `AddRecipes()` | Instance | `GlobalType` (inherited) | `undefined` | Once, with content ready. | [Code](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L15) |
| `AI(npc)` | Instance | `GlobalNPC` | `undefined` | Every frame. PreAI false skips the game's AI and the others' AI. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L6) |
| `AppliesToEntity(entity, lateInstantiation)` | Instance | `GlobalType` (inherited) | `true` | false: the Global's methods do not run for it. lateInstantiation is false on the ModifyNPCLoot sample. | [Code](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L8) |
| `ApplyDifficultyAndPlayerScaling(npc, numPlayers, balance, bossAdjustment)` | Instance | `GlobalNPC` | `undefined` | As in ModNPC, for any NPC. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L33) |
| `BossHeadRotation(npc, rotation)` | Instance | `GlobalNPC` | `undefined` | Ref of the boss icon. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L60) |
| `BossHeadSlot(npc, index)` | Instance | `GlobalNPC` | `undefined` | Ref of the boss icon. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L59) |
| `BossHeadSpriteEffects(npc, spriteEffects)` | Instance | `GlobalNPC` | `undefined` | Ref of the boss icon. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L61) |
| `CanBeHitByItem(npc, player, item)` | Instance | `GlobalNPC` | `null` | An item hit, as in ModNPC (Hits). | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L11) |
| `CanBeHitByNPC(npc, attacker)` | Instance | `GlobalNPC` | `true` | NPC against NPC. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L23) |
| `CanBeHitByProjectile(npc, projectile)` | Instance | `GlobalNPC` | `null` | A projectile hit. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L14) |
| `CanChat(npc)` | Instance | `GlobalNPC` | `null` | false prevents, true allows, null leaves it to the game. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L36) |
| `CanHitNPC(npc, target)` | Instance | `GlobalNPC` | `true` | NPC against NPC. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L22) |
| `CanHitPlayer(npc, target, cooldownSlot)` | Instance | `GlobalNPC` | `true` | Its hit on the player. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L19) |
| `CheckActive(npc)` | Instance | `GlobalNPC` | `true` | false: does not despawn far away / does not die at 0 life. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L31) |
| `CheckDead(npc)` | Instance | `GlobalNPC` | `true` | false: does not despawn far away / does not die at 0 life. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L32) |
| `Clone(from, to)` | Instance | `GlobalType` (inherited) | `Entities.Clone(this)` | Returns a copy of the global instance's fields. In the item flow, it is called by Item.Clone. | [Code](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L9) |
| `DrawEffects(npc, drawColor)` | Instance | `GlobalNPC` | `undefined` | Drawing (the Globals before the ModNPC in PreDraw). | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L56) |
| `EditSpawnFlags(spawnInfo)` | Instance | `GlobalNPC` | `undefined` | Before the rate, the area and the point: the player's fields in spawnInfo (noWorms, invaders, ZoneCorrupt...). The point does not exist yet (SpawnTileX = -1). | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L71) |
| `EditSpawnInfo(spawnInfo)` | Instance | `GlobalNPC` | `undefined` | With the point chosen, before the roll: the spawnInfo fields (waterTile, nearGranite...) change what the game and SpawnChance read. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L80) |
| `EditSpawnPool(pool, spawnInfo)` | Instance | `GlobalNPC` | `undefined` | The roll (SpawnPool): pool[type] = weight; 0 is the game's spawn. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L82) |
| `EditSpawnRange(player, spawnRangeX, spawnRangeY, safeRangeX, safeRangeY)` | Instance | `GlobalNPC` | `undefined` | Ref, in tiles: how far out it spawns and the minimum distance from the player. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L77) |
| `EditSpawnRate(player, spawnRate, maxSpawns)` | Instance | `GlobalNPC` | `undefined` | On each spawn attempt for the player. Ref: lower spawnRate = more spawns; maxSpawns, how many enemies nearby. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L74) |
| `FindFrame(npc, frameHeight)` | Instance | `GlobalNPC` | `undefined` | After the game's (or the ModNPC's) frame. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L29) |
| `GetAlpha(npc, drawColor)` | Instance | `GlobalNPC` | `null` | Drawing (the Globals before the ModNPC in PreDraw). | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L57) |
| `GetChat(npc, chat)` | Instance | `GlobalNPC` | `undefined` | The line; chat is a Ref. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L38) |
| `HitEffect(npc, hitDirection, damage)` | Instance | `GlobalNPC` | `undefined` | Took a hit (blood, gore). | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L8) |
| `get InstancePerEntity` | Getter | `GlobalType` (inherited) | `false` | true (field or getter): each entity gets its own copy, created in SetDefaults. | [Code](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L5) |
| `ModifyActiveShop(npc, shopName, items)` | Instance | `GlobalNPC` | `undefined` | On every opening, with the items ready. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L50) |
| `ModifyGlobalLoot(globalLoot)` | Instance | `GlobalNPC` | `undefined` | Once. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L64) |
| `ModifyHitByItem(npc, player, item, modifiers)` | Instance | `GlobalNPC` | `undefined` | An item hit, as in ModNPC (Hits). | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L12) |
| `ModifyHitByProjectile(npc, projectile, modifiers)` | Instance | `GlobalNPC` | `undefined` | A projectile hit. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L15) |
| `ModifyHitNPC(npc, target, modifiers)` | Instance | `GlobalNPC` | `undefined` | NPC against NPC. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L24) |
| `ModifyHitPlayer(npc, target, modifiers)` | Instance | `GlobalNPC` | `undefined` | Its hit on the player. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L20) |
| `ModifyIncomingHit(npc, modifiers)` | Instance | `GlobalNPC` | `undefined` | Last, on every hit. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L17) |
| `ModifyNPCHappiness(npc, player, primaryPlayerBiome, shopHelper, nearbyNPCsByType)` | Instance | `GlobalNPC` | `undefined` | As in ModNPC, for any NPC. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L39) |
| `ModifyNPCLoot(npc, npcLoot)` | Instance | `GlobalNPC` | `undefined` | Once per NPC type, with the game's sample. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L63) |
| `ModifyShop(shop)` | Instance | `GlobalNPC` | `undefined` | Once per shop (vanilla or mod), on the first opening: shop.Add goes after its items. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L48) |
| `NetReceive(npc, reader)` | Instance | `GlobalNPC` | `undefined` | Network: along with each NPC the server syncs. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L66) |
| `NetSend(npc, writer)` | Instance | `GlobalNPC` | `undefined` | Network: along with each NPC the server syncs. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L65) |
| `NewInstance(target)` | Instance | `GlobalType` (inherited) | `Entities.NewInstance(this)` | A new entity's copy. Default: the template's fields. | [Code](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L10) |
| `OnHitByItem(npc, player, item, hit, damageDone)` | Instance | `GlobalNPC` | `undefined` | An item hit, as in ModNPC (Hits). | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L13) |
| `OnHitByProjectile(npc, projectile, hit, damageDone)` | Instance | `GlobalNPC` | `undefined` | A projectile hit. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L16) |
| `OnHitNPC(npc, target, hit)` | Instance | `GlobalNPC` | `undefined` | NPC against NPC. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L25) |
| `OnHitPlayer(npc, target, hurtInfo)` | Instance | `GlobalNPC` | `undefined` | Its hit on the player. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L21) |
| `OnKill(npc)` | Instance | `GlobalNPC` | `undefined` | Death with drops, only on the server or in single player. PreKill false: no drop and no OnKill. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L27) |
| `OnSpawn(npc, source)` | Instance | `GlobalNPC` | `undefined` | Created by NewNPC (on the server or in single player). | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L3) |
| `PostAI(npc)` | Instance | `GlobalNPC` | `undefined` | Every frame. PreAI false skips the game's AI and the others' AI. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L7) |
| `PostDraw(npc, spriteBatch, screenPos, drawColor)` | Instance | `GlobalNPC` | `undefined` | Drawing (the Globals before the ModNPC in PreDraw). | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L55) |
| `PostSetupContent()` | Instance | `GlobalType` (inherited) | `undefined` | Once, with content ready. | [Code](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L16) |
| `PreAI(npc)` | Instance | `GlobalNPC` | `true` | Every frame. PreAI false skips the game's AI and the others' AI. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L5) |
| `PreDraw(npc, spriteBatch, screenPos, drawColor)` | Instance | `GlobalNPC` | `true` | Drawing (the Globals before the ModNPC in PreDraw). | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L54) |
| `PreKill(npc)` | Instance | `GlobalNPC` | `true` | Death with drops, only on the server or in single player. PreKill false: no drop and no OnKill. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L26) |
| `register(cls)` | Static | `GlobalNPC` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L86) |
| `ResetEffects(npc)` | Instance | `GlobalNPC` | `undefined` | Start of the NPC's update. | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L4) |
| `SetDefaults(npc)` | Instance | `GlobalNPC` | `undefined` | The NPC spawns (also on the client, when it arrives over the network). | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L2) |
| `SetStaticDefaults()` | Instance | `GlobalType` (inherited) | `undefined` | Once, with content ready. | [Code](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L12) |
| `SetupShop(type, shop, nextSlot)` | Instance | `GlobalNPC` | `undefined` | The old format: shop.item, the free slot in nextSlot (Ref). | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L49) |
| `SetupTravelShop(shop, nextSlot)` | Instance | `GlobalNPC` | `undefined` | The Traveling Merchant's shop (Main.travelShop). | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L51) |
| `SpawnNPC(npc, tileX, tileY)` | Instance | `GlobalNPC` | `undefined` | A rolled NPC that is not the game's spawned (npc, the index). | [Code](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L84) |

## GlobalProjectile

[Contract and fields](classes.md#globalprojectile). Inherits from `GlobalType`.

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `AddRecipeGroups()` | Instance | `GlobalType` (inherited) | `undefined` | Once, with content ready. | [Code](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L14) |
| `AddRecipes()` | Instance | `GlobalType` (inherited) | `undefined` | Once, with content ready. | [Code](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L15) |
| `AI(projectile)` | Instance | `GlobalProjectile` | `undefined` | Every frame. | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L5) |
| `AppliesToEntity(entity, lateInstantiation)` | Instance | `GlobalType` (inherited) | `true` | false: the Global's methods do not run for it. lateInstantiation is false on the ModifyNPCLoot sample. | [Code](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L8) |
| `CanCutTiles(projectile)` | Instance | `GlobalProjectile` | `undefined` | The first global boolean decides; false prevents default and custom cutting. | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L26) |
| `CanDamage(projectile)` | Instance | `GlobalProjectile` | `undefined` | A global false vetoes; a global true wins over the local method. Native conditions still apply. | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L21) |
| `CanHitNPC(projectile, target)` | Instance | `GlobalProjectile` | `null` | The hit on an NPC, as in ModProjectile (Hits). | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L10) |
| `CanHitPlayer(projectile, target)` | Instance | `GlobalProjectile` | `true` | The hit on a player (hostile / PvP). | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L14) |
| `CanHitPvp(projectile, target)` | Instance | `GlobalProjectile` | `true` | The hit on a player (hostile / PvP). | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L13) |
| `CanUseGrapple(type, player)` | Instance | `GlobalProjectile` | `undefined` | On the template, filtered by a sample of the type; the last global boolean answer decides. | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L28) |
| `Clone(from, to)` | Instance | `GlobalType` (inherited) | `Entities.Clone(this)` | Returns a copy of the global instance's fields. In the item flow, it is called by Item.Clone. | [Code](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L9) |
| `Colliding(projectile, projHitbox, targetHitbox)` | Instance | `GlobalProjectile` | `undefined` | The first global boolean decides; undefined/null asks the local method and the game. | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L20) |
| `CutTiles(projectile)` | Instance | `GlobalProjectile` | `undefined` | Globals, the local method and the default cutting, when allowed. | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L27) |
| `GetAlpha(projectile, lightColor)` | Instance | `GlobalProjectile` | `undefined` | The first global Color decides; undefined/null asks the local method and the game. | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L19) |
| `GrappleCanLatchOnTo(projectile, player, tile)` | Instance | `GlobalProjectile` | `undefined` | Receives the native tile; a global false vetoes, a global true allows. | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L30) |
| `get InstancePerEntity` | Getter | `GlobalType` (inherited) | `false` | true (field or getter): each entity gets its own copy, created in SetDefaults. | [Code](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L5) |
| `MinionContactDamage(projectile)` | Instance | `GlobalProjectile` | `false` | true allows a pet's contact damage. | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L25) |
| `ModifyDamageHitbox(projectile, hitbox)` | Instance | `GlobalProjectile` | `undefined` | Ref<Rectangle>, after the local method. | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L22) |
| `ModifyHitNPC(projectile, target, modifiers)` | Instance | `GlobalProjectile` | `undefined` | The hit on an NPC, as in ModProjectile (Hits). | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L11) |
| `ModifyHitPlayer(projectile, target, modifiers)` | Instance | `GlobalProjectile` | `undefined` | The hit on a player (hostile / PvP). | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L15) |
| `NetReceive(projectile, reader)` | Instance | `GlobalProjectile` | `undefined` | Network: along with each synced projectile, from whoever controls it. | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L33) |
| `NetSend(projectile, writer)` | Instance | `GlobalProjectile` | `undefined` | Network: along with each synced projectile, from whoever controls it. | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L32) |
| `NewInstance(target)` | Instance | `GlobalType` (inherited) | `Entities.NewInstance(this)` | A new entity's copy. Default: the template's fields. | [Code](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L10) |
| `OnHitNPC(projectile, target, hit, damageDone)` | Instance | `GlobalProjectile` | `undefined` | The hit on an NPC, as in ModProjectile (Hits). | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L12) |
| `OnHitPlayer(projectile, target, info)` | Instance | `GlobalProjectile` | `undefined` | The hit on a player (hostile / PvP). | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L16) |
| `OnKill(projectile, timeLeft)` | Instance | `GlobalProjectile` | `undefined` | After the native death, when PreKill did not veto. | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L8) |
| `OnSpawn(projectile, source)` | Instance | `GlobalProjectile` | `undefined` | Created by NewProjectile, on whoever created it. | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L3) |
| `OnTileCollide(projectile, oldVelocity)` | Instance | `GlobalProjectile` | `true` | false prevents death by impact; time and NPC still call OnKill. Every Global runs before the local method. | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L23) |
| `PostAI(projectile)` | Instance | `GlobalProjectile` | `undefined` | Every frame. | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L6) |
| `PostDraw(projectile, lightColor)` | Instance | `GlobalProjectile` | `undefined` | Direct color; runs after the local method, even with a draw veto. | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L18) |
| `PostSetupContent()` | Instance | `GlobalType` (inherited) | `undefined` | Once, with content ready. | [Code](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L16) |
| `PreAI(projectile)` | Instance | `GlobalProjectile` | `true` | Every frame. | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L4) |
| `PreDraw(projectile, lightColor)` | Instance | `GlobalProjectile` | `true` | Color in a Ref<Color>; false cancels the sprite. Every Global runs before the local method, keeping extras like chains. | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L17) |
| `PreKill(projectile, timeLeft)` | Instance | `GlobalProjectile` | `true` | Before death. false removes the projectile without the native effect and ends the flow before OnKill. | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L7) |
| `register(cls)` | Static | `GlobalProjectile` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L35) |
| `SetDefaults(projectile)` | Instance | `GlobalProjectile` | `undefined` | The projectile spawns (also on the other side of the network). | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L2) |
| `SetStaticDefaults()` | Instance | `GlobalType` (inherited) | `undefined` | Once, with content ready. | [Code](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L12) |
| `ShouldUpdatePosition(projectile)` | Instance | `GlobalProjectile` | `true` | false: the game does not add velocity to position. After the local method; one false is enough. | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L31) |
| `TileCollideStyle(projectile, width, height, fallThrough, hitboxCenterFrac)` | Instance | `GlobalProjectile` | `true` | Four Refs, after the local method; the first false skips collision for this movement. | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L24) |
| `UseGrapple(player, type)` | Instance | `GlobalProjectile` | `undefined` | Type in a Ref<number>, after the local method, for every template that implements the method. | [Code](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L29) |

## GlobalRecipe

[Contract and fields](classes.md#globalrecipe).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `ConsumeItem(recipe, type, amount)` | Instance | `GlobalRecipe` | `undefined` | Each ingredient on each craft: amount (Ref) is how much is spent, already with the Alchemy Table discount. Never more than the player has. | [Code](../../app/src/main/cpp/script/js/mod/GlobalRecipe.js#L14) |
| `OnCraft(item, recipe)` | Instance | `GlobalRecipe` | `undefined` | The crafted item, before it goes to the cursor or the inventory. | [Code](../../app/src/main/cpp/script/js/mod/GlobalRecipe.js#L17) |
| `PostSetupContent()` | Instance | `GlobalRecipe` | `undefined` | The base implementation does nothing. | [Code](../../app/src/main/cpp/script/js/mod/GlobalRecipe.js#L7) |
| `RecipeAvailable(recipe)` | Instance | `GlobalRecipe` | `true` | false: the recipe leaves the crafting menu (the Guide still shows it). | [Code](../../app/src/main/cpp/script/js/mod/GlobalRecipe.js#L10) |
| `register(cls)` | Static | `GlobalRecipe` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GlobalRecipe.js#L19) |
| `SetStaticDefaults()` | Instance | `GlobalRecipe` | `undefined` | With TileObjectData unlocked: changing a vanilla tile's shape or anchors works here. | [Code](../../app/src/main/cpp/script/js/mod/GlobalRecipe.js#L6) |

## GlobalTile

[Contract and fields](classes.md#globaltile).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `AdjTiles(type)` | Instance | `GlobalTile` | `[]` | The types a tile of type counts as near the player (crafting stations). Asked once per type. On mobile the game only checks with the inventory open. | [Code](../../app/src/main/cpp/script/js/mod/GlobalTile.js#L51) |
| `AnimateTile()` | Instance | `GlobalTile` | `undefined` | Once per frame. | [Code](../../app/src/main/cpp/script/js/mod/GlobalTile.js#L49) |
| `CanDrop(i, j, type)` | Instance | `GlobalTile` | `true` | false: the block drops nothing; Drop comes after its items. Vanilla multi-cell objects drop through their own code. | [Code](../../app/src/main/cpp/script/js/mod/GlobalTile.js#L23) |
| `CanExplode(i, j, type)` | Instance | `GlobalTile` | `true` | false: a projectile's explosion spares it. | [Code](../../app/src/main/cpp/script/js/mod/GlobalTile.js#L31) |
| `CanKillTile(i, j, type, blockDamaged)` | Instance | `GlobalTile` | `true` | false: the tile does not break. | [Code](../../app/src/main/cpp/script/js/mod/GlobalTile.js#L13) |
| `CanPlace(i, j, type)` | Instance | `GlobalTile` | `true` | false: the player does not place it. | [Code](../../app/src/main/cpp/script/js/mod/GlobalTile.js#L28) |
| `CreateDust(i, j, type, dustType)` | Instance | `GlobalTile` | `true` | Each particle, after the game picks it: dustType (Ref) swaps it; false removes it. | [Code](../../app/src/main/cpp/script/js/mod/GlobalTile.js#L19) |
| `Drop(i, j, type)` | Instance | `GlobalTile` | `undefined` | false: the block drops nothing; Drop comes after its items. Vanilla multi-cell objects drop through their own code. | [Code](../../app/src/main/cpp/script/js/mod/GlobalTile.js#L25) |
| `HitWire(i, j, type)` | Instance | `GlobalTile` | `undefined` | A wire signal; false stops it (the game's and HitWire). | [Code](../../app/src/main/cpp/script/js/mod/GlobalTile.js#L41) |
| `KillSound(i, j, type, fail)` | Instance | `GlobalTile` | `true` | false: no sound. | [Code](../../app/src/main/cpp/script/js/mod/GlobalTile.js#L21) |
| `KillTile(i, j, type, fail, effectOnly, noItem)` | Instance | `GlobalTile` | `undefined` | Before breaking (the three are Ref). | [Code](../../app/src/main/cpp/script/js/mod/GlobalTile.js#L15) |
| `MouseOver(i, j, type)` | Instance | `GlobalTile` | `undefined` | The tap on a tile, the tile under the cursor (near and far). | [Code](../../app/src/main/cpp/script/js/mod/GlobalTile.js#L45) |
| `MouseOverFar(i, j, type)` | Instance | `GlobalTile` | `undefined` | The tap on a tile, the tile under the cursor (near and far). | [Code](../../app/src/main/cpp/script/js/mod/GlobalTile.js#L46) |
| `NumDust(i, j, type, fail, num)` | Instance | `GlobalTile` | `undefined` | How many particles (num, Ref; only lowers it). | [Code](../../app/src/main/cpp/script/js/mod/GlobalTile.js#L17) |
| `PlaceInWorld(i, j, type, item)` | Instance | `GlobalTile` | `undefined` | After the player places it. | [Code](../../app/src/main/cpp/script/js/mod/GlobalTile.js#L29) |
| `PostSetupContent()` | Instance | `GlobalTile` | `undefined` | The base implementation does nothing. | [Code](../../app/src/main/cpp/script/js/mod/GlobalTile.js#L8) |
| `PostSetupTileMerge()` | Instance | `GlobalTile` | `undefined` | Once, after setup (Main.tileMerge). | [Code](../../app/src/main/cpp/script/js/mod/GlobalTile.js#L10) |
| `PreHitWire(i, j, type)` | Instance | `GlobalTile` | `true` | A wire signal; false stops it (the game's and HitWire). | [Code](../../app/src/main/cpp/script/js/mod/GlobalTile.js#L40) |
| `PreShakeTree(x, y, treeType)` | Instance | `GlobalTile` | `undefined` | Shaking a tree (TreeTypes): PreShakeTree before the drops; ShakeTree returning true means it dropped the main item. | [Code](../../app/src/main/cpp/script/js/mod/GlobalTile.js#L55) |
| `RandomUpdate(i, j, type, underground)` | Instance | `GlobalTile` | `undefined` | Each tile the world update picks (hundreds per frame). | [Code](../../app/src/main/cpp/script/js/mod/GlobalTile.js#L37) |
| `register(cls)` | Static | `GlobalTile` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GlobalTile.js#L58) |
| `RightClick(i, j, type)` | Instance | `GlobalTile` | `undefined` | The tap that cancels the buff; false keeps it. | [Code](../../app/src/main/cpp/script/js/mod/GlobalTile.js#L44) |
| `SetStaticDefaults()` | Instance | `GlobalTile` | `undefined` | With TileObjectData unlocked: changing a vanilla tile's shape or anchors works here. | [Code](../../app/src/main/cpp/script/js/mod/GlobalTile.js#L7) |
| `ShakeTree(x, y, treeType)` | Instance | `GlobalTile` | `false` | Shaking a tree (TreeTypes): PreShakeTree before the drops; ShakeTree returning true means it dropped the main item. | [Code](../../app/src/main/cpp/script/js/mod/GlobalTile.js#L56) |
| `Slope(i, j, type)` | Instance | `GlobalTile` | `true` | false: the player's hammer leaves it as it is. | [Code](../../app/src/main/cpp/script/js/mod/GlobalTile.js#L33) |

## HitInfo

[Contract and fields](classes.md#hits).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `constructor(values = {})` | Constructor | `HitInfo` | `instance` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HitInfo.js#L4) |

## HitModifiers

[Contract and fields](classes.md#hits).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `constructor(baseDamage = 0, baseKnockback = 0, hitDirection = 0, crit = false, defense = 0, damageType = null)` | Constructor | `HitModifiers` | `instance` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HitModifiers.js#L14) |
| `get crit` | Getter | `HitModifiers` | `this.#crit ?? this.#base.crit` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HitModifiers.js#L48) |
| `set crit(value)` | Setter | `HitModifiers` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HitModifiers.js#L49) |
| `get damage` | Getter | `HitModifiers` | `this.#base.damage` | Os nomes de antes (o golpe em números), ainda aceitos. | [Code](../../app/src/main/cpp/script/js/mod/HitModifiers.js#L42) |
| `set damage(value)` | Setter | `HitModifiers` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HitModifiers.js#L43) |
| `DisableCrit()` | Instance | `HitModifiers` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HitModifiers.js#L35) |
| `DisableKnockback()` | Instance | `HitModifiers` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HitModifiers.js#L37) |
| `GetDamage(baseDamage, crit)` | Instance | `HitModifiers` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HitModifiers.js#L51) |
| `GetKnockback(baseKnockback)` | Instance | `HitModifiers` | `this.#noKnockback ? 0 : Math.max(this.Knockback.ApplyTo(baseKnockback), 0)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HitModifiers.js#L68) |
| `HideCombatText()` | Instance | `HitModifiers` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HitModifiers.js#L39) |
| `get hitDirection` | Getter | `HitModifiers` | `this.HitDirectionOverride ?? this.HitDirection` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HitModifiers.js#L46) |
| `set hitDirection(value)` | Setter | `HitModifiers` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HitModifiers.js#L47) |
| `get knockBack` | Getter | `HitModifiers` | `this.#base.knockback` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HitModifiers.js#L44) |
| `set knockBack(value)` | Setter | `HitModifiers` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HitModifiers.js#L45) |
| `SetCrit()` | Instance | `HitModifiers` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HitModifiers.js#L36) |
| `SetInstantKill()` | Instance | `HitModifiers` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HitModifiers.js#L38) |
| `SetMaxDamage(limit)` | Instance | `HitModifiers` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HitModifiers.js#L34) |
| `ToHitInfo(baseDamage = this.#base.damage, crit = this.#base.crit, baseKnockback = this.#base.knockback)` | Instance | `HitModifiers` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HitModifiers.js#L72) |

## HurtInfo

[Contract and fields](classes.md#hits).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `constructor(values = {})` | Constructor | `HurtInfo` | `instance` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HurtInfo.js#L4) |

## HurtModifiers

[Contract and fields](classes.md#hits).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `Cancel()` | Instance | `HurtModifiers` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HurtModifiers.js#L31) |
| `get Cancelled` | Getter | `HurtModifiers` | `this.#cancelled` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HurtModifiers.js#L35) |
| `constructor(source = null, damage = 0, hitDirection = 0, pvp = false, cooldownCounter = -1, dodgeable = true, quiet = false, crit = false)` | Constructor | `HurtModifiers` | `instance` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HurtModifiers.js#L13) |
| `get crit` | Getter | `HurtModifiers` | `this.#base.crit` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HurtModifiers.js#L47) |
| `set crit(value)` | Setter | `HurtModifiers` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HurtModifiers.js#L48) |
| `get damage` | Getter | `HurtModifiers` | `this.#base.damage` | Os nomes de antes (o golpe em números), ainda aceitos. | [Code](../../app/src/main/cpp/script/js/mod/HurtModifiers.js#L41) |
| `set damage(value)` | Setter | `HurtModifiers` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HurtModifiers.js#L42) |
| `DisableDust()` | Instance | `HurtModifiers` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HurtModifiers.js#L32) |
| `DisableSound()` | Instance | `HurtModifiers` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HurtModifiers.js#L33) |
| `get dodgeable` | Getter | `HurtModifiers` | `this.Dodgeable` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HurtModifiers.js#L49) |
| `set dodgeable(value)` | Setter | `HurtModifiers` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HurtModifiers.js#L50) |
| `get DustDisabled` | Getter | `HurtModifiers` | `this.#dust` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HurtModifiers.js#L36) |
| `EffectiveDefense(defense)` | Instance | `HurtModifiers` | `See source` | A defesa que sobra depois da penetração. | [Code](../../app/src/main/cpp/script/js/mod/HurtModifiers.js#L59) |
| `Final(afterDefense)` | Instance | `HurtModifiers` | `See source` | Depois da defesa: o final e o teto. | [Code](../../app/src/main/cpp/script/js/mod/HurtModifiers.js#L65) |
| `get hitDirection` | Getter | `HurtModifiers` | `this.HitDirectionOverride ?? this.HitDirection` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HurtModifiers.js#L43) |
| `set hitDirection(value)` | Setter | `HurtModifiers` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HurtModifiers.js#L44) |
| `get MaxDamage` | Getter | `HurtModifiers` | `this.#limit` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HurtModifiers.js#L38) |
| `get quiet` | Getter | `HurtModifiers` | `this.#base.quiet \|\| (this.#dust && this.#sound)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HurtModifiers.js#L45) |
| `set quiet(value)` | Setter | `HurtModifiers` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HurtModifiers.js#L46) |
| `SetMaxDamage(limit)` | Instance | `HurtModifiers` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HurtModifiers.js#L30) |
| `get SoundDisabled` | Getter | `HurtModifiers` | `this.#sound` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/HurtModifiers.js#L37) |
| `SourceValue()` | Instance | `HurtModifiers` | `See source` | O dano que entra no Hurt do jogo (antes da defesa). | [Code](../../app/src/main/cpp/script/js/mod/HurtModifiers.js#L53) |
| `ToHurtInfo(damageDone)` | Instance | `HurtModifiers` | `See source` | O que o jogador já levou, para os OnHit/OnHurt. | [Code](../../app/src/main/cpp/script/js/mod/HurtModifiers.js#L71) |

## ItemLoot

[Contract and fields](classes.md#npc-helpers).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `Add(rule)` | Instance | `ItemLoot` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ItemLoot.js#L31) |
| `constructor(type)` | Constructor | `ItemLoot` | `instance` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ItemLoot.js#L10) |
| `For(m)` | Static | `ItemLoot` | `See source` | As regras do tipo; o ModifyItemLoot do ModItem roda na primeira vez. | [Code](../../app/src/main/cpp/script/js/mod/ItemLoot.js#L22) |
| `Get()` | Instance | `ItemLoot` | `[...this.rules]` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ItemLoot.js#L42) |
| `Remove(rule)` | Instance | `ItemLoot` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ItemLoot.js#L36) |
| `RemoveWhere(predicate)` | Instance | `ItemLoot` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ItemLoot.js#L46) |

## KeybindLoader

[Contract and fields](classes.md#modtouchbutton).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `RegisterKeybind(mod, name, defaultBinding)` | Static | `KeybindLoader` | `TouchButtonLoader.RegisterButton(mod, name)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/KeybindLoader.js#L5) |

## Mod

[Contract and fields](classes.md#mod-and-modloader).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `AddBossHeadTexture(texture, npcType = -1)` | Instance | `Mod` | `See source` | Registers one more boss icon (the 2nd phase, for example) and returns the slot, for BossHeadSlot. With type, it becomes that NPC's default icon. -1 without the file. Call it during registration (Load, SetStaticDefaults). | [Code](../../app/src/main/cpp/script/js/mod/Mod.js#L28) |
| `AddRecipeGroups()` | Instance | `Mod` | `undefined` | As in ModSystem. | [Code](../../app/src/main/cpp/script/js/mod/Mod.js#L21) |
| `AddRecipes()` | Instance | `Mod` | `undefined` | As in ModSystem. | [Code](../../app/src/main/cpp/script/js/mod/Mod.js#L22) |
| `Call(...args)` | Instance | `Mod` | `See source` | When another mod calls. | [Code](../../app/src/main/cpp/script/js/mod/Mod.js#L47) |
| `constructor()` | Constructor | `Mod` | `instance` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Mod.js#L2) |
| `get dataDirectory` | Getter | `Mod` | `ModRegistry.DataDirectory(this.uuid)` | Android/data/com.bunnyloader/mod_data/<uid>. | [Code](../../app/src/main/cpp/script/js/mod/Mod.js#L16) |
| `GetContent(base)` | Instance | `Mod` | `Templates.All(base, this)` | This mod's templates that extend Base, in registration order: this.GetContent(ModItem).map((i) => i.Type) gives the types of every item. Does not apply to ModPlayer (one per player: player.GetModPlayer). | [Code](../../app/src/main/cpp/script/js/mod/Mod.js#L35) |
| `GetPacket()` | Instance | `Mod` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Mod.js#L39) |
| `HandlePacket(reader, whoAmI)` | Instance | `Mod` | `undefined` | The base implementation does nothing. | [Code](../../app/src/main/cpp/script/js/mod/Mod.js#L44) |
| `get id` | Getter | `Mod` | `this[MOD_INFO].id` | Is it installed (and did it load)? | [Code](../../app/src/main/cpp/script/js/mod/Mod.js#L10) |
| `Load()` | Instance | `Mod` | `undefined` | During loading, after the content registration. | [Code](../../app/src/main/cpp/script/js/mod/Mod.js#L20) |
| `get name` | Getter | `Mod` | `this[MOD_INFO].name` | From the manifest. | [Code](../../app/src/main/cpp/script/js/mod/Mod.js#L12) |
| `get path` | Getter | `Mod` | `this[MOD_INFO].path` | Registers one more boss icon (the 2nd phase, for example) and returns the slot, for BossHeadSlot. With type, it becomes that NPC's default icon. -1 without the file. Call it during registration (Load, SetStaticDefaults). | [Code](../../app/src/main/cpp/script/js/mod/Mod.js#L14) |
| `PostSetupContent()` | Instance | `Mod` | `undefined` | As in ModSystem. | [Code](../../app/src/main/cpp/script/js/mod/Mod.js#L23) |
| `register()` | Static | `Mod` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Mod.js#L58) |
| `get root` | Getter | `Mod` | `this[MOD_INFO].root` | main.js's folder (content/) and the package's. | [Code](../../app/src/main/cpp/script/js/mod/Mod.js#L15) |
| `toString()` | Instance | `Mod` | `'Mod(' + (this.id \|\| this.uuid) + ')'` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Mod.js#L18) |
| `get uuid` | Getter | `Mod` | `this[MOD_INFO].uuid` | From the manifest. | [Code](../../app/src/main/cpp/script/js/mod/Mod.js#L11) |
| `get version` | Getter | `Mod` | `this[MOD_INFO].version` | From the manifest. | [Code](../../app/src/main/cpp/script/js/mod/Mod.js#L13) |

## ModAchievement

[Contract and fields](classes.md#modachievement).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `AddCondition(key = 'Condition')` | Instance | `ModAchievement` | `this.#Add(Terraria.GameContent.Achievements.CustomFlagCondition['AchievementCondition Create(string name)'](key))` | As condições, como no tModLoader. Devolvem a condição do jogo (a de número tem .Value). | [Code](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L32) |
| `AddFloatCondition(key, maxValue)` | Instance | `ModAchievement` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L39) |
| `AddIntCondition(key, maxValue)` | Instance | `ModAchievement` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L35) |
| `AddItemCraftCondition(itemIds)` | Instance | `ModAchievement` | `this.#AddByUser('ItemCraftCondition', 'short', 'items', 'item', itemIds)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L43) |
| `AddItemPickupCondition(itemIds)` | Instance | `ModAchievement` | `this.#AddByUser('ItemPickupCondition', 'short', 'items', 'item', itemIds)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L44) |
| `AddNPCKilledCondition(npcIds)` | Instance | `ModAchievement` | `this.#AddByUser('NPCKilledCondition', 'short', 'npcIds', 'npcId', npcIds)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L45) |
| `AddTileDestroyedCondition(tileIds)` | Instance | `ModAchievement` | `this.#AddByUser('TileDestroyedCondition', 'ushort', 'tileIds', null, Array.isArray(tileIds) ? tileIds : [tileIds])` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L46) |
| `get Category` | Getter | `ModAchievement` | `Terraria.Achievements.AchievementCategory.Slayer` | Terraria.Achievements.AchievementCategory.Slayer (default), Collector, Explorer, Challenger. | [Code](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L17) |
| `get Hidden` | Getter | `ModAchievement` | `false` | The name and description become "???" in the menu until it is completed. | [Code](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L19) |
| `get Index` | Getter | `ModAchievement` | `0` | The position in the sheet (columns of 66). | [Code](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L15) |
| `get IsCompleted` | Getter | `ModAchievement` | `!!(this.Achievement && this.Achievement.IsCompleted)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L20) |
| `OnCompleted(achievement)` | Instance | `ModAchievement` | `undefined` | Completed. | [Code](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L23) |
| `OnItemCraft(itemType, stack)` | Instance | `ModAchievement` | `undefined` | The game's notifications, while not completed (from ExMod; tModLoader uses AchievementsHelper's events). | [Code](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L27) |
| `OnItemPickup(player, itemType, stack)` | Instance | `ModAchievement` | `undefined` | The game's notifications, while not completed (from ExMod; tModLoader uses AchievementsHelper's events). | [Code](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L26) |
| `OnNPCKilled(player, npcId)` | Instance | `ModAchievement` | `undefined` | The game's notifications, while not completed (from ExMod; tModLoader uses AchievementsHelper's events). | [Code](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L25) |
| `OnTileDestroyed(player, tileType)` | Instance | `ModAchievement` | `undefined` | The game's notifications, while not completed (from ExMod; tModLoader uses AchievementsHelper's events). | [Code](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L28) |
| `register(cls)` | Static | `ModAchievement` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L65) |
| `SetStaticDefaults()` | Instance | `ModAchievement` | `undefined` | The conditions: AddCondition(key), AddIntCondition(key, max), AddFloatCondition, AddItemCraftCondition(id or [ids]), AddItemPickupCondition, AddNPCKilledCondition, AddTileDestroyedCondition([ids]). They return the game's condition (.Value on the numeric ones, .Complete()). | [Code](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L22) |

## ModBannerTile

[Contract and fields](classes.md#modbannertile). Inherits from `ModTile`.

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `AddMapEntry(color, name)` | Instance | `ModTile` (inherited) | `See source` | The color and the name on the map; each call is one more option. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L28) |
| `AnimateIndividualTile(type, i, j, frameXOffset, frameYOffset)` | Instance | `ModTile` (inherited) | `undefined` | the native drawing (GetTileDrawData); tile.drawdata mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L77) |
| `AnimateTile(frame, frameCounter)` | Instance | `ModTile` (inherited) | `undefined` | Main.AnimateTiles (only if some tile uses it) | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L76) |
| `CanDrop(i, j)` | Instance | `ModTile` (inherited) | `true` | WorldGen.KillTile, TileFrameImportant; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L70) |
| `CanKillTile(i, j, blockDamaged)` | Instance | `ModTile` (inherited) | `true` | WorldGen.KillTile, TileFrameImportant; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L63) |
| `CreateDust(i, j, type)` | Instance | `ModTile` (inherited) | `true` | WorldGen.KillTile, TileFrameImportant; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L68) |
| `CreateMapEntryName()` | Instance | `ModTile` (inherited) | `this.constructor.name` | The class name. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L52) |
| `DefaultContainerName(frameX, frameY)` | Instance | `ModTile` (inherited) | `this.CreateMapEntryName()` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L114) |
| `DrawEffects(i, j, spriteBatch, drawData)` | Instance | `ModTile` (inherited) | `undefined` | a pass after TileDrawing.PostDrawTiles; tile.draw mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L83) |
| `EmitParticles(i, j, tile, tileFrameX, tileFrameY, tileLight, visible)` | Instance | `ModTile` (inherited) | `undefined` | a pass after TileDrawing.PostDrawTiles; tile.draw mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L85) |
| `GetItemDrops(i, j)` | Instance | `ModTile` (inherited) | `undefined` | WorldGen.KillTile, TileFrameImportant; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L72) |
| `GetMapOption(i, j)` | Instance | `ModTile` (inherited) | `0` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L61) |
| `getModTile(type)` | Static | `ModTile` (inherited) | `TileLoader.ByType.get(type)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L145) |
| `getTypeByName(name)` | Static | `ModTile` (inherited) | `bl.tiles.typeOf(name)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L144) |
| `GrowSapling(i, j, underground, treeHeightAddon, ignoreWalls)` | Instance | `ModTile` (inherited) | `See source` | A muda (TileID.Sets.TreeSapling) virando árvore: o RandomUpdate dela chama WorldGen.AttemptToGrowTreeFromSapling(i, j, underground), que chega aqui. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L98) |
| `HasSmartInteract(i, j, settings)` | Instance | `ModTile` (inherited) | `Hooks.Overrides(this.constructor, ModTile, 'RightClick')` | Se o tile é alvo do smart-interact. No celular o toque num móvel passa por ele (é o clique direito do PC): sem sobrescrever, vale para quem tem RightClick. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L94) |
| `get HighlightTexture` | Getter | `ModTile` (inherited) | `this.Texture + '_Highlight'` | The frame sheet; the outline (HasOutlines). | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L18) |
| `HitWire(i, j)` | Instance | `ModTile` (inherited) | `undefined` | Wiring.HitWireSingle; tile.wire mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L95) |
| `IsLockedChest(i, j)` | Instance | `ModTile` (inherited) | `false` | Chest.IsLocked/Unlock/Lock; tile.chest mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L111) |
| `isModType(type)` | Static | `ModTile` (inherited) | `bl.tiles.isModTile(type)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L143) |
| `KillMultiTile(i, j, frameX, frameY)` | Instance | `ModTile` (inherited) | `undefined` | WorldGen.KillTile, TileFrameImportant; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L66) |
| `KillSound(i, j, fail)` | Instance | `ModTile` (inherited) | `true` | WorldGen.KillTile, TileFrameImportant; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L69) |
| `KillTile(i, j, fail, effectOnly, noItem)` | Instance | `ModTile` (inherited) | `undefined` | WorldGen.KillTile, TileFrameImportant; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L64) |
| `LockChest(i, j, frameXAdjustment, manual)` | Instance | `ModTile` (inherited) | `false` | Chest.IsLocked/Unlock/Lock; tile.chest mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L113) |
| `ModifyLight(i, j, r, g, b)` | Instance | `ModTile` (inherited) | `undefined` | Lighting.LightTiles (every 3 frames) + batched Lighting.AddLight; tile.light mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L75) |
| `ModifySittingTargetInfo(i, j, info)` | Instance | `ModTile` (inherited) | `undefined` | PlayerSittingHelper/PlayerSleepingHelper | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L108) |
| `ModifySleepingTargetInfo(i, j, info)` | Instance | `ModTile` (inherited) | `undefined` | PlayerSittingHelper/PlayerSleepingHelper | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L109) |
| `MouseOver(i, j)` | Instance | `ModTile` (inherited) | `undefined` | Player.TileInteractions; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L89) |
| `MouseOverFar(i, j)` | Instance | `ModTile` (inherited) | `undefined` | Player.TileInteractions; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L90) |
| `NearbyEffects(i, j, closer)` | Instance | `ModBannerTile` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModBannerTile.js#L39) |
| `NumDust(i, j, fail, num)` | Instance | `ModTile` (inherited) | `undefined` | WorldGen.KillTile, TileFrameImportant; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L67) |
| `PlaceInWorld(i, j, item)` | Instance | `ModTile` (inherited) | `undefined` | TileObjectData.CallPostPlacementPlayerHook and WorldGen.PlaceTile (when placing) | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L73) |
| `PostDraw(i, j, spriteBatch)` | Instance | `ModTile` (inherited) | `undefined` | a pass after TileDrawing.PostDrawTiles; tile.draw mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L82) |
| `PostSetDefaults()` | Instance | `ModTile` (inherited) | `undefined` | native, content ready | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L21) |
| `PostSetupContent()` | Instance | `ModTile` (inherited) | `undefined` | native, content ready | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L22) |
| `PreDraw(i, j)` | Instance | `ModBannerTile` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModBannerTile.js#L29) |
| `RandomUpdate(i, j)` | Instance | `ModTile` (inherited) | `undefined` | WorldGen.UpdateWorld_Tile; tile.random mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L105) |
| `register(cls)` | Static | `ModTile` (inherited) | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L116) |
| `RegisterItemDrop(itemType, ...styles)` | Instance | `ModTile` (inherited) | `See source` | The item that drops. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L55) |
| `RightClick(i, j)` | Instance | `ModTile` (inherited) | `false` | Player.TileInteractions; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L88) |
| `SetDrawPositions(i, j, width, offsetY)` | Instance | `ModBannerTile` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModBannerTile.js#L37) |
| `SetSpriteEffects(i, j, spriteEffects)` | Instance | `ModTile` (inherited) | `undefined` | the native drawing (GetTileDrawData); tile.drawdata mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L79) |
| `SetStaticDefaults()` | Instance | `ModBannerTile` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModBannerTile.js#L4) |
| `Slope(i, j)` | Instance | `ModTile` (inherited) | `true` | WorldGen.SlopeTile/PoundTile; tile.slope mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L104) |
| `SpecialDraw(i, j, spriteBatch)` | Instance | `ModTile` (inherited) | `undefined` | a pass after TileDrawing.PostDrawTiles; tile.draw mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L84) |
| `TileFrame(i, j, resetFrame, noBreak)` | Instance | `ModTile` (inherited) | `true` | WorldGen.TileFrame; tile.frame mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L106) |
| `UnlockChest(i, j, frameXAdjustment, dustType, manual)` | Instance | `ModTile` (inherited) | `false` | Chest.IsLocked/Unlock/Lock; tile.chest mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L112) |

## ModBiome

[Contract and fields](classes.md#modbiome-and-modsceneeffect). Inherits from `ModSceneEffect`.

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `GetWeight(player)` | Instance | `ModSceneEffect` (inherited) | `0.5` | 0 a 1: desempata efeitos da mesma Priority. | [Code](../../app/src/main/cpp/script/js/mod/ModSceneEffect.js#L15) |
| `IsBiomeActive(player)` | Instance | `ModBiome` | `false` | Every frame, after the game updates its zones. An error counts as false in this evaluation. | [Code](../../app/src/main/cpp/script/js/mod/ModBiome.js#L12) |
| `IsSceneEffectActive(player)` | Instance | `ModBiome` | `See source` | O resultado do IsBiomeActive deste quadro; não repete a detecção. | [Code](../../app/src/main/cpp/script/js/mod/ModBiome.js#L19) |
| `MapBackgroundColor(color)` | Instance | `ModSceneEffect` (inherited) | `undefined` | Ref (.value, a Color): the final color, after the two above. | [Code](../../app/src/main/cpp/script/js/mod/ModSceneEffect.js#L21) |
| `OnEnter(player)` | Instance | `ModBiome` | `undefined` | Once, on the change. When leaving the world inside the biome, OnLeave comes on exit. | [Code](../../app/src/main/cpp/script/js/mod/ModBiome.js#L14) |
| `OnInBiome(player)` | Instance | `ModBiome` | `undefined` | Every frame inside, including the entry frame. | [Code](../../app/src/main/cpp/script/js/mod/ModBiome.js#L15) |
| `OnLeave(player)` | Instance | `ModBiome` | `undefined` | Once, on the change. When leaving the world inside the biome, OnLeave comes on exit. | [Code](../../app/src/main/cpp/script/js/mod/ModBiome.js#L16) |
| `register(cls)` | Static | `ModSceneEffect` (inherited) | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModSceneEffect.js#L28) |
| `SetStaticDefaults()` | Instance | `ModSceneEffect` (inherited) | `undefined` | The base implementation does nothing. | [Code](../../app/src/main/cpp/script/js/mod/ModSceneEffect.js#L26) |
| `SpecialVisuals(player, isActive)` | Instance | `ModSceneEffect` (inherited) | `undefined` | Todo quadro, ativo ou não: é onde se liga e desliga um filtro de tela. | [Code](../../app/src/main/cpp/script/js/mod/ModSceneEffect.js#L24) |

## ModBossBar

[Contract and fields](classes.md#modbossbar).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `Collected(life, lifeMax, shield, shieldMax)` | Instance | `ModBossBar` | `See source` | Do BossBarLoader, a cada quadro com a barra. | [Code](../../app/src/main/cpp/script/js/mod/ModBossBar.js#L36) |
| `GetIconTexture(iconFrame)` | Instance | `ModBossBar` | `null` | The icon (Asset<Texture2D> or Texture2D); iconFrame (Ref) crops it. null: the game's "?". | [Code](../../app/src/main/cpp/script/js/mod/ModBossBar.js#L23) |
| `get Life` | Getter | `ModBossBar` | `this.#life` | The last frame's values. | [Code](../../app/src/main/cpp/script/js/mod/ModBossBar.js#L14) |
| `get LifeMax` | Getter | `ModBossBar` | `this.#lifeMax` | The last frame's values. | [Code](../../app/src/main/cpp/script/js/mod/ModBossBar.js#L15) |
| `ModifyInfo(info, life, lifeMax, shield, shieldMax)` | Instance | `ModBossBar` | `null` | null: the NPC's life; true: the values given (Ref); false: no bar (the game looks for another). info.npcIndexToAimAt changes the NPC. | [Code](../../app/src/main/cpp/script/js/mod/ModBossBar.js#L28) |
| `PostDraw(spriteBatch, npc, drawParams)` | Instance | `ModBossBar` | `undefined` | drawParams: tModLoader's BossBarDrawParams (see GlobalBossBar); false removes the game's drawing. | [Code](../../app/src/main/cpp/script/js/mod/ModBossBar.js#L33) |
| `PreDraw(spriteBatch, npc, drawParams)` | Instance | `ModBossBar` | `true` | drawParams: tModLoader's BossBarDrawParams (see GlobalBossBar); false removes the game's drawing. | [Code](../../app/src/main/cpp/script/js/mod/ModBossBar.js#L32) |
| `register(cls)` | Static | `ModBossBar` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModBossBar.js#L43) |
| `SetStaticDefaults()` | Instance | `ModBossBar` | `undefined` | On registration. | [Code](../../app/src/main/cpp/script/js/mod/ModBossBar.js#L19) |
| `get Shield` | Getter | `ModBossBar` | `this.#shield` | The last frame's values. | [Code](../../app/src/main/cpp/script/js/mod/ModBossBar.js#L16) |
| `get ShieldMax` | Getter | `ModBossBar` | `this.#shieldMax` | The last frame's values. | [Code](../../app/src/main/cpp/script/js/mod/ModBossBar.js#L17) |

## ModBuff

[Contract and fields](classes.md#modbuff).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `ApplyNPC(npc, buffTime)` | Instance | `ModBuff` | `undefined` | When it is added. | [Code](../../app/src/main/cpp/script/js/mod/ModBuff.js#L19) |
| `ApplyPlayer(player, buffTime)` | Instance | `ModBuff` | `undefined` | When it is added. | [Code](../../app/src/main/cpp/script/js/mod/ModBuff.js#L18) |
| `CanRemove(player, buffTime, buffIndex, debuff)` | Instance | `ModBuff` | `null` | On tapping the icon: true/false decides; null, the game. | [Code](../../app/src/main/cpp/script/js/mod/ModBuff.js#L24) |
| `getModBuff(type)` | Static | `ModBuff` | `BuffLoader.ByType.get(type)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModBuff.js#L71) |
| `getTypeByName(name)` | Static | `ModBuff` | `bl.buffs.typeOf(name)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModBuff.js#L70) |
| `isModType(type)` | Static | `ModBuff` | `bl.buffs.isModBuff(type)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModBuff.js#L69) |
| `ModifyBuffText(buffName, tip, rare)` | Instance | `ModBuff` | `undefined` | The buff's name and tip in the bar (three Refs; rare does not change the color on mobile). | [Code](../../app/src/main/cpp/script/js/mod/ModBuff.js#L28) |
| `ModifyDescription()` | Instance | `ModBuff` | `undefined` | Once per language: change this.DisplayName/this.Description. | [Code](../../app/src/main/cpp/script/js/mod/ModBuff.js#L14) |
| `ModifyDisplayName()` | Instance | `ModBuff` | `undefined` | Once per language: change this.DisplayName/this.Description. | [Code](../../app/src/main/cpp/script/js/mod/ModBuff.js#L13) |
| `OnRemove(player, buffTime, buffIndex)` | Instance | `ModBuff` | `undefined` | After being removed by the tap. | [Code](../../app/src/main/cpp/script/js/mod/ModBuff.js#L25) |
| `PostDraw(spriteBatch, buffIndex, drawParams)` | Instance | `ModBuff` | `undefined` | The icon in the buff bar. drawParams: see GlobalBuff; false hides the icon. | [Code](../../app/src/main/cpp/script/js/mod/ModBuff.js#L31) |
| `PostSetupContent()` | Instance | `ModBuff` | `undefined` | The base implementation does nothing. | [Code](../../app/src/main/cpp/script/js/mod/ModBuff.js#L12) |
| `PostStaticDefaults()` | Instance | `ModBuff` | `undefined` | The base implementation does nothing. | [Code](../../app/src/main/cpp/script/js/mod/ModBuff.js#L11) |
| `PreDraw(spriteBatch, buffIndex, drawParams)` | Instance | `ModBuff` | `true` | The icon in the buff bar. drawParams: see GlobalBuff; false hides the icon. | [Code](../../app/src/main/cpp/script/js/mod/ModBuff.js#L30) |
| `ReApplyNPC(npc, buffTime, buffIndex)` | Instance | `ModBuff` | `true` | When it is added again; false prevents renewing the time. | [Code](../../app/src/main/cpp/script/js/mod/ModBuff.js#L22) |
| `ReApplyPlayer(player, buffTime, buffIndex)` | Instance | `ModBuff` | `true` | When it is added again; false prevents renewing the time. | [Code](../../app/src/main/cpp/script/js/mod/ModBuff.js#L21) |
| `register(cls)` | Static | `ModBuff` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModBuff.js#L33) |
| `SetStaticDefaults()` | Instance | `ModBuff` | `undefined` | Once: Main.debuff[this.Type], buffNoSave, BuffID.Sets.... | [Code](../../app/src/main/cpp/script/js/mod/ModBuff.js#L10) |
| `UpdateNPC(npc, buffIndex)` | Instance | `ModBuff` | `undefined` | Every frame, active on the NPC. | [Code](../../app/src/main/cpp/script/js/mod/ModBuff.js#L17) |
| `UpdatePlayer(player, buffIndex)` | Instance | `ModBuff` | `undefined` | Every frame, active on the player. | [Code](../../app/src/main/cpp/script/js/mod/ModBuff.js#L16) |

## ModCloud

[Contract and fields](classes.md#modcloud).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `OnSpawn(cloud)` | Instance | `ModCloud` | `undefined` | The cloud spawned (the game's Cloud: spriteDir, scale...). | [Code](../../app/src/main/cpp/script/js/mod/ModCloud.js#L16) |
| `get RareCloud` | Getter | `ModCloud` | `false` | true: rolled among the rare ones (getter). | [Code](../../app/src/main/cpp/script/js/mod/ModCloud.js#L11) |
| `register(cls)` | Static | `ModCloud` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModCloud.js#L18) |
| `SetStaticDefaults()` | Instance | `ModCloud` | `undefined` | The base implementation does nothing. | [Code](../../app/src/main/cpp/script/js/mod/ModCloud.js#L13) |
| `SpawnChance()` | Instance | `ModCloud` | `1` | The weight: common ones against the game's 22 (weight 1 each), rare ones against the game's 18 rare ones. The common ones' is reread every 60 attempts at a new cloud (about one second: the game tries one per frame), not on all of them; the rare ones', on every roll. | [Code](../../app/src/main/cpp/script/js/mod/ModCloud.js#L14) |

## ModCommand

[Contract and fields](classes.md#modcommand).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `Action(caller, input, args)` | Instance | `ModCommand` | `undefined` | The command. caller.Player, caller.CommandType, caller.Reply(text, color). throw new UsageException(text, color) replies with the error; return false (from ExMod) replies with the Usage. | [Code](../../app/src/main/cpp/script/js/mod/ModCommand.js#L17) |
| `get Aliases` | Getter | `ModCommand` | `[]` | Other names (from ExMod): get Aliases() { return ['heal']; }. | [Code](../../app/src/main/cpp/script/js/mod/ModCommand.js#L8) |
| `get Command` | Getter | `ModCommand` | `''` | The name, without the slash (getter). | [Code](../../app/src/main/cpp/script/js/mod/ModCommand.js#L6) |
| `get Description` | Getter | `ModCommand` | `ModCommand.#Text(this, 'Description')` | Default: Commands.<Class>.Usage/.Description from Localization (/help shows them). | [Code](../../app/src/main/cpp/script/js/mod/ModCommand.js#L10) |
| `get IsCaseSensitive` | Getter | `ModCommand` | `false` | false: the text arrives in lowercase. | [Code](../../app/src/main/cpp/script/js/mod/ModCommand.js#L11) |
| `register(cls)` | Static | `ModCommand` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModCommand.js#L29) |
| `get ResponseColor` | Static getter | `ModCommand` | `Color.new(255, 240, 20)` | A cor das respostas do jogo (a do /help). | [Code](../../app/src/main/cpp/script/js/mod/ModCommand.js#L20) |
| `SetStaticDefaults()` | Instance | `ModCommand` | `undefined` | The base implementation does nothing. | [Code](../../app/src/main/cpp/script/js/mod/ModCommand.js#L13) |
| `get Type` | Getter | `ModCommand` | `CommandType.Chat` | CommandType.Chat (on the device of whoever typed it), Server (on the server, requested by a player), World (in single player, in the chat; in multiplayer, on the server). Console does not exist on mobile. | [Code](../../app/src/main/cpp/script/js/mod/ModCommand.js#L7) |
| `get Usage` | Getter | `ModCommand` | `ModCommand.#Text(this, 'Usage') \|\| '/' + this.Command` | Default: Commands.<Class>.Usage/.Description from Localization (/help shows them). | [Code](../../app/src/main/cpp/script/js/mod/ModCommand.js#L9) |

## ModConfig

[Contract and fields](classes.md#modconfig).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `Button(action, extra = {})` | Static | `ModConfig` | `{ ...extra, type: 'button', action }` | Color, action and address descriptors. | [Code](../../app/src/main/cpp/script/js/mod/ModConfig.js#L46) |
| `Color(defaultValue = '#FFFFFF', extra = {})` | Static | `ModConfig` | `{ ...extra, type: 'color', default: String(defaultValue).toUpperCase() }` | Color, action and address descriptors. | [Code](../../app/src/main/cpp/script/js/mod/ModConfig.js#L42) |
| `Cycle(defaultValue, choices, extra = {})` | Static | `ModConfig` | `{ ...extra, type: 'cycle', default: defaultValue, choices: [...choices] }` | Selection descriptors. | [Code](../../app/src/main/cpp/script/js/mod/ModConfig.js#L38) |
| `Dropdown(defaultValue, choices, extra = {})` | Static | `ModConfig` | `{ ...extra, type: 'dropdown', default: defaultValue, choices: [...choices] }` | Selection descriptors. | [Code](../../app/src/main/cpp/script/js/mod/ModConfig.js#L34) |
| `Header(extra = {})` | Static | `ModConfig` | `{ ...extra, type: 'header' }` | Title, toggle and slider descriptors. | [Code](../../app/src/main/cpp/script/js/mod/ModConfig.js#L18) |
| `Link(url, extra = {})` | Static | `ModConfig` | `{ ...extra, type: 'link', url: String(url) }` | Color, action and address descriptors. | [Code](../../app/src/main/cpp/script/js/mod/ModConfig.js#L50) |
| `OnApply()` | Instance | `ModConfig` | `undefined` | Notification that the options were applied. | [Code](../../app/src/main/cpp/script/js/mod/ModConfig.js#L8) |
| `OnChanged(key)` | Instance | `ModConfig` | `undefined` | When an option changes in the applied configuration. | [Code](../../app/src/main/cpp/script/js/mod/ModConfig.js#L7) |
| `OnLoaded()` | Instance | `ModConfig` | `undefined` | After the configuration loads. | [Code](../../app/src/main/cpp/script/js/mod/ModConfig.js#L6) |
| `Radio(defaultValue, choices, extra = {})` | Static | `ModConfig` | `{ ...extra, type: 'radio', default: defaultValue, choices: [...choices] }` | Selection descriptors. | [Code](../../app/src/main/cpp/script/js/mod/ModConfig.js#L30) |
| `Range(defaultValue, { min = 0, max = 100, step = 1, suffix = '', ...extra } = {})` | Static | `ModConfig` | `{ ...extra, type: 'range', default: defaultValue, min, max, step, suffix }` | Title, toggle and slider descriptors. | [Code](../../app/src/main/cpp/script/js/mod/ModConfig.js#L26) |
| `register(cls)` | Static | `ModConfig` | `See source` | Registers a configuration class by hand. | [Code](../../app/src/main/cpp/script/js/mod/ModConfig.js#L54) |
| `ResetToDefaults()` | Instance | `ModConfig` | `See source` | Asks the loader to restore the default values. | [Code](../../app/src/main/cpp/script/js/mod/ModConfig.js#L10) |
| `SetOption(key, value)` | Instance | `ModConfig` | `See source` | Changes an option through the configuration API. | [Code](../../app/src/main/cpp/script/js/mod/ModConfig.js#L14) |
| `Toggle(defaultValue = false, extra = {})` | Static | `ModConfig` | `{ ...extra, type: 'toggle', default: !!defaultValue }` | Title, toggle and slider descriptors. | [Code](../../app/src/main/cpp/script/js/mod/ModConfig.js#L22) |

## ModContent

[Contract and fields](classes.md#modcontent).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `BuffType(which)` | Static | `ModContent` | `ContentLookup.TypeOf(BuffLoader.ByType, which)` | The type. x is the class, the name ('ExampleBobber') or 'othermod/Name'. Not found: 0. | [Code](../../app/src/main/cpp/script/js/mod/ModContent.js#L5) |
| `Find(base, name)` | Static | `ModContent` | `See source` | The template by name; throws if missing. | [Code](../../app/src/main/cpp/script/js/mod/ModContent.js#L16) |
| `GetContent(base)` | Static | `ModContent` | `Templates.All(base)` | The templates from every mod that extend Base (ModItem, ModNPC, ModBuff, ModSystem...), in registration order. | [Code](../../app/src/main/cpp/script/js/mod/ModContent.js#L13) |
| `GetInstance(cls)` | Static | `ModContent` | `Templates.Get(cls)` | The template (the register instance). | [Code](../../app/src/main/cpp/script/js/mod/ModContent.js#L12) |
| `GetModBossHeadSlot(texture)` | Static | `ModContent` | `See source` | The slot of a registered boss icon (a boss's _Head_Boss or an AddBossHeadTexture), or -1. | [Code](../../app/src/main/cpp/script/js/mod/ModContent.js#L45) |
| `GetModBuff(type)` | Static | `ModContent` | `BuffLoader.ByType.get(type)` | The template by type. | [Code](../../app/src/main/cpp/script/js/mod/ModContent.js#L32) |
| `GetModItem(type)` | Static | `ModContent` | `ItemLoader.ByType.get(type)` | The template by type. | [Code](../../app/src/main/cpp/script/js/mod/ModContent.js#L29) |
| `GetModMount(type)` | Static | `ModContent` | `MountLoader.ByType.get(type)` | The template by type. | [Code](../../app/src/main/cpp/script/js/mod/ModContent.js#L36) |
| `GetModNPC(type)` | Static | `ModContent` | `NPCLoader.ByType.get(type)` | The template by type. | [Code](../../app/src/main/cpp/script/js/mod/ModContent.js#L31) |
| `GetModPrefix(type)` | Static | `ModContent` | `PrefixLoader.ByType.get(type)` | The template by type. | [Code](../../app/src/main/cpp/script/js/mod/ModContent.js#L35) |
| `GetModProjectile(type)` | Static | `ModContent` | `ProjectileLoader.ByType.get(type)` | The template by type. | [Code](../../app/src/main/cpp/script/js/mod/ModContent.js#L30) |
| `GetModRarity(type)` | Static | `ModContent` | `RarityLoader.GetRarity(type)` | The template by type. | [Code](../../app/src/main/cpp/script/js/mod/ModContent.js#L37) |
| `GetModTile(type)` | Static | `ModContent` | `TileLoader.ByType.get(type)` | The template by type. | [Code](../../app/src/main/cpp/script/js/mod/ModContent.js#L33) |
| `GetModWall(type)` | Static | `ModContent` | `WallLoader.ByType.get(type)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModContent.js#L34) |
| `HasAsset(path)` | Static | `ModContent` | `ContentLookup.FindTexture(path) !== null` | Does the texture exist? | [Code](../../app/src/main/cpp/script/js/mod/ModContent.js#L43) |
| `ItemType(which)` | Static | `ModContent` | `ContentLookup.TypeOf(ItemLoader.ByType, which)` | The type. x is the class, the name ('ExampleBobber') or 'othermod/Name'. Not found: 0. | [Code](../../app/src/main/cpp/script/js/mod/ModContent.js#L2) |
| `MountType(which)` | Static | `ModContent` | `ContentLookup.TypeOf(MountLoader.ByType, which)` | The type. x is the class, the name ('ExampleBobber') or 'othermod/Name'. Not found: 0. | [Code](../../app/src/main/cpp/script/js/mod/ModContent.js#L9) |
| `NPCType(which)` | Static | `ModContent` | `ContentLookup.TypeOf(NPCLoader.ByType, which)` | The type. x is the class, the name ('ExampleBobber') or 'othermod/Name'. Not found: 0. | [Code](../../app/src/main/cpp/script/js/mod/ModContent.js#L4) |
| `PrefixType(which)` | Static | `ModContent` | `ContentLookup.TypeOf(PrefixLoader.ByType, which)` | The type. x is the class, the name ('ExampleBobber') or 'othermod/Name'. Not found: 0. | [Code](../../app/src/main/cpp/script/js/mod/ModContent.js#L8) |
| `ProjectileType(which)` | Static | `ModContent` | `ContentLookup.TypeOf(ProjectileLoader.ByType, which)` | The type. x is the class, the name ('ExampleBobber') or 'othermod/Name'. Not found: 0. | [Code](../../app/src/main/cpp/script/js/mod/ModContent.js#L3) |
| `RarityType(which)` | Static | `ModContent` | `ContentLookup.TypeOf(RarityLoader.ByType, which)` | The type. x is the class, the name ('ExampleBobber') or 'othermod/Name'. Not found: 0. | [Code](../../app/src/main/cpp/script/js/mod/ModContent.js#L10) |
| `Request(path)` | Static | `ModContent` | `ContentLookup.Request(path)` | The game's Asset<Texture2D>, loaded once (.Value is the texture). Game thread. | [Code](../../app/src/main/cpp/script/js/mod/ModContent.js#L41) |
| `SoundStyle(path, options)` | Static | `ModContent` | `new SoundStyle(path, options)` | The same as new SoundStyle(...). | [Code](../../app/src/main/cpp/script/js/mod/ModContent.js#L49) |
| `Texture(path)` | Static | `ModContent` | `ContentLookup.Request(path).Value` | The Texture2D (Request's .Value). | [Code](../../app/src/main/cpp/script/js/mod/ModContent.js#L42) |
| `TileType(which)` | Static | `ModContent` | `ContentLookup.TypeOf(TileLoader.ByType, which)` | The type. x is the class, the name ('ExampleBobber') or 'othermod/Name'. Not found: 0. | [Code](../../app/src/main/cpp/script/js/mod/ModContent.js#L6) |
| `TryFind(base, name, result)` | Static | `ModContent` | `See source` | The same, in ref.value; returns true/false. | [Code](../../app/src/main/cpp/script/js/mod/ModContent.js#L23) |
| `WallType(which)` | Static | `ModContent` | `ContentLookup.TypeOf(WallLoader.ByType, which)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModContent.js#L7) |

## ModEmoteBubble

[Contract and fields](classes.md#modemotebubble).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `AddToCategory(categoryId)` | Instance | `ModEmoteBubble` | `See source` | In the emote menu (General, RockPaperScissors, Items, BiomesAndEvents, Town, CrittersAndMonsters, Dangers). In SetStaticDefaults. | [Code](../../app/src/main/cpp/script/js/mod/ModEmoteBubble.js#L12) |
| `GetFrame()` | Instance | `ModEmoteBubble` | `null` | The frame in the texture; null, the game's (half the texture). | [Code](../../app/src/main/cpp/script/js/mod/ModEmoteBubble.js#L20) |
| `GetFrameInEmoteMenu(frame, frameCounter)` | Instance | `ModEmoteBubble` | `null` | The frame in the menu (the menu animation has two). | [Code](../../app/src/main/cpp/script/js/mod/ModEmoteBubble.js#L23) |
| `IsUnlocked()` | Instance | `ModEmoteBubble` | `true` | In the menu only if unlocked. | [Code](../../app/src/main/cpp/script/js/mod/ModEmoteBubble.js#L15) |
| `OnSpawn()` | Instance | `ModEmoteBubble` | `undefined` | The bubble spawned (EmoteBubble.NewBubble). | [Code](../../app/src/main/cpp/script/js/mod/ModEmoteBubble.js#L16) |
| `PostDraw(spriteBatch, texture, position, frame, origin, spriteEffects)` | Instance | `ModEmoteBubble` | `undefined` | Around drawing the bubble; false does not draw it. | [Code](../../app/src/main/cpp/script/js/mod/ModEmoteBubble.js#L26) |
| `PreDraw(spriteBatch, texture, position, frame, origin, spriteEffects)` | Instance | `ModEmoteBubble` | `true` | Around drawing the bubble; false does not draw it. | [Code](../../app/src/main/cpp/script/js/mod/ModEmoteBubble.js#L25) |
| `register(cls)` | Static | `ModEmoteBubble` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModEmoteBubble.js#L28) |
| `SetStaticDefaults()` | Instance | `ModEmoteBubble` | `undefined` | The base implementation does nothing. | [Code](../../app/src/main/cpp/script/js/mod/ModEmoteBubble.js#L11) |
| `UpdateFrame()` | Instance | `ModEmoteBubble` | `true` | false: the animation belongs to the mod (this.EmoteBubble.frame, frameCounter). | [Code](../../app/src/main/cpp/script/js/mod/ModEmoteBubble.js#L18) |

## ModGore

[Contract and fields](classes.md#npc-helpers).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `getTypeByName(name)` | Static | `ModGore` | `bl.mod ? GoreLoader.TypeOf(bl.mod.uuid, name) : GoreLoader.TypeOfAny(name)` | O tipo do gore '<pasta>/Gores/<nome>.png' deste mod, ou 0. Fora da carga (num hook do jogo, sem mod na pilha), o primeiro mod que o tiver. | [Code](../../app/src/main/cpp/script/js/mod/ModGore.js#L4) |

## ModHair

[Contract and fields](classes.md#modhair).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `get AltTexture` | Getter | `ModHair` | `this.Texture + '_Alt'` | The hairstyle's and the one with a hat (<Texture>_Alt; without it, the same). Default: next to the file (Content/Hairs/X.js → Content/Hairs/X.png). | [Code](../../app/src/main/cpp/script/js/mod/ModHair.js#L10) |
| `get AvailableDuringCharacterCreation` | Getter | `ModHair` | `true` | In character creation (getter; default true). | [Code](../../app/src/main/cpp/script/js/mod/ModHair.js#L11) |
| `GetUnlockConditions()` | Instance | `ModHair` | `[]` | At the Stylist when all of them hold: objects with IsMet() or functions. | [Code](../../app/src/main/cpp/script/js/mod/ModHair.js#L14) |
| `IsUnlocked(isAtCharacterCreation, isAtStylist)` | Instance | `ModHair` | `See source` | Decides everything on its own (from ExMod), if written. | [Code](../../app/src/main/cpp/script/js/mod/ModHair.js#L16) |
| `register(cls)` | Static | `ModHair` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModHair.js#L26) |
| `SetStaticDefaults()` | Instance | `ModHair` | `undefined` | The base implementation does nothing. | [Code](../../app/src/main/cpp/script/js/mod/ModHair.js#L13) |

## ModItem

[Contract and fields](classes.md#moditem).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `AddArmorSets()` | Instance | `ModItem` | `undefined` | O conjunto do 1.4.5 (tooltip "Bônus definido" e efeito pelo jogo): no AddArmorSets, CreateArmorSet(cabeça, corpo, pernas, texto) com os tipos; 0 é "qualquer". O texto é uma chave de tradução ou o próprio texto. O efeito é o UpdateArmorSet de cada peça de mod vestida. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L112) |
| `AddRecipeGroups()` | Instance | `ModItem` | `undefined` | Once, before any recipe from any mod. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L44) |
| `AddRecipes()` | Instance | `ModItem` | `undefined` | Once, with the game's recipes ready. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L45) |
| `AllowPrefix(item, pre)` | Instance | `ModItem` | `true` | false removes that prefix from the options. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L174) |
| `AnglerQuestChat(description, catchLocation)` | Instance | `ModItem` | `undefined` | The Angler's line (Ref): description.value and catchLocation.value (shown in parentheses below). Without it, the item's name. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L64) |
| `ApplyPotionDelay(item, player, potionDelay)` | Instance | `ModItem` | `true` | false: prevents the counter and the potion sickness buff; keeps healing and consumption. false prevents the counter and PotionSickness, keeping the previous counter. Combines with ModPlayer; any veto wins. Healing and consumption go on. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L86) |
| `ApplyPrefix(item, pre)` | Instance | `ModItem` | `undefined` | After the prefix's stats. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L176) |
| `ArmorSetShadows(player)` | Instance | `ModItem` | `undefined` | The set's shadows (armorEffectDraw). | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L134) |
| `buyPrice(platinum = 0, gold = 0, silver = 0, copper = 0)` | Static | `ModItem` | `Terraria.Item.buyPrice(platinum, gold, silver, copper)` | Price in copper. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L323) |
| `CanAccessoryBeEquippedWith(equippedItem, incomingItem, player)` | Instance | `ModItem` | `true` | false recusa o par (arrastar para o slot e a troca pelo toque, que vai para o slot do que recusou). Chamado nos dois itens do par. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L105) |
| `CanBeChosenAsAmmo(item, weapon, player)` | Instance | `ModItem` | `null` | Ammo: combines into the selection; any veto wins. On the ammo. Combines with the weapon; any false wins. Includes vanilla weapons and native slot switching. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L77) |
| `CanBeConsumedAsAmmo(item, weapon, player)` | Instance | `ModItem` | `true` | Ammo: false keeps the stack. On the ammo. false keeps the stack and suppresses consumption notifications. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L79) |
| `CanChooseAmmo(item, ammo, player)` | Instance | `ModItem` | `null` | Weapon: false rejects; true allows another category; null keeps the native category. On the weapon. false rejects; true allows another category; null/undefined follow ammo == useAmmo. The ammo's veto wins. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L76) |
| `CanConsumeAmmo(item, ammo, player)` | Instance | `ModItem` | `true` | Weapon: false keeps the stack without preventing the shot. On the weapon. false keeps the stack without preventing the shot. Combines with the ammo and ModPlayer. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L78) |
| `CanHitNPC(item, player, target)` | Instance | `ModItem` | `null` | false vetoes; true allows eligibility; null keeps the game's. undefined also keeps the game's decision. Collision is still checked. ModPlayer and ModNPC vetoes win. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L91) |
| `CanHitPvp(item, player, target)` | Instance | `ModItem` | `true` | On the attacker; false prevents the hit before status and sending. Runs on the attacker during ItemCheck_MeleeHitPVP, before status and sending damage. Validated across two processes. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L93) |
| `CanMeleeAttackCollideWithNPC(item, player, hitbox, target)` | Instance | `ModItem` | `null` | false vetoes; true allows the collision; null keeps the game's. undefined also keeps the game's decision. Does not force Intersects of rectangles unrelated to the attack. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L96) |
| `CanPickup(worldItem, player)` | Instance | `ModItem` | `true` | false: the player neither picks up nor pulls the item from the ground. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L195) |
| `CanRightClick(item)` | Instance | `ModItem` | `false` | true: the item opens in the inventory (bag, crate); goes into ItemID.Sets.OpenableBag. On touch, it gets the "Open" button and the use buttons are locked, as with the game's bags. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L54) |
| `CanShoot(item, player)` | Instance | `ModItem` | `true` | false: uses, but does not shoot. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L72) |
| `CanUseItem(item, player)` | Instance | `ModItem` | `true` | Before use; false prevents it. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L48) |
| `ChoosePrefix(item, rand)` | Instance | `ModItem` | `-1` | A prefix forced when rolling (> 0), or -1. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L171) |
| `Clone(newItem)` | Instance | `ModItem` | `Entities.Clone(this)` | The game copied the item (Item.Clone); return the copy's instance. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L15) |
| `CloneDefaults(type)` | Instance | `ModItem` | `See source` | Copies a vanilla item's values. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L203) |
| `get CloneNewInstances` | Getter | `ModItem` | `false` | Uma instância para uma entidade nova (o NewInstance do tModLoader): pelo construtor; com CloneNewInstances, uma cópia do molde (Clone). | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L18) |
| `ConsumeItem(item, player)` | Instance | `ModItem` | `true` | false: opening does not spend the item. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L57) |
| `CreateArmorSet(head, body, legs, text = 'ArmorSetBonus.Empty', primaryPart = 0)` | Instance | `ModItem` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L113) |
| `CreateArmorSets(heads = [0], bodies = [0], legs = [0], text = 'ArmorSetBonus.Empty', primaryPart = 0)` | Instance | `ModItem` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L116) |
| `CreateRecipe(stack = 1)` | Instance | `ModItem` | `new ModRecipe().SetResult(this.Type, stack)` | A ModRecipe that gives this item. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L309) |
| `CreateRecipeGroup(itemTypes = [])` | Instance | `ModItem` | `See source` | A group named after the first item. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L314) |
| `DefaultToFood(buffType, buffTime, useGulpSound = false, animationTime = 17)` | Instance | `ModItem` | `See source` | Food. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L272) |
| `DefaultToGolfBall(projType)` | Instance | `ModItem` | `See source` | Golf ball. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L305) |
| `DefaultToMusicBox(tileType, styleToPlace = 0)` | Instance | `ModItem` | `See source` | A caixa de música do jogo, colocando o tile de mod (MusicLoader.AddMusicBox). | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L266) |
| `DefaultToPlaceableTile(typeToPlace, styleToPlace = 0)` | Instance | `ModItem` | `See source` | An item that places a block. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L241) |
| `DefaultToPlaceableWall(wallToPlace)` | Instance | `ModItem` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L254) |
| `DefaultToSpear(projType, pushForwardSpeed, animationTime)` | Instance | `ModItem` | `See source` | Spear. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L300) |
| `DefaultToTorch(tileType, styleToPlace = 0, allowWaterPlacement = false)` | Instance | `ModItem` | `See source` | A tocha do jogo (segurar, luz, colocar na parede), colocando o tile de mod. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L260) |
| `DefaultToWhip(projType, damage, knockBack, shootSpeed, animationTime = 30)` | Instance | `ModItem` | `See source` | Whip. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L295) |
| `EquipFrameEffects(player, type)` | Instance | `ModItem` | `undefined` | Every frame, with the item's texture worn. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L139) |
| `GetAlpha(item, lightColor)` | Instance | `ModItem` | `undefined` | Return the drawing Color. On the ground item is the WorldItem (with Center); in the inventory, the Item, without a position. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L178) |
| `getByName(name)` | Static | `ModItem` | `ItemLoader.ByType.get(bl.items.typeOf(name))` | The template. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L416) |
| `GetHealLife(item, player, quickHeal, healValue)` | Instance | `ModItem` | `undefined` | Life restored (Ref), with a positive base heal; also in QuickHeal's selection. Life Ref, only with a positive base healLife. ModItem before ModPlayer. QuickHeal uses the value in the selection; temporary fields restored. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L83) |
| `GetHealMana(item, player, quickHeal, healValue)` | Instance | `ModItem` | `undefined` | Mana restored (Ref), with a positive base heal. Mana Ref, only with a positive base healMana. quickHeal true in QuickHeal and QuickMana; ModItem before ModPlayer. Temporary fields restored. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L84) |
| `getModItem(type)` | Static | `ModItem` | `ItemLoader.ByType.get(type)` | The template. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L415) |
| `getTypeByName(name)` | Static | `ModItem` | `bl.items.typeOf(name)` | The type of an item from this mod; -1 if there is none. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L414) |
| `GrabRange(worldItem, player, grabRange)` | Instance | `ModItem` | `undefined` | The pickup range in pixels (Ref, over the game's). | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L198) |
| `GrabStyle(worldItem, player)` | Instance | `ModItem` | `false` | true: the mod moves the item to the player (change velocity) and the game does not pull it. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L199) |
| `HoldItem(item, player)` | Instance | `ModItem` | `undefined` | Every frame with the item in hand. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L65) |
| `HoldItemFrame(item, player)` | Instance | `ModItem` | `undefined` | After the native frame, idle and with the item visually allowed. After the complete PlayerFrame, with itemAnimation <= 0 and CanVisuallyHoldItem allowed. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L68) |
| `HoldoutOffset(item, player)` | Instance | `ModItem` | `undefined` | Return { X, Y }: shifts the weapon in hand. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L71) |
| `HoldStyle(item, player, mountOffset, heldItemFrame)` | Instance | `ModItem` | `undefined` | Every frame while held, after the game's style. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L70) |
| `HorizontalWingSpeeds(item, player, speed, acceleration)` | Instance | `ModItem` | `undefined` | Running in the air with these wings (two Refs). | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L143) |
| `IsAnglerQuestAvailable()` | Instance | `ModItem` | `true` | false: the daily roll does not land on it (it rolls again). | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L63) |
| `IsArmorSet(head, body, legs)` | Instance | `ModItem` | `false` | For each worn mod piece: true = complete set. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L122) |
| `isModItem(item)` | Static | `ModItem` | `!!item && bl.items.isModItem(item.type)` | Is it mod content? | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L413) |
| `isModType(type)` | Static | `ModItem` | `bl.items.isModItem(type)` | Is it mod content? | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L412) |
| `IsQuestFish()` | Instance | `ModItem` | `false` | true: an Angler quest fish (the same as ItemID.Sets.IsQuestFish[this.Type] = true in SetStaticDefaults). Goes at the end of Main.anglerQuestItemNetIDs. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L62) |
| `IsVanitySet(head, body, legs)` | Instance | `ModItem` | `See source` | The drawn SLOTS; true = vanity set (default: the IsArmorSet of those slots' items). | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L127) |
| `get Item` | Getter | `ModItem` | `Entities.Of(this)` | game Item | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L13) |
| `ItemSpace(item, player)` | Instance | `ModItem` | `false` | true: picks it up even with a full inventory (together with OnPickup, which consumes it). | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L197) |
| `Load()` | Instance | `ModItem` | `undefined` | On registration (AddEquipTexture goes here). | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L30) |
| `LoadData(tag)` | Instance | `ModItem` | `undefined` | Read the instance's data; old tags may be empty. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L23) |
| `MagicPrefix(item = this.Item)` | Instance | `ModItem` | `!!item.magic` | The item's prefix categories (default: melee without noUseGraphic, melee with it, ranged, magic, summon). Only applies to items with damage that are neither ammo nor consumables. Read once per type. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L168) |
| `MeleeEffects(item, player, hitbox)` | Instance | `ModItem` | `undefined` | After the native use visuals. Before ModPlayer. A per-type mark avoids JavaScript dispatch for items without an override, except with a global observer. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L97) |
| `MeleePrefix(item = this.Item)` | Instance | `ModItem` | `!!item.melee && !item.noUseGraphic` | The item's prefix categories (default: melee without noUseGraphic, melee with it, ranged, magic, summon). Only applies to items with damage that are neither ammo nor consumables. Read once per type. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L165) |
| `ModifyFishingLine(item, bobber, lineOriginOffset, lineColor)` | Instance | `ModItem` | `undefined` | Rod in hand, for each bobber: where the line comes from and its color (two Refs). | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L201) |
| `ModifyHitNPC(item, player, target, modifiers)` | Instance | `ModItem` | `undefined` | modifiers is a HitModifiers (Hits). JS contract: damage, knockBack, hitDirection, crit, SourceDamage and Knockback (StatModifier), SetCrit() and DisableCrit(). Melee; not reapplied on fromNet. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L92) |
| `ModifyHitPvp(item, player, target, modifiers)` | Instance | `ModItem` | `undefined` | On the victim; modifiers is a HurtModifiers (Hits). JS contract: damage, hitDirection, quiet, crit, dodgeable. Runs on the victim, with the weapon's origin and NetSend/NetReceive carried over the network. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L94) |
| `ModifyItemLoot(itemLoot)` | Instance | `ModItem` | `undefined` | Once, on the first opening: what comes out of the item (itemLoot.Add(rule)). The game's rules and ItemDropRule.CoinsBasedOnNPCValue(npc) / ItemDropRule.Coins(value); what would drop at the NPC goes to the player (QuickSpawnItem). | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L56) |
| `ModifyItemScale(item, player, scale)` | Instance | `ModItem` | `undefined` | scale is a Ref<number> of the multiplier, including the native glove. Acts on the queried scale and on the hitbox. Item.scale is restored after the native calculation. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L90) |
| `ModifyPotionDelay(item, player, baseDelay)` | Instance | `ModItem` | `undefined` | Delay computed by the mobile game (Ref), before the veto and the application. Ref of the delay already computed by the mobile game, including native reductions; there is no mobile Player.PotionDelayModifier. Before the veto and the application. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L85) |
| `ModifyShootStats(item, player, stats)` | Instance | `ModItem` | `undefined` | Before each projectile: stats.position, velocity, type, damage, knockBack. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L73) |
| `ModifyTooltipLines()` | Instance | `ModItem` | `undefined` | Once per language, with this.TooltipLines filled in. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L36) |
| `ModifyTooltips(item, tooltips)` | Instance | `ModItem` | `undefined` | Every time the tooltip shows up, and in the crafting guide popup (without color). | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L37) |
| `ModifyWeaponCrit(item, player, crit)` | Instance | `ModItem` | `undefined` | crit is a Ref<number>. ModItem before ModPlayer. Integer result; a non-finite value becomes zero. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L88) |
| `ModifyWeaponDamage(item, player, damage)` | Instance | `ModItem` | `undefined` | StatModifier; also accepts a numeric return. Shared order: ModItem, GlobalItem, ModPlayer. Finite, integer, non-negative result. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L87) |
| `ModifyWeaponKnockback(item, player, knockback)` | Instance | `ModItem` | `undefined` | knockback is a StatModifier. ModItem before ModPlayer. Finite non-negative result. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L89) |
| `NeedsAmmo(item, player)` | Instance | `ModItem` | `true` | false: can shoot with the temporary default ammo when there is no candidate. false uses a temporary default ammo when there is no candidate; it is not consumed and does not enter the inventory. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L75) |
| `NetReceive(reader)` | Instance | `ModItem` | `undefined` | Read in the same order NetSend wrote. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L27) |
| `NetSend(writer)` | Instance | `ModItem` | `undefined` | Write data with NetWriter. Takes precedence over the automatic SaveData sending. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L25) |
| `NewInstance(entity)` | Instance | `ModItem` | `this.CloneNewInstances ? this.Clone(entity) : Entities.NewInstance(this)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L19) |
| `OnConsumeAmmo(item, ammo, player)` | Instance | `ModItem` | `undefined` | Weapon: actual consumption, with the stack already reduced and before clearing the type. On the weapon, after an actual decrement in the mobile game. Type and instance kept on the last unit, before TurnToAir. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L80) |
| `OnConsumedAsAmmo(item, weapon, player)` | Instance | `ModItem` | `undefined` | Ammo: actual consumption, including the last unit. On the ammo, after an actual decrement. Before clearing the last unit and before ModPlayer.OnConsumeAmmo. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L81) |
| `OnCraft(item, player, recipe)` | Instance | `ModItem` | `undefined` | When crafting the item from the crafting menu. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L46) |
| `OnHitNPC(item, player, target, hit, damageDone)` | Instance | `ModItem` | `undefined` | After the melee hit; hit is a HitInfo. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L100) |
| `OnHitPvp(item, player, target, hurtInfo)` | Instance | `ModItem` | `undefined` | On the victim, only after positive damage, with the HurtInfo of the applied result. HurtInfo: DamageSource, Damage, HitDirection, PvP, Quiet, Crit, CooldownCounter, Dodgeable. Runs on the victim after positive damage, including when received over the network. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L95) |
| `OnPickup(worldItem, player)` | Instance | `ModItem` | `true` | On pickup, on the picker's device; false: the item disappears without entering the inventory (heart, orb). | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L196) |
| `PickAmmo(item, weapon, player, type, speed, damage, knockback)` | Instance | `ModItem` | `undefined` | Ammo: three Ref<number>s and the StatModifier of mobile's total damage. On the ammo: item, weapon, player, type/speed/knockback (Ref) and damage (StatModifier over mobile's total damage). After the native calculation and before the notifications. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L82) |
| `PostDrawInInventory(item, spriteBatch, position, frame, drawColor, itemColor, origin, scale)` | Instance | `ModItem` | `undefined` | After drawing, even with a veto. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L183) |
| `PostDrawInWorld(item, spriteBatch, lightColor, alphaColor, rotation, scale, whoAmI)` | Instance | `ModItem` | `undefined` | After drawing, even with a veto; rotation and scale are numbers. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L181) |
| `PostDrawTooltip(item, lines)` | Instance | `ModItem` | `undefined` | After all the lines (DrawableTooltipLine). | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L41) |
| `PostDrawTooltipLine(item, line)` | Instance | `ModItem` | `undefined` | After each line (including one not drawn). | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L43) |
| `PostSetDefaults(item)` | Instance | `ModItem` | `undefined` | Right after SetDefaults. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L34) |
| `PostSetupContent()` | Instance | `ModItem` | `undefined` | Once, with all mod content in the game. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L35) |
| `PostStaticDefaults()` | Instance | `ModItem` | `undefined` | Right after SetStaticDefaults. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L33) |
| `PostUpdate(item)` | Instance | `ModItem` | `undefined` | Every frame, with the item on the ground (item is the WorldItem). Light, dust. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L184) |
| `PostUpdateInWorld(item, worldItem)` | Instance | `ModItem` | `undefined` | The base implementation does nothing. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L189) |
| `PreDrawInInventory(item, spriteBatch, position, frame, drawColor, itemColor, origin, scale)` | Instance | `ModItem` | `true` | Receives the frame and scale computed by the game; false skips the vanilla drawing. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L182) |
| `PreDrawInWorld(item, spriteBatch, lightColor, alphaColor, rotation, scale, whoAmI)` | Instance | `ModItem` | `true` | item is a WorldItem; rotation and scale are Refs. false skips the vanilla drawing. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L180) |
| `PreDrawTooltip(item, lines, x, y)` | Instance | `ModItem` | `true` | Before the lines; x, y are Refs; false does not draw the lines. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L40) |
| `PreDrawTooltipLine(item, line, yOffset)` | Instance | `ModItem` | `true` | Before each line; yOffset (Ref) adds after each following line; false does not draw the line. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L42) |
| `PrefixChance(item, pre, rand)` | Instance | `ModItem` | `null` | false prevents, true forces a prefix; null = the game's (pre: -1 crafting/chest, -2 reforge). | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L173) |
| `PreUpdateInWorld(item, worldItem)` | Instance | `ModItem` | `true` | O item no chão, todo quadro: item é o Item, worldItem a WorldItem que o leva (position, velocity, Center). false no Pre pula a atualização do jogo naquele quadro (o Post roda igual). | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L188) |
| `PreUpdateVanitySet(player)` | Instance | `ModItem` | `undefined` | Before and after FrameEffects, with the vanity set. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L132) |
| `RangedPrefix(item = this.Item)` | Instance | `ModItem` | `!!item.ranged` | The item's prefix categories (default: melee without noUseGraphic, melee with it, ranged, magic, summon). Only applies to items with damage that are neither ammo nor consumables. Read once per type. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L167) |
| `register(cls)` | Static | `ModItem` | `See source` | Registers by hand (an exported class already registers itself); returns the type. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L327) |
| `RightClick(item, player)` | Instance | `ModItem` | `undefined` | On opening, before ItemLoot. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L55) |
| `SaveData(tag)` | Instance | `ModItem` | `undefined` | Write the instance's data into the TagCompound; the return value is ignored. Also the network fallback. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L21) |
| `sellPrice(platinum = 0, gold = 0, silver = 0, copper = 0)` | Static | `ModItem` | `Terraria.Item.sellPrice(platinum, gold, silver, copper)` | Price in copper. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L319) |
| `SetDefaults(item)` | Instance | `ModItem` | `undefined` | Every item of this type that spawns. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L32) |
| `SetDefaultWeaponStyle(useTime = 30, autoReuse = false)` | Instance | `ModItem` | `See source` | useTime, useAnimation, autoReuse and the matching useStyle. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L217) |
| `SetItemAnimation(frameCount, ticksPerFrame = 5, pingPong = false)` | Instance | `ModItem` | `See source` | Animated item (vertical strip). In SetStaticDefaults. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L288) |
| `SetMatch(male, equipSlot, robes)` | Instance | `ModItem` | `undefined` | This piece's drawn slot (Ref); the robe sets robes and the legs. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L137) |
| `SetShopValues(rarity = 0, coinValue = 0)` | Instance | `ModItem` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L236) |
| `SetStaticDefaults()` | Instance | `ModItem` | `undefined` | Once, with the type already in the game's tables. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L31) |
| `SetWeaponValues(damage = 0, knockBack = 0, crit = 0)` | Instance | `ModItem` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L230) |
| `SetWingStats(flyTime = 100, flySpeedOverride = -1, accelerationMultiplier = 1, hasHoldDownHoverFeatures = false, hoverFlySpeedOverride = -1, hoverAccelerationMultiplier = 1)` | Instance | `ModItem` | `See source` | The item's wings' WingStats. In SetStaticDefaults. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L148) |
| `Shoot(item, player, position, velocity, type, damage, knockBack)` | Instance | `ModItem` | `true` | false: the game's projectile does not spawn. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L74) |
| `SummonPrefix(item = this.Item)` | Instance | `ModItem` | `!!item.summon` | The item's prefix categories (default: melee without noUseGraphic, melee with it, ranged, magic, summon). Only applies to items with damage that are neither ammo nor consumables. Read once per type. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L169) |
| `UpdateAccessory(item, player, vanity, hideVisual)` | Instance | `ModItem` | `undefined` | Every frame, accessory equipped (also in the vanity slot). | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L102) |
| `UpdateArmorSet(item, player)` | Instance | `ModItem` | `undefined` | The set bonus; the text goes in player.setBonus. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L123) |
| `UpdateEquip(item, player)` | Instance | `ModItem` | `undefined` | Every frame, equipped (armor or accessory). | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L101) |
| `UpdateInventory(item, player)` | Instance | `ModItem` | `undefined` | Every frame, in the inventory. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L161) |
| `UpdateVanity(item, player)` | Instance | `ModItem` | `undefined` | Accessory in the vanity slot. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L107) |
| `UpdateVanitySet(player)` | Instance | `ModItem` | `undefined` | Before and after FrameEffects, with the vanity set. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L133) |
| `UseAnimation(item, player)` | Instance | `ModItem` | `undefined` | Before the native calculation of the animation duration. Before the native duration calculation. Shares ApplyItemAnimation with ModPlayer's modifiers. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L66) |
| `UseItem(item, player)` | Instance | `ModItem` | `undefined` | On the frame the use starts. true: the item counts as used (use time applied, like tModLoader's ApplyItemTime), and a consumable is spent. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L50) |
| `UseItemFrame(item, player)` | Instance | `ModItem` | `undefined` | After the native frame, during the use animation. After the complete PlayerFrame, with itemAnimation > 0. Native filter by the selected item. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L67) |
| `UseItemHitbox(item, player, hitbox, noHitbox)` | Instance | `ModItem` | `undefined` | Ref<Rectangle> and Ref<boolean> after the native hitbox. Accepts replacing hitbox.value. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L98) |
| `UseStyle(item, player, mountOffset, heldItemFrame)` | Instance | `ModItem` | `undefined` | Every frame of use, after the game's style. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L69) |
| `VerticalWingSpeeds(item, player, ascentWhenFalling, ascentWhenRising, maxCanAscendMultiplier, maxAscentMultiplier, constantAscend)` | Instance | `ModItem` | `undefined` | This item's wings in the air (five Refs). | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L142) |
| `WeaponPrefix(item = this.Item)` | Instance | `ModItem` | `!!item.melee && !!item.noUseGraphic` | The item's prefix categories (default: melee without noUseGraphic, melee with it, ranged, magic, summon). Only applies to items with damage that are neither ammo nor consumables. Read once per type. | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L166) |
| `WingUpdate(player, inUse)` | Instance | `ModItem` | `false` | true: the mod animates the wings (the game's WingFrame does not run). | [Code](../../app/src/main/cpp/script/js/mod/ModItem.js#L145) |

## ModLoader

[Contract and fields](classes.md#mod-and-modloader).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `GetMod(name)` | Static | `ModLoader` | `See source` | The Mod; throws if it is not there. | [Code](../../app/src/main/cpp/script/js/mod/ModLoader.js#L14) |
| `HasMod(name)` | Static | `ModLoader` | `ModRegistry.Find(name, 'ModLoader.HasMod') !== null` | Is it installed (and did it load)? | [Code](../../app/src/main/cpp/script/js/mod/ModLoader.js#L21) |
| `get Mods` | Static getter | `ModLoader` | `ModRegistry.All()` | All of them, in load order. | [Code](../../app/src/main/cpp/script/js/mod/ModLoader.js#L25) |
| `TryGetMod(name, result)` | Static | `ModLoader` | `See source` | true and the Mod in ref.value; or false and null. | [Code](../../app/src/main/cpp/script/js/mod/ModLoader.js#L2) |

## ModLocalization

[Contract and fields](classes.md#modlocalization).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `get ActiveCultureName` | Static getter | `ModLocalization` | `See source` | 'pt-BR', 'en-US'... | [Code](../../app/src/main/cpp/script/js/mod/ModLocalization.js#L2) |
| `Exists(key)` | Static | `ModLocalization` | `LocalizationLoader.Lookup(key) !== undefined \|\| Terraria.Localization.Language['bool Exists(string key)'](key)` | Whether the mod or the game has the text. | [Code](../../app/src/main/cpp/script/js/mod/ModLocalization.js#L48) |
| `GetText(key)` | Static | `ModLocalization` | `See source` | The mod's or the game's LocalizedText. | [Code](../../app/src/main/cpp/script/js/mod/ModLocalization.js#L27) |
| `GetTextValue(key)` | Static | `ModLocalization` | `See source` | The mod's text or, without it, the game's. | [Code](../../app/src/main/cpp/script/js/mod/ModLocalization.js#L22) |
| `Key(path)` | Static | `ModLocalization` | `See source` | The key Mods.<id>.Section.Key (what the Bestiary and currencies ask for). | [Code](../../app/src/main/cpp/script/js/mod/ModLocalization.js#L40) |
| `Register(key, text)` | Static | `ModLocalization` | `See source` | A text (or { culture: text }) under any key. | [Code](../../app/src/main/cpp/script/js/mod/ModLocalization.js#L54) |
| `Translate(path)` | Static | `ModLocalization` | `See source` | The text in the game's language (as in TL); without a text, the path itself. | [Code](../../app/src/main/cpp/script/js/mod/ModLocalization.js#L11) |
| `TryTranslate(path)` | Static | `ModLocalization` | `See source` | The same, with '' when there is no text. | [Code](../../app/src/main/cpp/script/js/mod/ModLocalization.js#L16) |

## ModMenu

[Contract and fields](classes.md#modmenu).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `get IsSelected` | Getter | `ModMenu` | `MenuLoader.CurrentMenu === this` | Whether it is the theme in effect now (read-only). | [Code](../../app/src/main/cpp/script/js/mod/ModMenu.js#L16) |
| `OnDeselected()` | Instance | `ModMenu` | `undefined` | When the theme takes effect (on game launch, already in the menus, or on tapping Menu Theme) and when it stops. | [Code](../../app/src/main/cpp/script/js/mod/ModMenu.js#L21) |
| `OnSelected()` | Instance | `ModMenu` | `undefined` | When the theme takes effect (on game launch, already in the menus, or on tapping Menu Theme) and when it stops. | [Code](../../app/src/main/cpp/script/js/mod/ModMenu.js#L19) |
| `PostDrawLogo(spriteBatch, logoDrawCenter, logoRotation, logoScale, drawColor)` | Instance | `ModMenu` | `undefined` | After the logo, with the values it was drawn with. | [Code](../../app/src/main/cpp/script/js/mod/ModMenu.js#L31) |
| `PreDrawLogo(spriteBatch, logoDrawCenter, logoRotation, logoScale, drawColor)` | Instance | `ModMenu` | `true` | Before the logo. The last four are Refs (.value): the center, rotation, scale and color of the game's logo on this frame. false does not draw the logo. | [Code](../../app/src/main/cpp/script/js/mod/ModMenu.js#L28) |
| `register(cls)` | Static | `ModMenu` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModMenu.js#L35) |
| `SetStaticDefaults()` | Instance | `ModMenu` | `undefined` | The base implementation does nothing. | [Code](../../app/src/main/cpp/script/js/mod/ModMenu.js#L33) |
| `Update(isOnTitleScreen)` | Instance | `ModMenu` | `undefined` | Every frame in the menus; isOnTitleScreen on the title screen (menuMode 0). | [Code](../../app/src/main/cpp/script/js/mod/ModMenu.js#L24) |

## ModMount

[Contract and fields](classes.md#modmount).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `AddDrawData(playerDrawData, data)` | Static | `ModMount` | `MountLoader.AddDrawData(playerDrawData, data)` | tModLoader's playerDrawData.Add, inside Draw. | [Code](../../app/src/main/cpp/script/js/mod/ModMount.js#L44) |
| `AimAbility(player, mousePosition)` | Instance | `ModMount` | `undefined` | The mount's ability. | [Code](../../app/src/main/cpp/script/js/mod/ModMount.js#L22) |
| `Dismount(player, skipDust)` | Instance | `ModMount` | `undefined` | On dismounting, before the game. | [Code](../../app/src/main/cpp/script/js/mod/ModMount.js#L25) |
| `Draw(playerDrawData, drawType, drawPlayer, texture, glowTexture, drawPosition, frame, drawColor, glowColor, rotation, spriteEffects, drawOrigin, drawScale, shadow)` | Instance | `ModMount` | `true` | Once per layer with a texture (0 Back, 1 BackExtra, 2 Front, 3 FrontExtra). The Refs carry what the game would draw; changed, it draws with the new ones. false: the layer is not drawn. | [Code](../../app/src/main/cpp/script/js/mod/ModMount.js#L32) |
| `GetSpecificData(player)` | Static | `ModMount` | `MountLoader.SpecificData(player)` | The player's data on this mount (tModLoader's _mountSpecificData, which in C# is object). Cleared on mounting another one and on dismounting. | [Code](../../app/src/main/cpp/script/js/mod/ModMount.js#L37) |
| `JumpHeight(mountedPlayer, jumpHeight, xVelocity)` | Instance | `ModMount` | `undefined` | The jump, with the game's value in the Ref. | [Code](../../app/src/main/cpp/script/js/mod/ModMount.js#L14) |
| `JumpSpeed(mountedPlayer, jumpSpeed, xVelocity)` | Instance | `ModMount` | `undefined` | The jump, with the game's value in the Ref. | [Code](../../app/src/main/cpp/script/js/mod/ModMount.js#L15) |
| `NewDrawData(texture, position, sourceRect, color, rotation, origin, scale, effect)` | Static | `ModMount` | `MountLoader.NewDrawData(texture, position, sourceRect, color, rotation, origin, scale, effect)` | A DrawData. | [Code](../../app/src/main/cpp/script/js/mod/ModMount.js#L41) |
| `register(cls)` | Static | `ModMount` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModMount.js#L46) |
| `SetMount(player, skipDust)` | Instance | `ModMount` | `undefined` | On mounting, after FinalizeMountData; skipDust.value = true skips the game's dust. | [Code](../../app/src/main/cpp/script/js/mod/ModMount.js#L24) |
| `SetSpecificData(player, value)` | Static | `ModMount` | `See source` | The player's data on this mount (tModLoader's _mountSpecificData, which in C# is object). Cleared on mounting another one and on dismounting. | [Code](../../app/src/main/cpp/script/js/mod/ModMount.js#L38) |
| `SetStaticDefaults()` | Instance | `ModMount` | `undefined` | Once, with the textures already in MountData. | [Code](../../app/src/main/cpp/script/js/mod/ModMount.js#L11) |
| `UpdateEffects(player)` | Instance | `ModMount` | `undefined` | Every frame while mounted, before the game's effects. | [Code](../../app/src/main/cpp/script/js/mod/ModMount.js#L17) |
| `UpdateFrame(mountedPlayer, state, velocity)` | Instance | `ModMount` | `true` | The animation; false skips the game's. | [Code](../../app/src/main/cpp/script/js/mod/ModMount.js#L20) |
| `UseAbility(player, mousePosition, toggleOn)` | Instance | `ModMount` | `undefined` | The mount's ability. | [Code](../../app/src/main/cpp/script/js/mod/ModMount.js#L21) |

## ModNPC

[Contract and fields](classes.md#modnpc).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `AddShops()` | Instance | `ModNPC` | `undefined` | Once: the shops (NPCShop). | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L117) |
| `AI(npc)` | Instance | `ModNPC` | `undefined` | Every frame. PreAI → false skips the game's AI. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L83) |
| `ApplyBuffImmunity(npc)` | Instance | `ModNPC` | `undefined` | After SetDefaults: npc.buffImmune[id] = true. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L50) |
| `ApplyDifficultyAndPlayerScaling(npc, numPlayers, balance, bossAdjustment)` | Instance | `ModNPC` | `undefined` | After the per-player scaling. Receives the count used by the NPC, the balance computed by the game and an adjustment of 0.85 in Master or 1 in the other modes. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L72) |
| `BossHeadRotation(npc, rotation)` | Instance | `ModNPC` | `undefined` | Receive a Ref initialized with the game's result. Index -1 hides the icon. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L79) |
| `BossHeadSlot(npc, index)` | Instance | `ModNPC` | `undefined` | Receive a Ref initialized with the game's result. Index -1 hides the icon. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L78) |
| `BossHeadSpriteEffects(npc, spriteEffects)` | Instance | `ModNPC` | `undefined` | Receive a Ref initialized with the game's result. Index -1 hides the icon. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L80) |
| `CanBeHitByItem(npc, player, item)` | Instance | `ModNPC` | `null` | false vetoes; true allows it in the eligibility test; null/undefined keeps the game's. A ModPlayer veto wins. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L61) |
| `CanBeHitByNPC(npc, attacker)` | Instance | `ModNPC` | `true` | NPC against NPC; false vetoes. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L67) |
| `CanBeHitByProjectile(npc, projectile)` | Instance | `ModNPC` | `null` | false vetoes, true forces, null leaves it to the game (player projectiles). | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L63) |
| `CanChat(npc)` | Instance | `ModNPC` | `npc.townNPC` | Decides whether it can chat, including outside AI 7; an undefined return keeps the game's. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L108) |
| `CanHitNPC(npc, target)` | Instance | `ModNPC` | `true` | NPC against NPC; false vetoes. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L66) |
| `CanHitPlayer(npc, player, cooldownSlot)` | Instance | `ModNPC` | `true` | false vetoes damage; cooldownSlot.value changes the immunity slot. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L58) |
| `CanTownNPCSpawn(numTownNPCs)` | Instance | `ModNPC` | `false` | Every so often, with none of this one in the world: true and it moves in. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L104) |
| `CheckActive(npc)` | Instance | `ModNPC` | `true` | false: does not despawn when far away. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L88) |
| `CheckConditions(left, right, top, bottom)` | Instance | `ModNPC` | `true` | Does the room suit it? | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L105) |
| `CheckDead(npc)` | Instance | `ModNPC` | `true` | With the NPC active and life ≤ 0: false prevents death; restore life to avoid another check. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L71) |
| `Clone(newNPC)` | Instance | `ModNPC` | `Entities.Clone(this)` | A copy of the instance. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L36) |
| `CloneDefaults(type)` | Instance | `ModNPC` | `See source` | Copies classic defaults from a vanilla or mod NPC; keeps type, netID, ModNPC, globals and the players' participation. The world difficulty is applied afterwards. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L43) |
| `get CloneNewInstances` | Getter | `ModNPC` | `false` | Uma instância para uma entidade nova (o NewInstance do tModLoader): pelo construtor; com CloneNewInstances, uma cópia do molde (Clone). | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L39) |
| `DrawBehind(npc, index)` | Instance | `ModNPC` | `undefined` | After the draw cache; add the index to the Main.instance.DrawCacheNPCs lists. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L76) |
| `DrawEffects(npc, drawColor)` | Instance | `ModNPC` | `undefined` | Ref<Color> of the center's lighting; runs before PreDraw. The changed color goes through the game's buff tint. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L75) |
| `DrawHealthBar` | Not implemented | Not implemented | — | Main.DrawHealthBar does not receive the NPC and also serves players and worm segments. Matching by position or life could change the wrong entity. | [Code](../../tools/tests/modnpchooks/README.md#dropped-methods) |
| `DrawTownAttackGun` | Not implemented | Not implemented | — | Texture, frame, scale and offsets are locals of the renderer. There is no method with all of the mutable arguments. | [Code](../../tools/tests/modnpchooks/README.md#dropped-methods) |
| `DrawTownAttackSwing` | Not implemented | Not implemented | — | Texture, frame, scale and offsets are locals of the renderer. There is no method with all of the mutable arguments. | [Code](../../tools/tests/modnpchooks/README.md#dropped-methods) |
| `FindFrame(npc, frameHeight)` | Instance | `ModNPC` | `undefined` | Custom animation: change npc.frame. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L87) |
| `GetAlpha(npc, drawColor)` | Instance | `ModNPC` | `null` | A Color replaces the result, including alpha 0; null/undefined uses the game's. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L77) |
| `getByName(name)` | Static | `ModNPC` | `NPCLoader.ByType.get(bl.npcs.typeOf(name))` | As for items. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L240) |
| `GetChat(npc)` | Instance | `ModNPC` | `undefined` | What it says when you chat. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L107) |
| `getModNPC(type)` | Static | `ModNPC` | `NPCLoader.ByType.get(type)` | As for items. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L239) |
| `getTypeByName(name)` | Static | `ModNPC` | `bl.npcs.typeOf(name)` | As for items. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L238) |
| `get Happiness` | Getter | `ModNPC` | `new NPCHappiness(this.Type)` | NPCHappiness | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L34) |
| `HitEffect(npc, hitDirection, damage)` | Instance | `ModNPC` | `undefined` | On each hit, after the game's effect. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L53) |
| `isModNPC(npc)` | Static | `ModNPC` | `!!npc && bl.npcs.isModNpc(npc.type)` | As for items. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L237) |
| `isModType(type)` | Static | `ModNPC` | `bl.npcs.isModNpc(type)` | As for items. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L236) |
| `ModifyActiveShop(npc, shopName, items)` | Instance | `ModNPC` | `undefined` | When filling a registered shop of this NPC in a chat, after NPCShop builds the items. Receives the shop's local name and the native item array. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L110) |
| `ModifyDeathMessage` | Not implemented | Not implemented | — | The message and the color are locals of checkDead. DropTombstoneTownNPC only exposes the tombstone text. | [Code](../../tools/tests/modnpchooks/README.md#dropped-methods) |
| `ModifyHitByItem(npc, player, item, modifiers)` | Instance | `ModNPC` | `undefined` | Before the item hit; HitModifiers. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L62) |
| `ModifyHitByProjectile(npc, projectile, modifiers)` | Instance | `ModNPC` | `undefined` | Before the projectile hit; HitModifiers. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L64) |
| `ModifyHitNPC(npc, target, modifiers)` | Instance | `ModNPC` | `undefined` | Its hit on another NPC. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L68) |
| `ModifyHitPlayer(npc, player, modifiers)` | Instance | `ModNPC` | `undefined` | Changes the damage parameters mobile supports before Hurt. JS contract: damage, hitDirection, quiet, crit and dodgeable. Does not have every field of tModLoader's HurtModifiers. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L59) |
| `ModifyIncomingHit(npc, modifiers)` | Instance | `ModNPC` | `undefined` | Last, on every hit (also with no source). Not reapplied on fromNet. JS contract: damage, knockBack, hitDirection, crit, SourceDamage and Knockback (StatModifier), SetCrit() and DisableCrit(). Not reapplied on fromNet. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L70) |
| `ModifyNPCHappiness(npc, player, primaryPlayerBiome, shopHelper, nearbyNPCsByType)` | Instance | `ModNPC` | `undefined` | After the native preferences, before clamping the price. Uses ExMod's alternative; the method is disabled in tModLoader stable. ExMod adaptation. Primary biome: 0 forest, 1 underground, 2 snow, 3 desert, 4 jungle, 5 ocean, 6 hallow, 7 mushroom, 8 dungeon, 9 corruption, 10 crimson. shopHelper and the neighbors are native. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L111) |
| `ModifyNPCLoot(npcLoot)` | Instance | `ModNPC` | `undefined` | Once: the drops. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L51) |
| `NewInstance(entity)` | Instance | `ModNPC` | `this.CloneNewInstances ? this.Clone(entity) : Entities.NewInstance(this)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L40) |
| `get NPC` | Getter | `ModNPC` | `Entities.Of(this)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L33) |
| `NPCHeadSlot()` | Instance | `ModNPC` | `bl.npcs.headSlot(this.Type)` | (to read) the head's index, -1 without a head. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L112) |
| `NPCValue(p = 0, g = 0, s = 0, c = 0)` | Static | `ModNPC` | `p * 1000000 + g * 10000 + s * 100 + c` | The money it drops. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L123) |
| `OnChatButtonClicked(npc, firstButton)` | Instance | `ModNPC` | `undefined` | A button was tapped; return a shop's name to open it. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L116) |
| `OnHitByItem(npc, player, item, hit, damageDone)` | Instance | `ModNPC` | `undefined` | After actual item damage, once per hit. hit has Damage, SourceDamage, Knockback, HitDirection and Crit. damageDone is the actual native return. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L56) |
| `OnHitByProjectile(npc, projectile, hit, damageDone)` | Instance | `ModNPC` | `undefined` | After actual projectile damage, including with no player owner. Includes projectiles with no player owner. hit has Damage, SourceDamage, Knockback, HitDirection and Crit. damageDone is the actual native return. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L57) |
| `OnHitNPC(npc, target, hit)` | Instance | `ModNPC` | `undefined` | Its hit on another NPC. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L69) |
| `OnHitPlayer(npc, player, hurtInfo)` | Instance | `ModNPC` | `undefined` | After actual damage, receives the hit's data and the slot used. hurtInfo has DamageSource, Damage, HitDirection, PvP, Quiet, Crit, CooldownCounter and Dodgeable. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L60) |
| `OnKill(npc)` | Instance | `ModNPC` | `undefined` | On death. PreKill → false cancels the drop. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L90) |
| `OnSpawn(npc, source)` | Instance | `ModNPC` | `undefined` | Once after spawning; receives the native source. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L54) |
| `PostAI(npc)` | Instance | `ModNPC` | `undefined` | Every frame. PreAI → false skips the game's AI. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L84) |
| `PostDraw(npc, spriteBatch, screenPos, drawColor)` | Instance | `ModNPC` | `undefined` | After drawing, even when PreDraw vetoes. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L74) |
| `PostSetDefaults(npc)` | Instance | `ModNPC` | `undefined` | The base implementation does nothing. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L48) |
| `PostSetupContent()` | Instance | `ModNPC` | `undefined` | The base implementation does nothing. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L49) |
| `PostStaticDefaults()` | Instance | `ModNPC` | `undefined` | The base implementation does nothing. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L47) |
| `PreAI(npc)` | Instance | `ModNPC` | `true` | Every frame. PreAI → false skips the game's AI. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L82) |
| `PreDraw(npc, spriteBatch, screenPos, drawColor)` | Instance | `ModNPC` | `true` | false skips the native drawing. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L73) |
| `PreKill(npc)` | Instance | `ModNPC` | `true` | On death. PreKill → false cancels the drop. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L89) |
| `ReceiveExtraAI(reader)` | Instance | `ModNPC` | `undefined` | The base implementation does nothing. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L86) |
| `register(cls)` | Static | `ModNPC` | `See source` | As for items. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L127) |
| `ResetEffects(npc)` | Instance | `ModNPC` | `undefined` | After the buff flags are reset, before reapplying effects and running the AI. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L55) |
| `SendExtraAI(writer)` | Instance | `ModNPC` | `undefined` | The base implementation does nothing. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L85) |
| `SetBestiary(database, bestiaryEntry)` | Instance | `ModNPC` | `undefined` | Once: the Bestiary entry. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L52) |
| `SetChatButtons(npc, buttons)` | Instance | `ModNPC` | `undefined` | The chat buttons (up to two on mobile). | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L114) |
| `SetDefaults(npc)` | Instance | `ModNPC` | `undefined` | Every NPC of this type that spawns. The Expert/Master scaling comes after. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L46) |
| `SetNPCNameList()` | Instance | `ModNPC` | `[]` | Its first names; one is picked. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L106) |
| `SetStaticDefaults()` | Instance | `ModNPC` | `undefined` | Once. Main.npcFrameCount[this.Type] = n: the texture's frames. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L45) |
| `SpawnChance(spawnInfo)` | Instance | `ModNPC` | `0` | On each natural spawn: return the weight (the game's weighs 1; 0 = does not spawn). Single player or server only. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L95) |
| `SpawnNPC(tileX, tileY)` | Instance | `ModNPC` | `See source` | Picked: how it spawns, at the spawn tile; returns the index. Default: on top of the tile (tileX  16 + 8, tileY  16), as in tModLoader. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L98) |
| `TownNPCAttackCooldown` | Not implemented | Not implemented | — | The cooldown and its parameters live in local variables of the town NPC AI. There is no interceptable helper with the complete references. | [Code](../../tools/tests/modnpchooks/README.md#dropped-methods) |
| `TownNPCAttackMagic` | Not implemented | Not implemented | — | The aura parameters live in local variables of the town NPC AI. There is no interceptable helper with the complete references. | [Code](../../tools/tests/modnpchooks/README.md#dropped-methods) |
| `TownNPCAttackProj(npc, attack)` | Instance | `ModNPC` | `undefined` | The attack's projectile (projType, attackDelay). | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L120) |
| `TownNPCAttackProjSpeed(npc, attack)` | Instance | `ModNPC` | `undefined` | speed, gravityCorrection, randomOffset. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L121) |
| `TownNPCAttackShoot` | Not implemented | Not implemented | — | The shot's parameters live in local variables of the town NPC AI. There is no interceptable helper with the complete references. | [Code](../../tools/tests/modnpchooks/README.md#dropped-methods) |
| `TownNPCAttackStrength(npc, attack)` | Instance | `ModNPC` | `undefined` | damage, knockback. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L119) |
| `TownNPCAttackSwing` | Not implemented | Not implemented | — | The hitbox parameters live in local variables of the town NPC AI. There is no interceptable helper with the complete references. | [Code](../../tools/tests/modnpchooks/README.md#dropped-methods) |
| `TownNPCProfile()` | Instance | `ModNPC` | `null` | Once after content loads: return a native ITownNPCProfile; null keeps the automatic appearance. Called once on the template, with content ready. Return a native ITownNPCProfile or null. | [Code](../../app/src/main/cpp/script/js/mod/ModNPC.js#L109) |
| `UpdateLifeRegen` | Not implemented | Not implemented | — | UpdateNPC_BuffApplyDOTs accumulates and applies the damage in a local DOTTally. The damage reference is not reachable before it is applied. | [Code](../../tools/tests/modnpchooks/README.md#dropped-methods) |

## ModPacket

[Contract and fields](classes.md#network). Inherits from `NetWriter`.

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `constructor(mod)` | Constructor | `ModPacket` | `instance` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModPacket.js#L3) |
| `Send(toClient = -1, ignoreClient = -1)` | Instance | `ModPacket` | `See source` | From the client: to the server. From the server: to one client, or to everyone except one. | [Code](../../app/src/main/cpp/script/js/mod/ModPacket.js#L9) |
| `Write(value)` | Instance | `NetWriter` (inherited) | `See source` | Stores a number, text, boolean, array, plain object or Vector2. The typed names (WriteInt32...) do the same. Writes JS values in the loader's protocol, not BinaryWriter bytes. The WriteByte, WriteInt32 etc. variants are aliases of Write. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L6) |
| `Write7BitEncodedInt(value)` | Instance | `NetWriter` (inherited) | `See source` | Alias of NetWriter.Write. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteBoolean(value)` | Instance | `NetWriter` (inherited) | `See source` | Alias of NetWriter.Write. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteByte(value)` | Instance | `NetWriter` (inherited) | `See source` | Alias of NetWriter.Write. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteDouble(value)` | Instance | `NetWriter` (inherited) | `See source` | Alias of NetWriter.Write. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteFlags(...flags)` | Instance | `NetWriter` (inherited) | `See source` | Several booleans; a vector. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L12) |
| `WriteInt16(value)` | Instance | `NetWriter` (inherited) | `See source` | Alias of NetWriter.Write. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteInt32(value)` | Instance | `NetWriter` (inherited) | `See source` | Alias of NetWriter.Write. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteInt64(value)` | Instance | `NetWriter` (inherited) | `See source` | Alias of NetWriter.Write. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteSByte(value)` | Instance | `NetWriter` (inherited) | `See source` | Alias of NetWriter.Write. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteSingle(value)` | Instance | `NetWriter` (inherited) | `See source` | Alias of NetWriter.Write. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteString(value)` | Instance | `NetWriter` (inherited) | `See source` | Alias of NetWriter.Write. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteUInt16(value)` | Instance | `NetWriter` (inherited) | `See source` | Alias of NetWriter.Write. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteUInt32(value)` | Instance | `NetWriter` (inherited) | `See source` | Alias of NetWriter.Write. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteVector2(v)` | Instance | `NetWriter` (inherited) | `See source` | Several booleans; a vector. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L13) |

## ModPlayer

[Contract and fields](classes.md#modplayer).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `AddDrawData(drawInfo, drawData)` | Static | `ModPlayer` | `See source` | A ModPlayer static: writes into the next active slot and increments DrawDataCacheCount after the write. Returns true on insertion and false for a full cache, an invalid counter or a missing cache/data. Reuses the native array, without allocating another array or installing hooks. Constant time and extra space. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L153) |
| `AddMaterialsForCrafting(player, itemConsumedCallback)` | Instance | `ModPlayer` | `null` | Returns items; put a (item, index) function in itemConsumedCallback.value. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L135) |
| `AddStartingItems(player, mediumCoreDeath)` | Instance | `ModPlayer` | `[]` | Returns a list of items; empty by default. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L133) |
| `AnglerQuestReward(player, rareMultiplier, rewardItems)` | Instance | `ModPlayer` | `undefined` | A mutable JS list of items before delivery. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L122) |
| `ApplyPotionDelay(player, item, potionDelay)` | Instance | `ModPlayer` | `true` | Receives the effective time, including rolls; false prevents the delay and the buff. Receives the final delay after ModItem.ModifyPotionDelay. Any ModItem or ModPlayer veto prevents the counter and the buff; keeps healing and consumption. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L87) |
| `ArmorSetBonusActivated(player)` | Instance | `ModPlayer` | `undefined` | Double tap in the configured activation direction. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L96) |
| `ArmorSetBonusHeld(player, holdTime)` | Instance | `ModPlayer` | `undefined` | Activation direction held down. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L97) |
| `CanAutoReuseItem(player, item)` | Instance | `ModPlayer` | `null` | An optional decision, keeping the item's permanent fields. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L73) |
| `CanBeHitByNPC(player, npc, cooldownSlot)` | Instance | `ModPlayer` | `true` | Veto; cooldownSlot is a Ref<int>. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L61) |
| `CanBeHitByProjectile(player, projectile)` | Instance | `ModPlayer` | `true` | Veto on damage taken from a projectile. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L62) |
| `CanBeTeleportedTo(player, teleportPosition, context)` | Instance | `ModPlayer` | `true` | Veto on the native teleport; context 'TeleportRod', 'TeleportationPotion', 'Teleport' or 'Wormhole'. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L139) |
| `CanBuyItem(player, vendor, shopInventory, item)` | Instance | `ModPlayer` | `true` | Veto before paying for the purchase. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L123) |
| `CanCatchNPC(player, target, item)` | Instance | `ModPlayer` | `null` | An optional decision for the catch. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L131) |
| `CanConsumeAmmo(player, weapon, ammo)` | Instance | `ModPlayer` | `true` | false keeps the ammo; not asked on dontConsume calls. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L71) |
| `CanConsumeBait(player, bait)` | Instance | `ModPlayer` | `null` | An optional decision after the native consumption roll. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L121) |
| `CanHitNPC(player, target)` | Instance | `ModPlayer` | `true` | true by default; a general veto. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L46) |
| `CanHitNPCWithItem(player, item, target)` | Instance | `ModPlayer` | `null` | null by default; an optional decision for the item. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L47) |
| `CanHitNPCWithProj(player, projectile, target)` | Instance | `ModPlayer` | `null` | An optional decision for the projectile. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L48) |
| `CanHitPvp(player, item, target)` | Instance | `ModPlayer` | `true` | false prevents the melee hit on another player. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L49) |
| `CanHitPvpWithProj(player, projectile, target)` | Instance | `ModPlayer` | `true` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L50) |
| `CanMeleeAttackCollideWithNPC(player, item, hitbox, target)` | Instance | `ModPlayer` | `null` | An optional decision for the swing's intersection. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L51) |
| `CanSellItem(player, vendor, shopInventory, item)` | Instance | `ModPlayer` | `true` | Veto before the sale. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L125) |
| `CanShoot(player, item)` | Instance | `ModPlayer` | `true` | Vetoes the shot, keeping the use time. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L68) |
| `CanShowExtraJumpVisuals(player, jump)` | Instance | `ModPlayer` | `true` | Veto on the active jump's native visuals. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L101) |
| `CanStartExtraJump(player, jump)` | Instance | `ModPlayer` | `true` | Veto on the available extra jump. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L100) |
| `CanUseItem(player, item)` | Instance | `ModPlayer` | `true` | false prevents use. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L33) |
| `CatchFish(attempt, itemDrop, npcSpawn, sonar, sonarPosition)` | Instance | `ModPlayer` | `undefined` | Caught a fish, after the game's roll: itemDrop.value and npcSpawn.value (Ref, 0 = nothing) decide what comes out; attempt has questFish, common/uncommon/rare..., inLava... sonar/sonarPosition do nothing yet. itemDrop and npcSpawn are Refs. sonar and sonarPosition are not applied to the game yet. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L44) |
| `ConsumableDodge(player, info)` | Instance | `ModPlayer` | `false` | true avoids the damage after ModifyHurt and FreeDodge. Only for the local player's dodgeable hits. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L38) |
| `CopyClientState(player, targetCopy)` | Instance | `ModPlayer` | `undefined` | Copies into another instance of the same class, tied to the native clone. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L146) |
| `DrawEffects(player, drawInfo, r, g, b, a, fullBright)` | Instance | `ModPlayer` | `undefined` | Ref<float> multipliers and Ref<bool>; after the native preparation. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L109) |
| `DrawPlayer(player, camera)` | Instance | `ModPlayer` | `undefined` | After the complete native drawing. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L110) |
| `EmitEnchantmentVisualsAt(player, projectile, position, width, height)` | Instance | `ModPlayer` | `undefined` | After the projectile's enchantment visuals. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L60) |
| `ExtraJumpVisuals(player, jump)` | Instance | `ModPlayer` | `undefined` | Additional visuals for the allowed jump. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L102) |
| `FrameEffects(player)` | Instance | `ModPlayer` | `undefined` | After the game assembles what gets drawn: switching player.head/body/legs changes the drawing. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L28) |
| `FreeDodge(player, damageSource, damage, hitDirection, pvp, quiet, crit, cooldownCounter, dodgeable)` | Instance | `ModPlayer` | `false` | true: dodges. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L37) |
| `get(player)` | Static | `ModPlayer` | `PlayerLoader.Of(player).get(this)` | The same. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L162) |
| `getByName(name)` | Static | `ModPlayer` | `PlayerLoader.Find(Terraria.Main.player[Terraria.Main.myPlayer], name)` | The local player's (for UI). | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L166) |
| `GetDyeTraderReward(player, rewardPool)` | Instance | `ModPlayer` | `undefined` | A mutable JS list of IDs before the roll. An empty list produces an empty item. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L130) |
| `GetFishingLevel(player, fishingRod, bait, fishingLevel)` | Instance | `ModPlayer` | `undefined` | Ref<float>; the items can be null when not found. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L118) |
| `GetHealLife(player, item, quickHeal, healValue)` | Instance | `ModPlayer` | `undefined` | healValue is a Ref<int>; it also influences the quick heal choice. After ModItem, only with a positive base healLife. quickHeal uses a per-player context; also takes part in QuickHeal's selection. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L85) |
| `GetHealMana(player, item, quickHeal, healValue)` | Instance | `ModPlayer` | `undefined` | healValue is a Ref<int>. After ModItem, only with a positive base healMana. quickHeal true in QuickHeal and QuickMana; temporary fields restored. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L86) |
| `HideDrawLayers(player, drawInfo)` | Instance | `ModPlayer` | `undefined` | Call PlayerDrawLayers.Head.Hide(), for example, for that draw. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L111) |
| `HoverSlot(player, inventory, context, slot)` | Instance | `ModPlayer` | `false` | true handles the hover and skips the native one. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L137) |
| `ImmuneTo(player, damageSource, cooldownCounter, dodgeable)` | Instance | `ModPlayer` | `false` | true: the hit does not happen. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L36) |
| `Initialize()` | Instance | `ModPlayer` | `undefined` | Once, when the instance is created. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L8) |
| `Kill(player, damageSource, damage, hitDirection, pvp)` | Instance | `ModPlayer` | `undefined` | Died. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L43) |
| `LoadData(data)` | Instance | `ModPlayer` | `undefined` | When the character loads. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L151) |
| `MeleeEffects(player, item, hitbox)` | Instance | `ModPlayer` | `undefined` | During the use visuals. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L59) |
| `ModifyCaughtFish(player, fish)` | Instance | `ModPlayer` | `undefined` | Changes the actual item before handing it over. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L120) |
| `ModifyDrawInfo(player, drawInfo)` | Instance | `ModPlayer` | `undefined` | Changes the PlayerDrawSet before the layers are built. The mobile cache is DrawData[] with DrawDataCacheCount, not List<DrawData>. Use ModPlayer.AddDrawData(drawInfo, drawData). The entry comes before the native layers and may end up behind the player. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L112) |
| `ModifyDrawLayerOrdering(player, positions)` | Instance | `ModPlayer` | `undefined` | A Map from descriptor to { Before: descriptor } or { After: descriptor }. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L113) |
| `ModifyExtraJumpDurationMultiplier(player, jump, duration)` | Instance | `ModPlayer` | `undefined` | duration is a Ref<float>, initially 1. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L103) |
| `ModifyFishingAttempt(player, attempt)` | Instance | `ModPlayer` | `undefined` | Before the item roll; changes the attempt directly. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L119) |
| `ModifyHitByNPC(player, npc, modifiers)` | Instance | `ModPlayer` | `undefined` | Before ModifyHurt. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L63) |
| `ModifyHitByProjectile(player, projectile, modifiers)` | Instance | `ModPlayer` | `undefined` | Before ModifyHurt. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L64) |
| `ModifyHitNPC(player, target, modifiers)` | Instance | `ModPlayer` | `undefined` | Before the native damage, followed by the source-specific callback. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L52) |
| `ModifyHitNPCWithItem(player, item, target, modifiers)` | Instance | `ModPlayer` | `undefined` | Modifies the item hit. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L53) |
| `ModifyHitNPCWithProj(player, projectile, target, modifiers)` | Instance | `ModPlayer` | `undefined` | Modifies the projectile hit. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L54) |
| `ModifyHurt(player, modifiers)` | Instance | `ModPlayer` | `undefined` | Before the hit: modifiers.damage, hitDirection, quiet, crit, dodgeable. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L39) |
| `ModifyItemScale(player, item, scale)` | Instance | `ModPlayer` | `undefined` | scale is a Ref<float> of the multiplier. Acts on GetAdjustedItemScale and the native hitbox, together with ModItem. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L76) |
| `ModifyLuck(player, luck)` | Instance | `ModPlayer` | `undefined` | Luck, luck.value; PreModifyLuck returning false skips the game's. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L31) |
| `ModifyManaCost(player, item, reduce, mult)` | Instance | `ModPlayer` | `undefined` | Ref<float>: current cost × (1 - reduce.value) × mult.value. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L80) |
| `ModifyMaxStats(player)` | Instance | `ModPlayer` | `See source` | After ResetEffects. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L13) |
| `ModifyNurseHeal(player, nurse, health, removeDebuffs, chatText)` | Instance | `ModPlayer` | `true` | Three Refs; false prevents the service and uses the given text. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L128) |
| `ModifyNursePrice(player, nurse, health, removeDebuffs, price)` | Instance | `ModPlayer` | `undefined` | price is a Ref<int>; also asked when showing the price. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L127) |
| `ModifyScreenPosition(player)` | Instance | `ModPlayer` | `undefined` | After updating the camera; change Main.screenPosition. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L115) |
| `ModifyShootStats(player, item, position, velocity, type, damage, knockBack)` | Instance | `ModPlayer` | `undefined` | The last five parameters are Refs. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L70) |
| `ModifyStartingInventory(player, itemsByMod, mediumCoreDeath)` | Instance | `ModPlayer` | `undefined` | A Map from UUID to item lists; native items under 'Terraria'. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L134) |
| `ModifyWeaponCrit(player, item, crit)` | Instance | `ModPlayer` | `undefined` | crit is a Ref<int>. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L74) |
| `ModifyWeaponDamage(player, item, damage)` | Instance | `ModPlayer` | `undefined` | Return the new damage. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L34) |
| `ModifyWeaponKnockback(player, item, knockback)` | Instance | `ModPlayer` | `undefined` | knockback is a StatModifier. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L75) |
| `ModifyZoom(player, zoom)` | Instance | `ModPlayer` | `undefined` | Ref<float>; keeps the axes' aspect ratio. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L116) |
| `NaturalLifeRegen(player, regen)` | Instance | `ModPlayer` | `undefined` | Ref<float> in the native calculation of natural regeneration. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L92) |
| `OnCatchNPC(player, npc, item, failed)` | Instance | `ModPlayer` | `undefined` | A completed catch or an attempt that causes damage when trying to catch a lava critter. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L132) |
| `OnConsumeAmmo(player, weapon, ammo)` | Instance | `ModPlayer` | `undefined` | When the stack goes down; keeps the last ammo's type before TurnToAir. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L72) |
| `OnConsumeMana(player, item, manaConsumed)` | Instance | `ModPlayer` | `undefined` | Notifies the mana consumed. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L81) |
| `OnEnterWorld(player)` | Instance | `ModPlayer` | `undefined` | Entered the world. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L9) |
| `OnEquipmentLoadoutSwitched(player, oldLoadoutIndex, loadoutIndex)` | Instance | `ModPlayer` | `undefined` | After an actual switch. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L98) |
| `OnExtraJumpCleared(player, jump)` | Instance | `ModPlayer` | `undefined` | When a jump's availability is removed without enabling equipment. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L107) |
| `OnExtraJumpEnded(player, jump)` | Instance | `ModPlayer` | `undefined` | When the active jump ends. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L105) |
| `OnExtraJumpRefreshed(player, jump)` | Instance | `ModPlayer` | `undefined` | After renewing the jumps available from equipment. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L106) |
| `OnExtraJumpStarted(player, jump, playSound)` | Instance | `ModPlayer` | `undefined` | playSound is a Ref<bool>; false suppresses the start sound. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L104) |
| `OnHitAnything(player, x, y, victim)` | Instance | `ModPlayer` | `undefined` | After Player.OnHit. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L58) |
| `OnHitByNPC(player, npc, info)` | Instance | `ModPlayer` | `undefined` | After positive damage caused by the NPC. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L65) |
| `OnHitByProjectile(player, projectile, info)` | Instance | `ModPlayer` | `undefined` | After positive damage caused by the projectile. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L66) |
| `OnHitNPC(player, target, hit, damageDone)` | Instance | `ModPlayer` | `undefined` | After a hit with positive damage. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L55) |
| `OnHitNPCWithItem(player, item, target, hit, damageDone)` | Instance | `ModPlayer` | `undefined` | Item-specific notification. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L56) |
| `OnHitNPCWithProj(player, projectile, target, hit, damageDone)` | Instance | `ModPlayer` | `undefined` | Projectile-specific notification. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L57) |
| `OnHurt(player, damageSource, damage, hitDirection, pvp, quiet, crit, cooldownCounter, dodgeable)` | Instance | `ModPlayer` | `undefined` | After the hit. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L40) |
| `OnMissingMana(player, item, neededMana)` | Instance | `ModPlayer` | `undefined` | Before the native attempt to recover mana. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L82) |
| `OnPickup(player, item)` | Instance | `ModPlayer` | `true` | false removes the item from the ground without putting it in the inventory. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L136) |
| `OnRespawn(player)` | Instance | `ModPlayer` | `undefined` | Came back to life. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L10) |
| `get Player` | Getter | `ModPlayer` | `Entities.Of(this)` | This instance's player. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L6) |
| `PlayerConnect(player)` | Instance | `ModPlayer` | `undefined` | After the player connects. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L147) |
| `PlayerDisconnect(player)` | Instance | `ModPlayer` | `undefined` | Before removing the disconnected player. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L148) |
| `PostBuyItem(player, vendor, shopInventory, item)` | Instance | `ModPlayer` | `undefined` | After a paid purchase; item received through the cursor. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L124) |
| `PostHurt(player, damageSource, damage, hitDirection, pvp, quiet, crit, cooldownCounter, dodgeable)` | Instance | `ModPlayer` | `undefined` | After the hit, if the player survived. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L41) |
| `PostItemCheck(player)` | Instance | `ModPlayer` | `undefined` | After the check, even when vetoed. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L84) |
| `PostNurseHeal(player, nurse, health, removeDebuffs, price)` | Instance | `ModPlayer` | `undefined` | After a paid service, including a zero price. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L129) |
| `PostSavePlayer(player)` | Instance | `ModPlayer` | `undefined` | When the native save and the auxiliary data finish, even if the native save throws. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L142) |
| `PostSellItem(player, vendor, shopInventory, item)` | Instance | `ModPlayer` | `undefined` | After an accepted sale. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L126) |
| `PostUpdate(player)` | Instance | `ModPlayer` | `undefined` | End of the frame. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L18) |
| `PostUpdateBuffs(player)` | Instance | `ModPlayer` | `undefined` | Around the buffs. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L20) |
| `PostUpdateEquips(player)` | Instance | `ModPlayer` | `undefined` | After the equipment. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L22) |
| `PostUpdateMiscEffects(player)` | Instance | `ModPlayer` | `undefined` | After CapAttackSpeeds. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L90) |
| `PostUpdateRunSpeeds(player)` | Instance | `ModPlayer` | `undefined` | Before HorizontalMovement. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L91) |
| `PreItemCheck(player)` | Instance | `ModPlayer` | `true` | false skips the item check. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L83) |
| `PreKill(player, damageSource, damage, hitDirection, pvp)` | Instance | `ModPlayer` | `true` | false prevents death. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L42) |
| `PreModifyLuck(player, luck)` | Instance | `ModPlayer` | `true` | Luck, luck.value; PreModifyLuck returning false skips the game's. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L30) |
| `PreSaveCustomData(player)` | Instance | `ModPlayer` | `undefined` | Before building the .plr.bl.json file's data. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L143) |
| `PreSavePlayer(player)` | Instance | `ModPlayer` | `undefined` | Before the native save. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L141) |
| `PreUpdate(player)` | Instance | `ModPlayer` | `undefined` | Start of the player's frame. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L17) |
| `PreUpdateBuffs(player)` | Instance | `ModPlayer` | `undefined` | Around the buffs. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L19) |
| `PreUpdateMovement(player)` | Instance | `ModPlayer` | `undefined` | Before the frame's first collision and movement step. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L89) |
| `ProcessTriggers(player, triggersSet)` | Instance | `ModPlayer` | `undefined` | After copying the controls to the local player. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L94) |
| `register(cls)` | Static | `ModPlayer` | `See source` | Registers. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L170) |
| `ResetEffects(player)` | Instance | `ModPlayer` | `undefined` | Right after the game resets the effects. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L12) |
| `ResetInfoAccessories(player)` | Instance | `ModPlayer` | `undefined` | After resetting effects and after updating the info accessories. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L95) |
| `SaveData(data)` | Instance | `ModPlayer` | `undefined` | On each save: put what to remember in data. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L150) |
| `SendClientChanges(player, clientPlayer)` | Instance | `ModPlayer` | `undefined` | On the client, compares with the instance copied on the previous frame. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L145) |
| `ShiftClickSlot(player, inventory, context, slot)` | Instance | `ModPlayer` | `false` | With Shift active, true handles the click. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L138) |
| `Shoot(player, item, source, position, velocity, type, damage, knockBack)` | Instance | `ModPlayer` | `true` | false prevents creating the projectile. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L69) |
| `SyncPlayer(player, toWho, fromWho, newPlayer)` | Instance | `ModPlayer` | `undefined` | After syncing the native player. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L144) |
| `TransformDrawData(player, drawInfo)` | Instance | `ModPlayer` | `undefined` | After the native transformations and before rendering the cache. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L114) |
| `UpdateAutopause(player)` | Instance | `ModPlayer` | `undefined` | After the local player's paused update. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L93) |
| `UpdateBadLifeRegen(player)` | Instance | `ModPlayer` | `undefined` | Before and after life regeneration. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L23) |
| `UpdateDead(player)` | Instance | `ModPlayer` | `undefined` | Every frame while dead. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L26) |
| `UpdateEquips(player)` | Instance | `ModPlayer` | `undefined` | After the equipment. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L21) |
| `UpdateLifeRegen(player)` | Instance | `ModPlayer` | `undefined` | Before and after life regeneration. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L24) |
| `UpdateManaRegen(player)` | Instance | `ModPlayer` | `undefined` | After mana regeneration. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L25) |
| `UpdateMovement(player)` | Instance | `ModPlayer` | `undefined` | Custom movement (dash), near the end of the frame. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L27) |
| `UseAnimationMultiplier(player, item)` | Instance | `ModPlayer` | `1` | Multiplies the animation time; default 1. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L79) |
| `UseSpeedMultiplier(player, item)` | Instance | `ModPlayer` | `1` | Divisor of the use and animation times; default 1. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L77) |
| `UseTimeMultiplier(player, item)` | Instance | `ModPlayer` | `1` | Multiplies the use time; default 1. | [Code](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L78) |

## ModPrefix

[Contract and fields](classes.md#modprefix).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `AllStatChangesHaveEffectOn(item)` | Instance | `ModPrefix` | `true` | false: does not take on this item (for stats that are not the game's). | [Code](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L29) |
| `Apply(item)` | Instance | `ModPrefix` | `undefined` | After the stats: whatever else the prefix changes on the item. | [Code](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L32) |
| `ApplyAccessoryEffects(player)` | Instance | `ModPrefix` | `undefined` | Every frame, with the accessory equipped. | [Code](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L38) |
| `CanRoll(item)` | Instance | `ModPrefix` | `this.RollChance(item) > 0` | false: this item cannot get it (default: RollChance > 0). | [Code](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L19) |
| `get Category` | Getter | `ModPrefix` | `PrefixCategory.Custom` | PrefixCategory | [Code](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L12) |
| `get FullName` | Getter | `ModPrefix` | `(this.Mod ? this.Mod.id \|\| this.Mod.uuid : '?') + '/' + this.Name` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L9) |
| `getModPrefix(type)` | Static | `ModPrefix` | `PrefixLoader.GetPrefix(type)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L55) |
| `GetTooltipLines(item)` | Instance | `ModPrefix` | `null` | Extra lines (an array of TooltipLine; IsModifier = true paints it green). The game already writes the damage, speed, crit etc. ones. | [Code](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L41) |
| `getTypeByName(name)` | Static | `ModPrefix` | `ContentLookup.TypeOf(PrefixLoader.ByType, name)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L56) |
| `isModType(type)` | Static | `ModPrefix` | `PrefixLoader.GetPrefix(type) !== undefined` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L54) |
| `ModifyValue(valueMult)` | Instance | `ModPrefix` | `undefined` | The price (Ref); the rarity goes up or down with it. | [Code](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L35) |
| `get Name` | Getter | `ModPrefix` | `this.constructor.name` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L8) |
| `register(cls)` | Static | `ModPrefix` | `See source` | Registers by hand; returns the type. | [Code](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L43) |
| `RollChance(item)` | Instance | `ModPrefix` | `1` | The weight in the roll (each vanilla prefix weighs 1). | [Code](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L18) |
| `SetStaticDefaults()` | Instance | `ModPrefix` | `undefined` | Once, with content ready and the tables grown (PrefixID.Sets.ReducedNaturalChance[this.Type]). | [Code](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L15) |
| `SetStats(damageMult, knockbackMult, useTimeMult, scaleMult, shootSpeedMult, manaMult, critBonus, tagDamage, armorPenetration)` | Instance | `ModPrefix` | `undefined` | The stats, as Refs (damageMult.value = 1.2). With a single parameter, it receives { damage, knockBack, speed, size, shootSpeed, mana, crit, tagDamage, armorPenetration } (TL's ExMod form). The game applies and checks them, like its own: if some stat does not actually change on the item, the prefix does not take. | [Code](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L25) |

## ModProjectile

[Contract and fields](classes.md#modprojectile).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `AI(proj)` | Instance | `ModProjectile` | `undefined` | Every frame. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L36) |
| `AutoStaticDefaults()` | Instance | `ModProjectile` | `See source` | Como o do tModLoader: roda antes do SetStaticDefaults, com this.Projectile já montado pelo SetDefaults. Sem o projHook, o botão de gancho do celular (QuickGrapple) não acha o item. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L20) |
| `CanCutTiles(proj)` | Instance | `ModProjectile` | `undefined` | Cutting grass and cobwebs. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L63) |
| `CanDamage(proj)` | Instance | `ModProjectile` | `true` | false: deals no damage. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L60) |
| `CanHitNPC(proj, target)` | Instance | `ModProjectile` | `null` | false vetoes, true forces, null leaves it to the game (player projectiles). | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L52) |
| `CanHitPlayer(proj, target)` | Instance | `ModProjectile` | `true` | false vetoes the hit on the player (hostile / PvP). | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L56) |
| `CanHitPvp(proj, target)` | Instance | `ModProjectile` | `true` | false vetoes the hit on the player (hostile / PvP). | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L55) |
| `CanUseGrapple(player, type)` | Instance | `ModProjectile` | `true` | On the template, before launching the hook; false prevents it. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L71) |
| `Clone(newProjectile)` | Instance | `ModProjectile` | `Entities.Clone(this)` | A copy of the instance. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L11) |
| `CloneDefaults(type)` | Instance | `ModProjectile` | `See source` | Copies a vanilla projectile's values. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L87) |
| `get CloneNewInstances` | Getter | `ModProjectile` | `false` | Uma instância para uma entidade nova (o NewInstance do tModLoader): pelo construtor; com CloneNewInstances, uma cópia do molde (Clone). | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L14) |
| `Colliding(proj, projHitbox, targetHitbox)` | Instance | `ModProjectile` | `undefined` | true/false decides the hit; undefined, the game's. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L59) |
| `CutTiles(proj)` | Instance | `ModProjectile` | `undefined` | Cutting grass and cobwebs. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L64) |
| `DefaultToDrillOrChainsaw()` | Instance | `ModProjectile` | `See source` | The game's defaults for each family of held projectiles. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L105) |
| `DefaultToFlail()` | Instance | `ModProjectile` | `See source` | The game's defaults for each family of held projectiles. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L103) |
| `DefaultToKite()` | Instance | `ModProjectile` | `See source` | The game's defaults for each family of held projectiles. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L106) |
| `DefaultToSpear()` | Instance | `ModProjectile` | `See source` | The game's defaults for each family of held projectiles. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L101) |
| `DefaultToWhip()` | Instance | `ModProjectile` | `See source` | The game's defaults for each family of held projectiles. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L104) |
| `DefaultToYoyo()` | Instance | `ModProjectile` | `See source` | The game's defaults for each family of held projectiles. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L102) |
| `GetAlpha(proj, lightColor)` | Instance | `ModProjectile` | `undefined` | The final color; undefined, the game's. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L65) |
| `getByName(name)` | Static | `ModProjectile` | `ProjectileLoader.ByType.get(bl.projectiles.typeOf(name))` | As for items. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L170) |
| `getModProjectile(type)` | Static | `ModProjectile` | `ProjectileLoader.ByType.get(type)` | As for items. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L169) |
| `getTypeByName(name)` | Static | `ModProjectile` | `bl.projectiles.typeOf(name)` | As for items. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L168) |
| `GrappleCanLatchOnTo(proj, player, tile)` | Instance | `ModProjectile` | `undefined` | true/false: latches onto this tile. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L73) |
| `GrapplePullSpeed(proj, player, speed)` | Instance | `ModProjectile` | `undefined` | The speed at which the latched hook pulls the player (Ref; the game's: 11). The first latched hook's applies. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L82) |
| `GrappleRange(proj)` | Instance | `ModProjectile` | `300` | Hook (aiStyle 7): the distance in pixels at which it returns. Default 300, as in tModLoader; with a vanilla hook's AIType, that hook's range applies. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L79) |
| `GrappleRetreatSpeed(proj, player, speed)` | Instance | `ModProjectile` | `undefined` | The return speed (Ref; the game's: 11). | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L81) |
| `GrappleTargetPoint(proj, player, grappleX, grappleY)` | Instance | `ModProjectile` | `undefined` | The point the hook pulls toward (Ref): the sum of the centers of the hooks latched up to this one, as in tModLoader. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L83) |
| `isModProjectile(proj)` | Static | `ModProjectile` | `!!proj && bl.projectiles.isModProjectile(proj.type)` | As for items. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L167) |
| `isModType(type)` | Static | `ModProjectile` | `bl.projectiles.isModProjectile(type)` | As for items. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L166) |
| `MinionContactDamage(proj)` | Instance | `ModProjectile` | `false` | true: a minion or pet hurts on contact. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L61) |
| `ModifyDamageHitbox(proj, hitbox)` | Instance | `ModProjectile` | `undefined` | Change the damage area's Rectangle. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L62) |
| `ModifyHitNPC(proj, target, modifiers)` | Instance | `ModProjectile` | `undefined` | modifiers is a HitModifiers (Hits). | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L53) |
| `ModifyHitPlayer(proj, target, modifiers)` | Instance | `ModProjectile` | `undefined` | modifiers is a HurtModifiers. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L57) |
| `NewInstance(entity)` | Instance | `ModProjectile` | `this.CloneNewInstances ? this.Clone(entity) : Entities.NewInstance(this)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L15) |
| `NumGrappleHooks(proj, player, numHooks)` | Instance | `ModProjectile` | `undefined` | How many stay latched (Ref; the game gives a mod hook 3). Above that, the oldest lets go. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L80) |
| `OnHitNPC(proj, target, hit, damageDone)` | Instance | `ModProjectile` | `undefined` | After the hit; hit is a HitInfo. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L54) |
| `OnHitPlayer(proj, target, info)` | Instance | `ModProjectile` | `undefined` | After the damage; info is a HurtInfo. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L58) |
| `OnKill(proj, timeLeft)` | Instance | `ModProjectile` | `undefined` | On death. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L41) |
| `OnSpawn(proj)` | Instance | `ModProjectile` | `undefined` | Once, on the first frame of life. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L34) |
| `OnTileCollide(proj, oldVelocity)` | Instance | `ModProjectile` | `true` | Hit a tile and was about to die; false keeps it alive. false only prevents the death caused by the collision in this movement. Expiration and NPC hits still go through Kill and OnKill. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L48) |
| `PostAI(proj)` | Instance | `ModProjectile` | `undefined` | After the AI. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L37) |
| `PostDraw(proj, lightColor)` | Instance | `ModProjectile` | `undefined` | After drawing, even with PreDraw false (as in tModLoader). | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L69) |
| `PostSetDefaults(proj)` | Instance | `ModProjectile` | `undefined` | Every projectile of this type that spawns. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L31) |
| `PostSetupContent()` | Instance | `ModProjectile` | `undefined` | Once. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L32) |
| `PostStaticDefaults()` | Instance | `ModProjectile` | `undefined` | Once. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L30) |
| `PreAI(proj)` | Instance | `ModProjectile` | `true` | Before the AI; false skips the game's AI and AI. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L35) |
| `PreDraw(proj, lightColor)` | Instance | `ModProjectile` | `true` | Before drawing; false does not draw the game's sprite (the PreDrawExtras extras remain). | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L68) |
| `PreDrawExtras(proj)` | Instance | `ModProjectile` | `true` | Before PreDraw; false removes what the game draws before the sprite: the hook's chain, the fishing line, the yoyo string, the flail's chain. A hook with its own chain draws it here. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L67) |
| `PreKill(proj, timeLeft)` | Instance | `ModProjectile` | `true` | Before dying; false removes the game's effects. false removes the projectile without the native death effects and ends the flow before OnKill. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L40) |
| `get Projectile` | Getter | `ModProjectile` | `Entities.Of(this)` | game Projectile | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L9) |
| `ReceiveExtraAI(reader)` | Instance | `ModProjectile` | `undefined` | The base implementation does nothing. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L39) |
| `register(cls)` | Static | `ModProjectile` | `See source` | As for items. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L108) |
| `SendExtraAI(writer)` | Instance | `ModProjectile` | `undefined` | The base implementation does nothing. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L38) |
| `SetDefaults(proj)` | Instance | `ModProjectile` | `undefined` | Every projectile of this type that spawns. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L29) |
| `SetStaticDefaults()` | Instance | `ModProjectile` | `undefined` | Once. Main.projFrames[this.Type] = n here counts as the texture's frames. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L28) |
| `ShouldUpdatePosition(proj)` | Instance | `ModProjectile` | `true` | false: the game does not add velocity to position (the mod sets the position: held projectile, laser). | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L85) |
| `TileCollideStyle(proj, width, height, fallThrough, hitboxCenterFrac)` | Instance | `ModProjectile` | `true` | Four Refs: collision size, falling through platforms and center. false skips collision for this movement. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L45) |
| `UseGrapple(player, type)` | Instance | `ModProjectile` | `type` | On the template: return the type to launch. | [Code](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L72) |

## ModPylon

[Contract and fields](classes.md#modpylon-and-temoddedpylon). Inherits from `ModTile`.

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `AddMapEntry(color, name)` | Instance | `ModTile` (inherited) | `See source` | The color and the name on the map; each call is one more option. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L28) |
| `AnimateIndividualTile(type, i, j, frameXOffset, frameYOffset)` | Instance | `ModTile` (inherited) | `undefined` | the native drawing (GetTileDrawData); tile.drawdata mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L77) |
| `AnimateTile(frame, frameCounter)` | Instance | `ModTile` (inherited) | `undefined` | Main.AnimateTiles (only if some tile uses it) | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L76) |
| `CanDrop(i, j)` | Instance | `ModTile` (inherited) | `true` | WorldGen.KillTile, TileFrameImportant; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L70) |
| `CanKillTile(i, j, blockDamaged)` | Instance | `ModTile` (inherited) | `true` | WorldGen.KillTile, TileFrameImportant; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L63) |
| `CanPlacePylon()` | Instance | `ModPylon` | `!Terraria.Main.PylonSystem.HasPylonOfType(this.PylonType)` | The default: only one per type in the world. | [Code](../../app/src/main/cpp/script/js/mod/ModPylon.js#L14) |
| `CreateDust(i, j, type)` | Instance | `ModTile` (inherited) | `true` | WorldGen.KillTile, TileFrameImportant; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L68) |
| `CreateMapEntryName()` | Instance | `ModTile` (inherited) | `this.constructor.name` | The class name. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L52) |
| `DefaultContainerName(frameX, frameY)` | Instance | `ModTile` (inherited) | `this.CreateMapEntryName()` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L114) |
| `DefaultDrawMapIcon(context, mapIcon, drawCenter, drawColor, deselectedScale, selectedScale)` | Instance | `ModPylon` | `PylonLoader.DrawIcon(context, mapIcon, drawCenter, drawColor, deselectedScale, selectedScale)` | tModLoader's helpers (drawing; name and tap). | [Code](../../app/src/main/cpp/script/js/mod/ModPylon.js#L34) |
| `DefaultDrawPylonCrystal(spriteBatch, i, j, crystalTexture, crystalHighlightTexture, crystalOffset, pylonShadowColor, dustColor, dustChanceDenominator, crystalVerticalFrameCount)` | Instance | `ModPylon` | `See source` | The floating crystal, in SpecialDraw (without the smart cursor outline). | [Code](../../app/src/main/cpp/script/js/mod/ModPylon.js#L44) |
| `DefaultMapClickHandle(mouseIsHovering, pylonInfo, hoveringTextKey, mouseOverText)` | Instance | `ModPylon` | `See source` | tModLoader's helpers (drawing; name and tap). | [Code](../../app/src/main/cpp/script/js/mod/ModPylon.js#L39) |
| `DrawEffects(i, j, spriteBatch, drawData)` | Instance | `ModPylon` | `See source` | The default: tapping opens the map; SpecialDraw through the top-left cell. | [Code](../../app/src/main/cpp/script/js/mod/ModPylon.js#L58) |
| `DrawMapIcon(context, mouseOverText, pylonInfo, isNearPylon, drawColor, deselectedScale, selectedScale)` | Instance | `ModPylon` | `See source` | The icon on the map. The default: <Texture>_MapIcon.png (without it, the vanilla pylon's) and a tap teleports. | [Code](../../app/src/main/cpp/script/js/mod/ModPylon.js#L27) |
| `EmitParticles(i, j, tile, tileFrameX, tileFrameY, tileLight, visible)` | Instance | `ModTile` (inherited) | `undefined` | a pass after TileDrawing.PostDrawTiles; tile.draw mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L85) |
| `GetItemDrops(i, j)` | Instance | `ModTile` (inherited) | `undefined` | WorldGen.KillTile, TileFrameImportant; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L72) |
| `GetMapOption(i, j)` | Instance | `ModTile` (inherited) | `0` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L61) |
| `getModTile(type)` | Static | `ModTile` (inherited) | `TileLoader.ByType.get(type)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L145) |
| `getTypeByName(name)` | Static | `ModTile` (inherited) | `bl.tiles.typeOf(name)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L144) |
| `GrowSapling(i, j, underground, treeHeightAddon, ignoreWalls)` | Instance | `ModTile` (inherited) | `See source` | A muda (TileID.Sets.TreeSapling) virando árvore: o RandomUpdate dela chama WorldGen.AttemptToGrowTreeFromSapling(i, j, underground), que chega aqui. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L98) |
| `HasSmartInteract(i, j, settings)` | Instance | `ModPylon` | `true` | The default: tapping opens the map; SpecialDraw through the top-left cell. | [Code](../../app/src/main/cpp/script/js/mod/ModPylon.js#L55) |
| `get HighlightTexture` | Getter | `ModTile` (inherited) | `this.Texture + '_Highlight'` | The frame sheet; the outline (HasOutlines). | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L18) |
| `HitWire(i, j)` | Instance | `ModTile` (inherited) | `undefined` | Wiring.HitWireSingle; tile.wire mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L95) |
| `IsLockedChest(i, j)` | Instance | `ModTile` (inherited) | `false` | Chest.IsLocked/Unlock/Lock; tile.chest mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L111) |
| `isModType(type)` | Static | `ModTile` (inherited) | `bl.tiles.isModTile(type)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L143) |
| `KillMultiTile(i, j, frameX, frameY)` | Instance | `ModTile` (inherited) | `undefined` | WorldGen.KillTile, TileFrameImportant; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L66) |
| `KillSound(i, j, fail)` | Instance | `ModTile` (inherited) | `true` | WorldGen.KillTile, TileFrameImportant; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L69) |
| `KillTile(i, j, fail, effectOnly, noItem)` | Instance | `ModTile` (inherited) | `undefined` | WorldGen.KillTile, TileFrameImportant; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L64) |
| `LockChest(i, j, frameXAdjustment, manual)` | Instance | `ModTile` (inherited) | `false` | Chest.IsLocked/Unlock/Lock; tile.chest mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L113) |
| `get MapIconTexture` | Getter | `ModPylon` | `this.Texture + '_MapIcon'` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModPylon.js#L11) |
| `ModifyLight(i, j, r, g, b)` | Instance | `ModTile` (inherited) | `undefined` | Lighting.LightTiles (every 3 frames) + batched Lighting.AddLight; tile.light mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L75) |
| `ModifySittingTargetInfo(i, j, info)` | Instance | `ModTile` (inherited) | `undefined` | PlayerSittingHelper/PlayerSleepingHelper | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L108) |
| `ModifySleepingTargetInfo(i, j, info)` | Instance | `ModTile` (inherited) | `undefined` | PlayerSittingHelper/PlayerSleepingHelper | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L109) |
| `MouseOver(i, j)` | Instance | `ModTile` (inherited) | `undefined` | Player.TileInteractions; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L89) |
| `MouseOverFar(i, j)` | Instance | `ModTile` (inherited) | `undefined` | Player.TileInteractions; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L90) |
| `NearbyEffects(i, j, closer)` | Instance | `ModTile` (inherited) | `undefined` | SceneMetrics.Scan | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L86) |
| `NumDust(i, j, fail, num)` | Instance | `ModTile` (inherited) | `undefined` | WorldGen.KillTile, TileFrameImportant; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L67) |
| `PlaceInWorld(i, j, item)` | Instance | `ModTile` (inherited) | `undefined` | TileObjectData.CallPostPlacementPlayerHook and WorldGen.PlaceTile (when placing) | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L73) |
| `PostDraw(i, j, spriteBatch)` | Instance | `ModTile` (inherited) | `undefined` | a pass after TileDrawing.PostDrawTiles; tile.draw mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L82) |
| `PostSetDefaults()` | Instance | `ModTile` (inherited) | `undefined` | native, content ready | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L21) |
| `PostSetupContent()` | Instance | `ModTile` (inherited) | `undefined` | native, content ready | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L22) |
| `PreDraw(i, j, spriteBatch)` | Instance | `ModTile` (inherited) | `true` | a pass after TileDrawing.PostDrawTiles; tile.draw mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L81) |
| `RandomUpdate(i, j)` | Instance | `ModTile` (inherited) | `undefined` | WorldGen.UpdateWorld_Tile; tile.random mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L105) |
| `register(cls)` | Static | `ModPylon` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModPylon.js#L65) |
| `RegisterItemDrop(itemType, ...styles)` | Instance | `ModTile` (inherited) | `See source` | The item that drops. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L55) |
| `RightClick(i, j)` | Instance | `ModPylon` | `See source` | The default: tapping opens the map; SpecialDraw through the top-left cell. | [Code](../../app/src/main/cpp/script/js/mod/ModPylon.js#L50) |
| `SetDrawPositions(i, j, width, offsetY, height, tileFrameX, tileFrameY)` | Instance | `ModTile` (inherited) | `undefined` | the native drawing (GetTileDrawData); tile.drawdata mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L78) |
| `SetSpriteEffects(i, j, spriteEffects)` | Instance | `ModTile` (inherited) | `undefined` | the native drawing (GetTileDrawData); tile.drawdata mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L79) |
| `SetStaticDefaults()` | Instance | `ModTile` (inherited) | `undefined` | native, content ready | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L20) |
| `Slope(i, j)` | Instance | `ModTile` (inherited) | `true` | WorldGen.SlopeTile/PoundTile; tile.slope mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L104) |
| `SpecialDraw(i, j, spriteBatch)` | Instance | `ModTile` (inherited) | `undefined` | a pass after TileDrawing.PostDrawTiles; tile.draw mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L84) |
| `TileFrame(i, j, resetFrame, noBreak)` | Instance | `ModTile` (inherited) | `true` | WorldGen.TileFrame; tile.frame mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L106) |
| `UnlockChest(i, j, frameXAdjustment, dustType, manual)` | Instance | `ModTile` (inherited) | `false` | Chest.IsLocked/Unlock/Lock; tile.chest mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L112) |
| `ValidTeleportCheck_BiomeRequirements(pylonInfo, sceneData)` | Instance | `ModPylon` | `true` | The biome around the destination pylon (SceneMetrics scanned there); false refuses with the game's message. | [Code](../../app/src/main/cpp/script/js/mod/ModPylon.js#L23) |
| `ValidTeleportCheck_NPCCount(pylonInfo, defaultNecessaryNPCCount)` | Instance | `ModPylon` | `See source` | NPCs around (the destination and the pylon you leave from). The default is the game's (2 town NPCs). | [Code](../../app/src/main/cpp/script/js/mod/ModPylon.js#L17) |

## ModRarity

[Contract and fields](classes.md#modrarity).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `GetPrefixedRarity(offset, valueMult)` | Instance | `ModRarity` | `this.Type` | The rarity when a prefix moves the item up (1, 2) or down (-1, -2); default this.Type. | [Code](../../app/src/main/cpp/script/js/mod/ModRarity.js#L12) |
| `get RarityColor` | Getter | `ModRarity` | `Color.White` | The name's color (read on every draw: it can flash). | [Code](../../app/src/main/cpp/script/js/mod/ModRarity.js#L10) |
| `register(cls)` | Static | `ModRarity` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModRarity.js#L14) |
| `SetStaticDefaults()` | Instance | `ModRarity` | `undefined` | Once, with the game ready. | [Code](../../app/src/main/cpp/script/js/mod/ModRarity.js#L8) |

## ModRecipe

[Contract and fields](classes.md#modrecipe).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `AddCondition(condition, predicate)` | Instance | `ModRecipe` | `See source` | A Condition: outside it, the recipe leaves the crafting menu. | [Code](../../app/src/main/cpp/script/js/mod/ModRecipe.js#L74) |
| `AddConsumeIngredientCallback(callback)` | Instance | `ModRecipe` | `See source` | How much of each ingredient is spent (amount is Ref; 0: not spent). AddConsumeItemCallback is the old name. | [Code](../../app/src/main/cpp/script/js/mod/ModRecipe.js#L87) |
| `AddConsumeItemCallback(callback)` | Instance | `ModRecipe` | `this.AddConsumeIngredientCallback(callback)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModRecipe.js#L93) |
| `AddCustomShimmerResult(type, stack = 1)` | Instance | `ModRecipe` | `See source` | What comes out in Shimmer. | [Code](../../app/src/main/cpp/script/js/mod/ModRecipe.js#L64) |
| `AddIngredient(type, stack = 1)` | Instance | `ModRecipe` | `See source` | Up to 15. | [Code](../../app/src/main/cpp/script/js/mod/ModRecipe.js#L22) |
| `AddOnCraftCallback(callback)` | Instance | `ModRecipe` | `See source` | After crafting. On mobile consumedItems and destinationStack arrive null. | [Code](../../app/src/main/cpp/script/js/mod/ModRecipe.js#L97) |
| `AddRecipeGroup(group, stack = 1)` | Instance | `ModRecipe` | `See source` | Accepts any item in the group (object, a vanilla name like 'IronBar', or one created by a mod). | [Code](../../app/src/main/cpp/script/js/mod/ModRecipe.js#L36) |
| `AddTile(tileType)` | Instance | `ModRecipe` | `See source` | The station (one per recipe). | [Code](../../app/src/main/cpp/script/js/mod/ModRecipe.js#L53) |
| `constructor()` | Constructor | `ModRecipe` | `instance` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModRecipe.js#L5) |
| `CreateRecipeGroup(name, itemTypes = [])` | Static | `ModRecipe` | `See source` | A new group (in AddRecipeGroups). | [Code](../../app/src/main/cpp/script/js/mod/ModRecipe.js#L141) |
| `GetGroup(groupOrName)` | Static | `ModRecipe` | `See source` | A group by object, name or number. | [Code](../../app/src/main/cpp/script/js/mod/ModRecipe.js#L120) |
| `GetGroupByName(name)` | Static | `ModRecipe` | `ModRecipe.GetGroup(name)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModRecipe.js#L138) |
| `Register()` | Instance | `ModRecipe` | `See source` | Puts it into the game. | [Code](../../app/src/main/cpp/script/js/mod/ModRecipe.js#L108) |
| `RemoveCondition(condition)` | Instance | `ModRecipe` | `See source` | Takes one out (true if it was there). | [Code](../../app/src/main/cpp/script/js/mod/ModRecipe.js#L79) |
| `SetProperty(name, value)` | Instance | `ModRecipe` | `See source` | needWater, needLava, needHoney, needSnowBiome, needGraveyardBiome, needTorchGodsFavor, needMechdusa, notDecraftable, crimson, corruption, alchemy. | [Code](../../app/src/main/cpp/script/js/mod/ModRecipe.js#L103) |
| `SetResult(type, stack = 1)` | Instance | `ModRecipe` | `See source` | What it gives. | [Code](../../app/src/main/cpp/script/js/mod/ModRecipe.js#L15) |

## ModSceneEffect

[Contract and fields](classes.md#modbiome-and-modsceneeffect).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `GetWeight(player)` | Instance | `ModSceneEffect` | `0.5` | 0 a 1: desempata efeitos da mesma Priority. | [Code](../../app/src/main/cpp/script/js/mod/ModSceneEffect.js#L15) |
| `IsSceneEffectActive(player)` | Instance | `ModSceneEffect` | `false` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModSceneEffect.js#L17) |
| `MapBackgroundColor(color)` | Instance | `ModSceneEffect` | `undefined` | Ref (.value, a Color): the final color, after the two above. | [Code](../../app/src/main/cpp/script/js/mod/ModSceneEffect.js#L21) |
| `register(cls)` | Static | `ModSceneEffect` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModSceneEffect.js#L28) |
| `SetStaticDefaults()` | Instance | `ModSceneEffect` | `undefined` | The base implementation does nothing. | [Code](../../app/src/main/cpp/script/js/mod/ModSceneEffect.js#L26) |
| `SpecialVisuals(player, isActive)` | Instance | `ModSceneEffect` | `undefined` | Todo quadro, ativo ou não: é onde se liga e desliga um filtro de tela. | [Code](../../app/src/main/cpp/script/js/mod/ModSceneEffect.js#L24) |

## ModScreenEffect

[Contract and fields](classes.md#screen-effects).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `get Active` | Getter | `ModScreenEffect` | `!this.#killed` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Effects/ModScreenEffect.js#L52) |
| `Draw(cmd)` | Instance | `ModScreenEffect` | `See source` | The life cycle. | [Code](../../app/src/main/cpp/script/js/mod/Effects/ModScreenEffect.js#L40) |
| `DrawDistortion(d)` | Instance | `ModScreenEffect` | `undefined` | The life cycle. | [Code](../../app/src/main/cpp/script/js/mod/Effects/ModScreenEffect.js#L38) |
| `Kill()` | Instance | `ModScreenEffect` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Effects/ModScreenEffect.js#L43) |
| `OnKill()` | Instance | `ModScreenEffect` | `undefined` | The life cycle. | [Code](../../app/src/main/cpp/script/js/mod/Effects/ModScreenEffect.js#L41) |
| `OnSpawn()` | Instance | `ModScreenEffect` | `undefined` | The life cycle. | [Code](../../app/src/main/cpp/script/js/mod/Effects/ModScreenEffect.js#L33) |
| `get Opacity` | Getter | `ModScreenEffect` | `See source` | Time in ticks. | [Code](../../app/src/main/cpp/script/js/mod/Effects/ModScreenEffect.js#L66) |
| `get Progress` | Getter | `ModScreenEffect` | `this.Duration > 0 ? Math.min(this.Age / this.Duration, 1) : 0` | Time in ticks. | [Code](../../app/src/main/cpp/script/js/mod/Effects/ModScreenEffect.js#L57) |
| `register(cls)` | Static | `ModScreenEffect` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Effects/ModScreenEffect.js#L77) |
| `SetDefaults()` | Instance | `ModScreenEffect` | `undefined` | The life cycle. | [Code](../../app/src/main/cpp/script/js/mod/Effects/ModScreenEffect.js#L32) |
| `ShouldDraw()` | Instance | `ModScreenEffect` | `true` | The life cycle. | [Code](../../app/src/main/cpp/script/js/mod/Effects/ModScreenEffect.js#L36) |
| `Spawn(center = null, options = {})` | Static | `ModScreenEffect` | `ScreenEffects.Spawn(this, center, options)` | center: a position, an entity (followed) or null (whole screen); options in camelCase. | [Code](../../app/src/main/cpp/script/js/mod/Effects/ModScreenEffect.js#L73) |
| `get TimeLeft` | Getter | `ModScreenEffect` | `this.Duration > 0 ? Math.max(this.Duration - this.Age, 0) : Infinity` | Time in ticks. | [Code](../../app/src/main/cpp/script/js/mod/Effects/ModScreenEffect.js#L61) |
| `Tween(property, to, duration, easing = Easing.Linear, options = {})` | Instance | `ModScreenEffect` | `ScreenTween.To(this, property, to, duration, easing, options)` | Anima uma propriedade do efeito (ScreenUtils.Tween.To). | [Code](../../app/src/main/cpp/script/js/mod/Effects/ModScreenEffect.js#L48) |
| `Update()` | Instance | `ModScreenEffect` | `undefined` | The life cycle. | [Code](../../app/src/main/cpp/script/js/mod/Effects/ModScreenEffect.js#L35) |

## ModSurfaceBackgroundStyle

[Contract and fields](classes.md#mod-backgrounds).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `ChooseCloseTexture(scale, parallax, a, b)` | Instance | `ModSurfaceBackgroundStyle` | `-1` | scale, parallax, a e b são Ref (.value), como os ref do tModLoader: escala 1.25, parallax 0.37, e a altura = a × (posição da tela) + b. | [Code](../../app/src/main/cpp/script/js/mod/ModSurfaceBackgroundStyle.js#L25) |
| `ChooseFarTexture()` | Instance | `ModSurfaceBackgroundStyle` | `-1` | O número da textura, ou -1 para não desenhar essa camada. | [Code](../../app/src/main/cpp/script/js/mod/ModSurfaceBackgroundStyle.js#L17) |
| `ChooseMiddleTexture()` | Instance | `ModSurfaceBackgroundStyle` | `-1` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModSurfaceBackgroundStyle.js#L18) |
| `ModifyFarFades(fades, transitionSpeed)` | Instance | `ModSurfaceBackgroundStyle` | `See source` | fades: um por estilo (Main.bgAlphaFrontLayer). O padrão sobe o deste estilo e desce os outros, o que o Example Mod do tModLoader escreve. | [Code](../../app/src/main/cpp/script/js/mod/ModSurfaceBackgroundStyle.js#L8) |
| `PreDrawCloseBackground(spriteBatch)` | Instance | `ModSurfaceBackgroundStyle` | `true` | false: não desenha a camada da frente (o mod desenha a dele aqui). | [Code](../../app/src/main/cpp/script/js/mod/ModSurfaceBackgroundStyle.js#L21) |
| `register(cls)` | Static | `ModSurfaceBackgroundStyle` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModSurfaceBackgroundStyle.js#L29) |
| `SetStaticDefaults()` | Instance | `ModSurfaceBackgroundStyle` | `undefined` | The base implementation does nothing. | [Code](../../app/src/main/cpp/script/js/mod/ModSurfaceBackgroundStyle.js#L27) |

## ModSystem

[Contract and fields](classes.md#modsystem).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `AddRecipeGroups()` | Instance | `ModSystem` | `undefined` | Once, before any recipe. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L9) |
| `AddRecipes()` | Instance | `ModSystem` | `undefined` | Once, with the game's recipes ready. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L10) |
| `CanWorldBePlayed(playerData, worldData)` | Instance | `ModSystem` | `true` | false blocks the character and world combination. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L22) |
| `ClearWorld()` | Instance | `ModSystem` | `undefined` | On entering any world (and before generating one). | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L30) |
| `HijackGetData(messageType, reader, playerNumber)` | Instance | `ModSystem` | `false` | Type and reader Refs; any true cancels the packet. Ref of the byte and the native BinaryReader. Position goes back to 1 between systems. true cancels the whole packet; the client/server is MessageBuffer.whoAmI. Replacing the reader copies its payload. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L72) |
| `HijackSendData(whoAmI, msgType, remoteClient, ignoreClient, text, number, number2, number3, number4, number5, number6, number7)` | Instance | `ModSystem` | `false` | Any true cancels sending and the entity envelope. Any true cancels SendData and the entity envelope. whoAmI is the buffer index: remoteClient on the server's targeted send, 256 in the other cases. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L73) |
| `Load()` | Instance | `ModSystem` | `undefined` | Load antes do OnModLoad, como o ModType do tModLoader (onde se registram os ModTouchButton/ModKeybind). | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L4) |
| `LoadWorldData(tag)` | Instance | `ModSystem` | `undefined` | After OnWorldLoad, with the world's data. Only on the server or in single player. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L18) |
| `ModifyHardmodeTasks(tasks)` | Instance | `ModSystem` | `undefined` | A JS array; a single native Hardmode Conversion step. Mobile adaptation: a single default Hardmode Conversion step, which keeps initializeHardMode. Allows inserting, removing, reordering and disabling steps; does not expose Good/Evil/Walls separately. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L29) |
| `ModifyLightingBrightness(scale)` | Instance | `ModSystem` | `undefined` | A brightness scale Ref after the native calculation. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L59) |
| `ModifyScreenPosition()` | Instance | `ModSystem` | `undefined` | After the camera and ModPlayer, inside the world; change Main.screenPosition. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L60) |
| `ModifySunLightColor(tileColor, backgroundColor)` | Instance | `ModSystem` | `undefined` | Two color Refs after the native calculation. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L58) |
| `ModifyTimeRate(timeRate, tileUpdateRate, eventUpdateRate)` | Instance | `ModSystem` | `undefined` | Three independent Refs; double clock, tiles and events with fractional accumulation. Three independent Refs. The clock uses double; tiles/events accumulate fractions and write mobile's integer rates. Non-finite, negative values or values above Int32.MaxValue keep the native value. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L57) |
| `ModifyTransformMatrix(transform)` | Instance | `ModSystem` | `undefined` | A SpriteViewMatrix Ref after the zoom, before the matrix is read. Ref of Terraria.Graphics.SpriteViewMatrix; applied after the native zoom and before drawing. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L61) |
| `ModifyWorldGenTasks(tasks)` | Instance | `ModSystem` | `undefined` | A JS array of GenPass: insert, remove, reorder, change weight and disable. tasks is a JS array of GenPass. The native list is replaced before execution; weight and Enabled stay native. Signature of the local tModLoader copy: tasks only. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L27) |
| `NetReceive(reader)` | Instance | `ModSystem` | `undefined` | Network: the server sends it along with the world data (on joining and on each sync); the client reads. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L71) |
| `NetSend(writer)` | Instance | `ModSystem` | `undefined` | Network: the server sends it along with the world data (on joining and on each sync); the client reads. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L70) |
| `OnLocalizationsLoaded()` | Instance | `ModSystem` | `undefined` | After applying the mods' texts, initially and on each language change. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L7) |
| `OnModLoad()` | Instance | `ModSystem` | `undefined` | On register. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L5) |
| `OnModUnload()` | Instance | `ModSystem` | `undefined` | Once on a normal exit or on the JS engine's shutdown. Once on a normal exit or on the JS engine's shutdown; a forced process termination by the system does not run callbacks. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L6) |
| `OnWorldLoad()` | Instance | `ModSystem` | `undefined` | The world opened; on the client, when it arrives from the server. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L17) |
| `OnWorldUnload()` | Instance | `ModSystem` | `undefined` | After leaving. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L32) |
| `PostAddRecipes()` | Instance | `ModSystem` | `undefined` | Once, with the game's recipes ready. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L11) |
| `PostSetupContent()` | Instance | `ModSystem` | `undefined` | Once, with all mod content in the game. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L13) |
| `PostSetupRecipes()` | Instance | `ModSystem` | `undefined` | After every recipe, post-hooks and caches. After every recipe, PostAddRecipes and the rebuild of the native caches. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L12) |
| `PostUpdateDusts()` | Instance | `ModSystem` | `undefined` | Before and after updating particles. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L54) |
| `PostUpdateEverything()` | Instance | `ModSystem` | `undefined` | Every frame, on everyone. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L39) |
| `PostUpdateGores()` | Instance | `ModSystem` | `undefined` | Once at the edges of the gore group. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L48) |
| `PostUpdateInput()` | Instance | `ModSystem` | `undefined` | After input processing. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L41) |
| `PostUpdateInvasions()` | Instance | `ModSystem` | `undefined` | Before and after the invasion routine. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L56) |
| `PostUpdateItems()` | Instance | `ModSystem` | `undefined` | Once at the edges of the WorldItem group. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L52) |
| `PostUpdateNPCs()` | Instance | `ModSystem` | `undefined` | Once at the edges of the NPC group. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L46) |
| `PostUpdatePlayers()` | Instance | `ModSystem` | `undefined` | Once at the edges of the player group. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L44) |
| `PostUpdateProjectiles()` | Instance | `ModSystem` | `undefined` | Once at the edges of the projectile group. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L50) |
| `PostUpdateTime()` | Instance | `ModSystem` | `undefined` | Every frame. Only on the server or in single player. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L38) |
| `PostUpdateWorld()` | Instance | `ModSystem` | `undefined` | Every frame. Only on the server or in single player. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L36) |
| `PostWorldGen()` | Instance | `ModSystem` | `undefined` | After a completed generation; cancelling does not call it. Only when the native generator returns true; does not run on a cancelled generation. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L28) |
| `PostWorldLoad()` | Instance | `ModSystem` | `undefined` | After the data. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L19) |
| `PreSaveAndQuit()` | Instance | `ModSystem` | `undefined` | On leaving, before saving. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L31) |
| `PreUpdateDusts()` | Instance | `ModSystem` | `undefined` | Before and after updating particles. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L53) |
| `PreUpdateEntities()` | Instance | `ModSystem` | `undefined` | Before the entity groups. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L42) |
| `PreUpdateGores()` | Instance | `ModSystem` | `undefined` | Once at the edges of the gore group. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L47) |
| `PreUpdateInvasions()` | Instance | `ModSystem` | `undefined` | Before and after the invasion routine. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L55) |
| `PreUpdateItems()` | Instance | `ModSystem` | `undefined` | Once at the edges of the WorldItem group. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L51) |
| `PreUpdateNPCs()` | Instance | `ModSystem` | `undefined` | Once at the edges of the NPC group. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L45) |
| `PreUpdatePlayers()` | Instance | `ModSystem` | `undefined` | Once at the edges of the player group. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L43) |
| `PreUpdateProjectiles()` | Instance | `ModSystem` | `undefined` | Once at the edges of the projectile group. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L49) |
| `PreUpdateTime()` | Instance | `ModSystem` | `undefined` | Every frame. Only on the server or in single player. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L37) |
| `PreUpdateWorld()` | Instance | `ModSystem` | `undefined` | Every frame. Only on the server or in single player. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L35) |
| `PreWorldGen()` | Instance | `ModSystem` | `undefined` | Before running the generation list already built by mobile. Runs before the WorldGenerator steps, after the game builds its generation list. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L26) |
| `register(cls)` | Static | `ModSystem` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L75) |
| `RequiresScreenTarget` | Not implemented | Not implemented | — | The mobile renderer uses Unity, does not call FilterManager.CanCapture and has no Main.finalScreenTarget. tModLoader-compatible automatic capture was dropped. | [Code](../../tools/tests/modsystemhooks/README.md#dropped-method) |
| `ResetNearbyTileEffects()` | Instance | `ModSystem` | `undefined` | Before the game counts the blocks around the local player (every 5 frames), and also on leaving the world and loading another (the previous count does not apply to the new one). | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L67) |
| `ResizeArrays()` | Instance | `ModSystem` | `undefined` | Content ready, before the setup phases. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L8) |
| `SaveWorldData(tag)` | Instance | `ModSystem` | `undefined` | On each world save. Only on the server or in single player. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L20) |
| `SaveWorldHeader(tag)` | Instance | `ModSystem` | `undefined` | Header in .wld.bl.header.json, separate from the full data. TagCompound in <world>.wld.bl.header.json, separate from the full data; TryGetHeaderData accepts a ModSystem instance or class. No cloud persistence. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L21) |
| `TileCountsAvailable(tileCounts)` | Instance | `ModSystem` | `undefined` | With the count ready: tileCounts[type] is how many blocks of that type are around. Valid during the call: keep the number, not the array. Only the local player's scan (the pylons' and the camera's do not call it). | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L68) |
| `UpdateUI(gameTime)` | Instance | `ModSystem` | `undefined` | After the in-world UI update, even while paused. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L40) |
| `WorldCanBePlayedRejectionMessage(playerData, worldData)` | Instance | `ModSystem` | `'O personagem ' + playerData.Name + ' não pode entrar no mundo ' + worldData.Name + '.'` | The message from the first system that blocked. | [Code](../../app/src/main/cpp/script/js/mod/ModSystem.js#L23) |

## ModTile

[Contract and fields](classes.md#modtile).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `AddMapEntry(color, name)` | Instance | `ModTile` | `See source` | The color and the name on the map; each call is one more option. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L28) |
| `AnimateIndividualTile(type, i, j, frameXOffset, frameYOffset)` | Instance | `ModTile` | `undefined` | the native drawing (GetTileDrawData); tile.drawdata mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L77) |
| `AnimateTile(frame, frameCounter)` | Instance | `ModTile` | `undefined` | Main.AnimateTiles (only if some tile uses it) | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L76) |
| `CanDrop(i, j)` | Instance | `ModTile` | `true` | WorldGen.KillTile, TileFrameImportant; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L70) |
| `CanKillTile(i, j, blockDamaged)` | Instance | `ModTile` | `true` | WorldGen.KillTile, TileFrameImportant; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L63) |
| `CreateDust(i, j, type)` | Instance | `ModTile` | `true` | WorldGen.KillTile, TileFrameImportant; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L68) |
| `CreateMapEntryName()` | Instance | `ModTile` | `this.constructor.name` | The class name. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L52) |
| `DefaultContainerName(frameX, frameY)` | Instance | `ModTile` | `this.CreateMapEntryName()` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L114) |
| `DrawEffects(i, j, spriteBatch, drawData)` | Instance | `ModTile` | `undefined` | a pass after TileDrawing.PostDrawTiles; tile.draw mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L83) |
| `EmitParticles(i, j, tile, tileFrameX, tileFrameY, tileLight, visible)` | Instance | `ModTile` | `undefined` | a pass after TileDrawing.PostDrawTiles; tile.draw mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L85) |
| `GetItemDrops(i, j)` | Instance | `ModTile` | `undefined` | WorldGen.KillTile, TileFrameImportant; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L72) |
| `GetMapOption(i, j)` | Instance | `ModTile` | `0` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L61) |
| `getModTile(type)` | Static | `ModTile` | `TileLoader.ByType.get(type)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L145) |
| `getTypeByName(name)` | Static | `ModTile` | `bl.tiles.typeOf(name)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L144) |
| `GrowSapling(i, j, underground, treeHeightAddon, ignoreWalls)` | Instance | `ModTile` | `See source` | A muda (TileID.Sets.TreeSapling) virando árvore: o RandomUpdate dela chama WorldGen.AttemptToGrowTreeFromSapling(i, j, underground), que chega aqui. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L98) |
| `HasSmartInteract(i, j, settings)` | Instance | `ModTile` | `Hooks.Overrides(this.constructor, ModTile, 'RightClick')` | Se o tile é alvo do smart-interact. No celular o toque num móvel passa por ele (é o clique direito do PC): sem sobrescrever, vale para quem tem RightClick. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L94) |
| `get HighlightTexture` | Getter | `ModTile` | `this.Texture + '_Highlight'` | The frame sheet; the outline (HasOutlines). | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L18) |
| `HitWire(i, j)` | Instance | `ModTile` | `undefined` | Wiring.HitWireSingle; tile.wire mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L95) |
| `IsLockedChest(i, j)` | Instance | `ModTile` | `false` | Chest.IsLocked/Unlock/Lock; tile.chest mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L111) |
| `isModType(type)` | Static | `ModTile` | `bl.tiles.isModTile(type)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L143) |
| `KillMultiTile(i, j, frameX, frameY)` | Instance | `ModTile` | `undefined` | WorldGen.KillTile, TileFrameImportant; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L66) |
| `KillSound(i, j, fail)` | Instance | `ModTile` | `true` | WorldGen.KillTile, TileFrameImportant; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L69) |
| `KillTile(i, j, fail, effectOnly, noItem)` | Instance | `ModTile` | `undefined` | WorldGen.KillTile, TileFrameImportant; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L64) |
| `LockChest(i, j, frameXAdjustment, manual)` | Instance | `ModTile` | `false` | Chest.IsLocked/Unlock/Lock; tile.chest mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L113) |
| `ModifyLight(i, j, r, g, b)` | Instance | `ModTile` | `undefined` | Lighting.LightTiles (every 3 frames) + batched Lighting.AddLight; tile.light mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L75) |
| `ModifySittingTargetInfo(i, j, info)` | Instance | `ModTile` | `undefined` | PlayerSittingHelper/PlayerSleepingHelper | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L108) |
| `ModifySleepingTargetInfo(i, j, info)` | Instance | `ModTile` | `undefined` | PlayerSittingHelper/PlayerSleepingHelper | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L109) |
| `MouseOver(i, j)` | Instance | `ModTile` | `undefined` | Player.TileInteractions; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L89) |
| `MouseOverFar(i, j)` | Instance | `ModTile` | `undefined` | Player.TileInteractions; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L90) |
| `NearbyEffects(i, j, closer)` | Instance | `ModTile` | `undefined` | SceneMetrics.Scan | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L86) |
| `NumDust(i, j, fail, num)` | Instance | `ModTile` | `undefined` | WorldGen.KillTile, TileFrameImportant; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L67) |
| `PlaceInWorld(i, j, item)` | Instance | `ModTile` | `undefined` | TileObjectData.CallPostPlacementPlayerHook and WorldGen.PlaceTile (when placing) | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L73) |
| `PostDraw(i, j, spriteBatch)` | Instance | `ModTile` | `undefined` | a pass after TileDrawing.PostDrawTiles; tile.draw mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L82) |
| `PostSetDefaults()` | Instance | `ModTile` | `undefined` | native, content ready | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L21) |
| `PostSetupContent()` | Instance | `ModTile` | `undefined` | native, content ready | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L22) |
| `PreDraw(i, j, spriteBatch)` | Instance | `ModTile` | `true` | a pass after TileDrawing.PostDrawTiles; tile.draw mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L81) |
| `RandomUpdate(i, j)` | Instance | `ModTile` | `undefined` | WorldGen.UpdateWorld_Tile; tile.random mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L105) |
| `register(cls)` | Static | `ModTile` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L116) |
| `RegisterItemDrop(itemType, ...styles)` | Instance | `ModTile` | `See source` | The item that drops. | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L55) |
| `RightClick(i, j)` | Instance | `ModTile` | `false` | Player.TileInteractions; tile filter | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L88) |
| `SetDrawPositions(i, j, width, offsetY, height, tileFrameX, tileFrameY)` | Instance | `ModTile` | `undefined` | the native drawing (GetTileDrawData); tile.drawdata mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L78) |
| `SetSpriteEffects(i, j, spriteEffects)` | Instance | `ModTile` | `undefined` | the native drawing (GetTileDrawData); tile.drawdata mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L79) |
| `SetStaticDefaults()` | Instance | `ModTile` | `undefined` | native, content ready | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L20) |
| `Slope(i, j)` | Instance | `ModTile` | `true` | WorldGen.SlopeTile/PoundTile; tile.slope mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L104) |
| `SpecialDraw(i, j, spriteBatch)` | Instance | `ModTile` | `undefined` | a pass after TileDrawing.PostDrawTiles; tile.draw mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L84) |
| `TileFrame(i, j, resetFrame, noBreak)` | Instance | `ModTile` | `true` | WorldGen.TileFrame; tile.frame mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L106) |
| `UnlockChest(i, j, frameXAdjustment, dustType, manual)` | Instance | `ModTile` | `false` | Chest.IsLocked/Unlock/Lock; tile.chest mark | [Code](../../app/src/main/cpp/script/js/mod/ModTile.js#L112) |

## ModTileEntity

[Contract and fields](classes.md#modtileentity).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `get ByID` | Static getter | `ModTileEntity` | `TileEntityLoader.ByID` | The ones that exist (Map). | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L11) |
| `get ByPosition` | Static getter | `ModTileEntity` | `TileEntityLoader.ByPosition` | The ones that exist (Map). | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L12) |
| `CountInWorld()` | Static | `ModTileEntity` | `TileEntityLoader.ByID.size` | The ones that exist (Map). | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L62) |
| `Find(i, j)` | Instance | `ModTileEntity` | `See source` | On the template: places it (returns the ID), removes it, and the ID of the one of this type at (i, j) (or -1). | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L47) |
| `get FullName` | Getter | `ModTileEntity` | `(this.Mod ? this.Mod.id \|\| this.Mod.uuid : 'BunnyLoader') + '/' + this.Name` | Fields. Position is the object's top-left corner. | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L21) |
| `Generic_Hook_AfterPlacement(i, j, type, style, direction, alternate)` | Instance | `ModTileEntity` | `TileEntityLoader.AfterPlacement(this, i, j, type, style, alternate)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L58) |
| `get Generic_HookPostPlaceMyPlayer` | Getter | `ModTileEntity` | `TileEntityLoader.PlacementHook(this, false, true)` | For TileObjectData.newTile.HookPostPlaceMyPlayer: the entity is created at the top-left corner of what the player placed (on the client, the server places and sends it). | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L54) |
| `Hook_AfterPlacement(i, j, type, style, direction, alternate)` | Instance | `ModTileEntity` | `-1` | The same with a custom hook. | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L41) |
| `IsTileValidForEntity(x, y)` | Instance | `ModTileEntity` | `false` | Required: is the block at (x, y) still this entity's? One that fails is discarded. | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L25) |
| `Kill(i, j)` | Instance | `ModTileEntity` | `See source` | On the template: places it (returns the ID), removes it, and the ID of the one of this type at (i, j) (or -1). | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L45) |
| `Load()` | Instance | `ModTileEntity` | `undefined` | On registration. | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L27) |
| `LoadData(tag)` | Instance | `ModTileEntity` | `undefined` | TagCompound. | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L36) |
| `get Name` | Getter | `ModTileEntity` | `this.constructor.name` | Fields. Position is the object's top-left corner. | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L20) |
| `NetReceive(reader)` | Instance | `ModTileEntity` | `undefined` | What goes from the server to the clients. To send again: NetMessage.SendData(86, -1, -1, null, this.ID, ...) (tModLoader's TileEntitySharing). | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L39) |
| `NetSend(writer)` | Instance | `ModTileEntity` | `undefined` | What goes from the server to the clients. To send again: NetMessage.SendData(86, -1, -1, null, this.ID, ...) (tModLoader's TileEntitySharing). | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L38) |
| `OnKill()` | Instance | `ModTileEntity` | `undefined` | Only through Kill(i, j). | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L34) |
| `OnNetPlace()` | Instance | `ModTileEntity` | `undefined` | On the server, placed at a client's request, before going to everyone. | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L32) |
| `Place(i, j)` | Instance | `ModTileEntity` | `TileEntityLoader.Place(this, i, j)` | On the template: places it (returns the ID), removes it, and the ID of the one of this type at (i, j) (or -1). | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L44) |
| `PlacementHook(processedCoordinates = false)` | Instance | `ModTileEntity` | `TileEntityLoader.PlacementHook(this, true, processedCoordinates)` | The same with a custom hook. | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L56) |
| `PostGlobalUpdate()` | Instance | `ModTileEntity` | `undefined` | On every world update, on the server or in single player (never on the client). | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L30) |
| `PreGlobalUpdate()` | Instance | `ModTileEntity` | `undefined` | On every world update, on the server or in single player (never on the client). | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L29) |
| `register(cls)` | Static | `ModTileEntity` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L72) |
| `SaveData(tag)` | Instance | `ModTileEntity` | `undefined` | TagCompound. | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L35) |
| `TryGet(i, j, cls = ModTileEntity)` | Static | `ModTileEntity` | `See source` | The entity of the object at (i, j) (any cell) if it is of that class; otherwise null. | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L66) |
| `Update()` | Instance | `ModTileEntity` | `undefined` | On every world update, on the server or in single player (never on the client). | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L28) |

## ModTouchButton

[Contract and fields](classes.md#modtouchbutton).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `constructor(entry)` | Constructor | `ModTouchButton` | `instance` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModTouchButton.js#L11) |
| `get Current` | Getter | `ModTouchButton` | `TouchButtonLoader.State(this.#entry).current` | The state in the current update. Read them in ModPlayer.ProcessTriggers. | [Code](../../app/src/main/cpp/script/js/mod/ModTouchButton.js#L27) |
| `get DisplayName` | Getter | `ModTouchButton` | `See source` | { Key, Value }, from TouchButtons.<Name>.DisplayName (or Keybinds.). | [Code](../../app/src/main/cpp/script/js/mod/ModTouchButton.js#L21) |
| `get FullName` | Getter | `ModTouchButton` | `this.#entry.key` | FullName is 'modid/Name', the saved name. | [Code](../../app/src/main/cpp/script/js/mod/ModTouchButton.js#L19) |
| `GetAssignedKeys()` | Instance | `ModTouchButton` | `this.IsBound ? [TouchButtonLoader.Label(this.#entry)] : []` | The button's name when it is in the layout, or []. | [Code](../../app/src/main/cpp/script/js/mod/ModTouchButton.js#L51) |
| `get IsBound` | Getter | `ModTouchButton` | `TouchButtonLoader.IsBound(this.#entry)` | Whether it is in the layout in use. | [Code](../../app/src/main/cpp/script/js/mod/ModTouchButton.js#L47) |
| `get JustPressed` | Getter | `ModTouchButton` | `TouchButtonLoader.State(this.#entry).justPressed` | The state in the current update. Read them in ModPlayer.ProcessTriggers. | [Code](../../app/src/main/cpp/script/js/mod/ModTouchButton.js#L30) |
| `get JustReleased` | Getter | `ModTouchButton` | `TouchButtonLoader.State(this.#entry).justReleased` | The state in the current update. Read them in ModPlayer.ProcessTriggers. | [Code](../../app/src/main/cpp/script/js/mod/ModTouchButton.js#L33) |
| `get Mod` | Getter | `ModTouchButton` | `this.#entry.mod` | FullName is 'modid/Name', the saved name. | [Code](../../app/src/main/cpp/script/js/mod/ModTouchButton.js#L15) |
| `get Name` | Getter | `ModTouchButton` | `this.#entry.name` | FullName is 'modid/Name', the saved name. | [Code](../../app/src/main/cpp/script/js/mod/ModTouchButton.js#L16) |
| `get Old` | Getter | `ModTouchButton` | `TouchButtonLoader.State(this.#entry).old` | The state in the current update. Read them in ModPlayer.ProcessTriggers. | [Code](../../app/src/main/cpp/script/js/mod/ModTouchButton.js#L36) |
| `get RetroCurrent` | Getter | `ModTouchButton` | `See source` | Current, false with the chat, a conversation or a sign open. | [Code](../../app/src/main/cpp/script/js/mod/ModTouchButton.js#L39) |
| `toString()` | Instance | `ModTouchButton` | `'ModTouchButton(' + this.#entry.key + ')'` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModTouchButton.js#L55) |

## ModTree

[Contract and fields](classes.md#modtree).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `CanDropAcorn()` | Instance | `ModTree` | `true` | The wood when cutting; the acorn from the top. | [Code](../../app/src/main/cpp/script/js/mod/ModTree.js#L27) |
| `get CountsAsTreeType` | Getter | `ModTree` | `TreeTypes.Custom` | The TreeTypes the game sees; the default is TreeTypes.Custom. | [Code](../../app/src/main/cpp/script/js/mod/ModTree.js#L20) |
| `CreateDust()` | Instance | `ModTree` | `7` | The dust when hitting. | [Code](../../app/src/main/cpp/script/js/mod/ModTree.js#L22) |
| `DropWood()` | Instance | `ModTree` | `Terraria.ID.ItemID.Wood` | The wood when cutting; the acorn from the top. | [Code](../../app/src/main/cpp/script/js/mod/ModTree.js#L34) |
| `GetBranchTextures()` | Instance | `ModTree` | `TreeLoader.Texture(this, '_Branches')` | The trunk (a 176-wide strip), the tops (3 of 80x80) and the branches (2x3 of 40x40). The default: <Class>.png, _Tops, _Branches. | [Code](../../app/src/main/cpp/script/js/mod/ModTree.js#L17) |
| `GetTexture()` | Instance | `ModTree` | `TreeLoader.Texture(this, '')` | The trunk (a 176-wide strip), the tops (3 of 80x80) and the branches (2x3 of 40x40). The default: <Class>.png, _Tops, _Branches. | [Code](../../app/src/main/cpp/script/js/mod/ModTree.js#L15) |
| `GetTopTextures()` | Instance | `ModTree` | `TreeLoader.Texture(this, '_Tops')` | The trunk (a 176-wide strip), the tops (3 of 80x80) and the branches (2x3 of 40x40). The default: <Class>.png, _Tops, _Branches. | [Code](../../app/src/main/cpp/script/js/mod/ModTree.js#L16) |
| `register(cls)` | Static | `ModTree` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModTree.js#L36) |
| `SaplingGrowthType(style)` | Instance | `ModTree` | `See source` | The sapling an acorn becomes on its ground (style: Ref). | [Code](../../app/src/main/cpp/script/js/mod/ModTree.js#L29) |
| `SetStaticDefaults()` | Instance | `ModTree` | `undefined` | When content loads, on the game thread (it can request textures). | [Code](../../app/src/main/cpp/script/js/mod/ModTree.js#L14) |
| `Shake(x, y, createLeaves)` | Instance | `ModTree` | `true` | On shaking (the top at (x, y)); false removes the game's drops. | [Code](../../app/src/main/cpp/script/js/mod/ModTree.js#L26) |
| `TreeLeaf()` | Instance | `ModTree` | `-1` | The leaf gore; -1 none. | [Code](../../app/src/main/cpp/script/js/mod/ModTree.js#L24) |

## ModUndergroundBackgroundStyle

[Contract and fields](classes.md#mod-backgrounds).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `FillTextureArray(textureSlots)` | Instance | `ModUndergroundBackgroundStyle` | `undefined` | textureSlots[0]: a borda entre o céu e a terra (160x16); [1]: a terra; [2]: a borda entre a terra e a pedra (160x16); [3]: a pedra (160x96). [4]: a passagem para o inferno (vem com a do jogo). Números do BackgroundTextureLoader.GetBackgroundSlot. | [Code](../../app/src/main/cpp/script/js/mod/ModUndergroundBackgroundStyle.js#L9) |
| `register(cls)` | Static | `ModUndergroundBackgroundStyle` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModUndergroundBackgroundStyle.js#L13) |
| `SetStaticDefaults()` | Instance | `ModUndergroundBackgroundStyle` | `undefined` | The base implementation does nothing. | [Code](../../app/src/main/cpp/script/js/mod/ModUndergroundBackgroundStyle.js#L11) |

## ModWall

[Contract and fields](classes.md#modwall).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `AddMapEntry(color, name)` | Instance | `ModWall` | `See source` | The color and the name on the map. | [Code](../../app/src/main/cpp/script/js/mod/ModWall.js#L12) |
| `AnimateWall(frame, frameCounter)` | Instance | `ModWall` | `undefined` | The frame (Main.wallFrame), per type, every frame. | [Code](../../app/src/main/cpp/script/js/mod/ModWall.js#L42) |
| `CreateDust(i, j, type)` | Instance | `ModWall` | `true` | Each dust particle. | [Code](../../app/src/main/cpp/script/js/mod/ModWall.js#L38) |
| `CreateMapEntryName()` | Instance | `ModWall` | `this.constructor.name` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModWall.js#L34) |
| `Drop(i, j, type)` | Instance | `ModWall` | `true` | The item that drops; false nothing. | [Code](../../app/src/main/cpp/script/js/mod/ModWall.js#L40) |
| `getModWall(type)` | Static | `ModWall` | `WallLoader.ByType.get(type)` | As for tiles. | [Code](../../app/src/main/cpp/script/js/mod/ModWall.js#L74) |
| `getTypeByName(name)` | Static | `ModWall` | `bl.walls.typeOf(name)` | As for tiles. | [Code](../../app/src/main/cpp/script/js/mod/ModWall.js#L73) |
| `isModType(type)` | Static | `ModWall` | `bl.walls.isModWall(type)` | As for tiles. | [Code](../../app/src/main/cpp/script/js/mod/ModWall.js#L72) |
| `KillSound(i, j, fail)` | Instance | `ModWall` | `true` | false silences it. | [Code](../../app/src/main/cpp/script/js/mod/ModWall.js#L39) |
| `KillWall(i, j, fail)` | Instance | `ModWall` | `undefined` | On hitting or breaking (fail, num: Ref). | [Code](../../app/src/main/cpp/script/js/mod/ModWall.js#L36) |
| `ModifyLight(i, j, r, g, b)` | Instance | `ModWall` | `undefined` | The wall's light (Ref), near the screen. | [Code](../../app/src/main/cpp/script/js/mod/ModWall.js#L41) |
| `NumDust(i, j, fail, num)` | Instance | `ModWall` | `undefined` | On hitting or breaking (fail, num: Ref). | [Code](../../app/src/main/cpp/script/js/mod/ModWall.js#L37) |
| `PostSetupContent()` | Instance | `ModWall` | `undefined` | The base implementation does nothing. | [Code](../../app/src/main/cpp/script/js/mod/ModWall.js#L10) |
| `RandomUpdate(i, j)` | Instance | `ModWall` | `undefined` | Random update. | [Code](../../app/src/main/cpp/script/js/mod/ModWall.js#L43) |
| `register(cls)` | Static | `ModWall` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModWall.js#L46) |
| `SetStaticDefaults()` | Instance | `ModWall` | `undefined` | With the type already in the tables (Main.wallHouse, WallID.Sets). | [Code](../../app/src/main/cpp/script/js/mod/ModWall.js#L9) |
| `WallFrame(i, j, randomizeFrame, style, frameNumber)` | Instance | `ModWall` | `true` | The border's frame; false keeps the previous one. | [Code](../../app/src/main/cpp/script/js/mod/ModWall.js#L44) |

## ModWaterStyle

[Contract and fields](classes.md#mod-water).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `BiomeHairColor()` | Instance | `ModWaterStyle` | `Color.new(28, 216, 94, 255)` | A cor da tintura de bioma (a do cabelo) com esta água. O padrão é a da floresta, como no tModLoader. | [Code](../../app/src/main/cpp/script/js/mod/ModWaterStyle.js#L39) |
| `ChooseWaterfallStyle()` | Instance | `ModWaterStyle` | `0` | O número da cachoeira desta água: ModContent.GetInstance(ModWaterfallStyle).Slot ou uma do jogo (0 é a da floresta). | [Code](../../app/src/main/cpp/script/js/mod/ModWaterStyle.js#L9) |
| `GetDropletGore()` | Instance | `ModWaterStyle` | `706` | O gore da gota que pinga do bloco. Uma de mod (ModGore.getTypeByName) se comporta como a gota d'água do jogo (GoreID.WaterDrip), como o UpdateType do tModLoader. | [Code](../../app/src/main/cpp/script/js/mod/ModWaterStyle.js#L17) |
| `GetRainTexture()` | Instance | `ModWaterStyle` | `null` | A textura da chuva: o caminho de um PNG no mod (ou um Asset<Texture2D>), ou null para a do jogo (o padrão). | [Code](../../app/src/main/cpp/script/js/mod/ModWaterStyle.js#L35) |
| `GetRainVariant()` | Instance | `ModWaterStyle` | `Rand.Next(3)` | A variante da chuva: a coluna da textura dividida por 4 (a do jogo tem 3 por estilo; 0 a 2 é a da floresta). Com GetRainTexture, a coluna na textura do mod. O padrão é o sorteio do próprio jogo, sem custo; sobrescrever custa uma entrada no JS por gota nova. | [Code](../../app/src/main/cpp/script/js/mod/ModWaterStyle.js#L31) |
| `GetSplashDust()` | Instance | `ModWaterStyle` | `33` | O pó do respingo (quem cai na água). O padrão é o do jogo (DustID.Water). | [Code](../../app/src/main/cpp/script/js/mod/ModWaterStyle.js#L12) |
| `LightColorMultiplier(r, g, b)` | Instance | `ModWaterStyle` | `See source` | r, g, b são Ref (.value): quanto da luz atravessa a água (o motor de luz novo, os modos Cor e Branco). O padrão é o do jogo. | [Code](../../app/src/main/cpp/script/js/mod/ModWaterStyle.js#L21) |
| `register(cls)` | Static | `ModWaterStyle` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModWaterStyle.js#L43) |
| `SetStaticDefaults()` | Instance | `ModWaterStyle` | `undefined` | The base implementation does nothing. | [Code](../../app/src/main/cpp/script/js/mod/ModWaterStyle.js#L41) |

## ModWaterfallStyle

[Contract and fields](classes.md#mod-water).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `AddLight(i, j)` | Instance | `ModWaterfallStyle` | `undefined` | Luz em cada ponto de cachoeira deste estilo na tela (Lighting.AddLight). O celular embute a luz das cachoeiras do jogo no desenho; esta roda depois dele, uma vez por quadro. | [Code](../../app/src/main/cpp/script/js/mod/ModWaterfallStyle.js#L9) |
| `ColorMultiplier(r, g, b, a)` | Instance | `ModWaterfallStyle` | `undefined` | A cor da cachoeira: r, g, b são Ref (.value, 0 a 255, a luz do lugar vezes a opacidade) e a, a opacidade. Para cores que mudam com o tempo. | [Code](../../app/src/main/cpp/script/js/mod/ModWaterfallStyle.js#L13) |
| `register(cls)` | Static | `ModWaterfallStyle` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModWaterfallStyle.js#L17) |
| `SetStaticDefaults()` | Instance | `ModWaterfallStyle` | `undefined` | The base implementation does nothing. | [Code](../../app/src/main/cpp/script/js/mod/ModWaterfallStyle.js#L15) |

## MusicLoader

[Contract and fields](classes.md#sound-and-music).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `AddMusicBox(mod, musicSlot, itemType, tileType, tileFrameY = 0)` | Static | `MusicLoader` | `See source` | A caixa de música de mod: o tile tileType (2x2, como a do jogo) toca a faixa musicSlot quando ligado (quadro X a partir de 36) e na tela. | [Code](../../app/src/main/cpp/script/js/mod/MusicLoader.js#L34) |
| `GetMusicSlot(modOrPath, path)` | Static | `MusicLoader` | `See source` | A mod track's number (0 = does not exist). | [Code](../../app/src/main/cpp/script/js/mod/MusicLoader.js#L5) |
| `IsMusicPlaying(slot)` | Static | `MusicLoader` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/MusicLoader.js#L49) |
| `get MusicCount` | Static getter | `MusicLoader` | `ModMusic.Base() + ModMusic.Tracks.length` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/MusicLoader.js#L2) |
| `MusicExists(modOrPath, path)` | Static | `MusicLoader` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/MusicLoader.js#L27) |

## NPCHappiness

[Contract and fields](classes.md#npc-helpers).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `constructor(npcType)` | Constructor | `NPCHappiness` | `instance` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCHappiness.js#L2) |
| `SetBiomeAffection(biome, level)` | Instance | `NPCHappiness` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCHappiness.js#L16) |
| `SetNPCAffection(npcType, level)` | Instance | `NPCHappiness` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCHappiness.js#L6) |

## NPCLoot

[Contract and fields](classes.md#npc-helpers).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `Add(rule)` | Instance | `NPCLoot` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCLoot.js#L7) |
| `constructor(type)` | Constructor | `NPCLoot` | `instance` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCLoot.js#L2) |
| `Get()` | Instance | `NPCLoot` | `See source` | Sem as regras globais. | [Code](../../app/src/main/cpp/script/js/mod/NPCLoot.js#L20) |
| `Remove(rule)` | Instance | `NPCLoot` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCLoot.js#L13) |
| `RemoveWhere(predicate)` | Instance | `NPCLoot` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCLoot.js#L25) |

## NPCShop

[Contract and fields](classes.md#npc-helpers).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `Add(type, options = {})` | Instance | `NPCShop` | `See source` | options: { condition: () => bool, price, currency }. | [Code](../../app/src/main/cpp/script/js/mod/NPCShop.js#L25) |
| `get All` | Static getter | `NPCShop` | `See source` | As lojas, as do jogo e as de mod. | [Code](../../app/src/main/cpp/script/js/mod/NPCShop.js#L54) |
| `constructor(npcType, name = 'Shop')` | Constructor | `NPCShop` | `instance` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCShop.js#L17) |
| `get(npcType, name = 'Shop')` | Static | `NPCShop` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCShop.js#L48) |
| `Install()` | Static | `NPCShop` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCShop.js#L59) |
| `InstallTravel()` | Static | `NPCShop` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCShop.js#L63) |
| `Open()` | Instance | `NPCShop` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCShop.js#L41) |
| `Register()` | Instance | `NPCShop` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCShop.js#L30) |

## NPCSpawnInfo

[Contract and fields](classes.md#npc-helpers).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `get AboveSurface` | Getter | `NPCSpawnInfo` | `this.Surface \|\| this.Sky` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L48) |
| `get AnyEvent` | Getter | `NPCSpawnInfo` | `this.SlimeRain \|\| this.SolarEclipse \|\| this.PumpkinMoon \|\| this.FrostMoon` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L59) |
| `get AnyTower` | Getter | `NPCSpawnInfo` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L89) |
| `get BelowSurface` | Getter | `NPCSpawnInfo` | `!this.AboveSurface && !this.Underworld` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L49) |
| `get BloodMoon` | Getter | `NPCSpawnInfo` | `Terraria.Main.bloodMoon` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L55) |
| `get Cavern` | Getter | `NPCSpawnInfo` | `this.Player.ZoneRockLayerHeight` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L46) |
| `get CommonEnemy` | Getter | `NPCSpawnInfo` | `!this.Invasion && !this.AnyEvent && !this.AnyTower` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L88) |
| `constructor(spawner, tileX, tileY, target, xRange = false)` | Constructor | `NPCSpawnInfo` | `instance` | spawner: o NPC.Spawner do jogo; tileX/tileY: o ponto do spawn (-1 no EditSpawnFlags, antes de o jogo escolher); target: o índice do jogador. xRange: longe o bastante na horizontal (SafeRangeX). | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L25) |
| `get Corruption` | Getter | `NPCSpawnInfo` | `this.Player.ZoneCorrupt && this.AboveSurface` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L65) |
| `get Crimson` | Getter | `NPCSpawnInfo` | `this.Player.ZoneCrimson && this.AboveSurface` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L67) |
| `get Day` | Getter | `NPCSpawnInfo` | `Terraria.Main.dayTime` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L51) |
| `get Desert` | Getter | `NPCSpawnInfo` | `this.Player.ZoneDesert` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L79) |
| `get DesertCave` | Getter | `NPCSpawnInfo` | `this.Player.ZoneUndergroundDesert` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L80) |
| `get Dungeon` | Getter | `NPCSpawnInfo` | `this.Player.ZoneDungeon` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L84) |
| `get Expert` | Getter | `NPCSpawnInfo` | `Terraria.Main.expertMode` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L62) |
| `get FrostMoon` | Getter | `NPCSpawnInfo` | `Terraria.Main.snowMoon` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L58) |
| `get Granite` | Getter | `NPCSpawnInfo` | `this.Player.ZoneGranite` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L82) |
| `get Graveyard` | Getter | `NPCSpawnInfo` | `this.Player.ZoneGraveyard` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L83) |
| `get Hallow` | Getter | `NPCSpawnInfo` | `this.Player.ZoneHallow && this.AboveSurface` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L69) |
| `get HardMode` | Getter | `NPCSpawnInfo` | `Terraria.Main.hardMode` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L61) |
| `get Ice` | Getter | `NPCSpawnInfo` | `this.Player.ZoneSnow && (this.Underground \|\| this.Cavern)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L72) |
| `get Invasion` | Getter | `NPCSpawnInfo` | `Terraria.Main.invasionType > 0` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L87) |
| `get Jungle` | Getter | `NPCSpawnInfo` | `this.Player.ZoneJungle && this.AboveSurface` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L73) |
| `get Lihzahrd` | Getter | `NPCSpawnInfo` | `this.Player.ZoneLihzhardTemple` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L85) |
| `get Marble` | Getter | `NPCSpawnInfo` | `this.Player.ZoneMarble` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L81) |
| `get Master` | Getter | `NPCSpawnInfo` | `Terraria.Main.masterMode` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L63) |
| `get Meteor` | Getter | `NPCSpawnInfo` | `this.Player.ZoneMeteor` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L78) |
| `get Mushroom` | Getter | `NPCSpawnInfo` | `this.Player.ZoneGlowshroom && this.Cavern` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L76) |
| `get Night` | Getter | `NPCSpawnInfo` | `!Terraria.Main.dayTime` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L52) |
| `get Ocean` | Getter | `NPCSpawnInfo` | `this.Player.ZoneBeach` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L77) |
| `get PumpkinMoon` | Getter | `NPCSpawnInfo` | `Terraria.Main.pumpkinMoon` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L57) |
| `get Rain` | Getter | `NPCSpawnInfo` | `Terraria.Main.raining` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L53) |
| `get Sky` | Getter | `NPCSpawnInfo` | `this.Player.ZoneSkyHeight` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L43) |
| `get SlimeRain` | Getter | `NPCSpawnInfo` | `Terraria.Main.slimeRain` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L54) |
| `get Snow` | Getter | `NPCSpawnInfo` | `this.Player.ZoneSnow && this.AboveSurface` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L71) |
| `get SolarEclipse` | Getter | `NPCSpawnInfo` | `Terraria.Main.eclipse` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L56) |
| `get Surface` | Getter | `NPCSpawnInfo` | `this.Player.ZoneOverworldHeight` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L44) |
| `get SurfaceMushroom` | Getter | `NPCSpawnInfo` | `this.Player.ZoneGlowshroom && this.Surface` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L75) |
| `get Underground` | Getter | `NPCSpawnInfo` | `this.Player.ZoneDirtLayerHeight` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L45) |
| `get UndergroundCorruption` | Getter | `NPCSpawnInfo` | `this.Player.ZoneCorrupt && (this.Underground \|\| this.Cavern)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L66) |
| `get UndergroundCrimson` | Getter | `NPCSpawnInfo` | `this.Player.ZoneCrimson && (this.Underground \|\| this.Cavern)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L68) |
| `get UndergroundHallow` | Getter | `NPCSpawnInfo` | `this.Player.ZoneHallow && (this.Underground \|\| this.Cavern)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L70) |
| `get UndergroundJungle` | Getter | `NPCSpawnInfo` | `this.Player.ZoneJungle && (this.Underground \|\| this.Cavern)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L74) |
| `get Underworld` | Getter | `NPCSpawnInfo` | `this.Player.ZoneUnderworldHeight` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L47) |

## NetReader

[Contract and fields](classes.md#network).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `constructor(values)` | Constructor | `NetReader` | `instance` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NetReader.js#L2) |
| `get HasMore` | Getter | `NetReader` | `this.index < this.values.length` | Are there more values? | [Code](../../app/src/main/cpp/script/js/mod/NetReader.js#L7) |
| `Read()` | Instance | `NetReader` | `See source` | The next value, as it was written. Reads in the order written by NetWriter. Throws RangeError when the message has no more values. | [Code](../../app/src/main/cpp/script/js/mod/NetReader.js#L9) |
| `Read7BitEncodedInt()` | Instance | `NetReader` | `Number(this.Read()) \| 0` | Alias of NetReader.ReadInt32. | [Code](../../app/src/main/cpp/script/js/mod/NetReader.js#L29) |
| `ReadBoolean()` | Instance | `NetReader` | `!!this.Read()` | The next one, converted. | [Code](../../app/src/main/cpp/script/js/mod/NetReader.js#L18) |
| `ReadByte()` | Instance | `NetReader` | `Number(this.Read()) \| 0` | The next one, converted. | [Code](../../app/src/main/cpp/script/js/mod/NetReader.js#L29) |
| `ReadDouble()` | Instance | `NetReader` | `Number(this.Read())` | The next one, converted. | [Code](../../app/src/main/cpp/script/js/mod/NetReader.js#L32) |
| `ReadFlags()` | Instance | `NetReader` | `this.Read()` | An array of booleans; a Vector2. | [Code](../../app/src/main/cpp/script/js/mod/NetReader.js#L20) |
| `ReadInt16()` | Instance | `NetReader` | `Number(this.Read()) \| 0` | Alias of NetReader.ReadInt32. | [Code](../../app/src/main/cpp/script/js/mod/NetReader.js#L29) |
| `ReadInt32()` | Instance | `NetReader` | `Number(this.Read()) \| 0` | The next one, converted. | [Code](../../app/src/main/cpp/script/js/mod/NetReader.js#L16) |
| `ReadInt64()` | Instance | `NetReader` | `Number(this.Read())` | Alias of NetReader.ReadSingle. | [Code](../../app/src/main/cpp/script/js/mod/NetReader.js#L31) |
| `ReadSByte()` | Instance | `NetReader` | `Number(this.Read()) \| 0` | Alias of NetReader.ReadInt32. | [Code](../../app/src/main/cpp/script/js/mod/NetReader.js#L29) |
| `ReadSingle()` | Instance | `NetReader` | `Number(this.Read())` | The next one, converted. | [Code](../../app/src/main/cpp/script/js/mod/NetReader.js#L17) |
| `ReadString()` | Instance | `NetReader` | `String(this.Read())` | The next one, converted. | [Code](../../app/src/main/cpp/script/js/mod/NetReader.js#L19) |
| `ReadUInt16()` | Instance | `NetReader` | `Number(this.Read()) \| 0` | Alias of NetReader.ReadInt32. | [Code](../../app/src/main/cpp/script/js/mod/NetReader.js#L29) |
| `ReadUInt32()` | Instance | `NetReader` | `Number(this.Read()) \| 0` | Alias of NetReader.ReadInt32. | [Code](../../app/src/main/cpp/script/js/mod/NetReader.js#L29) |
| `ReadVector2()` | Instance | `NetReader` | `See source` | An array of booleans; a Vector2. | [Code](../../app/src/main/cpp/script/js/mod/NetReader.js#L22) |

## NetWriter

[Contract and fields](classes.md#network).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `constructor()` | Constructor | `NetWriter` | `instance` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L2) |
| `Write(value)` | Instance | `NetWriter` | `See source` | Stores a number, text, boolean, array, plain object or Vector2. The typed names (WriteInt32...) do the same. Writes JS values in the loader's protocol, not BinaryWriter bytes. The WriteByte, WriteInt32 etc. variants are aliases of Write. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L6) |
| `Write7BitEncodedInt(value)` | Instance | `NetWriter` | `See source` | Alias of NetWriter.Write. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteBoolean(value)` | Instance | `NetWriter` | `See source` | Alias of NetWriter.Write. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteByte(value)` | Instance | `NetWriter` | `See source` | Alias of NetWriter.Write. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteDouble(value)` | Instance | `NetWriter` | `See source` | Alias of NetWriter.Write. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteFlags(...flags)` | Instance | `NetWriter` | `See source` | Several booleans; a vector. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L12) |
| `WriteInt16(value)` | Instance | `NetWriter` | `See source` | Alias of NetWriter.Write. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteInt32(value)` | Instance | `NetWriter` | `See source` | Alias of NetWriter.Write. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteInt64(value)` | Instance | `NetWriter` | `See source` | Alias of NetWriter.Write. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteSByte(value)` | Instance | `NetWriter` | `See source` | Alias of NetWriter.Write. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteSingle(value)` | Instance | `NetWriter` | `See source` | Alias of NetWriter.Write. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteString(value)` | Instance | `NetWriter` | `See source` | Alias of NetWriter.Write. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteUInt16(value)` | Instance | `NetWriter` | `See source` | Alias of NetWriter.Write. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteUInt32(value)` | Instance | `NetWriter` | `See source` | Alias of NetWriter.Write. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteVector2(v)` | Instance | `NetWriter` | `See source` | Several booleans; a vector. | [Code](../../app/src/main/cpp/script/js/mod/NetWriter.js#L13) |

## PassLegacy

[Contract and fields](classes.md#genpass-and-passlegacy). Inherits from `GenPass`.

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `Apply(progress, configuration)` | Instance | `GenPass` (inherited) | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GenPass.js#L21) |
| `ApplyPass(progress, configuration)` | Instance | `PassLegacy` | `See source` | See the implementation in the source. Calls the function given to the constructor with the native GenerationProgress and GameConfiguration. | [Code](../../app/src/main/cpp/script/js/mod/GenPass.js#L33) |
| `constructor(name, method, weight = 1)` | Constructor | `PassLegacy` | `instance` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GenPass.js#L28) |
| `Disable()` | Instance | `GenPass` (inherited) | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GenPass.js#L19) |
| `Enable()` | Instance | `GenPass` (inherited) | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GenPass.js#L20) |
| `get Enabled` | Getter | `GenPass` (inherited) | `this.Native.Enabled` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GenPass.js#L18) |
| `get Name` | Getter | `GenPass` (inherited) | `this.Native.Name` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GenPass.js#L11) |
| `set Name(value)` | Setter | `GenPass` (inherited) | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GenPass.js#L12) |
| `get Weight` | Getter | `GenPass` (inherited) | `this.Native.Weight` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GenPass.js#L13) |
| `set Weight(value)` | Setter | `GenPass` (inherited) | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/GenPass.js#L14) |

## PlayerDrawLayer

[Contract and fields](classes.md#modplayer).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `AfterParent(layer)` | Static | `PlayerDrawLayer` | `{ After: layer }` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/PlayerDrawHooks.js#L11) |
| `BeforeParent(layer)` | Static | `PlayerDrawLayer` | `{ Before: layer }` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/PlayerDrawHooks.js#L10) |
| `constructor(name, method = null)` | Constructor | `PlayerDrawLayer` | `instance` | See the implementation in the source. Base for custom layers and descriptor of the native ones. Exported classes are registered automatically; the default name is the class name. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/PlayerDrawHooks.js#L2) |
| `Draw(drawInfo)` | Instance | `PlayerDrawLayer` | `undefined` | The base implementation does nothing. Required. Add DrawData with ModPlayer.AddDrawData(drawInfo, drawData). Runs together with the parent, before the native transformations and rendering. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/PlayerDrawHooks.js#L8) |
| `get FullName` | Getter | `PlayerDrawLayer` | `this.Mod ? this.Mod.uuid + '/' + this.Name : this.Name` | See the implementation in the source. Qualified name as mod-uuid/layer-name. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/PlayerDrawHooks.js#L4) |
| `GetDefaultPosition()` | Instance | `PlayerDrawLayer` | `See source` | See the implementation in the source. Required. Return BeforeParent or AfterParent with a native descriptor, a registered class, an instance or a full name. Default positions are cached. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/PlayerDrawHooks.js#L6) |
| `GetDefaultVisibility(drawInfo)` | Instance | `PlayerDrawLayer` | `true` | See the implementation in the source. Asked on each draw and for each player. false hides the layer and its children. Check drawInfo.shadow to exclude afterimages. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/PlayerDrawHooks.js#L7) |
| `Hide()` | Instance | `PlayerDrawLayer` | `See source` | See the implementation in the source. Hides the layer and its children in the current draw. The state is restored at the end, including in nested calls. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/PlayerDrawHooks.js#L9) |
| `register(cls)` | Static | `PlayerDrawLayer` | `See source` | Registers. Manual registration for classes with Autoload=false or created at runtime. Validates Draw and GetDefaultPosition, rejects duplicates and creates the ModContent.GetInstance template. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/PlayerDrawHooks.js#L12) |
| `SetStaticDefaults()` | Instance | `PlayerDrawLayer` | `undefined` | The base implementation does nothing. Once with content ready, after the items and equipment are registered. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/PlayerDrawHooks.js#L5) |
| `get Visible` | Getter | `PlayerDrawLayer` | `!this.IsHidden` | See the implementation in the source. The layer's own visibility in the current draw; a hidden parent also suppresses its execution. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/PlayerDrawHooks.js#L3) |

## PrefixLoader

[Contract and fields](classes.md#modprefix).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `AllowPrefix(item, pre)` | Static | `PrefixLoader` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/PrefixLoader.js#L122) |
| `Categories(item)` | Static | `PrefixLoader` | `See source` | The item's categories; a category's vanilla prefixes. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/PrefixLoader.js#L83) |
| `GetPrefix(type)` | Static | `PrefixLoader` | `PrefixLoader.ByType.get(type)` | The ModPrefix, or undefined (a vanilla prefix). | [Code](../../app/src/main/cpp/script/js/mod/Loaders/PrefixLoader.js#L26) |
| `GetPrefixesInCategory(category)` | Static | `PrefixLoader` | `PrefixLoader.#byCategory.get(category) \|\| []` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/PrefixLoader.js#L28) |
| `IsWeaponSubCategory(category)` | Static | `PrefixLoader` | `category === PrefixCategory.Melee \|\| category === PrefixCategory.Ranged \|\| category === PrefixCategory.Magic` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/PrefixLoader.js#L30) |
| `get PrefixCount` | Static getter | `PrefixLoader` | `PrefixLoader.VanillaCount + PrefixLoader.ByType.size` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/PrefixLoader.js#L24) |
| `Register(inst)` | Static | `PrefixLoader` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/PrefixLoader.js#L34) |
| `TooltipLines(item)` | Static | `PrefixLoader` | `See source` | As linhas do GetTooltipLines do prefixo do item, ou null. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/PrefixLoader.js#L132) |
| `get VanillaCount` | Static getter | `PrefixLoader` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/PrefixLoader.js#L19) |
| `VanillaPrefixes(category)` | Static | `PrefixLoader` | `See source` | The item's categories; a category's vanilla prefixes. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/PrefixLoader.js#L109) |
| `WantItemHooks(methods)` | Static | `PrefixLoader` | `See source` | Os métodos de prefixo que um ModItem/GlobalItem sobrescreve (ChoosePrefix, AllowPrefix...). | [Code](../../app/src/main/cpp/script/js/mod/Loaders/PrefixLoader.js#L64) |
| `WantRollable()` | Static | `PrefixLoader` | `See source` | Item de mod ganha os prefixos das categorias dele (MeleePrefix...): sem isto, as tabelas PrefixLegacy.ItemSets do jogo não o conhecem e ele não ganha nenhum. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/PrefixLoader.js#L73) |
| `WantRolling()` | Static | `PrefixLoader` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/PrefixLoader.js#L77) |

## RarityLoader

[Contract and fields](classes.md#modrarity).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `Add(inst)` | Static | `RarityLoader` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/RarityLoader.js#L19) |
| `ColorOf(type)` | Static | `RarityLoader` | `See source` | A cor de uma raridade de mod; branco se o RarityColor falhar. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/RarityLoader.js#L30) |
| `GetRarity(type)` | Static | `RarityLoader` | `RarityLoader.ByType.get(type) \|\| null` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/RarityLoader.js#L27) |
| `get RarityCount` | Static getter | `RarityLoader` | `RarityLoader.VanillaCount + RarityLoader.ByType.size` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/RarityLoader.js#L17) |

## ScreenEffects

[Contract and fields](classes.md#screen-effects).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `get Active` | Static getter | `ScreenEffects` | `ScreenEffects.#active.filter((fx) => fx.Active)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Effects/ScreenEffects.js#L127) |
| `get Classes` | Static getter | `ScreenEffects` | `See source` | As classes de efeito conhecidas: os prontos e as já registradas (as de Content/ entram na carga do mod). | [Code](../../app/src/main/cpp/script/js/mod/Effects/ScreenEffects.js#L98) |
| `get Debug` | Static getter | `ScreenEffects` | `ScreenDebug.Enabled` | A sobreposição de debug (/fx debug): contornos e painel de custo. | [Code](../../app/src/main/cpp/script/js/mod/Effects/ScreenEffects.js#L88) |
| `set Debug(value)` | Static setter | `ScreenEffects` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Effects/ScreenEffects.js#L92) |
| `Find(key)` | Static | `ScreenEffects` | `ScreenEffects.#active.find((fx) => fx.Key === key && fx.Active) ?? null` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Effects/ScreenEffects.js#L113) |
| `Install()` | Static | `ScreenEffects` | `See source` | O hook do quadro (no primeiro Spawn ou Tween). | [Code](../../app/src/main/cpp/script/js/mod/Effects/ScreenEffects.js#L150) |
| `Kill(key)` | Static | `ScreenEffects` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Effects/ScreenEffects.js#L117) |
| `KillAll()` | Static | `ScreenEffects` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Effects/ScreenEffects.js#L123) |
| `Register(cls)` | Static | `ScreenEffects` | `See source` | Na carga do mod (ContentAutoload) ou no primeiro Spawn. O shader vem, nesta ordem: static Source da própria classe, o Shader que o SetDefaults deu (ScreenShader ou caminho), o .frag ao lado do .js e, por último, o static Source herdado (quem estende um pronto). | [Code](../../app/src/main/cpp/script/js/mod/Effects/ScreenEffects.js#L24) |
| `Reload()` | Static | `ScreenEffects` | `See source` | Relê os shaders de arquivo; quantos mudaram. | [Code](../../app/src/main/cpp/script/js/mod/Effects/ScreenEffects.js#L105) |
| `Spawn(cls, center = null, options = {})` | Static | `ScreenEffects` | `See source` | center: a position, an entity (followed) or null (whole screen); options in camelCase. | [Code](../../app/src/main/cpp/script/js/mod/Effects/ScreenEffects.js#L60) |

## SoundEngine

[Contract and fields](classes.md#sound-and-music).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `FindActiveSound(style)` | Static | `SoundEngine` | `See source` | The newest one started, or 0 if pending/paused/inactive; cancels a pending request or stops a started one. | [Code](../../app/src/main/cpp/script/js/mod/SoundEngine.js#L19) |
| `GetSoundState(handle)` | Static | `SoundEngine` | `handle > 0 ? bl.sounds.playbackState(handle) : 0` | -1 failed; 0 finished/unknown; 1 pending; 2 started (estimated end); 3 paused; 4 cancelled; 5 expired in the queue. History limited to the 256 most recent requests, keeping the active ones. | [Code](../../app/src/main/cpp/script/js/mod/SoundEngine.js#L28) |
| `PlaySound(style, position)` | Static | `SoundEngine` | `See source` | Mod sound: an asynchronous request identifier (0 = refused). Acceptance does not confirm playback. | [Code](../../app/src/main/cpp/script/js/mod/SoundEngine.js#L4) |
| `StopSound(handle)` | Static | `SoundEngine` | `See source` | The newest one started, or 0 if pending/paused/inactive; cancels a pending request or stops a started one. | [Code](../../app/src/main/cpp/script/js/mod/SoundEngine.js#L32) |

## SoundStyle

[Contract and fields](classes.md#sound-and-music).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `constructor(path, options)` | Constructor | `SoundStyle` | `instance` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/SoundStyle.js#L5) |

## SpawnCondition

[Contract and fields](classes.md#npc-helpers).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `get Active` | Getter | `SpawnCondition` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/SpawnCondition.js#L37) |
| `Begin(info)` | Static | `SpawnCondition` | `See source` | O spawnInfo do sorteio que começa (o SpawnLoader chama antes dos SpawnChance). | [Code](../../app/src/main/cpp/script/js/mod/SpawnCondition.js#L41) |
| `get BlockWeight` | Getter | `SpawnCondition` | `this.#blockWeight` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/SpawnCondition.js#L38) |
| `get Chance` | Getter | `SpawnCondition` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/SpawnCondition.js#L36) |
| `CheckAll(info)` | Static | `SpawnCondition` | `See source` | Todas as condições no spawnInfo dado, na hora (para testes). | [Code](../../app/src/main/cpp/script/js/mod/SpawnCondition.js#L47) |
| `constructor(parent, test, blockWeight = 1)` | Constructor | `SpawnCondition` | `instance` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/SpawnCondition.js#L24) |
| `Name(all)` | Static | `SpawnCondition` | `See source` | Os nomes, para o aviso (depois de criar todas). | [Code](../../app/src/main/cpp/script/js/mod/SpawnCondition.js#L92) |

## SpawnPool

[Contract and fields](classes.md#npc-helpers).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `Add(type, weight)` | Instance | `SpawnPool` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/SpawnPool.js#L9) |
| `Choose()` | Instance | `SpawnPool` | `See source` | O tipo sorteado pelo Main.rand, ou null com o total 0 (nada nasce). Peso negativo vale 0. | [Code](../../app/src/main/cpp/script/js/mod/SpawnPool.js#L28) |
| `Clear()` | Instance | `SpawnPool` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/SpawnPool.js#L22) |
| `ContainsKey(type)` | Instance | `SpawnPool` | `Object.prototype.hasOwnProperty.call(this, type)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/SpawnPool.js#L14) |
| `get Count` | Getter | `SpawnPool` | `Object.keys(this).length` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/SpawnPool.js#L6) |
| `get Keys` | Getter | `SpawnPool` | `Object.keys(this).map(Number)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/SpawnPool.js#L7) |
| `Remove(type)` | Instance | `SpawnPool` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/SpawnPool.js#L16) |

## StatInheritanceData

[Contract and fields](classes.md#damageclass).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `constructor(damageInheritance = 0, critChanceInheritance = 0, attackSpeedInheritance = 0, armorPenInheritance = 0, knockbackInheritance = 0)` | Constructor | `StatInheritanceData` | `instance` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/StatInheritanceData.js#L5) |
| `get Full` | Static getter | `StatInheritanceData` | `new StatInheritanceData(1, 1, 1, 1, 1)` | Um novo a cada leitura: o Full with { attackSpeedInheritance = 0 } do C# vira Object.assign(StatInheritanceData.Full, { attackSpeedInheritance: 0 }). | [Code](../../app/src/main/cpp/script/js/mod/StatInheritanceData.js#L24) |
| `get IsNone` | Getter | `StatInheritanceData` | `!this.damageInheritance && !this.critChanceInheritance && !this.attackSpeedInheritance && !this.armorPenInheritance && !this.knockbackInheritance` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/StatInheritanceData.js#L27) |
| `get None` | Static getter | `StatInheritanceData` | `new StatInheritanceData()` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/StatInheritanceData.js#L25) |

## StatModifier

[Contract and fields](classes.md#damageclass).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `ApplyTo(baseValue)` | Instance | `StatModifier` | `(baseValue + this.Base) * this.Additive * this.Multiplicative + this.Flat` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/StatModifier.js#L15) |
| `Clone()` | Instance | `StatModifier` | `new StatModifier(this.Additive, this.Multiplicative, this.Flat, this.Base)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/StatModifier.js#L33) |
| `CombineWith(m)` | Instance | `StatModifier` | `new StatModifier(this.Additive + m.Additive - 1, this.Multiplicative * m.Multiplicative, this.Flat + m.Flat, this.Base + m.Base)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/StatModifier.js#L19) |
| `constructor(additive = 1, multiplicative = 1, flat = 0, base = 0)` | Constructor | `StatModifier` | `instance` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/StatModifier.js#L6) |
| `get Default` | Static getter | `StatModifier` | `new StatModifier()` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/StatModifier.js#L13) |
| `Equals(m)` | Instance | `StatModifier` | `!!m && this.Additive === m.Additive && this.Multiplicative === m.Multiplicative && this.Flat === m.Flat && this.Base === m.Base` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/StatModifier.js#L37) |
| `ForValue(value)` | Static | `StatModifier` | `See source` | O damage dos ModifyWeaponDamage: vale como número (return damage  2, o jeito de antes) e como StatModifier (damage.Additive += 0.1, o do tModLoader). O que o mod devolver em número ganha; senão vale o modificador. | [Code](../../app/src/main/cpp/script/js/mod/StatModifier.js#L45) |
| `Resolve(result, modifier, value)` | Static | `StatModifier` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/StatModifier.js#L51) |
| `Scale(scale)` | Instance | `StatModifier` | `new StatModifier(1 + (this.Additive - 1) * scale, 1 + (this.Multiplicative - 1) * scale, this.Flat * scale, this.Base * scale)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/StatModifier.js#L24) |
| `Undo(currentValue)` | Instance | `StatModifier` | `(currentValue - this.Flat) / (this.Multiplicative * this.Additive) - this.Base` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/StatModifier.js#L29) |

## TEModdedPylon

[Contract and fields](classes.md#modpylon-and-temoddedpylon). Inherits from `ModTileEntity`.

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `get ByID` | Static getter | `ModTileEntity` (inherited) | `TileEntityLoader.ByID` | The ones that exist (Map). | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L11) |
| `get ByPosition` | Static getter | `ModTileEntity` (inherited) | `TileEntityLoader.ByPosition` | The ones that exist (Map). | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L12) |
| `CountInWorld()` | Static | `ModTileEntity` (inherited) | `TileEntityLoader.ByID.size` | The ones that exist (Map). | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L62) |
| `Find(i, j)` | Instance | `ModTileEntity` (inherited) | `See source` | On the template: places it (returns the ID), removes it, and the ID of the one of this type at (i, j) (or -1). | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L47) |
| `get FullName` | Getter | `ModTileEntity` (inherited) | `(this.Mod ? this.Mod.id \|\| this.Mod.uuid : 'BunnyLoader') + '/' + this.Name` | Fields. Position is the object's top-left corner. | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L21) |
| `Generic_Hook_AfterPlacement(i, j, type, style, direction, alternate)` | Instance | `ModTileEntity` (inherited) | `TileEntityLoader.AfterPlacement(this, i, j, type, style, alternate)` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L58) |
| `get Generic_HookPostPlaceMyPlayer` | Getter | `ModTileEntity` (inherited) | `TileEntityLoader.PlacementHook(this, false, true)` | For TileObjectData.newTile.HookPostPlaceMyPlayer: the entity is created at the top-left corner of what the player placed (on the client, the server places and sends it). | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L54) |
| `Hook_AfterPlacement(i, j, type, style, direction, alternate)` | Instance | `TEModdedPylon` | `this.Generic_Hook_AfterPlacement(i, j, type, style, direction, alternate)` | The entity at the top-left corner; placing and removing rebuild the game's list. | [Code](../../app/src/main/cpp/script/js/mod/TEModdedPylon.js#L22) |
| `IsTileValidForEntity(x, y)` | Instance | `TEModdedPylon` | `See source` | The entity at the top-left corner; placing and removing rebuild the game's list. | [Code](../../app/src/main/cpp/script/js/mod/TEModdedPylon.js#L6) |
| `Kill(i, j)` | Instance | `ModTileEntity` (inherited) | `See source` | On the template: places it (returns the ID), removes it, and the ID of the one of this type at (i, j) (or -1). | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L45) |
| `Load()` | Instance | `ModTileEntity` (inherited) | `undefined` | On registration. | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L27) |
| `LoadData(tag)` | Instance | `ModTileEntity` (inherited) | `undefined` | TagCompound. | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L36) |
| `get Name` | Getter | `ModTileEntity` (inherited) | `this.constructor.name` | Fields. Position is the object's top-left corner. | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L20) |
| `NetReceive(reader)` | Instance | `ModTileEntity` (inherited) | `undefined` | What goes from the server to the clients. To send again: NetMessage.SendData(86, -1, -1, null, this.ID, ...) (tModLoader's TileEntitySharing). | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L39) |
| `NetSend(writer)` | Instance | `ModTileEntity` (inherited) | `undefined` | What goes from the server to the clients. To send again: NetMessage.SendData(86, -1, -1, null, this.ID, ...) (tModLoader's TileEntitySharing). | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L38) |
| `OnKill()` | Instance | `ModTileEntity` (inherited) | `undefined` | Only through Kill(i, j). | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L34) |
| `OnNetPlace()` | Instance | `ModTileEntity` (inherited) | `undefined` | On the server, placed at a client's request, before going to everyone. | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L32) |
| `Place(i, j)` | Instance | `ModTileEntity` (inherited) | `TileEntityLoader.Place(this, i, j)` | On the template: places it (returns the ID), removes it, and the ID of the one of this type at (i, j) (or -1). | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L44) |
| `PlacementHook(processedCoordinates = false)` | Instance | `ModTileEntity` (inherited) | `TileEntityLoader.PlacementHook(this, true, processedCoordinates)` | The same with a custom hook. | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L56) |
| `PostGlobalUpdate()` | Instance | `ModTileEntity` (inherited) | `undefined` | On every world update, on the server or in single player (never on the client). | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L30) |
| `PreGlobalUpdate()` | Instance | `ModTileEntity` (inherited) | `undefined` | On every world update, on the server or in single player (never on the client). | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L29) |
| `register(cls)` | Static | `ModTileEntity` (inherited) | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L72) |
| `SaveData(tag)` | Instance | `ModTileEntity` (inherited) | `undefined` | TagCompound. | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L35) |
| `TryGet(i, j, cls = ModTileEntity)` | Static | `ModTileEntity` (inherited) | `See source` | The entity of the object at (i, j) (any cell) if it is of that class; otherwise null. | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L66) |
| `TryGetModPylon()` | Instance | `TEModdedPylon` | `See source` | The entity at the top-left corner; placing and removing rebuild the game's list. | [Code](../../app/src/main/cpp/script/js/mod/TEModdedPylon.js#L15) |
| `Update()` | Instance | `ModTileEntity` (inherited) | `undefined` | On every world update, on the server or in single player (never on the client). | [Code](../../app/src/main/cpp/script/js/mod/ModTileEntity.js#L28) |

## TagCompound

[Contract and fields](classes.md#tagcompound).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `Add(key, value)` | Instance | `TagCompound` | `See source` | Writing and deleting. | [Code](../../app/src/main/cpp/script/js/mod/TagCompound.js#L19) |
| `ContainsKey(key)` | Instance | `TagCompound` | `Object.prototype.hasOwnProperty.call(this, key)` | Was the key saved? | [Code](../../app/src/main/cpp/script/js/mod/TagCompound.js#L7) |
| `get Count` | Getter | `TagCompound` | `Object.keys(this).length` | How many keys. | [Code](../../app/src/main/cpp/script/js/mod/TagCompound.js#L3) |
| `from(data)` | Static | `TagCompound` | `See source` | A TagCompound with the object's fields. | [Code](../../app/src/main/cpp/script/js/mod/TagCompound.js#L51) |
| `Get(key, fallback)` | Instance | `TagCompound` | `this.ContainsKey(key) ? this[key] : fallback` | The value, or the default. | [Code](../../app/src/main/cpp/script/js/mod/TagCompound.js#L11) |
| `GetBool(key)` | Instance | `TagCompound` | `!!this.Get(key, false)` | The value already converted (false, 0, '', [] if missing). | [Code](../../app/src/main/cpp/script/js/mod/TagCompound.js#L27) |
| `GetCompound(key)` | Instance | `TagCompound` | `TagCompound.from(this[key])` | The value already converted (false, 0, '', [] if missing). | [Code](../../app/src/main/cpp/script/js/mod/TagCompound.js#L47) |
| `GetFloat(key)` | Instance | `TagCompound` | `Number(this.Get(key, 0))` | The value already converted (false, 0, '', [] if missing). | [Code](../../app/src/main/cpp/script/js/mod/TagCompound.js#L35) |
| `GetInt(key)` | Instance | `TagCompound` | `Number(this.Get(key, 0)) \| 0` | The value already converted (false, 0, '', [] if missing). | [Code](../../app/src/main/cpp/script/js/mod/TagCompound.js#L31) |
| `GetList(key)` | Instance | `TagCompound` | `Array.isArray(this[key]) ? this[key] : []` | The value already converted (false, 0, '', [] if missing). | [Code](../../app/src/main/cpp/script/js/mod/TagCompound.js#L43) |
| `GetString(key)` | Instance | `TagCompound` | `String(this.Get(key, ''))` | The value already converted (false, 0, '', [] if missing). | [Code](../../app/src/main/cpp/script/js/mod/TagCompound.js#L39) |
| `Remove(key)` | Instance | `TagCompound` | `See source` | Writing and deleting. | [Code](../../app/src/main/cpp/script/js/mod/TagCompound.js#L23) |
| `Set(key, value)` | Instance | `TagCompound` | `See source` | Writing and deleting. | [Code](../../app/src/main/cpp/script/js/mod/TagCompound.js#L15) |

## TooltipLine

[Contract and fields](classes.md#tooltipline).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `colorTag(text, color)` | Static | `TooltipLine` | `'[c/' + TooltipLoader.Hex(color) + ':' + text + ']'` | '[c/RRGGBB:text]' to paint a span. | [Code](../../app/src/main/cpp/script/js/mod/TooltipLine.js#L20) |
| `constructor(...args)` | Constructor | `TooltipLine` | `instance` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/TooltipLine.js#L5) |
| `Hide()` | Instance | `TooltipLine` | `See source` | Hide() removes the line from the tooltip. | [Code](../../app/src/main/cpp/script/js/mod/TooltipLine.js#L18) |

## TouchButtonLoader

[Contract and fields](classes.md#modtouchbutton).

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `ActionOf(entry)` | Static | `TouchButtonLoader` | `TouchButtonLoader.Ids.firstAction + entry.index` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/TouchButtonLoader.js#L75) |
| `get All` | Static getter | `TouchButtonLoader` | `TouchButtonLoader.#entries` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/TouchButtonLoader.js#L73) |
| `ByAction(action)` | Static | `TouchButtonLoader` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/TouchButtonLoader.js#L83) |
| `ByKey(key)` | Static | `TouchButtonLoader` | `TouchButtonLoader.#byKey.get(key) \|\| null` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/TouchButtonLoader.js#L81) |
| `ByQuick(quick)` | Static | `TouchButtonLoader` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/TouchButtonLoader.js#L88) |
| `Controller()` | Static | `TouchButtonLoader` | `Safe.Run('TouchButtons: controle', () => bl.classOf('', 'GUIInstance').Active.GUIVirtualInputController) \|\| null` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/TouchButtonLoader.js#L157) |
| `ControlOf(entry)` | Static | `TouchButtonLoader` | `TouchButtonLoader.Ids.firstControl + entry.index` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/TouchButtonLoader.js#L77) |
| `get Count` | Static getter | `TouchButtonLoader` | `TouchButtonLoader.#entries.length` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/TouchButtonLoader.js#L72) |
| `Enabled(entry)` | Static | `TouchButtonLoader` | `!entry.enabled \|\| Safe.Run(entry.key + ' enabled', () => entry.enabled() !== false) !== false` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/TouchButtonLoader.js#L117) |
| `get Ids` | Static getter | `TouchButtonLoader` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/TouchButtonLoader.js#L54) |
| `IsBound(entry)` | Static | `TouchButtonLoader` | `See source` | Whether it is in the layout in use. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/TouchButtonLoader.js#L165) |
| `IsModded(action)` | Static | `TouchButtonLoader` | `action >= TouchButtonLoader.Ids.firstAction` | Uma ação além das do jogo: de um mod deste boot ou de um que saiu. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/TouchButtonLoader.js#L94) |
| `Label(entry)` | Static | `TouchButtonLoader` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/TouchButtonLoader.js#L98) |
| `get LastAction` | Static getter | `TouchButtonLoader` | `TouchButtonLoader.Ids.firstAction + TouchButtonLoader.Count - 1` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/TouchButtonLoader.js#L79) |
| `Press(entry)` | Static | `TouchButtonLoader` | `See source` | Um toque no botão: o que dispara o onPress e garante o JustPressed de um toque mais curto que uma atualização. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/TouchButtonLoader.js#L151) |
| `QuickOf(entry)` | Static | `TouchButtonLoader` | `TouchButtonLoader.Ids.firstQuick + entry.index` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/TouchButtonLoader.js#L76) |
| `RegisterButton(mod, name, options = {})` | Static | `TouchButtonLoader` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/TouchButtonLoader.js#L20) |
| `State(entry)` | Static | `TouchButtonLoader` | `See source` | O estado da atualização em curso do jogo: tirado uma vez por atualização, na primeira leitura (o ProcessTriggers lê toda atualização). | [Code](../../app/src/main/cpp/script/js/mod/Loaders/TouchButtonLoader.js#L123) |
| `Styles()` | Static | `TouchButtonLoader` | `Safe.Run('TouchButtons: estilos', () => bl.classOf('', 'VirtualControlsStyles_Layout').Instance) \|\| null` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/TouchButtonLoader.js#L161) |
| `Texture(entry)` | Static | `TouchButtonLoader` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/TouchButtonLoader.js#L108) |
| `Tick()` | Static | `TouchButtonLoader` | `See source` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/TouchButtonLoader.js#L128) |
| `Visible(entry)` | Static | `TouchButtonLoader` | `!entry.visible \|\| Safe.Run(entry.key + ' visible', () => entry.visible() !== false) !== false` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/Loaders/TouchButtonLoader.js#L113) |

## UsageException

[Contract and fields](classes.md#modcommand). Inherits from `Error`.

| Method / signature | Kind | Declaration | Base return | Contract / limit | Source |
|---|---|---|---|---|---|
| `constructor(msg, color)` | Constructor | `UsageException` | `instance` | See the implementation in the source. | [Code](../../app/src/main/cpp/script/js/mod/ModCommand.js#L45) |
