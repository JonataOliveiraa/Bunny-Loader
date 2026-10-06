# Métodos por classe

Catálogo gerado das classes públicas de `Exports.js`, com uma linha por método, construtor ou getter/setter declarado em JavaScript. Inclui métodos herdados e aliases de `NetWriter`/`NetReader`. Campos, enums e objetos de descritores ficam fora da contagem de métodos. Os métodos das classes nativas de `Terraria.*`, de `bl.*` e os loaders privados são consultados pela [ponte](ponte-e-bl.md).

Abra a [planilha Excel](metodos.xlsx) para filtrar a tabela por classe, método, tipo ou status. O [CSV UTF-8](metodos.csv) contém os mesmos registros e o [JSON](metodos.json) permite consulta por ferramentas.

`Declarado` confirma que o membro existe na API JavaScript atual. Não indica que todos os seus fluxos foram testados no jogo. A coluna de retorno mostra a expressão da implementação base, não uma declaração de tipo nem o retorno obrigatório do override. `Consultar fonte` identifica implementações com mais de uma instrução. As assinaturas são do Bunny Loader e podem diferir das do tModLoader.

Contratos e parâmetros mutáveis: [referência das classes](classes.md), [ModPlayer](modplayer-hooks.md) e [ref/out](../mods/02-ref-e-out.md). Os dez métodos de ModNPC sem adaptação completa aparecem com status `Não implementado` e sem assinatura inventada.

Instale a dependência com `npm --prefix tools/docs ci` e atualize com `node tools/docs/generate.mjs`. Confira diferenças com `node tools/docs/generate.mjs --check`. A [ferramenta de documentação](../../tools/docs/README.md) explica a atualização da planilha.

## Índice

| Classe | Membros próprios | Herdados | Contratos |
|---|---:|---:|---|
| [`BackgroundTextureLoader`](#backgroundtextureloader) | 6 | 0 | [Referência](classes.md#fundos-de-mod) |
| [`CloudLoader`](#cloudloader) | 4 | 0 | [Referência](classes.md#modcloud) |
| [`CommandLoader`](#commandloader) | 4 | 0 | [Referência](classes.md#modcommand) |
| [`DamageClass`](#damageclass) | 20 | 0 | [Referência](classes.md#damageclass) |
| [`DamageClassLoader`](#damageclassloader) | 19 | 0 | [Referência](classes.md#damageclass) |
| [`DrawableTooltipLine`](#drawabletooltipline) | 1 | 2 | [Referência](classes.md#tooltipline) |
| [`EquipLoader`](#equiploader) | 8 | 0 | [Referência](classes.md#texturas-vestidas-equiploader-e-equiptexture) |
| [`EquipTexture`](#equiptexture) | 9 | 0 | [Referência](classes.md#texturas-vestidas-equiploader-e-equiptexture) |
| [`ExtraJump`](#extrajump) | 1 | 0 | [Referência](classes.md#modplayer) |
| [`GlobalItem`](#globalitem) | 38 | 8 | [Referência](classes.md#globalitem) |
| [`GlobalLoot`](#globalloot) | 11 | 0 | [Referência](classes.md#globalloot) |
| [`GlobalNPC`](#globalnpc) | 23 | 8 | [Referência](classes.md#globalnpc) |
| [`GlobalProjectile`](#globalprojectile) | 26 | 8 | [Referência](classes.md#globalprojectile) |
| [`ItemLoot`](#itemloot) | 6 | 0 | [Referência](classes.md#ajudantes-de-npc) |
| [`Mod`](#mod) | 18 | 0 | [Referência](classes.md#mod-e-modloader) |
| [`ModAchievement`](#modachievement) | 18 | 0 | [Referência](classes.md#modachievement) |
| [`ModBiome`](#modbiome) | 5 | 5 | [Referência](classes.md#modbiome-e-modsceneeffect) |
| [`ModBuff`](#modbuff) | 17 | 0 | [Referência](classes.md#modbuff) |
| [`ModCloud`](#modcloud) | 5 | 0 | [Referência](classes.md#modcloud) |
| [`ModCommand`](#modcommand) | 10 | 0 | [Referência](classes.md#modcommand) |
| [`ModConfig`](#modconfig) | 15 | 0 | [Referência](classes.md#modconfig) |
| [`ModContent`](#modcontent) | 26 | 0 | [Referência](classes.md#modcontent) |
| [`ModEmoteBubble`](#modemotebubble) | 10 | 0 | [Referência](classes.md#modemotebubble) |
| [`ModGore`](#modgore) | 1 | 0 | [Referência](classes.md#ajudantes-de-npc) |
| [`ModHair`](#modhair) | 6 | 0 | [Referência](classes.md#modhair) |
| [`ModItem`](#moditem) | 114 | 0 | [Referência](classes.md#moditem) |
| [`ModLoader`](#modloader) | 4 | 0 | [Referência](classes.md#mod-e-modloader) |
| [`ModLocalization`](#modlocalization) | 8 | 0 | [Referência](classes.md#modlocalization) |
| [`ModMenu`](#modmenu) | 8 | 0 | [Referência](classes.md#modmenu) |
| [`ModMount`](#modmount) | 15 | 0 | [Referência](classes.md#modmount) |
| [`ModNPC`](#modnpc) | 64 | 0 | [Referência](classes.md#modnpc) |
| [`ModPacket`](#modpacket) | 2 | 15 | [Referência](classes.md#rede) |
| [`ModPlayer`](#modplayer) | 132 | 0 | [Referência](classes.md#modplayer) |
| [`ModPrefix`](#modprefix) | 16 | 0 | [Referência](classes.md#modprefix) |
| [`ModProjectile`](#modprojectile) | 46 | 0 | [Referência](classes.md#modprojectile) |
| [`ModRarity`](#modrarity) | 4 | 0 | [Referência](classes.md#modrarity) |
| [`ModRecipe`](#modrecipe) | 11 | 0 | [Referência](classes.md#modrecipe) |
| [`ModSceneEffect`](#modsceneeffect) | 6 | 0 | [Referência](classes.md#modbiome-e-modsceneeffect) |
| [`ModSurfaceBackgroundStyle`](#modsurfacebackgroundstyle) | 7 | 0 | [Referência](classes.md#fundos-de-mod) |
| [`ModSystem`](#modsystem) | 22 | 0 | [Referência](classes.md#modsystem) |
| [`ModTile`](#modtile) | 46 | 0 | [Referência](classes.md#modtile) |
| [`ModUndergroundBackgroundStyle`](#modundergroundbackgroundstyle) | 3 | 0 | [Referência](classes.md#fundos-de-mod) |
| [`ModWall`](#modwall) | 17 | 0 | [Referência](classes.md#modwall) |
| [`ModWaterStyle`](#modwaterstyle) | 9 | 0 | [Referência](classes.md#água-de-mod) |
| [`ModWaterfallStyle`](#modwaterfallstyle) | 4 | 0 | [Referência](classes.md#água-de-mod) |
| [`MusicLoader`](#musicloader) | 5 | 0 | [Referência](classes.md#som-e-música) |
| [`NPCHappiness`](#npchappiness) | 3 | 0 | [Referência](classes.md#ajudantes-de-npc) |
| [`NPCLoot`](#npcloot) | 5 | 0 | [Referência](classes.md#ajudantes-de-npc) |
| [`NPCShop`](#npcshop) | 5 | 0 | [Referência](classes.md#ajudantes-de-npc) |
| [`NPCSpawnInfo`](#npcspawninfo) | 44 | 0 | [Referência](classes.md#ajudantes-de-npc) |
| [`NetReader`](#netreader) | 17 | 0 | [Referência](classes.md#rede) |
| [`NetWriter`](#netwriter) | 16 | 0 | [Referência](classes.md#rede) |
| [`PlayerDrawLayer`](#playerdrawlayer) | 4 | 0 | [Referência](classes.md#modplayer) |
| [`PrefixLoader`](#prefixloader) | 13 | 0 | [Referência](classes.md#modprefix) |
| [`RarityLoader`](#rarityloader) | 4 | 0 | [Referência](classes.md#modrarity) |
| [`SoundEngine`](#soundengine) | 3 | 0 | [Referência](classes.md#som-e-música) |
| [`SoundStyle`](#soundstyle) | 1 | 0 | [Referência](classes.md#som-e-música) |
| [`SpawnCondition`](#spawncondition) | 7 | 0 | [Referência](classes.md#ajudantes-de-npc) |
| [`SpawnPool`](#spawnpool) | 7 | 0 | [Referência](classes.md#ajudantes-de-npc) |
| [`StatInheritanceData`](#statinheritancedata) | 4 | 0 | [Referência](classes.md#damageclass) |
| [`StatModifier`](#statmodifier) | 10 | 0 | [Referência](classes.md#damageclass) |
| [`TagCompound`](#tagcompound) | 13 | 0 | [Referência](classes.md#tagcompound) |
| [`TooltipLine`](#tooltipline) | 3 | 0 | [Referência](classes.md#tooltipline) |
| [`UsageException`](#usageexception) | 1 | 0 | [Referência](classes.md#modcommand) |

64 classes públicas, 1056 registros declarados e 10 métodos não implementados. A contagem inclui o mesmo membro em cada classe que o herda.

## Objetos e enums

`AffectionLevel`, `CommandType`, `EquipType`, `MountTextureType`, `PlayerDrawLayers`, `PrefixCategory`, `SceneEffectPriority`, `SoundLimitBehavior`. São exports públicos sem declaração de classe JavaScript neste módulo.

## BackgroundTextureLoader

[Contrato e campos](classes.md#fundos-de-mod).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `AddBackgroundTexture(mod, texture)` | Estático | `BackgroundTextureLoader` | `Consultar fonte` | Um fundo fora de uma pasta Backgrounds/; só durante a carga do mod. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/BackgroundTextureLoader.js#L32) |
| `Autoload()` | Estático | `BackgroundTextureLoader` | `Consultar fonte` | Na carga de cada mod, com o mod na pilha. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/BackgroundTextureLoader.js#L21) |
| `GetBackgroundSlot(modOrKey, path)` | Estático | `BackgroundTextureLoader` | `Consultar fonte` | GetBackgroundSlot(mod, 'Assets/Textures/Backgrounds/Nome') ou GetBackgroundSlot('examplemod/Assets/Textures/Backgrounds/Nome'). Lança se não existe, como no tModLoader. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/BackgroundTextureLoader.js#L52) |
| `get TotalCount` | Getter estático | `BackgroundTextureLoader` | `BackgroundTextureLoader.VanillaCount + BackgroundTextureLoader.#count` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/BackgroundTextureLoader.js#L18) |
| `TryGetBackgroundSlot(modOrKey, path, result)` | Estático | `BackgroundTextureLoader` | `Consultar fonte` | TryGetBackgroundSlot(chave, ref) ou (mod, caminho, ref): o número em ref.value. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/BackgroundTextureLoader.js#L61) |
| `get VanillaCount` | Getter estático | `BackgroundTextureLoader` | `Terraria.Main.maxBackgrounds` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/BackgroundTextureLoader.js#L17) |

## CloudLoader

[Contrato e campos](classes.md#modcloud).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `Add(inst)` | Estático | `CloudLoader` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/CloudLoader.js#L15) |
| `AddCloudFromTexture(texture, spawnChance = 1, rareCloud = false)` | Estático | `CloudLoader` | `Consultar fonte` | Uma nuvem só de textura (o caminho no mod, sem extensão), no Mod.Load. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/CloudLoader.js#L27) |
| `Autoload()` | Estático | `CloudLoader` | `Consultar fonte` | Todo PNG numa pasta Clouds/ do mod sem nuvem com o mesmo nome (classe ou AddCloudFromTexture) vira nuvem comum de peso 1, como no tModLoader. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/CloudLoader.js#L39) |
| `ChooseCloud(vanillaPool, rare)` | Estático | `CloudLoader` | `Consultar fonte` | O ChooseCloud do tModLoader: 0 é "uma do jogo". | [Código](../../app/src/main/cpp/script/js/mod/Loaders/CloudLoader.js#L101) |

## CommandLoader

[Contrato e campos](classes.md#modcommand).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `Add(cmd)` | Estático | `CommandLoader` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/CommandLoader.js#L10) |
| `GetHelp(callerType)` | Estático | `CommandLoader` | `Consultar fonte` | "/nome descrição" de cada comando que vale para este tipo de chamada (o GetHelp do tModLoader); o mod na frente quando o nome se repete. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/CommandLoader.js#L120) |
| `Handle(input, caller)` | Estático | `CommandLoader` | `Consultar fonte` | true: era um comando de mod (rodou, ou respondeu o erro). | [Código](../../app/src/main/cpp/script/js/mod/Loaders/CommandLoader.js#L92) |
| `Matches(commandType, callerType)` | Estático | `CommandLoader` | `Consultar fonte` | O CommandType.World vale como Chat sozinho e como Server no servidor. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/CommandLoader.js#L27) |

## DamageClass

[Contrato e campos](classes.md#damageclass).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `CountsAsClass(damageClass)` | Instância | `DamageClass` | `DamageClassLoader.CountsAs(this, DamageClassLoader.Resolve(damageClass))` | Consultam as relações entre classes registradas. | [Código](../../app/src/main/cpp/script/js/mod/DamageClass.js#L38) |
| `get Default` | Getter estático | `DamageClass` | `DamageClassLoader.Vanilla.Default` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/DamageClass.js#L10) |
| `get Generic` | Getter estático | `DamageClass` | `DamageClassLoader.Vanilla.Generic` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/DamageClass.js#L11) |
| `GetEffectInheritance(damageClass)` | Instância | `DamageClass` | `false` | Define se herda os efeitos da outra classe; padrão false. | [Código](../../app/src/main/cpp/script/js/mod/DamageClass.js#L25) |
| `GetModifierInheritance(damageClass)` | Instância | `DamageClass` | `damageClass === DamageClass.Generic ? StatInheritanceData.Full : StatInheritanceData.None` | Retorna StatInheritanceData; por padrão, herda todos os bônus de Generic e nenhum dos demais. | [Código](../../app/src/main/cpp/script/js/mod/DamageClass.js#L21) |
| `GetPrefixInheritance(damageClass)` | Instância | `DamageClass` | `this.GetEffectInheritance(damageClass)` | Define a herança de prefixos; padrão delega a GetEffectInheritance. | [Código](../../app/src/main/cpp/script/js/mod/DamageClass.js#L27) |
| `GetsPrefixesFor(damageClass)` | Instância | `DamageClass` | `Consultar fonte` | Consultam as relações entre classes registradas. | [Código](../../app/src/main/cpp/script/js/mod/DamageClass.js#L42) |
| `get Magic` | Getter estático | `DamageClass` | `DamageClassLoader.Vanilla.Magic` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/DamageClass.js#L15) |
| `get MagicSummonHybrid` | Getter estático | `DamageClass` | `DamageClassLoader.Vanilla.MagicSummonHybrid` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/DamageClass.js#L18) |
| `get Melee` | Getter estático | `DamageClass` | `DamageClassLoader.Vanilla.Melee` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/DamageClass.js#L12) |
| `get MeleeNoSpeed` | Getter estático | `DamageClass` | `DamageClassLoader.Vanilla.MeleeNoSpeed` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/DamageClass.js#L13) |
| `get Ranged` | Getter estático | `DamageClass` | `DamageClassLoader.Vanilla.Ranged` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/DamageClass.js#L14) |
| `register(cls)` | Estático | `DamageClass` | `Consultar fonte` | Registra manualmente e retorna o tipo. | [Código](../../app/src/main/cpp/script/js/mod/DamageClass.js#L47) |
| `SetDefaultStats(player)` | Instância | `DamageClass` | `undefined` | Inicialização do tipo e dos atributos do jogador. | [Código](../../app/src/main/cpp/script/js/mod/DamageClass.js#L31) |
| `SetStaticDefaults()` | Instância | `DamageClass` | `undefined` | Inicialização do tipo e dos atributos do jogador. | [Código](../../app/src/main/cpp/script/js/mod/DamageClass.js#L29) |
| `ShowStatTooltipLine(player, lineName)` | Instância | `DamageClass` | `true` | Controla a linha de atributo no tooltip; padrão true. | [Código](../../app/src/main/cpp/script/js/mod/DamageClass.js#L36) |
| `get Summon` | Getter estático | `DamageClass` | `DamageClassLoader.Vanilla.Summon` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/DamageClass.js#L16) |
| `get SummonMeleeSpeed` | Getter estático | `DamageClass` | `DamageClassLoader.Vanilla.SummonMeleeSpeed` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/DamageClass.js#L17) |
| `get Throwing` | Getter estático | `DamageClass` | `DamageClassLoader.Vanilla.Throwing` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/DamageClass.js#L19) |
| `get UseStandardCritCalcs` | Getter | `DamageClass` | `true` | Getter com padrão true. | [Código](../../app/src/main/cpp/script/js/mod/DamageClass.js#L33) |

## DamageClassLoader

[Contrato e campos](classes.md#damageclass).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `Activate()` | Estático | `DamageClassLoader` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L319) |
| `Add(inst)` | Estático | `DamageClassLoader` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L37) |
| `CountsAs(cls, other)` | Estático | `DamageClassLoader` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L64) |
| `get DamageClassCount` | Getter estático | `DamageClassLoader` | `DamageClassLoader.Vanilla && DamageClassLoader.#classes.length` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L31) |
| `Data(player)` | Estático | `DamageClassLoader` | `Consultar fonte` | Os bônus por classe deste jogador, zerados todo quadro (ResetEffects). | [Código](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L174) |
| `Defaulting(entity, fn)` | Estático | `DamageClassLoader` | `Consultar fonte` | O SetDefaults de um item ou projétil: o DamageType escrito nele vale para o tipo inteiro (sobrevive ao Clone, ao NewItem e à rede). | [Código](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L78) |
| `GetDamageClass(type)` | Estático | `DamageClassLoader` | `(DamageClassLoader.Vanilla && DamageClassLoader.#classes[type]) \|\| null` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L33) |
| `Install()` | Estático | `DamageClassLoader` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L549) |
| `IsHooked(cls)` | Estático | `DamageClassLoader` | `!DamageClassLoader.#native.has(cls)` | De gancho: o jogo não a entende pelas flags. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L62) |
| `ItemClass(item)` | Estático | `DamageClassLoader` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L88) |
| `NameOf(cls)` | Estático | `DamageClassLoader` | `cls.Name \|\| cls.constructor.name` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L35) |
| `ProjectileClass(proj)` | Estático | `DamageClassLoader` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L113) |
| `Resolve(which)` | Estático | `DamageClassLoader` | `Consultar fonte` | A classe pedida como instância, classe registrada ou Type. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L50) |
| `SetItemClass(item, which)` | Estático | `DamageClassLoader` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L94) |
| `SetProjectileClass(proj, which)` | Estático | `DamageClassLoader` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L119) |
| `Stats(player, which)` | Estático | `DamageClassLoader` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L184) |
| `Total(player, which)` | Estático | `DamageClassLoader` | `Consultar fonte` | O total de uma classe: o dela mais o de cada classe herdada, como o GetTotalDamage do tModLoader. As do jogo são lidas dos campos do jogador. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L193) |
| `UsesCrit(cls)` | Estático | `DamageClassLoader` | `!!Safe.Run(DamageClassLoader.NameOf(cls) + '.UseStandardCritCalcs', () => cls.UseStandardCritCalcs)` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L460) |
| `get Vanilla` | Getter estático | `DamageClassLoader` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/DamageClassLoader.js#L26) |

## DrawableTooltipLine

[Contrato e campos](classes.md#tooltipline). Herda de `TooltipLine`.

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `colorTag(text, color)` | Estático | `TooltipLine` (herdado) | `'[c/' + TooltipLoader.Hex(color) + ':' + text + ']'` | '[c/RRGGBB:texto]' para pintar um trecho. | [Código](../../app/src/main/cpp/script/js/mod/TooltipLine.js#L20) |
| `constructor(parent, index, x, y, color)` | Construtor | `DrawableTooltipLine` | `instância` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/DrawableTooltipLine.js#L7) |
| `Hide()` | Instância | `TooltipLine` (herdado) | `Consultar fonte` | Hide() tira a linha do tooltip. | [Código](../../app/src/main/cpp/script/js/mod/TooltipLine.js#L18) |

## EquipLoader

[Contrato e campos](classes.md#texturas-vestidas-equiploader-e-equiptexture).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `AddEquipTexture(...args)` | Estático | `EquipLoader` | `Consultar fonte` | Registra à mão, no Load(). | [Código](../../app/src/main/cpp/script/js/mod/Loaders/EquipLoader.js#L59) |
| `Apply(item, type)` | Estático | `EquipLoader` | `Consultar fonte` | No SetDefaults de cada Item, antes do do mod (que pode trocar). | [Código](../../app/src/main/cpp/script/js/mod/Loaders/EquipLoader.js#L89) |
| `Autoload(inst, cls, name)` | Estático | `EquipLoader` | `Consultar fonte` | No registro do item (bl.mod ainda é o dele): as texturas <Texture>_<tipo>, ou só as de static AutoloadEquip = [EquipType.Head, ...] (o atributo do tModLoader; [] desliga, para quem registra à mão com AddEquipTexture). | [Código](../../app/src/main/cpp/script/js/mod/Loaders/EquipLoader.js#L46) |
| `GetEquipSlot(...args)` | Estático | `EquipLoader` | `Consultar fonte` | O slot; -1 se não há. Do SetStaticDefaults em diante. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/EquipLoader.js#L76) |
| `GetEquipTexture(kind, slot)` | Estático | `EquipLoader` | `EquipLoader.#textures.get(kind + ':' + slot)` | A EquipTexture do slot. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/EquipLoader.js#L73) |
| `Install()` | Estático | `EquipLoader` | `Consultar fonte` | Uma vez, na primeira vez que um item de mod precisa do slot (a amostra do jogo ou o SetStaticDefaults): nessa hora o jogo já carregou as texturas dele. Antes disso, subir o Count faria o jogo procurar Armor_Head_293. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/EquipLoader.js#L99) |
| `InstalledKinds()` | Estático | `EquipLoader` | `EquipLoader.#kinds` | Os tipos de equipamento que ganharam textura de mod. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/EquipLoader.js#L85) |
| `VanillaCount(kind)` | Estático | `EquipLoader` | `EquipLoader.#vanilla.get(kind) \|\| 0` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/EquipLoader.js#L82) |

## EquipTexture

[Contrato e campos](classes.md#texturas-vestidas-equiploader-e-equiptexture).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `ArmorSetShadows(player)` | Instância | `EquipTexture` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/EquipTexture.js#L28) |
| `FrameEffects(player, type)` | Instância | `EquipTexture` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/EquipTexture.js#L11) |
| `HorizontalWingSpeeds(player, speed, acceleration)` | Instância | `EquipTexture` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/EquipTexture.js#L45) |
| `IsVanitySet(head, body, legs)` | Instância | `EquipTexture` | `this.Item ? this.Item.IsVanitySet(head, body, legs) : false` | head, body, legs: os SLOTS vestidos (player.head...), não itens. | [Código](../../app/src/main/cpp/script/js/mod/EquipTexture.js#L16) |
| `PreUpdateVanitySet(player)` | Instância | `EquipTexture` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/EquipTexture.js#L20) |
| `SetMatch(male, equipSlot, robes)` | Instância | `EquipTexture` | `Consultar fonte` | equipSlot e robes são Ref (.value). | [Código](../../app/src/main/cpp/script/js/mod/EquipTexture.js#L33) |
| `UpdateVanitySet(player)` | Instância | `EquipTexture` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/EquipTexture.js#L24) |
| `VerticalWingSpeeds(player, ascentWhenFalling, ascentWhenRising, maxCanAscendMultiplier, maxAscentMultiplier, constantAscend)` | Instância | `EquipTexture` | `Consultar fonte` | Para asas sem item vestido (a textura posta pelo FrameEffects). | [Código](../../app/src/main/cpp/script/js/mod/EquipTexture.js#L38) |
| `WingUpdate(player, inUse)` | Instância | `EquipTexture` | `this.Item ? this.Item.WingUpdate(player, inUse) === true : false` | true: o mod anima as asas (o WingFrame do jogo não roda). | [Código](../../app/src/main/cpp/script/js/mod/EquipTexture.js#L50) |

## ExtraJump

[Contrato e campos](classes.md#modplayer).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `constructor(name, field)` | Construtor | `ExtraJump` | `instância` | Consulte a implementação na fonte. Descritor dos saltos nativos. Registrar novos tipos de salto não faz parte da API atual. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/PlayerJumpHooks.js#L2) |

## GlobalItem

[Contrato e campos](classes.md#globalitem). Herda de `GlobalType`.

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `AddRecipeGroups()` | Instância | `GlobalType` (herdado) | `undefined` | Uma vez, com o conteúdo pronto. | [Código](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L14) |
| `AddRecipes()` | Instância | `GlobalType` (herdado) | `undefined` | Uma vez, com o conteúdo pronto. | [Código](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L15) |
| `AllowPrefix(item, pre)` | Instância | `GlobalItem` | `true` | Os de prefixo do ModItem, para qualquer item (o global vem antes no ChoosePrefix). | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L31) |
| `AppliesToEntity(entity, lateInstantiation)` | Instância | `GlobalType` (herdado) | `true` | false: os métodos do Global não rodam para ela. lateInstantiation é false na amostra do ModifyNPCLoot. | [Código](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L8) |
| `ApplyPrefix(item, pre)` | Instância | `GlobalItem` | `undefined` | Os de prefixo do ModItem, para qualquer item (o global vem antes no ChoosePrefix). | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L32) |
| `ArmorSetShadows(player, set)` | Instância | `GlobalItem` | `undefined` | Vaidade pelos slots desenhados. | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L47) |
| `CanAccessoryBeEquippedWith(equippedItem, incomingItem, player)` | Instância | `GlobalItem` | `true` | Conjuntos de qualquer item: o nome do conjunto ('' = nenhum), e o efeito por ele. | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L35) |
| `CanShoot(item, player)` | Instância | `GlobalItem` | `true` | Antes do tiro; false não atira. | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L10) |
| `CanUseItem(item, player)` | Instância | `GlobalItem` | `true` | Antes de usar; false impede. | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L3) |
| `ChoosePrefix(item, rand)` | Instância | `GlobalItem` | `-1` | Os de prefixo do ModItem, para qualquer item (o global vem antes no ChoosePrefix). | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L29) |
| `Clone(from, to)` | Instância | `GlobalType` (herdado) | `Entities.Clone(this)` | Retorna uma cópia dos campos da instância global. No fluxo de itens, é chamado por Item.Clone. | [Código](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L9) |
| `HoldItem(item, player)` | Instância | `GlobalItem` | `undefined` | A cada quadro com o item na mão. | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L8) |
| `HoldStyle(item, player, mountOffset, heldItemFrame)` | Instância | `GlobalItem` | `undefined` | A cada quadro com o item na mão. | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L7) |
| `HorizontalWingSpeeds(item, player, speed, acceleration)` | Instância | `GlobalItem` | `undefined` | Asas, também as do jogo. | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L52) |
| `get InstancePerEntity` | Getter | `GlobalType` (herdado) | `false` | true (campo ou getter): cada entidade ganha a própria cópia, nascida no SetDefaults. | [Código](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L5) |
| `IsArmorSet(head, body, legs)` | Instância | `GlobalItem` | `''` | Conjunto de qualquer item ('' = nenhum). | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L36) |
| `IsVanitySet(head, body, legs)` | Instância | `GlobalItem` | `Consultar fonte` | Vaidade pelos slots desenhados. | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L40) |
| `ModifyShootStats(item, player, stats)` | Instância | `GlobalItem` | `undefined` | stats = { position, velocity, type, damage, knockBack }. | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L11) |
| `ModifyTooltips(item, tooltips)` | Instância | `GlobalItem` | `undefined` | O tooltip; depois do ModItem. | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L21) |
| `ModifyWeaponDamage(item, player, damage)` | Instância | `GlobalItem` | `damage` | O dano da arma; devolva o novo. | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L9) |
| `NewInstance(target)` | Instância | `GlobalType` (herdado) | `Entities.Clone(this)` | A cópia de uma entidade nova. Padrão: os campos do modelo. | [Código](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L10) |
| `OnCraft(item, player, recipe)` | Instância | `GlobalItem` | `undefined` | Criado numa receita. | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L20) |
| `OnHitNPC(item, player, target, damageDone, knockBack, crit)` | Instância | `GlobalItem` | `undefined` | Golpe corpo a corpo acertou. | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L13) |
| `PostDrawTooltip(item, lines)` | Instância | `GlobalItem` | `undefined` | Os do ModItem, com item na frente, para qualquer item; depois do ModItem. | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L25) |
| `PostDrawTooltipLine(item, line)` | Instância | `GlobalItem` | `undefined` | Os do ModItem, com item na frente, para qualquer item; depois do ModItem. | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L27) |
| `PostSetupContent()` | Instância | `GlobalType` (herdado) | `undefined` | Uma vez, com o conteúdo pronto. | [Código](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L16) |
| `PostUpdateInWorld(item, worldItem)` | Instância | `GlobalItem` | `undefined` | A implementação base não executa ações. | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L19) |
| `PreDrawTooltip(item, lines, x, y)` | Instância | `GlobalItem` | `true` | Os do ModItem, com item na frente, para qualquer item; depois do ModItem. | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L24) |
| `PreDrawTooltipLine(item, line, yOffset)` | Instância | `GlobalItem` | `true` | Os do ModItem, com item na frente, para qualquer item; depois do ModItem. | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L26) |
| `PrefixChance(item, pre, rand)` | Instância | `GlobalItem` | `null` | Os de prefixo do ModItem, para qualquer item (o global vem antes no ChoosePrefix). | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L30) |
| `PreUpdateInWorld(item, worldItem)` | Instância | `GlobalItem` | `true` | O item no chão, todo quadro (ver ModItem.PreUpdateInWorld). | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L18) |
| `PreUpdateVanitySet(player, set)` | Instância | `GlobalItem` | `undefined` | Vaidade pelos slots desenhados. | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L45) |
| `register(cls)` | Estático | `GlobalItem` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L55) |
| `SetDefaults(item)` | Instância | `GlobalItem` | `undefined` | O item nasce ou troca de tipo. | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L2) |
| `SetMatch(armorSlot, type, male, equipSlot, robes)` | Instância | `GlobalItem` | `undefined` | O slot desenhado de uma parte (0 cabeça, 1 corpo, 2 pernas). | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L49) |
| `SetStaticDefaults()` | Instância | `GlobalType` (herdado) | `undefined` | Uma vez, com o conteúdo pronto. | [Código](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L12) |
| `Shoot(item, player, position, velocity, type, damage, knockBack, source)` | Instância | `GlobalItem` | `true` | false: o projétil do jogo não sai. | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L12) |
| `UpdateAccessory(item, player, vanity, hideVisual)` | Instância | `GlobalItem` | `undefined` | Acessório equipado (também de vaidade). | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L16) |
| `UpdateArmorSet(player, set)` | Instância | `GlobalItem` | `undefined` | Conjunto de qualquer item ('' = nenhum). | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L37) |
| `UpdateEquip(item, player)` | Instância | `GlobalItem` | `undefined` | Equipado (acessório ou armadura). | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L15) |
| `UpdateInventory(item, player)` | Instância | `GlobalItem` | `undefined` | A cada quadro, para as 58 casas do inventário. | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L14) |
| `UpdateVanitySet(player, set)` | Instância | `GlobalItem` | `undefined` | Vaidade pelos slots desenhados. | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L46) |
| `UseItem(item, player)` | Instância | `GlobalItem` | `undefined` | O uso começa; true como no ModItem. | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L5) |
| `UseStyle(item, player, mountOffset, heldItemFrame)` | Instância | `GlobalItem` | `undefined` | A cada quadro com o item na mão. | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L6) |
| `VerticalWingSpeeds(item, player, ascentWhenFalling, ascentWhenRising, maxCanAscendMultiplier, maxAscentMultiplier, constantAscend)` | Instância | `GlobalItem` | `undefined` | Asas, também as do jogo. | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L51) |
| `WingUpdate(wings, player, inUse)` | Instância | `GlobalItem` | `false` | Asas, também as do jogo. | [Código](../../app/src/main/cpp/script/js/mod/GlobalItem.js#L53) |

## GlobalLoot

[Contrato e campos](classes.md#globalloot).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `Add(rule)` | Instância | `GlobalLoot` | `Consultar fonte` | Pôr ou tirar uma regra global. | [Código](../../app/src/main/cpp/script/js/mod/GlobalLoot.js#L15) |
| `constructor()` | Construtor | `GlobalLoot` | `instância` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/GlobalLoot.js#L5) |
| `Get()` | Instância | `GlobalLoot` | `NPCLoader.Rules(Terraria.Main.ItemDropsDB._globalEntries)` | As regras globais, num array. | [Código](../../app/src/main/cpp/script/js/mod/GlobalLoot.js#L11) |
| `GetRulesForNPCID(id)` | Instância | `GlobalLoot` | `Terraria.Main.ItemDropsDB['GetRulesForNPCID(int npcNetId, bool includeGlobalDrops)'](id, false)` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/GlobalLoot.js#L38) |
| `get itemDropDatabase` | Getter | `GlobalLoot` | `Terraria.Main.ItemDropsDB` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/GlobalLoot.js#L9) |
| `ModifyGlobalLoot()` | Instância | `GlobalLoot` | `undefined` | A implementação base não executa ações. | [Código](../../app/src/main/cpp/script/js/mod/GlobalLoot.js#L36) |
| `register(cls)` | Estático | `GlobalLoot` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/GlobalLoot.js#L54) |
| `RegisterToNPC(id, rule)` | Instância | `GlobalLoot` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/GlobalLoot.js#L42) |
| `Remove(rule)` | Instância | `GlobalLoot` | `Consultar fonte` | Pôr ou tirar uma regra global. | [Código](../../app/src/main/cpp/script/js/mod/GlobalLoot.js#L20) |
| `RemoveFromNPC(id, rule)` | Instância | `GlobalLoot` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/GlobalLoot.js#L48) |
| `RemoveWhere(predicate)` | Instância | `GlobalLoot` | `Consultar fonte` | Tira as que casam; devolve quantas. | [Código](../../app/src/main/cpp/script/js/mod/GlobalLoot.js#L25) |

## GlobalNPC

[Contrato e campos](classes.md#globalnpc). Herda de `GlobalType`.

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `AddRecipeGroups()` | Instância | `GlobalType` (herdado) | `undefined` | Uma vez, com o conteúdo pronto. | [Código](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L14) |
| `AddRecipes()` | Instância | `GlobalType` (herdado) | `undefined` | Uma vez, com o conteúdo pronto. | [Código](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L15) |
| `AI(npc)` | Instância | `GlobalNPC` | `undefined` | A cada quadro. PreAI false pula a IA do jogo e o AI dos outros. | [Código](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L6) |
| `AppliesToEntity(entity, lateInstantiation)` | Instância | `GlobalType` (herdado) | `true` | false: os métodos do Global não rodam para ela. lateInstantiation é false na amostra do ModifyNPCLoot. | [Código](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L8) |
| `Clone(from, to)` | Instância | `GlobalType` (herdado) | `Entities.Clone(this)` | Retorna uma cópia dos campos da instância global. No fluxo de itens, é chamado por Item.Clone. | [Código](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L9) |
| `EditSpawnFlags(spawnInfo)` | Instância | `GlobalNPC` | `undefined` | Antes da taxa, da área e do ponto: os campos do jogador no spawnInfo (noWorms, invaders, ZoneCorrupt...). O ponto ainda não existe (SpawnTileX = -1). | [Código](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L24) |
| `EditSpawnInfo(spawnInfo)` | Instância | `GlobalNPC` | `undefined` | Com o ponto escolhido, antes do sorteio: os campos do spawnInfo (waterTile, nearGranite...) mudam o que o jogo e o SpawnChance leem. | [Código](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L33) |
| `EditSpawnPool(pool, spawnInfo)` | Instância | `GlobalNPC` | `undefined` | O sorteio (SpawnPool): pool[tipo] = peso; o 0 é o spawn do jogo. | [Código](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L35) |
| `EditSpawnRange(player, spawnRangeX, spawnRangeY, safeRangeX, safeRangeY)` | Instância | `GlobalNPC` | `undefined` | Ref, em blocos: até onde nasce e a distância mínima do jogador. | [Código](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L30) |
| `EditSpawnRate(player, spawnRate, maxSpawns)` | Instância | `GlobalNPC` | `undefined` | A cada tentativa de spawn para o jogador. Ref: spawnRate menor = mais spawn; maxSpawns, quantos inimigos por perto. | [Código](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L27) |
| `GetChat(npc, chat)` | Instância | `GlobalNPC` | `undefined` | A fala; chat é um Ref. | [Código](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L14) |
| `HitEffect(npc, hitDirection, damage)` | Instância | `GlobalNPC` | `undefined` | Levou golpe (sangue, gore). | [Código](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L8) |
| `get InstancePerEntity` | Getter | `GlobalType` (herdado) | `false` | true (campo ou getter): cada entidade ganha a própria cópia, nascida no SetDefaults. | [Código](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L5) |
| `ModifyGlobalLoot(globalLoot)` | Instância | `GlobalNPC` | `undefined` | Uma vez. | [Código](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L17) |
| `ModifyNPCLoot(npc, npcLoot)` | Instância | `GlobalNPC` | `undefined` | Uma vez por tipo de NPC, com a amostra do jogo. | [Código](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L16) |
| `NetReceive(npc, reader)` | Instância | `GlobalNPC` | `undefined` | Rede: junto com cada NPC que o servidor sincroniza. | [Código](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L19) |
| `NetSend(npc, writer)` | Instância | `GlobalNPC` | `undefined` | Rede: junto com cada NPC que o servidor sincroniza. | [Código](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L18) |
| `NewInstance(target)` | Instância | `GlobalType` (herdado) | `Entities.Clone(this)` | A cópia de uma entidade nova. Padrão: os campos do modelo. | [Código](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L10) |
| `OnHitByItem(npc, player, item, damageDone, knockBack, crit)` | Instância | `GlobalNPC` | `undefined` | Golpe corpo a corpo. | [Código](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L9) |
| `OnHitByProjectile(npc, projectile)` | Instância | `GlobalNPC` | `undefined` | Acertado por projétil. | [Código](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L10) |
| `OnKill(npc)` | Instância | `GlobalNPC` | `undefined` | A morte com drop, só no servidor ou sozinho. PreKill false: sem drop e sem OnKill. | [Código](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L12) |
| `OnSpawn(npc, source)` | Instância | `GlobalNPC` | `undefined` | Criado por NewNPC (no servidor ou sozinho). | [Código](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L3) |
| `PostAI(npc)` | Instância | `GlobalNPC` | `undefined` | A cada quadro. PreAI false pula a IA do jogo e o AI dos outros. | [Código](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L7) |
| `PostSetupContent()` | Instância | `GlobalType` (herdado) | `undefined` | Uma vez, com o conteúdo pronto. | [Código](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L16) |
| `PreAI(npc)` | Instância | `GlobalNPC` | `true` | A cada quadro. PreAI false pula a IA do jogo e o AI dos outros. | [Código](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L5) |
| `PreKill(npc)` | Instância | `GlobalNPC` | `true` | A morte com drop, só no servidor ou sozinho. PreKill false: sem drop e sem OnKill. | [Código](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L11) |
| `register(cls)` | Estático | `GlobalNPC` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L39) |
| `ResetEffects(npc)` | Instância | `GlobalNPC` | `undefined` | Começo da atualização do NPC. | [Código](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L4) |
| `SetDefaults(npc)` | Instância | `GlobalNPC` | `undefined` | O NPC nasce (também no cliente, ao chegar pela rede). | [Código](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L2) |
| `SetStaticDefaults()` | Instância | `GlobalType` (herdado) | `undefined` | Uma vez, com o conteúdo pronto. | [Código](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L12) |
| `SpawnNPC(npc, tileX, tileY)` | Instância | `GlobalNPC` | `undefined` | Nasceu um NPC sorteado que não é o do jogo (npc, o índice). | [Código](../../app/src/main/cpp/script/js/mod/GlobalNPC.js#L37) |

## GlobalProjectile

[Contrato e campos](classes.md#globalprojectile). Herda de `GlobalType`.

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `AddRecipeGroups()` | Instância | `GlobalType` (herdado) | `undefined` | Uma vez, com o conteúdo pronto. | [Código](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L14) |
| `AddRecipes()` | Instância | `GlobalType` (herdado) | `undefined` | Uma vez, com o conteúdo pronto. | [Código](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L15) |
| `AI(projectile)` | Instância | `GlobalProjectile` | `undefined` | A cada quadro. | [Código](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L5) |
| `AppliesToEntity(entity, lateInstantiation)` | Instância | `GlobalType` (herdado) | `true` | false: os métodos do Global não rodam para ela. lateInstantiation é false na amostra do ModifyNPCLoot. | [Código](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L8) |
| `CanCutTiles(projectile)` | Instância | `GlobalProjectile` | `undefined` | Primeiro booleano global decide; false impede corte padrão e personalizado. | [Código](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L20) |
| `CanDamage(projectile)` | Instância | `GlobalProjectile` | `undefined` | false global veta; true global prevalece sobre o método local. Condições nativas continuam valendo. | [Código](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L15) |
| `CanUseGrapple(type, player)` | Instância | `GlobalProjectile` | `undefined` | No template, filtrado por amostra do tipo; última resposta booleana global decide. | [Código](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L22) |
| `Clone(from, to)` | Instância | `GlobalType` (herdado) | `Entities.Clone(this)` | Retorna uma cópia dos campos da instância global. No fluxo de itens, é chamado por Item.Clone. | [Código](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L9) |
| `Colliding(projectile, projHitbox, targetHitbox)` | Instância | `GlobalProjectile` | `undefined` | Primeiro booleano global decide; undefined/null consulta o método local e o jogo. | [Código](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L14) |
| `CutTiles(projectile)` | Instância | `GlobalProjectile` | `undefined` | Globais, método local e corte padrão, quando permitido. | [Código](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L21) |
| `GetAlpha(projectile, lightColor)` | Instância | `GlobalProjectile` | `undefined` | Primeiro Color global decide; undefined/null consulta o método local e o jogo. | [Código](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L13) |
| `GrappleCanLatchOnTo(projectile, player, tile)` | Instância | `GlobalProjectile` | `undefined` | Recebe o bloco nativo; false global veta, true global permite. | [Código](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L24) |
| `get InstancePerEntity` | Getter | `GlobalType` (herdado) | `false` | true (campo ou getter): cada entidade ganha a própria cópia, nascida no SetDefaults. | [Código](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L5) |
| `MinionContactDamage(projectile)` | Instância | `GlobalProjectile` | `false` | true permite dano por contato de pet. | [Código](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L19) |
| `ModifyDamageHitbox(projectile, hitbox)` | Instância | `GlobalProjectile` | `undefined` | Ref<Rectangle>, depois do método local. | [Código](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L16) |
| `NetReceive(projectile, reader)` | Instância | `GlobalProjectile` | `undefined` | Rede: junto com cada projétil sincronizado, de quem o controla. | [Código](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L26) |
| `NetSend(projectile, writer)` | Instância | `GlobalProjectile` | `undefined` | Rede: junto com cada projétil sincronizado, de quem o controla. | [Código](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L25) |
| `NewInstance(target)` | Instância | `GlobalType` (herdado) | `Entities.Clone(this)` | A cópia de uma entidade nova. Padrão: os campos do modelo. | [Código](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L10) |
| `OnHitNPC(projectile, target)` | Instância | `GlobalProjectile` | `undefined` | Acertou um NPC. | [Código](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L9) |
| `OnHitPlayer(projectile, target)` | Instância | `GlobalProjectile` | `undefined` | Acertou um jogador. | [Código](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L10) |
| `OnKill(projectile, timeLeft)` | Instância | `GlobalProjectile` | `undefined` | Depois da morte nativa, quando PreKill não vetou. | [Código](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L8) |
| `OnSpawn(projectile, source)` | Instância | `GlobalProjectile` | `undefined` | Criado por NewProjectile, em quem criou. | [Código](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L3) |
| `OnTileCollide(projectile, oldVelocity)` | Instância | `GlobalProjectile` | `true` | false impede morte por choque; tempo e NPC continuam chamando OnKill. Todos os Globais rodam antes do método local. | [Código](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L17) |
| `PostAI(projectile)` | Instância | `GlobalProjectile` | `undefined` | A cada quadro. | [Código](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L6) |
| `PostDraw(projectile, lightColor)` | Instância | `GlobalProjectile` | `undefined` | Cor direta; roda depois do método local, mesmo com veto no desenho. | [Código](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L12) |
| `PostSetupContent()` | Instância | `GlobalType` (herdado) | `undefined` | Uma vez, com o conteúdo pronto. | [Código](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L16) |
| `PreAI(projectile)` | Instância | `GlobalProjectile` | `true` | A cada quadro. | [Código](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L4) |
| `PreDraw(projectile, lightColor)` | Instância | `GlobalProjectile` | `true` | Cor em Ref<Color>; false cancela o sprite. Todos os Globais rodam antes do método local, mantendo extras como correntes. | [Código](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L11) |
| `PreKill(projectile, timeLeft)` | Instância | `GlobalProjectile` | `true` | Antes da morte. false remove o projétil sem o efeito nativo e encerra o fluxo antes de OnKill. | [Código](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L7) |
| `register(cls)` | Estático | `GlobalProjectile` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L28) |
| `SetDefaults(projectile)` | Instância | `GlobalProjectile` | `undefined` | O projétil nasce (também no outro lado da rede). | [Código](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L2) |
| `SetStaticDefaults()` | Instância | `GlobalType` (herdado) | `undefined` | Uma vez, com o conteúdo pronto. | [Código](../../app/src/main/cpp/script/js/mod/Core/GlobalType.js#L12) |
| `TileCollideStyle(projectile, width, height, fallThrough, hitboxCenterFrac)` | Instância | `GlobalProjectile` | `true` | Quatro Ref, depois do método local; primeiro false pula colisão neste movimento. | [Código](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L18) |
| `UseGrapple(player, type)` | Instância | `GlobalProjectile` | `undefined` | Tipo em Ref<number>, depois do método local, para todos os templates que implementam o método. | [Código](../../app/src/main/cpp/script/js/mod/GlobalProjectile.js#L23) |

## ItemLoot

[Contrato e campos](classes.md#ajudantes-de-npc).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `Add(rule)` | Instância | `ItemLoot` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ItemLoot.js#L31) |
| `constructor(type)` | Construtor | `ItemLoot` | `instância` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ItemLoot.js#L10) |
| `For(m)` | Estático | `ItemLoot` | `Consultar fonte` | As regras do tipo; o ModifyItemLoot do ModItem roda na primeira vez. | [Código](../../app/src/main/cpp/script/js/mod/ItemLoot.js#L22) |
| `Get()` | Instância | `ItemLoot` | `[...this.rules]` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ItemLoot.js#L42) |
| `Remove(rule)` | Instância | `ItemLoot` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ItemLoot.js#L36) |
| `RemoveWhere(predicate)` | Instância | `ItemLoot` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ItemLoot.js#L46) |

## Mod

[Contrato e campos](classes.md#mod-e-modloader).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `AddRecipeGroups()` | Instância | `Mod` | `undefined` | Como no ModSystem. | [Código](../../app/src/main/cpp/script/js/mod/Mod.js#L21) |
| `AddRecipes()` | Instância | `Mod` | `undefined` | Como no ModSystem. | [Código](../../app/src/main/cpp/script/js/mod/Mod.js#L22) |
| `Call(...args)` | Instância | `Mod` | `Consultar fonte` | Quando outro mod chama. | [Código](../../app/src/main/cpp/script/js/mod/Mod.js#L37) |
| `constructor()` | Construtor | `Mod` | `instância` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Mod.js#L2) |
| `get dataDirectory` | Getter | `Mod` | `ModRegistry.DataDirectory(this.uuid)` | Android/data/com.bunnyloader/mod_data/<uid>. | [Código](../../app/src/main/cpp/script/js/mod/Mod.js#L16) |
| `GetContent(base)` | Instância | `Mod` | `Templates.All(base, this)` | Os modelos deste mod que estendem Base, na ordem do registro: this.GetContent(ModItem).map((i) => i.Type) dá os tipos de todos os itens. Não vale para ModPlayer (um por jogador: player.GetModPlayer). | [Código](../../app/src/main/cpp/script/js/mod/Mod.js#L25) |
| `GetPacket()` | Instância | `Mod` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Mod.js#L29) |
| `HandlePacket(reader, whoAmI)` | Instância | `Mod` | `undefined` | A implementação base não executa ações. | [Código](../../app/src/main/cpp/script/js/mod/Mod.js#L34) |
| `get id` | Getter | `Mod` | `this[MOD_INFO].id` | Está instalado (e carregou)? | [Código](../../app/src/main/cpp/script/js/mod/Mod.js#L10) |
| `Load()` | Instância | `Mod` | `undefined` | Na carga, depois do registro do conteúdo. | [Código](../../app/src/main/cpp/script/js/mod/Mod.js#L20) |
| `get name` | Getter | `Mod` | `this[MOD_INFO].name` | Do manifesto. | [Código](../../app/src/main/cpp/script/js/mod/Mod.js#L12) |
| `get path` | Getter | `Mod` | `this[MOD_INFO].path` | A pasta do main.js (content/) e a do pacote. | [Código](../../app/src/main/cpp/script/js/mod/Mod.js#L14) |
| `PostSetupContent()` | Instância | `Mod` | `undefined` | Como no ModSystem. | [Código](../../app/src/main/cpp/script/js/mod/Mod.js#L23) |
| `register()` | Estático | `Mod` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Mod.js#L48) |
| `get root` | Getter | `Mod` | `this[MOD_INFO].root` | A pasta do main.js (content/) e a do pacote. | [Código](../../app/src/main/cpp/script/js/mod/Mod.js#L15) |
| `toString()` | Instância | `Mod` | `'Mod(' + (this.id \|\| this.uuid) + ')'` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Mod.js#L18) |
| `get uuid` | Getter | `Mod` | `this[MOD_INFO].uuid` | Do manifesto. | [Código](../../app/src/main/cpp/script/js/mod/Mod.js#L11) |
| `get version` | Getter | `Mod` | `this[MOD_INFO].version` | Do manifesto. | [Código](../../app/src/main/cpp/script/js/mod/Mod.js#L13) |

## ModAchievement

[Contrato e campos](classes.md#modachievement).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `AddCondition(key = 'Condition')` | Instância | `ModAchievement` | `this.#Add(Terraria.GameContent.Achievements.CustomFlagCondition['AchievementCondition Create(string name)'](key))` | As condições, como no tModLoader. Devolvem a condição do jogo (a de número tem .Value). | [Código](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L32) |
| `AddFloatCondition(key, maxValue)` | Instância | `ModAchievement` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L39) |
| `AddIntCondition(key, maxValue)` | Instância | `ModAchievement` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L35) |
| `AddItemCraftCondition(itemIds)` | Instância | `ModAchievement` | `this.#AddByUser('ItemCraftCondition', 'short', 'items', 'item', itemIds)` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L43) |
| `AddItemPickupCondition(itemIds)` | Instância | `ModAchievement` | `this.#AddByUser('ItemPickupCondition', 'short', 'items', 'item', itemIds)` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L44) |
| `AddNPCKilledCondition(npcIds)` | Instância | `ModAchievement` | `this.#AddByUser('NPCKilledCondition', 'short', 'npcIds', 'npcId', npcIds)` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L45) |
| `AddTileDestroyedCondition(tileIds)` | Instância | `ModAchievement` | `this.#AddByUser('TileDestroyedCondition', 'ushort', 'tileIds', null, Array.isArray(tileIds) ? tileIds : [tileIds])` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L46) |
| `get Category` | Getter | `ModAchievement` | `Terraria.Achievements.AchievementCategory.Slayer` | Terraria.Achievements.AchievementCategory.Slayer (padrão), Collector, Explorer, Challenger. | [Código](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L17) |
| `get Hidden` | Getter | `ModAchievement` | `false` | Nome e descrição viram "???" no menu enquanto não completa. | [Código](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L19) |
| `get Index` | Getter | `ModAchievement` | `0` | A posição na folha (colunas de 66). | [Código](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L15) |
| `get IsCompleted` | Getter | `ModAchievement` | `!!(this.Achievement && this.Achievement.IsCompleted)` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L20) |
| `OnCompleted(achievement)` | Instância | `ModAchievement` | `undefined` | Completou. | [Código](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L23) |
| `OnItemCraft(itemType, stack)` | Instância | `ModAchievement` | `undefined` | Os avisos do jogo, enquanto não completa (do ExMod; o tModLoader usa os eventos do AchievementsHelper). | [Código](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L27) |
| `OnItemPickup(player, itemType, stack)` | Instância | `ModAchievement` | `undefined` | Os avisos do jogo, enquanto não completa (do ExMod; o tModLoader usa os eventos do AchievementsHelper). | [Código](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L26) |
| `OnNPCKilled(player, npcId)` | Instância | `ModAchievement` | `undefined` | Os avisos do jogo, enquanto não completa (do ExMod; o tModLoader usa os eventos do AchievementsHelper). | [Código](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L25) |
| `OnTileDestroyed(player, tileType)` | Instância | `ModAchievement` | `undefined` | Os avisos do jogo, enquanto não completa (do ExMod; o tModLoader usa os eventos do AchievementsHelper). | [Código](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L28) |
| `register(cls)` | Estático | `ModAchievement` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L65) |
| `SetStaticDefaults()` | Instância | `ModAchievement` | `undefined` | As condições: AddCondition(chave), AddIntCondition(chave, máx), AddFloatCondition, AddItemCraftCondition(id ou [ids]), AddItemPickupCondition, AddNPCKilledCondition, AddTileDestroyedCondition([ids]). Devolvem a condição do jogo (.Value nas de número, .Complete()). | [Código](../../app/src/main/cpp/script/js/mod/ModAchievement.js#L22) |

## ModBiome

[Contrato e campos](classes.md#modbiome-e-modsceneeffect). Herda de `ModSceneEffect`.

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `GetWeight(player)` | Instância | `ModSceneEffect` (herdado) | `0.5` | 0 a 1: desempata efeitos da mesma Priority. | [Código](../../app/src/main/cpp/script/js/mod/ModSceneEffect.js#L15) |
| `IsBiomeActive(player)` | Instância | `ModBiome` | `false` | A cada quadro, depois de o jogo atualizar as zonas dele. Um erro vale false nesta avaliação. | [Código](../../app/src/main/cpp/script/js/mod/ModBiome.js#L12) |
| `IsSceneEffectActive(player)` | Instância | `ModBiome` | `Consultar fonte` | O resultado do IsBiomeActive deste quadro; não repete a detecção. | [Código](../../app/src/main/cpp/script/js/mod/ModBiome.js#L19) |
| `MapBackgroundColor(color)` | Instância | `ModSceneEffect` (herdado) | `undefined` | Ref (.value, uma Color): a cor final, depois das duas acima. | [Código](../../app/src/main/cpp/script/js/mod/ModSceneEffect.js#L21) |
| `OnEnter(player)` | Instância | `ModBiome` | `undefined` | Uma vez, na troca. Ao sair do mundo dentro do bioma, o OnLeave vem na saída. | [Código](../../app/src/main/cpp/script/js/mod/ModBiome.js#L14) |
| `OnInBiome(player)` | Instância | `ModBiome` | `undefined` | A cada quadro dentro, inclusive no da entrada. | [Código](../../app/src/main/cpp/script/js/mod/ModBiome.js#L15) |
| `OnLeave(player)` | Instância | `ModBiome` | `undefined` | Uma vez, na troca. Ao sair do mundo dentro do bioma, o OnLeave vem na saída. | [Código](../../app/src/main/cpp/script/js/mod/ModBiome.js#L16) |
| `register(cls)` | Estático | `ModSceneEffect` (herdado) | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModSceneEffect.js#L28) |
| `SetStaticDefaults()` | Instância | `ModSceneEffect` (herdado) | `undefined` | A implementação base não executa ações. | [Código](../../app/src/main/cpp/script/js/mod/ModSceneEffect.js#L26) |
| `SpecialVisuals(player, isActive)` | Instância | `ModSceneEffect` (herdado) | `undefined` | Todo quadro, ativo ou não: é onde se liga e desliga um filtro de tela. | [Código](../../app/src/main/cpp/script/js/mod/ModSceneEffect.js#L24) |

## ModBuff

[Contrato e campos](classes.md#modbuff).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `ApplyNPC(npc, buffTime)` | Instância | `ModBuff` | `undefined` | Quando entra. | [Código](../../app/src/main/cpp/script/js/mod/ModBuff.js#L19) |
| `ApplyPlayer(player, buffTime)` | Instância | `ModBuff` | `undefined` | Quando entra. | [Código](../../app/src/main/cpp/script/js/mod/ModBuff.js#L18) |
| `CanRemove(player, buffTime, buffIndex, debuff)` | Instância | `ModBuff` | `null` | Ao tocar no ícone: true/false decide; null, o jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModBuff.js#L24) |
| `getModBuff(type)` | Estático | `ModBuff` | `BuffLoader.ByType.get(type)` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModBuff.js#L65) |
| `getTypeByName(name)` | Estático | `ModBuff` | `bl.buffs.typeOf(name)` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModBuff.js#L64) |
| `isModType(type)` | Estático | `ModBuff` | `bl.buffs.isModBuff(type)` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModBuff.js#L63) |
| `ModifyDescription()` | Instância | `ModBuff` | `undefined` | Uma vez por idioma: mexa em this.DisplayName/this.Description. | [Código](../../app/src/main/cpp/script/js/mod/ModBuff.js#L14) |
| `ModifyDisplayName()` | Instância | `ModBuff` | `undefined` | Uma vez por idioma: mexa em this.DisplayName/this.Description. | [Código](../../app/src/main/cpp/script/js/mod/ModBuff.js#L13) |
| `OnRemove(player, buffTime, buffIndex)` | Instância | `ModBuff` | `undefined` | Depois de tirado pelo toque. | [Código](../../app/src/main/cpp/script/js/mod/ModBuff.js#L25) |
| `PostSetupContent()` | Instância | `ModBuff` | `undefined` | A implementação base não executa ações. | [Código](../../app/src/main/cpp/script/js/mod/ModBuff.js#L12) |
| `PostStaticDefaults()` | Instância | `ModBuff` | `undefined` | A implementação base não executa ações. | [Código](../../app/src/main/cpp/script/js/mod/ModBuff.js#L11) |
| `ReApplyNPC(npc, buffTime, buffIndex)` | Instância | `ModBuff` | `true` | Quando entra de novo; false impede renovar o tempo. | [Código](../../app/src/main/cpp/script/js/mod/ModBuff.js#L22) |
| `ReApplyPlayer(player, buffTime, buffIndex)` | Instância | `ModBuff` | `true` | Quando entra de novo; false impede renovar o tempo. | [Código](../../app/src/main/cpp/script/js/mod/ModBuff.js#L21) |
| `register(cls)` | Estático | `ModBuff` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModBuff.js#L27) |
| `SetStaticDefaults()` | Instância | `ModBuff` | `undefined` | Uma vez: Main.debuff[this.Type], buffNoSave, BuffID.Sets.... | [Código](../../app/src/main/cpp/script/js/mod/ModBuff.js#L10) |
| `UpdateNPC(npc, buffIndex)` | Instância | `ModBuff` | `undefined` | Todo quadro, ativo no NPC. | [Código](../../app/src/main/cpp/script/js/mod/ModBuff.js#L17) |
| `UpdatePlayer(player, buffIndex)` | Instância | `ModBuff` | `undefined` | Todo quadro, ativo no jogador. | [Código](../../app/src/main/cpp/script/js/mod/ModBuff.js#L16) |

## ModCloud

[Contrato e campos](classes.md#modcloud).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `OnSpawn(cloud)` | Instância | `ModCloud` | `undefined` | A nuvem nasceu (o Cloud do jogo: spriteDir, scale...). | [Código](../../app/src/main/cpp/script/js/mod/ModCloud.js#L15) |
| `get RareCloud` | Getter | `ModCloud` | `false` | true: sorteada entre as raras (getter). | [Código](../../app/src/main/cpp/script/js/mod/ModCloud.js#L10) |
| `register(cls)` | Estático | `ModCloud` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModCloud.js#L17) |
| `SetStaticDefaults()` | Instância | `ModCloud` | `undefined` | A implementação base não executa ações. | [Código](../../app/src/main/cpp/script/js/mod/ModCloud.js#L12) |
| `SpawnChance()` | Instância | `ModCloud` | `1` | O peso: as comuns contra as 22 do jogo (peso 1 cada), as raras contra as 18 raras do jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModCloud.js#L13) |

## ModCommand

[Contrato e campos](classes.md#modcommand).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `Action(caller, input, args)` | Instância | `ModCommand` | `undefined` | O comando. caller.Player, caller.CommandType, caller.Reply(texto, cor). throw new UsageException(texto, cor) responde o erro; return false (do ExMod) responde o Usage. | [Código](../../app/src/main/cpp/script/js/mod/ModCommand.js#L17) |
| `get Aliases` | Getter | `ModCommand` | `[]` | Outros nomes (do ExMod): get Aliases() { return ['curar']; }. | [Código](../../app/src/main/cpp/script/js/mod/ModCommand.js#L8) |
| `get Command` | Getter | `ModCommand` | `''` | O nome, sem a barra (getter). | [Código](../../app/src/main/cpp/script/js/mod/ModCommand.js#L6) |
| `get Description` | Getter | `ModCommand` | `ModCommand.#Text(this, 'Description')` | Padrão: Commands.<Classe>.Usage/.Description do Localization (o /help mostra). | [Código](../../app/src/main/cpp/script/js/mod/ModCommand.js#L10) |
| `get IsCaseSensitive` | Getter | `ModCommand` | `false` | false: o texto chega em minúsculas. | [Código](../../app/src/main/cpp/script/js/mod/ModCommand.js#L11) |
| `register(cls)` | Estático | `ModCommand` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModCommand.js#L29) |
| `get ResponseColor` | Getter estático | `ModCommand` | `Color.new(255, 240, 20)` | A cor das respostas do jogo (a do /help). | [Código](../../app/src/main/cpp/script/js/mod/ModCommand.js#L20) |
| `SetStaticDefaults()` | Instância | `ModCommand` | `undefined` | A implementação base não executa ações. | [Código](../../app/src/main/cpp/script/js/mod/ModCommand.js#L13) |
| `get Type` | Getter | `ModCommand` | `CommandType.Chat` | CommandType.Chat (no aparelho de quem digitou), Server (no servidor, pedido por um jogador), World (sozinho no chat; no multijogador, no servidor). Console não existe no celular. | [Código](../../app/src/main/cpp/script/js/mod/ModCommand.js#L7) |
| `get Usage` | Getter | `ModCommand` | `ModCommand.#Text(this, 'Usage') \|\| '/' + this.Command` | Padrão: Commands.<Classe>.Usage/.Description do Localization (o /help mostra). | [Código](../../app/src/main/cpp/script/js/mod/ModCommand.js#L9) |

## ModConfig

[Contrato e campos](classes.md#modconfig).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `Button(action, extra = {})` | Estático | `ModConfig` | `{ ...extra, type: 'button', action }` | Descritores de cor, ação e endereço. | [Código](../../app/src/main/cpp/script/js/mod/ModConfig.js#L42) |
| `Color(defaultValue = '#FFFFFF', extra = {})` | Estático | `ModConfig` | `{ ...extra, type: 'color', default: String(defaultValue).toUpperCase() }` | Descritores de cor, ação e endereço. | [Código](../../app/src/main/cpp/script/js/mod/ModConfig.js#L38) |
| `Cycle(defaultValue, choices, extra = {})` | Estático | `ModConfig` | `{ ...extra, type: 'cycle', default: defaultValue, choices: [...choices] }` | Descritores de seleção. | [Código](../../app/src/main/cpp/script/js/mod/ModConfig.js#L34) |
| `Dropdown(defaultValue, choices, extra = {})` | Estático | `ModConfig` | `{ ...extra, type: 'dropdown', default: defaultValue, choices: [...choices] }` | Descritores de seleção. | [Código](../../app/src/main/cpp/script/js/mod/ModConfig.js#L30) |
| `Header(extra = {})` | Estático | `ModConfig` | `{ ...extra, type: 'header' }` | Descritores de título, interruptor e barra. | [Código](../../app/src/main/cpp/script/js/mod/ModConfig.js#L14) |
| `Link(url, extra = {})` | Estático | `ModConfig` | `{ ...extra, type: 'link', url: String(url) }` | Descritores de cor, ação e endereço. | [Código](../../app/src/main/cpp/script/js/mod/ModConfig.js#L46) |
| `OnApply()` | Instância | `ModConfig` | `undefined` | Notificação da aplicação das opções. | [Código](../../app/src/main/cpp/script/js/mod/ModConfig.js#L4) |
| `OnChanged(key)` | Instância | `ModConfig` | `undefined` | Quando uma opção é alterada na configuração aplicada. | [Código](../../app/src/main/cpp/script/js/mod/ModConfig.js#L3) |
| `OnLoaded()` | Instância | `ModConfig` | `undefined` | Depois de carregar a configuração. | [Código](../../app/src/main/cpp/script/js/mod/ModConfig.js#L2) |
| `Radio(defaultValue, choices, extra = {})` | Estático | `ModConfig` | `{ ...extra, type: 'radio', default: defaultValue, choices: [...choices] }` | Descritores de seleção. | [Código](../../app/src/main/cpp/script/js/mod/ModConfig.js#L26) |
| `Range(defaultValue, { min = 0, max = 100, step = 1, suffix = '', ...extra } = {})` | Estático | `ModConfig` | `{ ...extra, type: 'range', default: defaultValue, min, max, step, suffix }` | Descritores de título, interruptor e barra. | [Código](../../app/src/main/cpp/script/js/mod/ModConfig.js#L22) |
| `register(cls)` | Estático | `ModConfig` | `Consultar fonte` | Registra manualmente uma classe de configuração. | [Código](../../app/src/main/cpp/script/js/mod/ModConfig.js#L50) |
| `ResetToDefaults()` | Instância | `ModConfig` | `Consultar fonte` | Solicita ao loader a restauração dos valores padrão. | [Código](../../app/src/main/cpp/script/js/mod/ModConfig.js#L6) |
| `SetOption(key, value)` | Instância | `ModConfig` | `Consultar fonte` | Altera uma opção pela API de configuração. | [Código](../../app/src/main/cpp/script/js/mod/ModConfig.js#L10) |
| `Toggle(defaultValue = false, extra = {})` | Estático | `ModConfig` | `{ ...extra, type: 'toggle', default: !!defaultValue }` | Descritores de título, interruptor e barra. | [Código](../../app/src/main/cpp/script/js/mod/ModConfig.js#L18) |

## ModContent

[Contrato e campos](classes.md#modcontent).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `BuffType(which)` | Estático | `ModContent` | `ContentLookup.TypeOf(BuffLoader.ByType, which)` | O tipo. x é a classe, o nome ('ExampleBobber') ou 'outromod/Nome'. Não achou: 0. | [Código](../../app/src/main/cpp/script/js/mod/ModContent.js#L5) |
| `Find(base, name)` | Estático | `ModContent` | `Consultar fonte` | O modelo pelo nome; lança se não há. | [Código](../../app/src/main/cpp/script/js/mod/ModContent.js#L16) |
| `GetContent(base)` | Estático | `ModContent` | `Templates.All(base)` | Os modelos de todos os mods que estendem Base (ModItem, ModNPC, ModBuff, ModSystem...), na ordem do registro. | [Código](../../app/src/main/cpp/script/js/mod/ModContent.js#L13) |
| `GetInstance(cls)` | Estático | `ModContent` | `Templates.Get(cls)` | O modelo (a instância do register). | [Código](../../app/src/main/cpp/script/js/mod/ModContent.js#L12) |
| `GetModBuff(type)` | Estático | `ModContent` | `BuffLoader.ByType.get(type)` | O modelo pelo tipo. | [Código](../../app/src/main/cpp/script/js/mod/ModContent.js#L32) |
| `GetModItem(type)` | Estático | `ModContent` | `ItemLoader.ByType.get(type)` | O modelo pelo tipo. | [Código](../../app/src/main/cpp/script/js/mod/ModContent.js#L29) |
| `GetModMount(type)` | Estático | `ModContent` | `MountLoader.ByType.get(type)` | O modelo pelo tipo. | [Código](../../app/src/main/cpp/script/js/mod/ModContent.js#L36) |
| `GetModNPC(type)` | Estático | `ModContent` | `NPCLoader.ByType.get(type)` | O modelo pelo tipo. | [Código](../../app/src/main/cpp/script/js/mod/ModContent.js#L31) |
| `GetModPrefix(type)` | Estático | `ModContent` | `PrefixLoader.ByType.get(type)` | O modelo pelo tipo. | [Código](../../app/src/main/cpp/script/js/mod/ModContent.js#L35) |
| `GetModProjectile(type)` | Estático | `ModContent` | `ProjectileLoader.ByType.get(type)` | O modelo pelo tipo. | [Código](../../app/src/main/cpp/script/js/mod/ModContent.js#L30) |
| `GetModRarity(type)` | Estático | `ModContent` | `RarityLoader.GetRarity(type)` | O modelo pelo tipo. | [Código](../../app/src/main/cpp/script/js/mod/ModContent.js#L37) |
| `GetModTile(type)` | Estático | `ModContent` | `TileLoader.ByType.get(type)` | O modelo pelo tipo. | [Código](../../app/src/main/cpp/script/js/mod/ModContent.js#L33) |
| `GetModWall(type)` | Estático | `ModContent` | `WallLoader.ByType.get(type)` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModContent.js#L34) |
| `HasAsset(path)` | Estático | `ModContent` | `ContentLookup.FindTexture(path) !== null` | A textura existe? | [Código](../../app/src/main/cpp/script/js/mod/ModContent.js#L43) |
| `ItemType(which)` | Estático | `ModContent` | `ContentLookup.TypeOf(ItemLoader.ByType, which)` | O tipo. x é a classe, o nome ('ExampleBobber') ou 'outromod/Nome'. Não achou: 0. | [Código](../../app/src/main/cpp/script/js/mod/ModContent.js#L2) |
| `MountType(which)` | Estático | `ModContent` | `ContentLookup.TypeOf(MountLoader.ByType, which)` | O tipo. x é a classe, o nome ('ExampleBobber') ou 'outromod/Nome'. Não achou: 0. | [Código](../../app/src/main/cpp/script/js/mod/ModContent.js#L9) |
| `NPCType(which)` | Estático | `ModContent` | `ContentLookup.TypeOf(NPCLoader.ByType, which)` | O tipo. x é a classe, o nome ('ExampleBobber') ou 'outromod/Nome'. Não achou: 0. | [Código](../../app/src/main/cpp/script/js/mod/ModContent.js#L4) |
| `PrefixType(which)` | Estático | `ModContent` | `ContentLookup.TypeOf(PrefixLoader.ByType, which)` | O tipo. x é a classe, o nome ('ExampleBobber') ou 'outromod/Nome'. Não achou: 0. | [Código](../../app/src/main/cpp/script/js/mod/ModContent.js#L8) |
| `ProjectileType(which)` | Estático | `ModContent` | `ContentLookup.TypeOf(ProjectileLoader.ByType, which)` | O tipo. x é a classe, o nome ('ExampleBobber') ou 'outromod/Nome'. Não achou: 0. | [Código](../../app/src/main/cpp/script/js/mod/ModContent.js#L3) |
| `RarityType(which)` | Estático | `ModContent` | `ContentLookup.TypeOf(RarityLoader.ByType, which)` | O tipo. x é a classe, o nome ('ExampleBobber') ou 'outromod/Nome'. Não achou: 0. | [Código](../../app/src/main/cpp/script/js/mod/ModContent.js#L10) |
| `Request(path)` | Estático | `ModContent` | `ContentLookup.Request(path)` | Asset<Texture2D> do jogo, carregado uma vez (.Value é a textura). Thread do jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModContent.js#L41) |
| `SoundStyle(path, options)` | Estático | `ModContent` | `new SoundStyle(path, options)` | O mesmo que new SoundStyle(...). | [Código](../../app/src/main/cpp/script/js/mod/ModContent.js#L44) |
| `Texture(path)` | Estático | `ModContent` | `ContentLookup.Request(path).Value` | A Texture2D (o .Value do Request). | [Código](../../app/src/main/cpp/script/js/mod/ModContent.js#L42) |
| `TileType(which)` | Estático | `ModContent` | `ContentLookup.TypeOf(TileLoader.ByType, which)` | O tipo. x é a classe, o nome ('ExampleBobber') ou 'outromod/Nome'. Não achou: 0. | [Código](../../app/src/main/cpp/script/js/mod/ModContent.js#L6) |
| `TryFind(base, name, result)` | Estático | `ModContent` | `Consultar fonte` | O mesmo, no ref.value; devolve true/false. | [Código](../../app/src/main/cpp/script/js/mod/ModContent.js#L23) |
| `WallType(which)` | Estático | `ModContent` | `ContentLookup.TypeOf(WallLoader.ByType, which)` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModContent.js#L7) |

## ModEmoteBubble

[Contrato e campos](classes.md#modemotebubble).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `AddToCategory(categoryId)` | Instância | `ModEmoteBubble` | `Consultar fonte` | No menu de emotes (General, RockPaperScissors, Items, BiomesAndEvents, Town, CrittersAndMonsters, Dangers). No SetStaticDefaults. | [Código](../../app/src/main/cpp/script/js/mod/ModEmoteBubble.js#L12) |
| `GetFrame()` | Instância | `ModEmoteBubble` | `null` | O quadro na textura; null, o do jogo (metade da textura). | [Código](../../app/src/main/cpp/script/js/mod/ModEmoteBubble.js#L20) |
| `GetFrameInEmoteMenu(frame, frameCounter)` | Instância | `ModEmoteBubble` | `null` | O quadro no menu (a animação do menu tem dois). | [Código](../../app/src/main/cpp/script/js/mod/ModEmoteBubble.js#L23) |
| `IsUnlocked()` | Instância | `ModEmoteBubble` | `true` | No menu só se liberado. | [Código](../../app/src/main/cpp/script/js/mod/ModEmoteBubble.js#L15) |
| `OnSpawn()` | Instância | `ModEmoteBubble` | `undefined` | A bolha nasceu (EmoteBubble.NewBubble). | [Código](../../app/src/main/cpp/script/js/mod/ModEmoteBubble.js#L16) |
| `PostDraw(spriteBatch, texture, position, frame, origin, spriteEffects)` | Instância | `ModEmoteBubble` | `undefined` | Em volta do desenho da bolha; false não desenha. | [Código](../../app/src/main/cpp/script/js/mod/ModEmoteBubble.js#L26) |
| `PreDraw(spriteBatch, texture, position, frame, origin, spriteEffects)` | Instância | `ModEmoteBubble` | `true` | Em volta do desenho da bolha; false não desenha. | [Código](../../app/src/main/cpp/script/js/mod/ModEmoteBubble.js#L25) |
| `register(cls)` | Estático | `ModEmoteBubble` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModEmoteBubble.js#L28) |
| `SetStaticDefaults()` | Instância | `ModEmoteBubble` | `undefined` | A implementação base não executa ações. | [Código](../../app/src/main/cpp/script/js/mod/ModEmoteBubble.js#L11) |
| `UpdateFrame()` | Instância | `ModEmoteBubble` | `true` | false: a animação é do mod (this.EmoteBubble.frame, frameCounter). | [Código](../../app/src/main/cpp/script/js/mod/ModEmoteBubble.js#L18) |

## ModGore

[Contrato e campos](classes.md#ajudantes-de-npc).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `getTypeByName(name)` | Estático | `ModGore` | `bl.mod ? GoreLoader.TypeOf(bl.mod.uuid, name) : GoreLoader.TypeOfAny(name)` | O tipo do gore '<pasta>/Gores/<nome>.png' deste mod, ou 0. Fora da carga (num hook do jogo, sem mod na pilha), o primeiro mod que o tiver. | [Código](../../app/src/main/cpp/script/js/mod/ModGore.js#L4) |

## ModHair

[Contrato e campos](classes.md#modhair).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `get AltTexture` | Getter | `ModHair` | `this.Texture + '_Alt'` | A do penteado e a com chapéu (<Texture>_Alt; sem ela, a mesma). Padrão: ao lado do arquivo (Content/Hairs/X.js → Content/Hairs/X.png). | [Código](../../app/src/main/cpp/script/js/mod/ModHair.js#L10) |
| `get AvailableDuringCharacterCreation` | Getter | `ModHair` | `true` | Na criação de personagem (getter; padrão true). | [Código](../../app/src/main/cpp/script/js/mod/ModHair.js#L11) |
| `GetUnlockConditions()` | Instância | `ModHair` | `[]` | No Cabeleireiro quando todas valem: objetos com IsMet() ou funções. | [Código](../../app/src/main/cpp/script/js/mod/ModHair.js#L14) |
| `IsUnlocked(isAtCharacterCreation, isAtStylist)` | Instância | `ModHair` | `Consultar fonte` | Decide tudo sozinho (do ExMod), se escrito. | [Código](../../app/src/main/cpp/script/js/mod/ModHair.js#L16) |
| `register(cls)` | Estático | `ModHair` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModHair.js#L26) |
| `SetStaticDefaults()` | Instância | `ModHair` | `undefined` | A implementação base não executa ações. | [Código](../../app/src/main/cpp/script/js/mod/ModHair.js#L13) |

## ModItem

[Contrato e campos](classes.md#moditem).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `AddArmorSets()` | Instância | `ModItem` | `undefined` | O conjunto do 1.4.5 (tooltip "Bônus definido" e efeito pelo jogo): no AddArmorSets, CreateArmorSet(cabeça, corpo, pernas, texto) com os tipos; 0 é "qualquer". O texto é uma chave de tradução ou o próprio texto. O efeito é o UpdateArmorSet de cada peça de mod vestida. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L95) |
| `AddRecipeGroups()` | Instância | `ModItem` | `undefined` | Uma vez, antes de qualquer receita de qualquer mod. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L32) |
| `AddRecipes()` | Instância | `ModItem` | `undefined` | Uma vez, com as receitas do jogo prontas. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L33) |
| `AllowPrefix(item, pre)` | Instância | `ModItem` | `true` | false tira esse prefixo das opções. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L157) |
| `AnglerQuestChat(description, catchLocation)` | Instância | `ModItem` | `undefined` | A fala do Pescador (Ref): description.value e catchLocation.value (sai entre parênteses embaixo). Sem ela, o nome do item. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L52) |
| `ApplyPrefix(item, pre)` | Instância | `ModItem` | `undefined` | Depois dos status do prefixo. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L159) |
| `ArmorSetShadows(player)` | Instância | `ModItem` | `undefined` | As sombras do conjunto (armorEffectDraw). | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L117) |
| `buyPrice(platinum = 0, gold = 0, silver = 0, copper = 0)` | Estático | `ModItem` | `Terraria.Item.buyPrice(platinum, gold, silver, copper)` | Preço em cobre. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L291) |
| `CanAccessoryBeEquippedWith(equippedItem, incomingItem, player)` | Instância | `ModItem` | `true` | false recusa o par (arrastar para o slot e a troca pelo toque, que vai para o slot do que recusou). Chamado nos dois itens do par. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L88) |
| `CanBeChosenAsAmmo(item, weapon, player)` | Instância | `ModItem` | `null` | Munição: compõe a seleção; qualquer veto prevalece. Na munição. Compõe com a arma; qualquer false prevalece. Inclui armas vanilla e alternância nativa de slots. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L65) |
| `CanBeConsumedAsAmmo(item, weapon, player)` | Instância | `ModItem` | `true` | Munição: false conserva a pilha. Na munição. false conserva a pilha e suprime notificações de consumo. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L67) |
| `CanChooseAmmo(item, ammo, player)` | Instância | `ModItem` | `null` | Arma: false rejeita; true permite outra categoria; null conserva categoria nativa. Na arma. false rejeita; true permite outra categoria; null/undefined seguem ammo == useAmmo. Veto da munição prevalece. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L64) |
| `CanConsumeAmmo(item, ammo, player)` | Instância | `ModItem` | `true` | Arma: false conserva a pilha sem impedir o tiro. Na arma. false conserva a pilha sem impedir o tiro. Compõe com a munição e ModPlayer. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L66) |
| `CanHitNPC(item, player, target)` | Instância | `ModItem` | `null` | false veta; true permite a elegibilidade; null mantém o jogo. undefined também mantém o jogo. A colisão continua sendo verificada. Vetos de ModPlayer e ModNPC prevalecem. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L75) |
| `CanHitPvp(item, player, target)` | Instância | `ModItem` | `true` | false impede o golpe corpo a corpo. Contexto de ItemCheck_MeleeHitPVP. Mensagens de Hurt recebidas isoladamente não reconstroem a arma de origem. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L77) |
| `CanMeleeAttackCollideWithNPC(item, player, hitbox, target)` | Instância | `ModItem` | `null` | false veta; true permite a colisão; null mantém o jogo. undefined também mantém o jogo. Não força Intersects de retângulos sem relação com o ataque. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L80) |
| `CanRightClick(item)` | Instância | `ModItem` | `false` | true: o item abre no inventário (bolsa, caixa); entra em ItemID.Sets.OpenableBag. No toque, ganha o botão "Abrir" e os botões de usar ficam travados, como nas bolsas do jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L42) |
| `CanShoot(item, player)` | Instância | `ModItem` | `true` | false: usa, mas não atira. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L60) |
| `CanUseItem(item, player)` | Instância | `ModItem` | `true` | Antes de usar; false impede. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L36) |
| `ChoosePrefix(item, rand)` | Instância | `ModItem` | `-1` | Um prefixo forçado ao rolar (> 0), ou -1. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L154) |
| `Clone(newItem)` | Instância | `ModItem` | `Entities.Clone(this)` | O jogo copiou o item (Item.Clone); devolva a instância da cópia. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L15) |
| `CloneDefaults(type)` | Instância | `ModItem` | `Consultar fonte` | Copia os valores de um item do jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L171) |
| `ConsumeItem(item, player)` | Instância | `ModItem` | `true` | false: abrir não gasta o item. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L45) |
| `CreateArmorSet(head, body, legs, text = 'ArmorSetBonus.Empty', primaryPart = 0)` | Instância | `ModItem` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L96) |
| `CreateArmorSets(heads = [0], bodies = [0], legs = [0], text = 'ArmorSetBonus.Empty', primaryPart = 0)` | Instância | `ModItem` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L99) |
| `CreateRecipe(stack = 1)` | Instância | `ModItem` | `new ModRecipe().SetResult(this.Type, stack)` | Uma ModRecipe que dá este item. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L277) |
| `CreateRecipeGroup(itemTypes = [])` | Instância | `ModItem` | `Consultar fonte` | Grupo com o nome do primeiro item. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L282) |
| `DefaultToFood(buffType, buffTime, useGulpSound = false, animationTime = 17)` | Instância | `ModItem` | `Consultar fonte` | Comida. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L240) |
| `DefaultToGolfBall(projType)` | Instância | `ModItem` | `Consultar fonte` | Bola de golfe. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L273) |
| `DefaultToMusicBox(tileType, styleToPlace = 0)` | Instância | `ModItem` | `Consultar fonte` | A caixa de música do jogo, colocando o tile de mod (MusicLoader.AddMusicBox). | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L234) |
| `DefaultToPlaceableTile(typeToPlace, styleToPlace = 0)` | Instância | `ModItem` | `Consultar fonte` | Item que coloca um bloco. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L209) |
| `DefaultToPlaceableWall(wallToPlace)` | Instância | `ModItem` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L222) |
| `DefaultToSpear(projType, pushForwardSpeed, animationTime)` | Instância | `ModItem` | `Consultar fonte` | Lança. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L268) |
| `DefaultToTorch(tileType, styleToPlace = 0, allowWaterPlacement = false)` | Instância | `ModItem` | `Consultar fonte` | A tocha do jogo (segurar, luz, colocar na parede), colocando o tile de mod. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L228) |
| `DefaultToWhip(projType, damage, knockBack, shootSpeed, animationTime = 30)` | Instância | `ModItem` | `Consultar fonte` | Chicote. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L263) |
| `EquipFrameEffects(player, type)` | Instância | `ModItem` | `undefined` | A cada quadro, com a textura do item vestida. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L122) |
| `GetAlpha(item, lightColor)` | Instância | `ModItem` | `undefined` | Devolva a Color do desenho. No chão o item é a WorldItem (com Center); no inventário, o Item, sem posição. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L161) |
| `getByName(name)` | Estático | `ModItem` | `ItemLoader.ByType.get(bl.items.typeOf(name))` | O molde. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L366) |
| `getModItem(type)` | Estático | `ModItem` | `ItemLoader.ByType.get(type)` | O molde. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L365) |
| `getTypeByName(name)` | Estático | `ModItem` | `bl.items.typeOf(name)` | O tipo de um item deste mod; -1 se não há. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L364) |
| `HoldItem(item, player)` | Instância | `ModItem` | `undefined` | Todo quadro com o item na mão. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L53) |
| `HoldItemFrame(item, player)` | Instância | `ModItem` | `undefined` | Depois do frame nativo, ocioso e com item visualmente permitido. Após PlayerFrame completo, com itemAnimation <= 0 e CanVisuallyHoldItem permitido. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L56) |
| `HoldoutOffset(item, player)` | Instância | `ModItem` | `undefined` | Devolva { X, Y }: desloca a arma na mão. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L59) |
| `HoldStyle(item, player, mountOffset, heldItemFrame)` | Instância | `ModItem` | `undefined` | Todo quadro segurando, depois do estilo do jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L58) |
| `HorizontalWingSpeeds(item, player, speed, acceleration)` | Instância | `ModItem` | `undefined` | A corrida no ar com estas asas (dois Ref). | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L126) |
| `IsAnglerQuestAvailable()` | Instância | `ModItem` | `true` | false: o sorteio do dia não cai nele (sorteia de novo). | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L51) |
| `IsArmorSet(head, body, legs)` | Instância | `ModItem` | `false` | Para cada peça vestida de mod: true = conjunto completo. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L105) |
| `isModItem(item)` | Estático | `ModItem` | `!!item && bl.items.isModItem(item.type)` | É de mod? | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L363) |
| `isModType(type)` | Estático | `ModItem` | `bl.items.isModItem(type)` | É de mod? | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L362) |
| `IsQuestFish()` | Instância | `ModItem` | `false` | true: peixe de missão do Pescador (o mesmo que ItemID.Sets.IsQuestFish[this.Type] = true no SetStaticDefaults). Entra no fim de Main.anglerQuestItemNetIDs. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L50) |
| `IsVanitySet(head, body, legs)` | Instância | `ModItem` | `Consultar fonte` | Os SLOTS desenhados; true = conjunto de vaidade (padrão: o IsArmorSet dos itens desses slots). | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L110) |
| `get Item` | Getter | `ModItem` | `Entities.Of(this)` | Item do jogo | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L13) |
| `Load()` | Instância | `ModItem` | `undefined` | No registro (o AddEquipTexture vai aqui). | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L18) |
| `MagicPrefix(item = this.Item)` | Instância | `ModItem` | `!!item.magic` | As categorias de prefixo do item (padrão: melee sem noUseGraphic, melee com, ranged, magic, summon). Só vale para item com dano, sem ser munição nem consumível. Lido uma vez por tipo. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L151) |
| `MeleeEffects(item, player, hitbox)` | Instância | `ModItem` | `undefined` | Depois dos efeitos visuais nativos de uso. Antes de ModPlayer. Marca por tipo evita despacho JavaScript para itens sem sobrescrita, salvo observador global. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L81) |
| `MeleePrefix(item = this.Item)` | Instância | `ModItem` | `!!item.melee && !item.noUseGraphic` | As categorias de prefixo do item (padrão: melee sem noUseGraphic, melee com, ranged, magic, summon). Só vale para item com dano, sem ser munição nem consumível. Lido uma vez por tipo. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L148) |
| `ModifyFishingLine(item, bobber, lineOriginOffset, lineColor)` | Instância | `ModItem` | `undefined` | Vara na mão, a cada boia: de onde a linha sai e a cor (dois Ref). | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L169) |
| `ModifyHitNPC(item, player, target, modifiers)` | Instância | `ModItem` | `undefined` | Modifica o dano, crítico, direção e repulsão do golpe corpo a corpo. Contrato JS: damage, knockBack, hitDirection, crit, SourceDamage e Knockback (StatModifier), SetCrit() e DisableCrit(). Corpo a corpo; não reaplica em fromNet. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L76) |
| `ModifyHitPvp(item, player, target, modifiers)` | Instância | `ModItem` | `undefined` | Objeto JS com os parâmetros mutáveis do dano recebido. Contrato JS: damage, hitDirection, quiet, crit, dodgeable. Contexto de ItemCheck_MeleeHitPVP. Origem entre processos ainda pendente. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L78) |
| `ModifyItemLoot(itemLoot)` | Instância | `ModItem` | `undefined` | Uma vez, na primeira abertura: o que sai do item (itemLoot.Add(regra)). As regras do jogo e ItemDropRule.CoinsBasedOnNPCValue(npc) / ItemDropRule.Coins(valor); o que cairia no NPC vai para o jogador (QuickSpawnItem). | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L44) |
| `ModifyItemScale(item, player, scale)` | Instância | `ModItem` | `undefined` | scale é Ref<number> do multiplicador, incluindo a luva nativa. Atua na escala consultada e na hitbox. Item.scale é restaurado após o cálculo nativo. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L74) |
| `ModifyShootStats(item, player, stats)` | Instância | `ModItem` | `undefined` | Antes de cada projétil: stats.position, velocity, type, damage, knockBack. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L61) |
| `ModifyTooltipLines()` | Instância | `ModItem` | `undefined` | Uma vez por idioma, com this.TooltipLines preenchido. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L24) |
| `ModifyTooltips(item, tooltips)` | Instância | `ModItem` | `undefined` | Toda vez que o tooltip aparece, e no popup do guia de criação (sem cor). | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L25) |
| `ModifyWeaponCrit(item, player, crit)` | Instância | `ModItem` | `undefined` | crit é Ref<number>. ModItem antes de ModPlayer. Resultado inteiro; valor não finito vira zero. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L72) |
| `ModifyWeaponDamage(item, player, damage)` | Instância | `ModItem` | `undefined` | StatModifier; também aceita retorno numérico. Ordem compartilhada: ModItem, GlobalItem, ModPlayer. Resultado finito, inteiro e não negativo. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L71) |
| `ModifyWeaponKnockback(item, player, knockback)` | Instância | `ModItem` | `undefined` | knockback é StatModifier. ModItem antes de ModPlayer. Resultado finito não negativo. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L73) |
| `NeedsAmmo(item, player)` | Instância | `ModItem` | `true` | false: pode atirar com a munição padrão temporária quando não há candidato. false usa munição padrão temporária quando não há candidato; ela não é consumida nem entra no inventário. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L63) |
| `OnConsumeAmmo(item, ammo, player)` | Instância | `ModItem` | `undefined` | Arma: consumo efetivo, com a pilha já reduzida e antes de limpar o tipo. Na arma, após decremento efetivo no jogo móvel. Tipo e instância preservados na última unidade, antes de TurnToAir. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L68) |
| `OnConsumedAsAmmo(item, weapon, player)` | Instância | `ModItem` | `undefined` | Munição: consumo efetivo, inclusive da última unidade. Na munição, após decremento efetivo. Antes da limpeza da última unidade e de ModPlayer.OnConsumeAmmo. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L69) |
| `OnCraft(item, player, recipe)` | Instância | `ModItem` | `undefined` | Ao criar o item no menu de criação. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L34) |
| `OnHitNPC(item, player, npc, damageDone, knockBack, crit)` | Instância | `ModItem` | `undefined` | Acerto corpo a corpo. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L83) |
| `OnHitPvp(item, player, target, hurtInfo)` | Instância | `ModItem` | `undefined` | Só após dano positivo, com HurtInfo do resultado aplicado. HurtInfo: DamageSource, Damage, HitDirection, PvP, Quiet, Crit, CooldownCounter, Dodgeable. Contexto de ItemCheck_MeleeHitPVP; origem entre processos ainda pendente. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L79) |
| `PickAmmo(item, weapon, player, type, speed, damage, knockback)` | Instância | `ModItem` | `undefined` | Munição: três Ref<number> e StatModifier do dano total móvel. Na munição: item, weapon, player, type/speed/knockback (Ref) e damage (StatModifier sobre dano total móvel). Após cálculo nativo e antes das notificações. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L70) |
| `PostDrawTooltip(item, lines)` | Instância | `ModItem` | `undefined` | Depois de todas as linhas (DrawableTooltipLine). | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L29) |
| `PostDrawTooltipLine(item, line)` | Instância | `ModItem` | `undefined` | Depois de cada linha (também a não desenhada). | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L31) |
| `PostSetDefaults(item)` | Instância | `ModItem` | `undefined` | Logo depois do SetDefaults. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L22) |
| `PostSetupContent()` | Instância | `ModItem` | `undefined` | Uma vez, com todo o conteúdo de mod no jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L23) |
| `PostStaticDefaults()` | Instância | `ModItem` | `undefined` | Logo depois do SetStaticDefaults. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L21) |
| `PostUpdate(item)` | Instância | `ModItem` | `undefined` | A cada quadro, com o item no chão (item é a WorldItem). Luz, poeira. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L162) |
| `PostUpdateInWorld(item, worldItem)` | Instância | `ModItem` | `undefined` | A implementação base não executa ações. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L167) |
| `PreDrawTooltip(item, lines, x, y)` | Instância | `ModItem` | `true` | Antes das linhas; x, y são Ref; false não desenha as linhas. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L28) |
| `PreDrawTooltipLine(item, line, yOffset)` | Instância | `ModItem` | `true` | Antes de cada linha; yOffset (Ref) soma depois de cada linha seguinte; false não desenha a linha. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L30) |
| `PrefixChance(item, pre, rand)` | Instância | `ModItem` | `null` | false impede, true força um prefixo; null = o do jogo (pre: -1 criar/baú, -2 reforja). | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L156) |
| `PreUpdateInWorld(item, worldItem)` | Instância | `ModItem` | `true` | O item no chão, todo quadro: item é o Item, worldItem a WorldItem que o leva (position, velocity, Center). false no Pre pula a atualização do jogo naquele quadro (o Post roda igual). | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L166) |
| `PreUpdateVanitySet(player)` | Instância | `ModItem` | `undefined` | Antes e depois do FrameEffects, com o conjunto de vaidade. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L115) |
| `RangedPrefix(item = this.Item)` | Instância | `ModItem` | `!!item.ranged` | As categorias de prefixo do item (padrão: melee sem noUseGraphic, melee com, ranged, magic, summon). Só vale para item com dano, sem ser munição nem consumível. Lido uma vez por tipo. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L150) |
| `register(cls)` | Estático | `ModItem` | `Consultar fonte` | Registra na mão (a classe exportada já é registrada sozinha); devolve o tipo. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L295) |
| `RightClick(item, player)` | Instância | `ModItem` | `undefined` | Ao abrir, antes do ItemLoot. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L43) |
| `sellPrice(platinum = 0, gold = 0, silver = 0, copper = 0)` | Estático | `ModItem` | `Terraria.Item.sellPrice(platinum, gold, silver, copper)` | Preço em cobre. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L287) |
| `SetDefaults(item)` | Instância | `ModItem` | `undefined` | Todo item deste tipo que nasce. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L20) |
| `SetDefaultWeaponStyle(useTime = 30, autoReuse = false)` | Instância | `ModItem` | `Consultar fonte` | useTime, useAnimation, autoReuse e o useStyle que combina. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L185) |
| `SetItemAnimation(frameCount, ticksPerFrame = 5, pingPong = false)` | Instância | `ModItem` | `Consultar fonte` | Item animado (tira vertical). No SetStaticDefaults. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L256) |
| `SetMatch(male, equipSlot, robes)` | Instância | `ModItem` | `undefined` | O slot desenhado desta peça (Ref); o manto põe robes e as pernas. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L120) |
| `SetShopValues(rarity = 0, coinValue = 0)` | Instância | `ModItem` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L204) |
| `SetStaticDefaults()` | Instância | `ModItem` | `undefined` | Uma vez, com o tipo já nas tabelas do jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L19) |
| `SetWeaponValues(damage = 0, knockBack = 0, crit = 0)` | Instância | `ModItem` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L198) |
| `SetWingStats(flyTime = 100, flySpeedOverride = -1, accelerationMultiplier = 1, hasHoldDownHoverFeatures = false, hoverFlySpeedOverride = -1, hoverAccelerationMultiplier = 1)` | Instância | `ModItem` | `Consultar fonte` | O WingStats das asas do item. No SetStaticDefaults. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L131) |
| `Shoot(item, player, position, velocity, type, damage, knockBack)` | Instância | `ModItem` | `true` | false: o projétil do jogo não nasce. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L62) |
| `SummonPrefix(item = this.Item)` | Instância | `ModItem` | `!!item.summon` | As categorias de prefixo do item (padrão: melee sem noUseGraphic, melee com, ranged, magic, summon). Só vale para item com dano, sem ser munição nem consumível. Lido uma vez por tipo. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L152) |
| `UpdateAccessory(item, player, vanity, hideVisual)` | Instância | `ModItem` | `undefined` | Todo quadro, acessório equipado (também no slot de vaidade). | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L85) |
| `UpdateArmorSet(item, player)` | Instância | `ModItem` | `undefined` | O bônus do conjunto; o texto vai em player.setBonus. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L106) |
| `UpdateEquip(item, player)` | Instância | `ModItem` | `undefined` | Todo quadro, equipado (armadura ou acessório). | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L84) |
| `UpdateInventory(item, player)` | Instância | `ModItem` | `undefined` | Todo quadro, no inventário. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L144) |
| `UpdateVanity(item, player)` | Instância | `ModItem` | `undefined` | Acessório no slot de vaidade. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L90) |
| `UpdateVanitySet(player)` | Instância | `ModItem` | `undefined` | Antes e depois do FrameEffects, com o conjunto de vaidade. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L116) |
| `UseAnimation(item, player)` | Instância | `ModItem` | `undefined` | Antes do cálculo nativo da duração da animação. Antes do cálculo nativo da duração. Compartilha ApplyItemAnimation com os modificadores de ModPlayer. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L54) |
| `UseItem(item, player)` | Instância | `ModItem` | `undefined` | No quadro em que o uso começa. true: o item conta como usado (tempo de uso aplicado, como o ApplyItemTime do tModLoader), e o consumível é gasto. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L38) |
| `UseItemFrame(item, player)` | Instância | `ModItem` | `undefined` | Depois do frame nativo, durante animação de uso. Após PlayerFrame completo, com itemAnimation > 0. Filtro nativo pelo item selecionado. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L55) |
| `UseItemHitbox(item, player, hitbox, noHitbox)` | Instância | `ModItem` | `undefined` | Ref<Rectangle> e Ref<boolean> depois da hitbox nativa. Aceita substituir hitbox.value. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L82) |
| `UseStyle(item, player, mountOffset, heldItemFrame)` | Instância | `ModItem` | `undefined` | Todo quadro de uso, depois do estilo do jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L57) |
| `VerticalWingSpeeds(item, player, ascentWhenFalling, ascentWhenRising, maxCanAscendMultiplier, maxAscentMultiplier, constantAscend)` | Instância | `ModItem` | `undefined` | As asas deste item no ar (cinco Ref). | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L125) |
| `WeaponPrefix(item = this.Item)` | Instância | `ModItem` | `!!item.melee && !!item.noUseGraphic` | As categorias de prefixo do item (padrão: melee sem noUseGraphic, melee com, ranged, magic, summon). Só vale para item com dano, sem ser munição nem consumível. Lido uma vez por tipo. | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L149) |
| `WingUpdate(player, inUse)` | Instância | `ModItem` | `false` | true: o mod anima as asas (o WingFrame do jogo não roda). | [Código](../../app/src/main/cpp/script/js/mod/ModItem.js#L128) |

## ModLoader

[Contrato e campos](classes.md#mod-e-modloader).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `GetMod(name)` | Estático | `ModLoader` | `Consultar fonte` | O Mod; lança se não está. | [Código](../../app/src/main/cpp/script/js/mod/ModLoader.js#L14) |
| `HasMod(name)` | Estático | `ModLoader` | `ModRegistry.Find(name, 'ModLoader.HasMod') !== null` | Está instalado (e carregou)? | [Código](../../app/src/main/cpp/script/js/mod/ModLoader.js#L21) |
| `get Mods` | Getter estático | `ModLoader` | `ModRegistry.All()` | Todos, na ordem de carga. | [Código](../../app/src/main/cpp/script/js/mod/ModLoader.js#L25) |
| `TryGetMod(name, result)` | Estático | `ModLoader` | `Consultar fonte` | true e o Mod em ref.value; ou false e null. | [Código](../../app/src/main/cpp/script/js/mod/ModLoader.js#L2) |

## ModLocalization

[Contrato e campos](classes.md#modlocalization).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `get ActiveCultureName` | Getter estático | `ModLocalization` | `Consultar fonte` | 'pt-BR', 'en-US'... | [Código](../../app/src/main/cpp/script/js/mod/ModLocalization.js#L2) |
| `Exists(key)` | Estático | `ModLocalization` | `LocalizationLoader.Lookup(key) !== undefined \|\| Terraria.Localization.Language['bool Exists(string key)'](key)` | Se o mod ou o jogo tem o texto. | [Código](../../app/src/main/cpp/script/js/mod/ModLocalization.js#L48) |
| `GetText(key)` | Estático | `ModLocalization` | `Consultar fonte` | O LocalizedText do mod ou do jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModLocalization.js#L27) |
| `GetTextValue(key)` | Estático | `ModLocalization` | `Consultar fonte` | Texto do mod ou, sem ele, o do jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModLocalization.js#L22) |
| `Key(path)` | Estático | `ModLocalization` | `Consultar fonte` | A chave Mods.<id>.Secao.Chave (o que o Bestiário e a moeda pedem). | [Código](../../app/src/main/cpp/script/js/mod/ModLocalization.js#L40) |
| `Register(key, text)` | Estático | `ModLocalization` | `Consultar fonte` | Um texto (ou { cultura: texto }) sob uma chave qualquer. | [Código](../../app/src/main/cpp/script/js/mod/ModLocalization.js#L54) |
| `Translate(path)` | Estático | `ModLocalization` | `Consultar fonte` | O texto no idioma do jogo (como no TL); sem texto, o próprio caminho. | [Código](../../app/src/main/cpp/script/js/mod/ModLocalization.js#L11) |
| `TryTranslate(path)` | Estático | `ModLocalization` | `Consultar fonte` | O mesmo, com '' quando não há texto. | [Código](../../app/src/main/cpp/script/js/mod/ModLocalization.js#L16) |

## ModMenu

[Contrato e campos](classes.md#modmenu).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `get IsSelected` | Getter | `ModMenu` | `MenuLoader.CurrentMenu === this` | Se é o tema que vale agora (só leitura). | [Código](../../app/src/main/cpp/script/js/mod/ModMenu.js#L16) |
| `OnDeselected()` | Instância | `ModMenu` | `undefined` | Quando o tema passa a valer (na abertura do jogo, já nos menus) e quando deixa de valer. | [Código](../../app/src/main/cpp/script/js/mod/ModMenu.js#L21) |
| `OnSelected()` | Instância | `ModMenu` | `undefined` | Quando o tema passa a valer (na abertura do jogo, já nos menus) e quando deixa de valer. | [Código](../../app/src/main/cpp/script/js/mod/ModMenu.js#L19) |
| `PostDrawLogo(spriteBatch, logoDrawCenter, logoRotation, logoScale, drawColor)` | Instância | `ModMenu` | `undefined` | Depois do logo, com os valores com que ele foi desenhado. | [Código](../../app/src/main/cpp/script/js/mod/ModMenu.js#L31) |
| `PreDrawLogo(spriteBatch, logoDrawCenter, logoRotation, logoScale, drawColor)` | Instância | `ModMenu` | `true` | Antes do logo. Os quatro últimos são Ref (.value): o centro, a rotação, a escala e a cor do logo do jogo neste quadro. false não desenha o logo. | [Código](../../app/src/main/cpp/script/js/mod/ModMenu.js#L28) |
| `register(cls)` | Estático | `ModMenu` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModMenu.js#L35) |
| `SetStaticDefaults()` | Instância | `ModMenu` | `undefined` | A implementação base não executa ações. | [Código](../../app/src/main/cpp/script/js/mod/ModMenu.js#L33) |
| `Update(isOnTitleScreen)` | Instância | `ModMenu` | `undefined` | Todo quadro nos menus; isOnTitleScreen na tela de título (menuMode 0). | [Código](../../app/src/main/cpp/script/js/mod/ModMenu.js#L24) |

## ModMount

[Contrato e campos](classes.md#modmount).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `AddDrawData(playerDrawData, data)` | Estático | `ModMount` | `MountLoader.AddDrawData(playerDrawData, data)` | O playerDrawData.Add do tModLoader, dentro do Draw. | [Código](../../app/src/main/cpp/script/js/mod/ModMount.js#L44) |
| `AimAbility(player, mousePosition)` | Instância | `ModMount` | `undefined` | Habilidade da montaria. | [Código](../../app/src/main/cpp/script/js/mod/ModMount.js#L22) |
| `Dismount(player, skipDust)` | Instância | `ModMount` | `undefined` | Ao desmontar, antes do jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModMount.js#L25) |
| `Draw(playerDrawData, drawType, drawPlayer, texture, glowTexture, drawPosition, frame, drawColor, glowColor, rotation, spriteEffects, drawOrigin, drawScale, shadow)` | Instância | `ModMount` | `true` | Uma vez por camada com textura (0 Back, 1 BackExtra, 2 Front, 3 FrontExtra). Os Ref trazem o que o jogo desenharia; mudados, desenha-se com os novos. false: a camada não é desenhada. | [Código](../../app/src/main/cpp/script/js/mod/ModMount.js#L32) |
| `GetSpecificData(player)` | Estático | `ModMount` | `MountLoader.SpecificData(player)` | O dado do jogador nesta montaria (o _mountSpecificData do tModLoader, que no C# é object). Apagado ao montar outra e ao desmontar. | [Código](../../app/src/main/cpp/script/js/mod/ModMount.js#L37) |
| `JumpHeight(mountedPlayer, jumpHeight, xVelocity)` | Instância | `ModMount` | `undefined` | O pulo, com o valor do jogo no Ref. | [Código](../../app/src/main/cpp/script/js/mod/ModMount.js#L14) |
| `JumpSpeed(mountedPlayer, jumpSpeed, xVelocity)` | Instância | `ModMount` | `undefined` | O pulo, com o valor do jogo no Ref. | [Código](../../app/src/main/cpp/script/js/mod/ModMount.js#L15) |
| `NewDrawData(texture, position, sourceRect, color, rotation, origin, scale, effect)` | Estático | `ModMount` | `MountLoader.NewDrawData(texture, position, sourceRect, color, rotation, origin, scale, effect)` | Um DrawData. | [Código](../../app/src/main/cpp/script/js/mod/ModMount.js#L41) |
| `register(cls)` | Estático | `ModMount` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModMount.js#L46) |
| `SetMount(player, skipDust)` | Instância | `ModMount` | `undefined` | Ao montar, depois do FinalizeMountData; skipDust.value = true pula a poeira do jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModMount.js#L24) |
| `SetSpecificData(player, value)` | Estático | `ModMount` | `Consultar fonte` | O dado do jogador nesta montaria (o _mountSpecificData do tModLoader, que no C# é object). Apagado ao montar outra e ao desmontar. | [Código](../../app/src/main/cpp/script/js/mod/ModMount.js#L38) |
| `SetStaticDefaults()` | Instância | `ModMount` | `undefined` | Uma vez, com as texturas já no MountData. | [Código](../../app/src/main/cpp/script/js/mod/ModMount.js#L11) |
| `UpdateEffects(player)` | Instância | `ModMount` | `undefined` | Todo quadro montado, antes dos efeitos do jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModMount.js#L17) |
| `UpdateFrame(mountedPlayer, state, velocity)` | Instância | `ModMount` | `true` | A animação; false pula a do jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModMount.js#L20) |
| `UseAbility(player, mousePosition, toggleOn)` | Instância | `ModMount` | `undefined` | Habilidade da montaria. | [Código](../../app/src/main/cpp/script/js/mod/ModMount.js#L21) |

## ModNPC

[Contrato e campos](classes.md#modnpc).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `AddShops()` | Instância | `ModNPC` | `undefined` | Uma vez: as lojas (NPCShop). | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L89) |
| `AI(npc)` | Instância | `ModNPC` | `undefined` | Todo quadro. PreAI → false pula a IA do jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L55) |
| `ApplyBuffImmunity(npc)` | Instância | `ModNPC` | `undefined` | Depois do SetDefaults: npc.buffImmune[id] = true. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L30) |
| `ApplyDifficultyAndPlayerScaling(npc, numPlayers, balance, bossAdjustment)` | Instância | `ModNPC` | `undefined` | Depois do escalonamento por jogadores. Recebe a contagem usada pelo NPC, balance calculado pelo jogo e ajuste 0,85 em Master ou 1 nos demais modos. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L44) |
| `BossHeadRotation(npc, rotation)` | Instância | `ModNPC` | `undefined` | Recebem Ref inicializada com o resultado do jogo. Índice -1 esconde o ícone. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L51) |
| `BossHeadSlot(npc, index)` | Instância | `ModNPC` | `undefined` | Recebem Ref inicializada com o resultado do jogo. Índice -1 esconde o ícone. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L50) |
| `BossHeadSpriteEffects(npc, spriteEffects)` | Instância | `ModNPC` | `undefined` | Recebem Ref inicializada com o resultado do jogo. Índice -1 esconde o ícone. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L52) |
| `CanBeHitByItem(npc, player, item)` | Instância | `ModNPC` | `null` | false veta; true permite no teste de elegibilidade; null/undefined mantém o jogo. Um veto de ModPlayer prevalece. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L41) |
| `CanBeHitByProjectile` | Não implementado | Não implementado | — | Não há um ponto de elegibilidade que cubra todos os fluxos de Damage_PVE, incluindo projéteis sem dono. Um veto em StrikeNPC ocorreria após efeitos de contato. | [Código](../../tools/tests/modnpchooks/README.md#métodos-descartados) |
| `CanChat(npc)` | Instância | `ModNPC` | `npc.townNPC` | Decide se pode conversar, inclusive fora de AI 7; retorno indefinido mantém o jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L80) |
| `CanHitPlayer(npc, player, cooldownSlot)` | Instância | `ModNPC` | `true` | false veta dano; cooldownSlot.value altera o slot de imunidade. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L38) |
| `CanTownNPCSpawn(numTownNPCs)` | Instância | `ModNPC` | `false` | De tempos em tempos, sem um deste no mundo: true e ele se muda. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L76) |
| `CheckActive(npc)` | Instância | `ModNPC` | `true` | false: não some quando longe. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L60) |
| `CheckConditions(left, right, top, bottom)` | Instância | `ModNPC` | `true` | A sala serve para ele? | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L77) |
| `CheckDead(npc)` | Instância | `ModNPC` | `true` | Com NPC ativo e vida ≤ 0: false impede a morte; restaure a vida para evitar nova checagem. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L43) |
| `Clone(newNPC)` | Instância | `ModNPC` | `Entities.Clone(this)` | Cópia da instância. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L23) |
| `DrawBehind(npc, index)` | Instância | `ModNPC` | `undefined` | Após o cache de desenho; adicione o índice às listas Main.instance.DrawCacheNPCs. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L48) |
| `DrawEffects(npc, drawColor)` | Instância | `ModNPC` | `undefined` | Ref<Color> da iluminação do centro; roda antes de PreDraw. A cor alterada passa pelo tint dos buffs do jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L47) |
| `DrawHealthBar` | Não implementado | Não implementado | — | Main.DrawHealthBar não recebe NPC e também atende jogadores e partes de vermes. Associar pela posição ou vida poderia alterar a entidade errada. | [Código](../../tools/tests/modnpchooks/README.md#métodos-descartados) |
| `DrawTownAttackGun` | Não implementado | Não implementado | — | Textura, frame, escala e offsets são locais do renderer. Não há um método com todos os argumentos mutáveis. | [Código](../../tools/tests/modnpchooks/README.md#métodos-descartados) |
| `DrawTownAttackSwing` | Não implementado | Não implementado | — | Textura, frame, escala e offsets são locais do renderer. Não há um método com todos os argumentos mutáveis. | [Código](../../tools/tests/modnpchooks/README.md#métodos-descartados) |
| `FindFrame(npc, frameHeight)` | Instância | `ModNPC` | `undefined` | Animação própria: mude npc.frame. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L59) |
| `GetAlpha(npc, drawColor)` | Instância | `ModNPC` | `null` | Color substitui o resultado, inclusive alfa 0; null/undefined usa o jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L49) |
| `getByName(name)` | Estático | `ModNPC` | `NPCLoader.ByType.get(bl.npcs.typeOf(name))` | Como no item. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L204) |
| `GetChat(npc)` | Instância | `ModNPC` | `undefined` | A fala ao conversar. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L79) |
| `getModNPC(type)` | Estático | `ModNPC` | `NPCLoader.ByType.get(type)` | Como no item. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L203) |
| `getTypeByName(name)` | Estático | `ModNPC` | `bl.npcs.typeOf(name)` | Como no item. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L202) |
| `get Happiness` | Getter | `ModNPC` | `new NPCHappiness(this.Type)` | NPCHappiness | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L21) |
| `HitEffect(npc, hitDirection, damage)` | Instância | `ModNPC` | `undefined` | A cada golpe, depois do efeito do jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L33) |
| `isModNPC(npc)` | Estático | `ModNPC` | `!!npc && bl.npcs.isModNpc(npc.type)` | Como no item. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L201) |
| `isModType(type)` | Estático | `ModNPC` | `bl.npcs.isModNpc(type)` | Como no item. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L200) |
| `ModifyActiveShop(npc, shopName, items)` | Instância | `ModNPC` | `undefined` | Ao preencher uma loja registrada deste NPC em conversa, após NPCShop montar os itens. Recebe o nome local da loja e o array nativo de itens. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L82) |
| `ModifyDeathMessage` | Não implementado | Não implementado | — | Mensagem e cor são locais de checkDead. DropTombstoneTownNPC expõe apenas o texto da lápide. | [Código](../../tools/tests/modnpchooks/README.md#métodos-descartados) |
| `ModifyHitPlayer(npc, player, modifiers)` | Instância | `ModNPC` | `undefined` | Altera os parâmetros de dano suportados pelo mobile antes de Hurt. Contrato JS: damage, hitDirection, quiet, crit e dodgeable. Não contém todos os campos de HurtModifiers do tModLoader. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L39) |
| `ModifyIncomingHit(npc, modifiers)` | Instância | `ModNPC` | `undefined` | Altera dano, empurrão, direção e crítico antes do golpe nativo. Não reaplica em fromNet. Contrato JS: damage, knockBack, hitDirection, crit, SourceDamage e Knockback (StatModifier), SetCrit() e DisableCrit(). Não reaplica em fromNet. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L42) |
| `ModifyNPCHappiness(npc, player, primaryPlayerBiome, shopHelper, nearbyNPCsByType)` | Instância | `ModNPC` | `undefined` | Após as preferências nativas, antes de limitar o preço. Usa a alternativa de ExMod; o método está desativado no tModLoader stable. Adaptação do ExMod. Bioma primário: 0 floresta, 1 subterrâneo, 2 neve, 3 deserto, 4 selva, 5 oceano, 6 sagrado, 7 cogumelo, 8 masmorra, 9 corrupção, 10 carmim. shopHelper e vizinhos são nativos. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L83) |
| `ModifyNPCLoot(npcLoot)` | Instância | `ModNPC` | `undefined` | Uma vez: os drops. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L31) |
| `get NPC` | Getter | `ModNPC` | `Entities.Of(this)` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L20) |
| `NPCHeadSlot()` | Instância | `ModNPC` | `bl.npcs.headSlot(this.Type)` | (para ler) o índice da cabeça, -1 sem cabeça. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L84) |
| `NPCValue(p = 0, g = 0, s = 0, c = 0)` | Estático | `ModNPC` | `p * 1000000 + g * 10000 + s * 100 + c` | O dinheiro que ele solta. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L95) |
| `OnChatButtonClicked(npc, firstButton)` | Instância | `ModNPC` | `undefined` | Tocou num botão; devolva o nome de uma loja para abri-la. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L88) |
| `OnHitByItem(npc, player, item, hit, damageDone)` | Instância | `ModNPC` | `undefined` | Após dano efetivo de item, uma vez por golpe. hit contém Damage, SourceDamage, Knockback, HitDirection e Crit. damageDone é o retorno efetivo nativo. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L36) |
| `OnHitByProjectile(npc, projectile, hit, damageDone)` | Instância | `ModNPC` | `undefined` | Após dano efetivo de projétil, inclusive sem dono jogador. Inclui projéteis sem dono jogador. hit contém Damage, SourceDamage, Knockback, HitDirection e Crit. damageDone é o retorno efetivo nativo. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L37) |
| `OnHitPlayer(npc, player, hurtInfo)` | Instância | `ModNPC` | `undefined` | Após dano efetivo, recebe os dados do golpe e o slot usado. hurtInfo contém DamageSource, Damage, HitDirection, PvP, Quiet, Crit, CooldownCounter e Dodgeable. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L40) |
| `OnKill(npc)` | Instância | `ModNPC` | `undefined` | Na morte. PreKill → false cancela o drop. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L62) |
| `OnSpawn(npc, source)` | Instância | `ModNPC` | `undefined` | Uma vez após nascer; recebe a fonte nativa. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L34) |
| `PostAI(npc)` | Instância | `ModNPC` | `undefined` | Todo quadro. PreAI → false pula a IA do jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L56) |
| `PostDraw(npc, spriteBatch, screenPos, drawColor)` | Instância | `ModNPC` | `undefined` | Após desenhar, inclusive quando PreDraw veta. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L46) |
| `PostSetDefaults(npc)` | Instância | `ModNPC` | `undefined` | A implementação base não executa ações. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L28) |
| `PostSetupContent()` | Instância | `ModNPC` | `undefined` | A implementação base não executa ações. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L29) |
| `PostStaticDefaults()` | Instância | `ModNPC` | `undefined` | A implementação base não executa ações. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L27) |
| `PreAI(npc)` | Instância | `ModNPC` | `true` | Todo quadro. PreAI → false pula a IA do jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L54) |
| `PreDraw(npc, spriteBatch, screenPos, drawColor)` | Instância | `ModNPC` | `true` | false pula o desenho nativo. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L45) |
| `PreKill(npc)` | Instância | `ModNPC` | `true` | Na morte. PreKill → false cancela o drop. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L61) |
| `ReceiveExtraAI(reader)` | Instância | `ModNPC` | `undefined` | A implementação base não executa ações. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L58) |
| `register(cls)` | Estático | `ModNPC` | `Consultar fonte` | Como no item. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L99) |
| `ResetEffects(npc)` | Instância | `ModNPC` | `undefined` | Depois de zerar os flags de buffs, antes de reaplicar efeitos e executar AI. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L35) |
| `SendExtraAI(writer)` | Instância | `ModNPC` | `undefined` | A implementação base não executa ações. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L57) |
| `SetBestiary(database, bestiaryEntry)` | Instância | `ModNPC` | `undefined` | Uma vez: a entrada do Bestiário. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L32) |
| `SetChatButtons(npc, buttons)` | Instância | `ModNPC` | `undefined` | Os botões da conversa (até dois no celular). | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L86) |
| `SetDefaults(npc)` | Instância | `ModNPC` | `undefined` | Todo NPC deste tipo que nasce. A escala de Expert/Mestre vem depois. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L26) |
| `SetNPCNameList()` | Instância | `ModNPC` | `[]` | Os nomes próprios; um é sorteado. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L78) |
| `SetStaticDefaults()` | Instância | `ModNPC` | `undefined` | Uma vez. Main.npcFrameCount[this.Type] = n: quadros da textura. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L25) |
| `SpawnChance(spawnInfo)` | Instância | `ModNPC` | `0` | A cada spawn natural: devolva o peso (o do jogo pesa 1; 0 = não nasce). Só sozinho ou no servidor. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L67) |
| `SpawnNPC(tileX, tileY)` | Instância | `ModNPC` | `Consultar fonte` | Sorteado: como nasce, no bloco do spawn; devolve o índice. Padrão: em cima do bloco (tileX  16 + 8, tileY  16), como no tModLoader. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L70) |
| `TownNPCAttackCooldown` | Não implementado | Não implementado | — | Cooldown e seus parâmetros ficam em variáveis locais da AI de moradores. Não há helper interceptável com as referências completas. | [Código](../../tools/tests/modnpchooks/README.md#métodos-descartados) |
| `TownNPCAttackMagic` | Não implementado | Não implementado | — | Os parâmetros da aura ficam em variáveis locais da AI de moradores. Não há helper interceptável com as referências completas. | [Código](../../tools/tests/modnpchooks/README.md#métodos-descartados) |
| `TownNPCAttackProj(npc, attack)` | Instância | `ModNPC` | `undefined` | O projétil do ataque (projType, attackDelay). | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L92) |
| `TownNPCAttackProjSpeed(npc, attack)` | Instância | `ModNPC` | `undefined` | speed, gravityCorrection, randomOffset. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L93) |
| `TownNPCAttackShoot` | Não implementado | Não implementado | — | Os parâmetros de tiro ficam em variáveis locais da AI de moradores. Não há helper interceptável com as referências completas. | [Código](../../tools/tests/modnpchooks/README.md#métodos-descartados) |
| `TownNPCAttackStrength(npc, attack)` | Instância | `ModNPC` | `undefined` | damage, knockback. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L91) |
| `TownNPCAttackSwing` | Não implementado | Não implementado | — | Os parâmetros da hitbox ficam em variáveis locais da AI de moradores. Não há helper interceptável com as referências completas. | [Código](../../tools/tests/modnpchooks/README.md#métodos-descartados) |
| `TownNPCProfile()` | Instância | `ModNPC` | `null` | Uma vez após carregar conteúdo: devolva um ITownNPCProfile nativo; null mantém a aparência automática. Chamado uma vez no template, com conteúdo pronto. Retorne um ITownNPCProfile nativo ou null. | [Código](../../app/src/main/cpp/script/js/mod/ModNPC.js#L81) |
| `UpdateLifeRegen` | Não implementado | Não implementado | — | UpdateNPC_BuffApplyDOTs acumula e aplica o dano em uma DOTTally local. A referência de dano não fica acessível antes da aplicação. | [Código](../../tools/tests/modnpchooks/README.md#métodos-descartados) |

## ModPacket

[Contrato e campos](classes.md#rede). Herda de `NetWriter`.

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `constructor(mod)` | Construtor | `ModPacket` | `instância` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModPacket.js#L3) |
| `Send(toClient = -1, ignoreClient = -1)` | Instância | `ModPacket` | `Consultar fonte` | Do cliente: ao servidor. Do servidor: a um cliente, ou a todos menos um. | [Código](../../app/src/main/cpp/script/js/mod/ModPacket.js#L9) |
| `Write(value)` | Instância | `NetWriter` (herdado) | `Consultar fonte` | Guarda número, texto, booleano, array, objeto simples ou Vector2. Os nomes com tipo (WriteInt32...) fazem o mesmo. Grava valores JS no protocolo do loader, não bytes de BinaryWriter. As variantes WriteByte, WriteInt32 etc. são aliases de Write. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L6) |
| `Write7BitEncodedInt(value)` | Instância | `NetWriter` (herdado) | `Consultar fonte` | Alias de NetWriter.Write. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteBoolean(value)` | Instância | `NetWriter` (herdado) | `Consultar fonte` | Alias de NetWriter.Write. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteByte(value)` | Instância | `NetWriter` (herdado) | `Consultar fonte` | Alias de NetWriter.Write. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteDouble(value)` | Instância | `NetWriter` (herdado) | `Consultar fonte` | Alias de NetWriter.Write. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteFlags(...flags)` | Instância | `NetWriter` (herdado) | `Consultar fonte` | Vários booleanos; um vetor. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L12) |
| `WriteInt16(value)` | Instância | `NetWriter` (herdado) | `Consultar fonte` | Alias de NetWriter.Write. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteInt32(value)` | Instância | `NetWriter` (herdado) | `Consultar fonte` | Alias de NetWriter.Write. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteInt64(value)` | Instância | `NetWriter` (herdado) | `Consultar fonte` | Alias de NetWriter.Write. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteSByte(value)` | Instância | `NetWriter` (herdado) | `Consultar fonte` | Alias de NetWriter.Write. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteSingle(value)` | Instância | `NetWriter` (herdado) | `Consultar fonte` | Alias de NetWriter.Write. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteString(value)` | Instância | `NetWriter` (herdado) | `Consultar fonte` | Alias de NetWriter.Write. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteUInt16(value)` | Instância | `NetWriter` (herdado) | `Consultar fonte` | Alias de NetWriter.Write. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteUInt32(value)` | Instância | `NetWriter` (herdado) | `Consultar fonte` | Alias de NetWriter.Write. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteVector2(v)` | Instância | `NetWriter` (herdado) | `Consultar fonte` | Vários booleanos; um vetor. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L13) |

## ModPlayer

[Contrato e campos](classes.md#modplayer).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `AddMaterialsForCrafting(player, itemConsumedCallback)` | Instância | `ModPlayer` | `null` | Retorna itens; ponha uma função (item, index) em itemConsumedCallback.value. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L134) |
| `AddStartingItems(player, mediumCoreDeath)` | Instância | `ModPlayer` | `[]` | Retorna uma lista de itens; padrão vazio. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L132) |
| `AnglerQuestReward(player, rareMultiplier, rewardItems)` | Instância | `ModPlayer` | `undefined` | Lista JS mutável de itens antes da entrega. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L121) |
| `ApplyPotionDelay(player, item, potionDelay)` | Instância | `ModPlayer` | `true` | Recebe o tempo efetivo, incluindo sorteios; false impede o atraso e o buff. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L86) |
| `ArmorSetBonusActivated(player)` | Instância | `ModPlayer` | `undefined` | Duplo toque na direção de ativação configurada. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L95) |
| `ArmorSetBonusHeld(player, holdTime)` | Instância | `ModPlayer` | `undefined` | Direção de ativação mantida pressionada. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L96) |
| `CanAutoReuseItem(player, item)` | Instância | `ModPlayer` | `null` | Decisão opcional, preservando os campos permanentes do item. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L72) |
| `CanBeHitByNPC(player, npc, cooldownSlot)` | Instância | `ModPlayer` | `true` | Veto; cooldownSlot é Ref<int>. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L60) |
| `CanBeHitByProjectile(player, projectile)` | Instância | `ModPlayer` | `true` | Veto ao dano recebido de projétil. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L61) |
| `CanBeTeleportedTo(player, teleportPosition, context)` | Instância | `ModPlayer` | `true` | Veto no teleporte nativo; contexto 'TeleportRod', 'TeleportationPotion', 'Teleport' ou 'Wormhole'. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L138) |
| `CanBuyItem(player, vendor, shopInventory, item)` | Instância | `ModPlayer` | `true` | Veto antes do pagamento da compra. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L122) |
| `CanCatchNPC(player, target, item)` | Instância | `ModPlayer` | `null` | Decisão opcional para a captura. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L130) |
| `CanConsumeAmmo(player, weapon, ammo)` | Instância | `ModPlayer` | `true` | false preserva a munição; não consulta em chamadas dontConsume. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L70) |
| `CanConsumeBait(player, bait)` | Instância | `ModPlayer` | `null` | Decisão opcional depois do sorteio nativo de consumo. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L120) |
| `CanHitNPC(player, target)` | Instância | `ModPlayer` | `true` | true por padrão; veto geral. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L46) |
| `CanHitNPCWithItem(player, item, target)` | Instância | `ModPlayer` | `null` | null por padrão; decisão opcional para o item. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L47) |
| `CanHitNPCWithProj(player, projectile, target)` | Instância | `ModPlayer` | `null` | Decisão opcional para o projétil. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L48) |
| `CanHitPvp(player, item, target)` | Instância | `ModPlayer` | `true` | false impede o acerto corpo a corpo em outro jogador. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L49) |
| `CanMeleeAttackCollideWithNPC(player, item, hitbox, target)` | Instância | `ModPlayer` | `null` | Decisão opcional para a interseção do golpe. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L50) |
| `CanSellItem(player, vendor, shopInventory, item)` | Instância | `ModPlayer` | `true` | Veto antes da venda. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L124) |
| `CanShoot(player, item)` | Instância | `ModPlayer` | `true` | Veta o disparo, mantendo o tempo de uso. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L67) |
| `CanShowExtraJumpVisuals(player, jump)` | Instância | `ModPlayer` | `true` | Veto aos efeitos nativos do salto ativo. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L100) |
| `CanStartExtraJump(player, jump)` | Instância | `ModPlayer` | `true` | Veto ao salto extra disponível. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L99) |
| `CanUseItem(player, item)` | Instância | `ModPlayer` | `true` | false impede usar. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L33) |
| `CatchFish(attempt, itemDrop, npcSpawn, sonar, sonarPosition)` | Instância | `ModPlayer` | `undefined` | Pescou, depois do sorteio do jogo: itemDrop.value e npcSpawn.value (Ref, 0 = nada) decidem o que sai; attempt tem questFish, common/uncommon/rare..., inLava... sonar/sonarPosition ainda não fazem nada. itemDrop e npcSpawn são Ref. sonar e sonarPosition ainda não são aplicados ao jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L44) |
| `ConsumableDodge(player, info)` | Instância | `ModPlayer` | `false` | true evita o dano após ModifyHurt e FreeDodge. Só para golpes esquiváveis do jogador local. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L38) |
| `CopyClientState(player, targetCopy)` | Instância | `ModPlayer` | `undefined` | Copia para outra instância da mesma classe, associada ao clone nativo. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L145) |
| `DrawEffects(player, drawInfo, r, g, b, a, fullBright)` | Instância | `ModPlayer` | `undefined` | Multiplicadores Ref<float> e Ref<bool>; depois da preparação nativa. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L108) |
| `DrawPlayer(player, camera)` | Instância | `ModPlayer` | `undefined` | Depois do desenho nativo completo. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L109) |
| `EmitEnchantmentVisualsAt(player, projectile, position, width, height)` | Instância | `ModPlayer` | `undefined` | Depois dos efeitos de encantamento do projétil. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L59) |
| `ExtraJumpVisuals(player, jump)` | Instância | `ModPlayer` | `undefined` | Efeitos adicionais do salto permitido. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L101) |
| `FrameEffects(player)` | Instância | `ModPlayer` | `undefined` | Depois de o jogo montar o que se desenha: trocar player.head/body/legs muda o desenho. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L28) |
| `FreeDodge(player, damageSource, damage, hitDirection, pvp, quiet, crit, cooldownCounter, dodgeable)` | Instância | `ModPlayer` | `false` | true: esquiva. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L37) |
| `get(player)` | Estático | `ModPlayer` | `PlayerLoader.Of(player).get(this)` | O mesmo. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L152) |
| `getByName(name)` | Estático | `ModPlayer` | `PlayerLoader.Find(Terraria.Main.player[Terraria.Main.myPlayer], name)` | A do jogador local (para interface). | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L156) |
| `GetDyeTraderReward(player, rewardPool)` | Instância | `ModPlayer` | `undefined` | Lista JS mutável de IDs antes do sorteio. Lista vazia produz item vazio. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L129) |
| `GetFishingLevel(player, fishingRod, bait, fishingLevel)` | Instância | `ModPlayer` | `undefined` | Ref<float>; os itens podem ser null quando não encontrados. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L117) |
| `GetHealLife(player, item, quickHeal, healValue)` | Instância | `ModPlayer` | `undefined` | healValue é Ref<int>; também influencia a escolha da cura rápida. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L84) |
| `GetHealMana(player, item, quickHeal, healValue)` | Instância | `ModPlayer` | `undefined` | healValue é Ref<int>. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L85) |
| `HideDrawLayers(player, drawInfo)` | Instância | `ModPlayer` | `undefined` | Chame PlayerDrawLayers.Head.Hide(), por exemplo, para aquele desenho. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L110) |
| `HoverSlot(player, inventory, context, slot)` | Instância | `ModPlayer` | `false` | true trata o hover e pula o nativo. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L136) |
| `ImmuneTo(player, damageSource, cooldownCounter, dodgeable)` | Instância | `ModPlayer` | `false` | true: o golpe não acontece. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L36) |
| `Initialize()` | Instância | `ModPlayer` | `undefined` | Uma vez, quando a instância nasce. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L8) |
| `Kill(player, damageSource, damage, hitDirection, pvp)` | Instância | `ModPlayer` | `undefined` | Morreu. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L43) |
| `LoadData(data)` | Instância | `ModPlayer` | `undefined` | Ao carregar o personagem. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L150) |
| `MeleeEffects(player, item, hitbox)` | Instância | `ModPlayer` | `undefined` | Durante os efeitos visuais de uso. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L58) |
| `ModifyCaughtFish(player, fish)` | Instância | `ModPlayer` | `undefined` | Altera o item efetivo antes de entregá-lo. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L119) |
| `ModifyDrawInfo(player, drawInfo)` | Instância | `ModPlayer` | `undefined` | Altera o PlayerDrawSet antes de construir as camadas. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L111) |
| `ModifyDrawLayerOrdering(player, positions)` | Instância | `ModPlayer` | `undefined` | Map de descritor para { Before: descritor } ou { After: descritor }. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L112) |
| `ModifyExtraJumpDurationMultiplier(player, jump, duration)` | Instância | `ModPlayer` | `undefined` | duration é Ref<float>, inicialmente 1. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L102) |
| `ModifyFishingAttempt(player, attempt)` | Instância | `ModPlayer` | `undefined` | Antes do sorteio do item; altera a tentativa diretamente. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L118) |
| `ModifyHitByNPC(player, npc, modifiers)` | Instância | `ModPlayer` | `undefined` | Antes de ModifyHurt. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L62) |
| `ModifyHitByProjectile(player, projectile, modifiers)` | Instância | `ModPlayer` | `undefined` | Antes de ModifyHurt. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L63) |
| `ModifyHitNPC(player, target, modifiers)` | Instância | `ModPlayer` | `undefined` | Antes do dano nativo, seguido do callback específico da origem. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L51) |
| `ModifyHitNPCWithItem(player, item, target, modifiers)` | Instância | `ModPlayer` | `undefined` | Modifica o golpe de item. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L52) |
| `ModifyHitNPCWithProj(player, projectile, target, modifiers)` | Instância | `ModPlayer` | `undefined` | Modifica o golpe de projétil. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L53) |
| `ModifyHurt(player, modifiers)` | Instância | `ModPlayer` | `undefined` | Antes do golpe: modifiers.damage, hitDirection, quiet, crit, dodgeable. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L39) |
| `ModifyItemScale(player, item, scale)` | Instância | `ModPlayer` | `undefined` | scale é Ref<float> do multiplicador. Atua em GetAdjustedItemScale e na hitbox nativa, junto com ModItem. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L75) |
| `ModifyLuck(player, luck)` | Instância | `ModPlayer` | `undefined` | A sorte, luck.value; PreModifyLuck false pula a do jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L31) |
| `ModifyManaCost(player, item, reduce, mult)` | Instância | `ModPlayer` | `undefined` | Ref<float>: custo atual × (1 - reduce.value) × mult.value. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L79) |
| `ModifyMaxStats(player)` | Instância | `ModPlayer` | `Consultar fonte` | Depois do ResetEffects. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L13) |
| `ModifyNurseHeal(player, nurse, health, removeDebuffs, chatText)` | Instância | `ModPlayer` | `true` | Três Ref; false impede o atendimento e usa o texto informado. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L127) |
| `ModifyNursePrice(player, nurse, health, removeDebuffs, price)` | Instância | `ModPlayer` | `undefined` | price é Ref<int>; consulta também ao mostrar o preço. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L126) |
| `ModifyScreenPosition(player)` | Instância | `ModPlayer` | `undefined` | Depois de atualizar a câmera; altere Main.screenPosition. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L114) |
| `ModifyShootStats(player, item, position, velocity, type, damage, knockBack)` | Instância | `ModPlayer` | `undefined` | Os cinco últimos parâmetros são Ref. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L69) |
| `ModifyStartingInventory(player, itemsByMod, mediumCoreDeath)` | Instância | `ModPlayer` | `undefined` | Map de UUID para listas de itens; itens nativos sob 'Terraria'. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L133) |
| `ModifyWeaponCrit(player, item, crit)` | Instância | `ModPlayer` | `undefined` | crit é Ref<int>. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L73) |
| `ModifyWeaponDamage(player, item, damage)` | Instância | `ModPlayer` | `undefined` | Devolva o dano novo. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L34) |
| `ModifyWeaponKnockback(player, item, knockback)` | Instância | `ModPlayer` | `undefined` | knockback é StatModifier. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L74) |
| `ModifyZoom(player, zoom)` | Instância | `ModPlayer` | `undefined` | Ref<float>; preserva a proporção dos eixos. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L115) |
| `NaturalLifeRegen(player, regen)` | Instância | `ModPlayer` | `undefined` | Ref<float> no cálculo nativo da regeneração natural. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L91) |
| `OnCatchNPC(player, npc, item, failed)` | Instância | `ModPlayer` | `undefined` | Captura concluída ou tentativa que provoca dano ao tentar pegar criatura de lava. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L131) |
| `OnConsumeAmmo(player, weapon, ammo)` | Instância | `ModPlayer` | `undefined` | Quando a pilha diminui; conserva o tipo da última munição antes de TurnToAir. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L71) |
| `OnConsumeMana(player, item, manaConsumed)` | Instância | `ModPlayer` | `undefined` | Notifica a mana consumida. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L80) |
| `OnEnterWorld(player)` | Instância | `ModPlayer` | `undefined` | Entrou no mundo. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L9) |
| `OnEquipmentLoadoutSwitched(player, oldLoadoutIndex, loadoutIndex)` | Instância | `ModPlayer` | `undefined` | Depois de uma troca efetiva. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L97) |
| `OnExtraJumpCleared(player, jump)` | Instância | `ModPlayer` | `undefined` | Ao retirar a disponibilidade de um salto sem equipamento habilitador. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L106) |
| `OnExtraJumpEnded(player, jump)` | Instância | `ModPlayer` | `undefined` | Ao encerrar o salto ativo. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L104) |
| `OnExtraJumpRefreshed(player, jump)` | Instância | `ModPlayer` | `undefined` | Após renovar os saltos disponíveis dos equipamentos. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L105) |
| `OnExtraJumpStarted(player, jump, playSound)` | Instância | `ModPlayer` | `undefined` | playSound é Ref<bool>; false suprime o som do início. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L103) |
| `OnHitAnything(player, x, y, victim)` | Instância | `ModPlayer` | `undefined` | Depois de Player.OnHit. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L57) |
| `OnHitByNPC(player, npc, info)` | Instância | `ModPlayer` | `undefined` | Depois do dano positivo causado pelo NPC. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L64) |
| `OnHitByProjectile(player, projectile, info)` | Instância | `ModPlayer` | `undefined` | Depois do dano positivo causado pelo projétil. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L65) |
| `OnHitNPC(player, target, hit, damageDone)` | Instância | `ModPlayer` | `undefined` | Depois de um acerto com dano positivo. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L54) |
| `OnHitNPCWithItem(player, item, target, hit, damageDone)` | Instância | `ModPlayer` | `undefined` | Notificação específica do item. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L55) |
| `OnHitNPCWithProj(player, projectile, target, hit, damageDone)` | Instância | `ModPlayer` | `undefined` | Notificação específica do projétil. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L56) |
| `OnHurt(player, damageSource, damage, hitDirection, pvp, quiet, crit, cooldownCounter, dodgeable)` | Instância | `ModPlayer` | `undefined` | Depois do golpe. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L40) |
| `OnMissingMana(player, item, neededMana)` | Instância | `ModPlayer` | `undefined` | Antes da tentativa nativa de recuperar mana. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L81) |
| `OnPickup(player, item)` | Instância | `ModPlayer` | `true` | false elimina o item do chão sem pôr no inventário. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L135) |
| `OnRespawn(player)` | Instância | `ModPlayer` | `undefined` | Voltou a viver. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L10) |
| `get Player` | Getter | `ModPlayer` | `Entities.Of(this)` | O jogador desta instância. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L6) |
| `PlayerConnect(player)` | Instância | `ModPlayer` | `undefined` | Após a conexão do jogador. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L146) |
| `PlayerDisconnect(player)` | Instância | `ModPlayer` | `undefined` | Antes de remover o jogador desconectado. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L147) |
| `PostBuyItem(player, vendor, shopInventory, item)` | Instância | `ModPlayer` | `undefined` | Após compra paga; item recebido pelo cursor. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L123) |
| `PostHurt(player, damageSource, damage, hitDirection, pvp, quiet, crit, cooldownCounter, dodgeable)` | Instância | `ModPlayer` | `undefined` | Depois do golpe, se sobreviveu. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L41) |
| `PostItemCheck(player)` | Instância | `ModPlayer` | `undefined` | Após a verificação, inclusive quando vetada. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L83) |
| `PostNurseHeal(player, nurse, health, removeDebuffs, price)` | Instância | `ModPlayer` | `undefined` | Depois de um atendimento pago, incluindo preço zero. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L128) |
| `PostSavePlayer(player)` | Instância | `ModPlayer` | `undefined` | Ao finalizar o save nativo e os dados auxiliares, inclusive se o save nativo lançar erro. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L141) |
| `PostSellItem(player, vendor, shopInventory, item)` | Instância | `ModPlayer` | `undefined` | Após venda aceita. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L125) |
| `PostUpdate(player)` | Instância | `ModPlayer` | `undefined` | Fim do quadro. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L18) |
| `PostUpdateBuffs(player)` | Instância | `ModPlayer` | `undefined` | Em volta dos buffs. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L20) |
| `PostUpdateEquips(player)` | Instância | `ModPlayer` | `undefined` | Depois dos equipamentos. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L22) |
| `PostUpdateMiscEffects(player)` | Instância | `ModPlayer` | `undefined` | Depois de CapAttackSpeeds. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L89) |
| `PostUpdateRunSpeeds(player)` | Instância | `ModPlayer` | `undefined` | Antes de HorizontalMovement. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L90) |
| `PreItemCheck(player)` | Instância | `ModPlayer` | `true` | false pula a verificação dos itens. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L82) |
| `PreKill(player, damageSource, damage, hitDirection, pvp)` | Instância | `ModPlayer` | `true` | false impede a morte. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L42) |
| `PreModifyLuck(player, luck)` | Instância | `ModPlayer` | `true` | A sorte, luck.value; PreModifyLuck false pula a do jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L30) |
| `PreSaveCustomData(player)` | Instância | `ModPlayer` | `undefined` | Antes de montar os dados do arquivo .plr.bl.json. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L142) |
| `PreSavePlayer(player)` | Instância | `ModPlayer` | `undefined` | Antes do save nativo. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L140) |
| `PreUpdate(player)` | Instância | `ModPlayer` | `undefined` | Começo do quadro do jogador. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L17) |
| `PreUpdateBuffs(player)` | Instância | `ModPlayer` | `undefined` | Em volta dos buffs. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L19) |
| `PreUpdateMovement(player)` | Instância | `ModPlayer` | `undefined` | Antes da primeira etapa de colisão e deslocamento do quadro. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L88) |
| `ProcessTriggers(player, triggersSet)` | Instância | `ModPlayer` | `undefined` | Após copiar os controles para o jogador local. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L93) |
| `register(cls)` | Estático | `ModPlayer` | `Consultar fonte` | Registra. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L160) |
| `ResetEffects(player)` | Instância | `ModPlayer` | `undefined` | Logo depois do jogo zerar os efeitos. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L12) |
| `ResetInfoAccessories(player)` | Instância | `ModPlayer` | `undefined` | Depois de zerar efeitos e depois de atualizar os acessórios informativos. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L94) |
| `SaveData(data)` | Instância | `ModPlayer` | `undefined` | A cada save: ponha o que lembrar em data. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L149) |
| `SendClientChanges(player, clientPlayer)` | Instância | `ModPlayer` | `undefined` | No cliente, compara com a instância copiada no quadro anterior. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L144) |
| `ShiftClickSlot(player, inventory, context, slot)` | Instância | `ModPlayer` | `false` | Com Shift ativo, true trata o clique. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L137) |
| `Shoot(player, item, source, position, velocity, type, damage, knockBack)` | Instância | `ModPlayer` | `true` | false impede a criação do projétil. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L68) |
| `SyncPlayer(player, toWho, fromWho, newPlayer)` | Instância | `ModPlayer` | `undefined` | Após sincronizar o jogador nativo. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L143) |
| `TransformDrawData(player, drawInfo)` | Instância | `ModPlayer` | `undefined` | Depois das transformações nativas e antes de renderizar o cache. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L113) |
| `UpdateAutopause(player)` | Instância | `ModPlayer` | `undefined` | Após a atualização pausada do jogador local. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L92) |
| `UpdateBadLifeRegen(player)` | Instância | `ModPlayer` | `undefined` | Antes e depois da regeneração de vida. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L23) |
| `UpdateDead(player)` | Instância | `ModPlayer` | `undefined` | Todo quadro morto. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L26) |
| `UpdateEquips(player)` | Instância | `ModPlayer` | `undefined` | Depois dos equipamentos. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L21) |
| `UpdateLifeRegen(player)` | Instância | `ModPlayer` | `undefined` | Antes e depois da regeneração de vida. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L24) |
| `UpdateManaRegen(player)` | Instância | `ModPlayer` | `undefined` | Depois da regeneração de mana. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L25) |
| `UpdateMovement(player)` | Instância | `ModPlayer` | `undefined` | Movimento próprio (dash), perto do fim do quadro. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L27) |
| `UseAnimationMultiplier(player, item)` | Instância | `ModPlayer` | `1` | Multiplica o tempo da animação; padrão 1. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L78) |
| `UseSpeedMultiplier(player, item)` | Instância | `ModPlayer` | `1` | Divisor dos tempos de uso e animação; padrão 1. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L76) |
| `UseTimeMultiplier(player, item)` | Instância | `ModPlayer` | `1` | Multiplica o tempo de uso; padrão 1. | [Código](../../app/src/main/cpp/script/js/mod/ModPlayer.js#L77) |

## ModPrefix

[Contrato e campos](classes.md#modprefix).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `AllStatChangesHaveEffectOn(item)` | Instância | `ModPrefix` | `true` | false: não pega neste item (para status que não são do jogo). | [Código](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L29) |
| `Apply(item)` | Instância | `ModPrefix` | `undefined` | Depois dos status: o que mais o prefixo muda no item. | [Código](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L32) |
| `ApplyAccessoryEffects(player)` | Instância | `ModPrefix` | `undefined` | A cada quadro, com o acessório equipado. | [Código](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L38) |
| `CanRoll(item)` | Instância | `ModPrefix` | `this.RollChance(item) > 0` | false: este item não pode ganhar (padrão: RollChance > 0). | [Código](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L19) |
| `get Category` | Getter | `ModPrefix` | `PrefixCategory.Custom` | PrefixCategory | [Código](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L12) |
| `get FullName` | Getter | `ModPrefix` | `(this.Mod ? this.Mod.id \|\| this.Mod.uuid : '?') + '/' + this.Name` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L9) |
| `getModPrefix(type)` | Estático | `ModPrefix` | `PrefixLoader.GetPrefix(type)` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L55) |
| `GetTooltipLines(item)` | Instância | `ModPrefix` | `null` | Linhas a mais (array de TooltipLine; IsModifier = true pinta de verde). As de dano, velocidade, crítico etc. o jogo já escreve. | [Código](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L41) |
| `getTypeByName(name)` | Estático | `ModPrefix` | `ContentLookup.TypeOf(PrefixLoader.ByType, name)` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L56) |
| `isModType(type)` | Estático | `ModPrefix` | `PrefixLoader.GetPrefix(type) !== undefined` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L54) |
| `ModifyValue(valueMult)` | Instância | `ModPrefix` | `undefined` | O preço (Ref); a raridade sobe ou desce junto. | [Código](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L35) |
| `get Name` | Getter | `ModPrefix` | `this.constructor.name` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L8) |
| `register(cls)` | Estático | `ModPrefix` | `Consultar fonte` | Registra na mão; devolve o tipo. | [Código](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L43) |
| `RollChance(item)` | Instância | `ModPrefix` | `1` | O peso na rolagem (cada prefixo do jogo pesa 1). | [Código](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L18) |
| `SetStaticDefaults()` | Instância | `ModPrefix` | `undefined` | Uma vez, com o conteúdo pronto e as tabelas crescidas (PrefixID.Sets.ReducedNaturalChance[this.Type]). | [Código](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L15) |
| `SetStats(damageMult, knockbackMult, useTimeMult, scaleMult, shootSpeedMult, manaMult, critBonus, tagDamage, armorPenetration)` | Instância | `ModPrefix` | `undefined` | Os status, em Ref (damageMult.value = 1.2). Com um parâmetro só, recebe { damage, knockBack, speed, size, shootSpeed, mana, crit, tagDamage, armorPenetration } (a forma do ExMod do TL). O jogo aplica e confere, como os dele: se algum status não mudar de verdade no item, o prefixo não pega. | [Código](../../app/src/main/cpp/script/js/mod/ModPrefix.js#L25) |

## ModProjectile

[Contrato e campos](classes.md#modprojectile).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `AI(proj)` | Instância | `ModProjectile` | `undefined` | Todo quadro. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L32) |
| `AutoStaticDefaults()` | Instância | `ModProjectile` | `Consultar fonte` | Como o do tModLoader: roda antes do SetStaticDefaults, com this.Projectile já montado pelo SetDefaults. Sem o projHook, o botão de gancho do celular (QuickGrapple) não acha o item. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L16) |
| `CanCutTiles(proj)` | Instância | `ModProjectile` | `undefined` | Cortar grama e teia. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L51) |
| `CanDamage(proj)` | Instância | `ModProjectile` | `true` | false: não causa dano. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L48) |
| `CanUseGrapple(player, type)` | Instância | `ModProjectile` | `true` | No molde, antes de lançar o gancho; false impede. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L59) |
| `Clone(newProjectile)` | Instância | `ModProjectile` | `Entities.Clone(this)` | Cópia da instância. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L11) |
| `CloneDefaults(type)` | Instância | `ModProjectile` | `Consultar fonte` | Copia os valores de um projétil do jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L63) |
| `Colliding(proj, projHitbox, targetHitbox)` | Instância | `ModProjectile` | `undefined` | true/false decide o acerto; undefined, o do jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L47) |
| `CutTiles(proj)` | Instância | `ModProjectile` | `undefined` | Cortar grama e teia. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L52) |
| `DefaultToDrillOrChainsaw()` | Instância | `ModProjectile` | `Consultar fonte` | Os padrões do jogo para cada família de projétil segurado. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L81) |
| `DefaultToFlail()` | Instância | `ModProjectile` | `Consultar fonte` | Os padrões do jogo para cada família de projétil segurado. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L79) |
| `DefaultToKite()` | Instância | `ModProjectile` | `Consultar fonte` | Os padrões do jogo para cada família de projétil segurado. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L82) |
| `DefaultToSpear()` | Instância | `ModProjectile` | `Consultar fonte` | Os padrões do jogo para cada família de projétil segurado. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L77) |
| `DefaultToWhip()` | Instância | `ModProjectile` | `Consultar fonte` | Os padrões do jogo para cada família de projétil segurado. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L80) |
| `DefaultToYoyo()` | Instância | `ModProjectile` | `Consultar fonte` | Os padrões do jogo para cada família de projétil segurado. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L78) |
| `GetAlpha(proj, lightColor)` | Instância | `ModProjectile` | `undefined` | A cor final; undefined, a do jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L53) |
| `getByName(name)` | Estático | `ModProjectile` | `ProjectileLoader.ByType.get(bl.projectiles.typeOf(name))` | Como no item. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L145) |
| `getModProjectile(type)` | Estático | `ModProjectile` | `ProjectileLoader.ByType.get(type)` | Como no item. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L144) |
| `getTypeByName(name)` | Estático | `ModProjectile` | `bl.projectiles.typeOf(name)` | Como no item. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L143) |
| `GrappleCanLatchOnTo(proj, player, tile)` | Instância | `ModProjectile` | `undefined` | true/false: agarra neste bloco. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L61) |
| `isModProjectile(proj)` | Estático | `ModProjectile` | `!!proj && bl.projectiles.isModProjectile(proj.type)` | Como no item. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L142) |
| `isModType(type)` | Estático | `ModProjectile` | `bl.projectiles.isModProjectile(type)` | Como no item. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L141) |
| `MinionContactDamage(proj)` | Instância | `ModProjectile` | `false` | true: lacaio ou pet fere ao encostar. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L49) |
| `ModifyDamageHitbox(proj, hitbox)` | Instância | `ModProjectile` | `undefined` | Mude o Rectangle da área de dano. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L50) |
| `OnHitNPC(proj, npc)` | Instância | `ModProjectile` | `undefined` | Acertou um NPC. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L45) |
| `OnHitPlayer(proj, player)` | Instância | `ModProjectile` | `undefined` | Acertou um jogador. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L46) |
| `OnKill(proj, timeLeft)` | Instância | `ModProjectile` | `undefined` | Ao morrer. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L37) |
| `OnSpawn(proj)` | Instância | `ModProjectile` | `undefined` | Uma vez, no primeiro quadro de vida. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L30) |
| `OnTileCollide(proj, oldVelocity)` | Instância | `ModProjectile` | `true` | Bateu num bloco e ia morrer; false o mantém vivo. false impede somente a morte causada pela colisão neste movimento. Expiração e acerto em NPC continuam no fluxo de Kill e OnKill. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L44) |
| `PostAI(proj)` | Instância | `ModProjectile` | `undefined` | Depois da IA. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L33) |
| `PostDraw(proj, lightColor)` | Instância | `ModProjectile` | `undefined` | Depois do desenho, mesmo com o PreDraw false (como no tModLoader). | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L57) |
| `PostSetDefaults(proj)` | Instância | `ModProjectile` | `undefined` | Todo projétil deste tipo que nasce. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L27) |
| `PostSetupContent()` | Instância | `ModProjectile` | `undefined` | Uma vez. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L28) |
| `PostStaticDefaults()` | Instância | `ModProjectile` | `undefined` | Uma vez. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L26) |
| `PreAI(proj)` | Instância | `ModProjectile` | `true` | Antes da IA; false pula a IA do jogo e o AI. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L31) |
| `PreDraw(proj, lightColor)` | Instância | `ModProjectile` | `true` | Antes do desenho; false não desenha o sprite do jogo (os extras do PreDrawExtras continuam). | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L56) |
| `PreDrawExtras(proj)` | Instância | `ModProjectile` | `true` | Antes do PreDraw; false tira o que o jogo desenha antes do sprite: a corrente do gancho, a linha de pesca, o fio do ioiô, a corrente do mangual. O gancho com corrente própria desenha a dele aqui. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L55) |
| `PreKill(proj, timeLeft)` | Instância | `ModProjectile` | `true` | Antes de morrer; false tira os efeitos do jogo. false remove o projétil sem os efeitos de morte nativos e encerra o fluxo antes de OnKill. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L36) |
| `get Projectile` | Getter | `ModProjectile` | `Entities.Of(this)` | Projectile do jogo | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L9) |
| `ReceiveExtraAI(reader)` | Instância | `ModProjectile` | `undefined` | A implementação base não executa ações. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L35) |
| `register(cls)` | Estático | `ModProjectile` | `Consultar fonte` | Como no item. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L84) |
| `SendExtraAI(writer)` | Instância | `ModProjectile` | `undefined` | A implementação base não executa ações. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L34) |
| `SetDefaults(proj)` | Instância | `ModProjectile` | `undefined` | Todo projétil deste tipo que nasce. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L25) |
| `SetStaticDefaults()` | Instância | `ModProjectile` | `undefined` | Uma vez. Main.projFrames[this.Type] = n aqui vale como os quadros da textura. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L24) |
| `TileCollideStyle(proj, width, height, fallThrough, hitboxCenterFrac)` | Instância | `ModProjectile` | `true` | Quatro Ref: dimensões da colisão, passagem por plataformas e centro. false pula a colisão neste movimento. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L41) |
| `UseGrapple(player, type)` | Instância | `ModProjectile` | `type` | No molde: devolva o tipo a lançar. | [Código](../../app/src/main/cpp/script/js/mod/ModProjectile.js#L60) |

## ModRarity

[Contrato e campos](classes.md#modrarity).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `GetPrefixedRarity(offset, valueMult)` | Instância | `ModRarity` | `this.Type` | A raridade quando um prefixo sobe (1, 2) ou desce (-1, -2) o item; padrão this.Type. | [Código](../../app/src/main/cpp/script/js/mod/ModRarity.js#L12) |
| `get RarityColor` | Getter | `ModRarity` | `Color.White` | A cor do nome (lida a cada desenho: pode piscar). | [Código](../../app/src/main/cpp/script/js/mod/ModRarity.js#L10) |
| `register(cls)` | Estático | `ModRarity` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModRarity.js#L14) |
| `SetStaticDefaults()` | Instância | `ModRarity` | `undefined` | Uma vez, com o jogo pronto. | [Código](../../app/src/main/cpp/script/js/mod/ModRarity.js#L8) |

## ModRecipe

[Contrato e campos](classes.md#modrecipe).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `AddCustomShimmerResult(type, stack = 1)` | Instância | `ModRecipe` | `Consultar fonte` | O que sai no Brilho. | [Código](../../app/src/main/cpp/script/js/mod/ModRecipe.js#L61) |
| `AddIngredient(type, stack = 1)` | Instância | `ModRecipe` | `Consultar fonte` | Até 15. | [Código](../../app/src/main/cpp/script/js/mod/ModRecipe.js#L19) |
| `AddRecipeGroup(group, stack = 1)` | Instância | `ModRecipe` | `Consultar fonte` | Aceita qualquer item do grupo (objeto, nome do jogo como 'IronBar', ou um criado por mod). | [Código](../../app/src/main/cpp/script/js/mod/ModRecipe.js#L33) |
| `AddTile(tileType)` | Instância | `ModRecipe` | `Consultar fonte` | A estação (uma por receita). | [Código](../../app/src/main/cpp/script/js/mod/ModRecipe.js#L50) |
| `constructor()` | Construtor | `ModRecipe` | `instância` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModRecipe.js#L5) |
| `CreateRecipeGroup(name, itemTypes = [])` | Estático | `ModRecipe` | `Consultar fonte` | Grupo novo (no AddRecipeGroups). | [Código](../../app/src/main/cpp/script/js/mod/ModRecipe.js#L107) |
| `GetGroup(groupOrName)` | Estático | `ModRecipe` | `Consultar fonte` | Um grupo pelo objeto, nome ou número. | [Código](../../app/src/main/cpp/script/js/mod/ModRecipe.js#L86) |
| `GetGroupByName(name)` | Estático | `ModRecipe` | `ModRecipe.GetGroup(name)` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModRecipe.js#L104) |
| `Register()` | Instância | `ModRecipe` | `Consultar fonte` | Põe no jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModRecipe.js#L74) |
| `SetProperty(name, value)` | Instância | `ModRecipe` | `Consultar fonte` | needWater, needLava, needHoney, needSnowBiome, needGraveyardBiome, needTorchGodsFavor, needMechdusa, notDecraftable, crimson, corruption, alchemy. | [Código](../../app/src/main/cpp/script/js/mod/ModRecipe.js#L69) |
| `SetResult(type, stack = 1)` | Instância | `ModRecipe` | `Consultar fonte` | O que ela dá. | [Código](../../app/src/main/cpp/script/js/mod/ModRecipe.js#L12) |

## ModSceneEffect

[Contrato e campos](classes.md#modbiome-e-modsceneeffect).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `GetWeight(player)` | Instância | `ModSceneEffect` | `0.5` | 0 a 1: desempata efeitos da mesma Priority. | [Código](../../app/src/main/cpp/script/js/mod/ModSceneEffect.js#L15) |
| `IsSceneEffectActive(player)` | Instância | `ModSceneEffect` | `false` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModSceneEffect.js#L17) |
| `MapBackgroundColor(color)` | Instância | `ModSceneEffect` | `undefined` | Ref (.value, uma Color): a cor final, depois das duas acima. | [Código](../../app/src/main/cpp/script/js/mod/ModSceneEffect.js#L21) |
| `register(cls)` | Estático | `ModSceneEffect` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModSceneEffect.js#L28) |
| `SetStaticDefaults()` | Instância | `ModSceneEffect` | `undefined` | A implementação base não executa ações. | [Código](../../app/src/main/cpp/script/js/mod/ModSceneEffect.js#L26) |
| `SpecialVisuals(player, isActive)` | Instância | `ModSceneEffect` | `undefined` | Todo quadro, ativo ou não: é onde se liga e desliga um filtro de tela. | [Código](../../app/src/main/cpp/script/js/mod/ModSceneEffect.js#L24) |

## ModSurfaceBackgroundStyle

[Contrato e campos](classes.md#fundos-de-mod).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `ChooseCloseTexture(scale, parallax, a, b)` | Instância | `ModSurfaceBackgroundStyle` | `-1` | scale, parallax, a e b são Ref (.value), como os ref do tModLoader: escala 1.25, parallax 0.37, e a altura = a × (posição da tela) + b. | [Código](../../app/src/main/cpp/script/js/mod/ModSurfaceBackgroundStyle.js#L25) |
| `ChooseFarTexture()` | Instância | `ModSurfaceBackgroundStyle` | `-1` | O número da textura, ou -1 para não desenhar essa camada. | [Código](../../app/src/main/cpp/script/js/mod/ModSurfaceBackgroundStyle.js#L17) |
| `ChooseMiddleTexture()` | Instância | `ModSurfaceBackgroundStyle` | `-1` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModSurfaceBackgroundStyle.js#L18) |
| `ModifyFarFades(fades, transitionSpeed)` | Instância | `ModSurfaceBackgroundStyle` | `Consultar fonte` | fades: um por estilo (Main.bgAlphaFrontLayer). O padrão sobe o deste estilo e desce os outros, o que o Example Mod do tModLoader escreve. | [Código](../../app/src/main/cpp/script/js/mod/ModSurfaceBackgroundStyle.js#L8) |
| `PreDrawCloseBackground(spriteBatch)` | Instância | `ModSurfaceBackgroundStyle` | `true` | false: não desenha a camada da frente (o mod desenha a dele aqui). | [Código](../../app/src/main/cpp/script/js/mod/ModSurfaceBackgroundStyle.js#L21) |
| `register(cls)` | Estático | `ModSurfaceBackgroundStyle` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModSurfaceBackgroundStyle.js#L29) |
| `SetStaticDefaults()` | Instância | `ModSurfaceBackgroundStyle` | `undefined` | A implementação base não executa ações. | [Código](../../app/src/main/cpp/script/js/mod/ModSurfaceBackgroundStyle.js#L27) |

## ModSystem

[Contrato e campos](classes.md#modsystem).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `AddRecipeGroups()` | Instância | `ModSystem` | `undefined` | Uma vez, antes de qualquer receita. | [Código](../../app/src/main/cpp/script/js/mod/ModSystem.js#L3) |
| `AddRecipes()` | Instância | `ModSystem` | `undefined` | Uma vez, com as receitas do jogo prontas. | [Código](../../app/src/main/cpp/script/js/mod/ModSystem.js#L4) |
| `ClearWorld()` | Instância | `ModSystem` | `undefined` | Ao entrar em qualquer mundo (e antes de gerar um). | [Código](../../app/src/main/cpp/script/js/mod/ModSystem.js#L14) |
| `LoadWorldData(tag)` | Instância | `ModSystem` | `undefined` | Depois do OnWorldLoad, com os dados do mundo. Só no servidor ou sozinho. | [Código](../../app/src/main/cpp/script/js/mod/ModSystem.js#L11) |
| `NetReceive(reader)` | Instância | `ModSystem` | `undefined` | Rede: o servidor manda junto com os dados do mundo (ao entrar e a cada sincronização); o cliente lê. | [Código](../../app/src/main/cpp/script/js/mod/ModSystem.js#L33) |
| `NetSend(writer)` | Instância | `ModSystem` | `undefined` | Rede: o servidor manda junto com os dados do mundo (ao entrar e a cada sincronização); o cliente lê. | [Código](../../app/src/main/cpp/script/js/mod/ModSystem.js#L32) |
| `OnModLoad()` | Instância | `ModSystem` | `undefined` | No register. | [Código](../../app/src/main/cpp/script/js/mod/ModSystem.js#L2) |
| `OnWorldLoad()` | Instância | `ModSystem` | `undefined` | O mundo abriu; no cliente, ao chegar do servidor. | [Código](../../app/src/main/cpp/script/js/mod/ModSystem.js#L10) |
| `OnWorldUnload()` | Instância | `ModSystem` | `undefined` | Depois de sair. | [Código](../../app/src/main/cpp/script/js/mod/ModSystem.js#L16) |
| `PostAddRecipes()` | Instância | `ModSystem` | `undefined` | Uma vez, com as receitas do jogo prontas. | [Código](../../app/src/main/cpp/script/js/mod/ModSystem.js#L5) |
| `PostSetupContent()` | Instância | `ModSystem` | `undefined` | Uma vez, com todo o conteúdo de mod no jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModSystem.js#L6) |
| `PostUpdateEverything()` | Instância | `ModSystem` | `undefined` | A cada quadro, em todos. | [Código](../../app/src/main/cpp/script/js/mod/ModSystem.js#L23) |
| `PostUpdateTime()` | Instância | `ModSystem` | `undefined` | A cada quadro. Só no servidor ou sozinho. | [Código](../../app/src/main/cpp/script/js/mod/ModSystem.js#L22) |
| `PostUpdateWorld()` | Instância | `ModSystem` | `undefined` | A cada quadro. Só no servidor ou sozinho. | [Código](../../app/src/main/cpp/script/js/mod/ModSystem.js#L20) |
| `PostWorldLoad()` | Instância | `ModSystem` | `undefined` | Depois dos dados. | [Código](../../app/src/main/cpp/script/js/mod/ModSystem.js#L12) |
| `PreSaveAndQuit()` | Instância | `ModSystem` | `undefined` | Ao sair, antes de salvar. | [Código](../../app/src/main/cpp/script/js/mod/ModSystem.js#L15) |
| `PreUpdateTime()` | Instância | `ModSystem` | `undefined` | A cada quadro. Só no servidor ou sozinho. | [Código](../../app/src/main/cpp/script/js/mod/ModSystem.js#L21) |
| `PreUpdateWorld()` | Instância | `ModSystem` | `undefined` | A cada quadro. Só no servidor ou sozinho. | [Código](../../app/src/main/cpp/script/js/mod/ModSystem.js#L19) |
| `register(cls)` | Estático | `ModSystem` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModSystem.js#L35) |
| `ResetNearbyTileEffects()` | Instância | `ModSystem` | `undefined` | Antes de o jogo contar os blocos em volta do jogador local (a cada 5 quadros), e também ao sair do mundo e ao carregar outro (a contagem do anterior não vale no novo). | [Código](../../app/src/main/cpp/script/js/mod/ModSystem.js#L29) |
| `SaveWorldData(tag)` | Instância | `ModSystem` | `undefined` | A cada save do mundo. Só no servidor ou sozinho. | [Código](../../app/src/main/cpp/script/js/mod/ModSystem.js#L13) |
| `TileCountsAvailable(tileCounts)` | Instância | `ModSystem` | `undefined` | Com a contagem pronta: tileCounts[tipo] é quantos blocos daquele tipo há em volta. Vale durante a chamada: guarde o número, não o array. Só a varredura do jogador local (a dos pilares e a da câmera não chamam). | [Código](../../app/src/main/cpp/script/js/mod/ModSystem.js#L30) |

## ModTile

[Contrato e campos](classes.md#modtile).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `AddMapEntry(color, name)` | Instância | `ModTile` | `Consultar fonte` | A cor e o nome no mapa; cada chamada é uma opção a mais. | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L28) |
| `AnimateIndividualTile(type, i, j, frameXOffset, frameYOffset)` | Instância | `ModTile` | `undefined` | o desenho nativo (GetTileDrawData); marca tile.drawdata | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L77) |
| `AnimateTile(frame, frameCounter)` | Instância | `ModTile` | `undefined` | Main.AnimateTiles (só se algum tile o usa) | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L76) |
| `CanDrop(i, j)` | Instância | `ModTile` | `true` | WorldGen.KillTile, TileFrameImportant; filtro de tile | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L70) |
| `CanKillTile(i, j, blockDamaged)` | Instância | `ModTile` | `true` | WorldGen.KillTile, TileFrameImportant; filtro de tile | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L63) |
| `CreateDust(i, j, type)` | Instância | `ModTile` | `true` | WorldGen.KillTile, TileFrameImportant; filtro de tile | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L68) |
| `CreateMapEntryName()` | Instância | `ModTile` | `this.constructor.name` | O nome da classe. | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L52) |
| `DefaultContainerName(frameX, frameY)` | Instância | `ModTile` | `this.CreateMapEntryName()` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L106) |
| `DrawEffects(i, j, spriteBatch, drawData)` | Instância | `ModTile` | `undefined` | passada depois do TileDrawing.PostDrawTiles; marca tile.draw | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L83) |
| `EmitParticles(i, j, tile, tileFrameX, tileFrameY, tileLight, visible)` | Instância | `ModTile` | `undefined` | passada depois do TileDrawing.PostDrawTiles; marca tile.draw | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L85) |
| `GetItemDrops(i, j)` | Instância | `ModTile` | `undefined` | WorldGen.KillTile, TileFrameImportant; filtro de tile | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L72) |
| `GetMapOption(i, j)` | Instância | `ModTile` | `0` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L61) |
| `getModTile(type)` | Estático | `ModTile` | `TileLoader.ByType.get(type)` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L137) |
| `getTypeByName(name)` | Estático | `ModTile` | `bl.tiles.typeOf(name)` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L136) |
| `HasSmartInteract(i, j, settings)` | Instância | `ModTile` | `Hooks.Overrides(this.constructor, ModTile, 'RightClick')` | Se o tile é alvo do smart-interact. No celular o toque num móvel passa por ele (é o clique direito do PC): sem sobrescrever, vale para quem tem RightClick. | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L94) |
| `get HighlightTexture` | Getter | `ModTile` | `this.Texture + '_Highlight'` | A folha de quadros; o contorno (HasOutlines). | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L18) |
| `HitWire(i, j)` | Instância | `ModTile` | `undefined` | Wiring.HitWireSingle; marca tile.wire | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L95) |
| `IsLockedChest(i, j)` | Instância | `ModTile` | `false` | Chest.IsLocked/Unlock/Lock; marca tile.chest | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L103) |
| `isModType(type)` | Estático | `ModTile` | `bl.tiles.isModTile(type)` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L135) |
| `KillMultiTile(i, j, frameX, frameY)` | Instância | `ModTile` | `undefined` | WorldGen.KillTile, TileFrameImportant; filtro de tile | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L66) |
| `KillSound(i, j, fail)` | Instância | `ModTile` | `true` | WorldGen.KillTile, TileFrameImportant; filtro de tile | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L69) |
| `KillTile(i, j, fail, effectOnly, noItem)` | Instância | `ModTile` | `undefined` | WorldGen.KillTile, TileFrameImportant; filtro de tile | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L64) |
| `LockChest(i, j, frameXAdjustment, manual)` | Instância | `ModTile` | `false` | Chest.IsLocked/Unlock/Lock; marca tile.chest | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L105) |
| `ModifyLight(i, j, r, g, b)` | Instância | `ModTile` | `undefined` | Lighting.LightTiles (a cada 3 quadros) + Lighting.AddLight em lote; marca tile.light | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L75) |
| `ModifySittingTargetInfo(i, j, info)` | Instância | `ModTile` | `undefined` | PlayerSittingHelper/PlayerSleepingHelper | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L100) |
| `ModifySleepingTargetInfo(i, j, info)` | Instância | `ModTile` | `undefined` | PlayerSittingHelper/PlayerSleepingHelper | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L101) |
| `MouseOver(i, j)` | Instância | `ModTile` | `undefined` | Player.TileInteractions; filtro de tile | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L89) |
| `MouseOverFar(i, j)` | Instância | `ModTile` | `undefined` | Player.TileInteractions; filtro de tile | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L90) |
| `NearbyEffects(i, j, closer)` | Instância | `ModTile` | `undefined` | SceneMetrics.Scan | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L86) |
| `NumDust(i, j, fail, num)` | Instância | `ModTile` | `undefined` | WorldGen.KillTile, TileFrameImportant; filtro de tile | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L67) |
| `PlaceInWorld(i, j, item)` | Instância | `ModTile` | `undefined` | TileObjectData.CallPostPlacementPlayerHook e WorldGen.PlaceTile (colocando) | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L73) |
| `PostDraw(i, j, spriteBatch)` | Instância | `ModTile` | `undefined` | passada depois do TileDrawing.PostDrawTiles; marca tile.draw | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L82) |
| `PostSetDefaults()` | Instância | `ModTile` | `undefined` | nativo, conteúdo pronto | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L21) |
| `PostSetupContent()` | Instância | `ModTile` | `undefined` | nativo, conteúdo pronto | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L22) |
| `PreDraw(i, j, spriteBatch)` | Instância | `ModTile` | `true` | passada depois do TileDrawing.PostDrawTiles; marca tile.draw | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L81) |
| `RandomUpdate(i, j)` | Instância | `ModTile` | `undefined` | WorldGen.UpdateWorld_Tile; marca tile.random | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L97) |
| `register(cls)` | Estático | `ModTile` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L108) |
| `RegisterItemDrop(itemType, ...styles)` | Instância | `ModTile` | `Consultar fonte` | O item que cai. | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L55) |
| `RightClick(i, j)` | Instância | `ModTile` | `false` | Player.TileInteractions; filtro de tile | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L88) |
| `SetDrawPositions(i, j, width, offsetY, height, tileFrameX, tileFrameY)` | Instância | `ModTile` | `undefined` | o desenho nativo (GetTileDrawData); marca tile.drawdata | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L78) |
| `SetSpriteEffects(i, j, spriteEffects)` | Instância | `ModTile` | `undefined` | o desenho nativo (GetTileDrawData); marca tile.drawdata | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L79) |
| `SetStaticDefaults()` | Instância | `ModTile` | `undefined` | nativo, conteúdo pronto | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L20) |
| `Slope(i, j)` | Instância | `ModTile` | `true` | WorldGen.SlopeTile/PoundTile; marca tile.slope | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L96) |
| `SpecialDraw(i, j, spriteBatch)` | Instância | `ModTile` | `undefined` | passada depois do TileDrawing.PostDrawTiles; marca tile.draw | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L84) |
| `TileFrame(i, j, resetFrame, noBreak)` | Instância | `ModTile` | `true` | WorldGen.TileFrame; marca tile.frame | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L98) |
| `UnlockChest(i, j, frameXAdjustment, dustType, manual)` | Instância | `ModTile` | `false` | Chest.IsLocked/Unlock/Lock; marca tile.chest | [Código](../../app/src/main/cpp/script/js/mod/ModTile.js#L104) |

## ModUndergroundBackgroundStyle

[Contrato e campos](classes.md#fundos-de-mod).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `FillTextureArray(textureSlots)` | Instância | `ModUndergroundBackgroundStyle` | `undefined` | textureSlots[0]: a borda entre o céu e a terra (160x16); [1]: a terra; [2]: a borda entre a terra e a pedra (160x16); [3]: a pedra (160x96). [4]: a passagem para o inferno (vem com a do jogo). Números do BackgroundTextureLoader.GetBackgroundSlot. | [Código](../../app/src/main/cpp/script/js/mod/ModUndergroundBackgroundStyle.js#L9) |
| `register(cls)` | Estático | `ModUndergroundBackgroundStyle` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModUndergroundBackgroundStyle.js#L13) |
| `SetStaticDefaults()` | Instância | `ModUndergroundBackgroundStyle` | `undefined` | A implementação base não executa ações. | [Código](../../app/src/main/cpp/script/js/mod/ModUndergroundBackgroundStyle.js#L11) |

## ModWall

[Contrato e campos](classes.md#modwall).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `AddMapEntry(color, name)` | Instância | `ModWall` | `Consultar fonte` | A cor e o nome no mapa. | [Código](../../app/src/main/cpp/script/js/mod/ModWall.js#L12) |
| `AnimateWall(frame, frameCounter)` | Instância | `ModWall` | `undefined` | O quadro (Main.wallFrame), por tipo, a cada quadro. | [Código](../../app/src/main/cpp/script/js/mod/ModWall.js#L42) |
| `CreateDust(i, j, type)` | Instância | `ModWall` | `true` | Cada poeira. | [Código](../../app/src/main/cpp/script/js/mod/ModWall.js#L38) |
| `CreateMapEntryName()` | Instância | `ModWall` | `this.constructor.name` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModWall.js#L34) |
| `Drop(i, j, type)` | Instância | `ModWall` | `true` | O item que cai; false nada. | [Código](../../app/src/main/cpp/script/js/mod/ModWall.js#L40) |
| `getModWall(type)` | Estático | `ModWall` | `WallLoader.ByType.get(type)` | Como no tile. | [Código](../../app/src/main/cpp/script/js/mod/ModWall.js#L74) |
| `getTypeByName(name)` | Estático | `ModWall` | `bl.walls.typeOf(name)` | Como no tile. | [Código](../../app/src/main/cpp/script/js/mod/ModWall.js#L73) |
| `isModType(type)` | Estático | `ModWall` | `bl.walls.isModWall(type)` | Como no tile. | [Código](../../app/src/main/cpp/script/js/mod/ModWall.js#L72) |
| `KillSound(i, j, fail)` | Instância | `ModWall` | `true` | false cala. | [Código](../../app/src/main/cpp/script/js/mod/ModWall.js#L39) |
| `KillWall(i, j, fail)` | Instância | `ModWall` | `undefined` | Ao bater ou quebrar (fail, num: Ref). | [Código](../../app/src/main/cpp/script/js/mod/ModWall.js#L36) |
| `ModifyLight(i, j, r, g, b)` | Instância | `ModWall` | `undefined` | A luz da parede (Ref), perto da tela. | [Código](../../app/src/main/cpp/script/js/mod/ModWall.js#L41) |
| `NumDust(i, j, fail, num)` | Instância | `ModWall` | `undefined` | Ao bater ou quebrar (fail, num: Ref). | [Código](../../app/src/main/cpp/script/js/mod/ModWall.js#L37) |
| `PostSetupContent()` | Instância | `ModWall` | `undefined` | A implementação base não executa ações. | [Código](../../app/src/main/cpp/script/js/mod/ModWall.js#L10) |
| `RandomUpdate(i, j)` | Instância | `ModWall` | `undefined` | Atualização aleatória. | [Código](../../app/src/main/cpp/script/js/mod/ModWall.js#L43) |
| `register(cls)` | Estático | `ModWall` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModWall.js#L46) |
| `SetStaticDefaults()` | Instância | `ModWall` | `undefined` | Com o tipo já nas tabelas (Main.wallHouse, WallID.Sets). | [Código](../../app/src/main/cpp/script/js/mod/ModWall.js#L9) |
| `WallFrame(i, j, randomizeFrame, style, frameNumber)` | Instância | `ModWall` | `true` | O quadro da moldura; false mantém o anterior. | [Código](../../app/src/main/cpp/script/js/mod/ModWall.js#L44) |

## ModWaterStyle

[Contrato e campos](classes.md#água-de-mod).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `BiomeHairColor()` | Instância | `ModWaterStyle` | `Color.new(28, 216, 94, 255)` | A cor da tintura de bioma (a do cabelo) com esta água. O padrão é a da floresta, como no tModLoader. | [Código](../../app/src/main/cpp/script/js/mod/ModWaterStyle.js#L39) |
| `ChooseWaterfallStyle()` | Instância | `ModWaterStyle` | `0` | O número da cachoeira desta água: ModContent.GetInstance(ModWaterfallStyle).Slot ou uma do jogo (0 é a da floresta). | [Código](../../app/src/main/cpp/script/js/mod/ModWaterStyle.js#L9) |
| `GetDropletGore()` | Instância | `ModWaterStyle` | `706` | O gore da gota que pinga do bloco. Uma de mod (ModGore.getTypeByName) se comporta como a gota d'água do jogo (GoreID.WaterDrip), como o UpdateType do tModLoader. | [Código](../../app/src/main/cpp/script/js/mod/ModWaterStyle.js#L17) |
| `GetRainTexture()` | Instância | `ModWaterStyle` | `null` | A textura da chuva: o caminho de um PNG no mod (ou um Asset<Texture2D>), ou null para a do jogo (o padrão). | [Código](../../app/src/main/cpp/script/js/mod/ModWaterStyle.js#L35) |
| `GetRainVariant()` | Instância | `ModWaterStyle` | `Rand.Next(3)` | A variante da chuva: a coluna da textura dividida por 4 (a do jogo tem 3 por estilo; 0 a 2 é a da floresta). Com GetRainTexture, a coluna na textura do mod. O padrão é o sorteio do próprio jogo, sem custo; sobrescrever custa uma entrada no JS por gota nova. | [Código](../../app/src/main/cpp/script/js/mod/ModWaterStyle.js#L31) |
| `GetSplashDust()` | Instância | `ModWaterStyle` | `33` | O pó do respingo (quem cai na água). O padrão é o do jogo (DustID.Water). | [Código](../../app/src/main/cpp/script/js/mod/ModWaterStyle.js#L12) |
| `LightColorMultiplier(r, g, b)` | Instância | `ModWaterStyle` | `Consultar fonte` | r, g, b são Ref (.value): quanto da luz atravessa a água (o motor de luz novo, os modos Cor e Branco). O padrão é o do jogo. | [Código](../../app/src/main/cpp/script/js/mod/ModWaterStyle.js#L21) |
| `register(cls)` | Estático | `ModWaterStyle` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModWaterStyle.js#L43) |
| `SetStaticDefaults()` | Instância | `ModWaterStyle` | `undefined` | A implementação base não executa ações. | [Código](../../app/src/main/cpp/script/js/mod/ModWaterStyle.js#L41) |

## ModWaterfallStyle

[Contrato e campos](classes.md#água-de-mod).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `AddLight(i, j)` | Instância | `ModWaterfallStyle` | `undefined` | Luz em cada ponto de cachoeira deste estilo na tela (Lighting.AddLight). O celular embute a luz das cachoeiras do jogo no desenho; esta roda depois dele, uma vez por quadro. | [Código](../../app/src/main/cpp/script/js/mod/ModWaterfallStyle.js#L9) |
| `ColorMultiplier(r, g, b, a)` | Instância | `ModWaterfallStyle` | `undefined` | A cor da cachoeira: r, g, b são Ref (.value, 0 a 255, a luz do lugar vezes a opacidade) e a, a opacidade. Para cores que mudam com o tempo. | [Código](../../app/src/main/cpp/script/js/mod/ModWaterfallStyle.js#L13) |
| `register(cls)` | Estático | `ModWaterfallStyle` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModWaterfallStyle.js#L17) |
| `SetStaticDefaults()` | Instância | `ModWaterfallStyle` | `undefined` | A implementação base não executa ações. | [Código](../../app/src/main/cpp/script/js/mod/ModWaterfallStyle.js#L15) |

## MusicLoader

[Contrato e campos](classes.md#som-e-música).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `AddMusicBox(mod, musicSlot, itemType, tileType, tileFrameY = 0)` | Estático | `MusicLoader` | `Consultar fonte` | A caixa de música de mod: o tile tileType (2x2, como a do jogo) toca a faixa musicSlot quando ligado (quadro X a partir de 36) e na tela. | [Código](../../app/src/main/cpp/script/js/mod/MusicLoader.js#L34) |
| `GetMusicSlot(modOrPath, path)` | Estático | `MusicLoader` | `Consultar fonte` | O número de uma música do mod (0 = não existe). | [Código](../../app/src/main/cpp/script/js/mod/MusicLoader.js#L5) |
| `IsMusicPlaying(slot)` | Estático | `MusicLoader` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/MusicLoader.js#L49) |
| `get MusicCount` | Getter estático | `MusicLoader` | `ModMusic.Base() + ModMusic.Tracks.length` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/MusicLoader.js#L2) |
| `MusicExists(modOrPath, path)` | Estático | `MusicLoader` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/MusicLoader.js#L27) |

## NPCHappiness

[Contrato e campos](classes.md#ajudantes-de-npc).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `constructor(npcType)` | Construtor | `NPCHappiness` | `instância` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCHappiness.js#L2) |
| `SetBiomeAffection(biome, level)` | Instância | `NPCHappiness` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCHappiness.js#L16) |
| `SetNPCAffection(npcType, level)` | Instância | `NPCHappiness` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCHappiness.js#L6) |

## NPCLoot

[Contrato e campos](classes.md#ajudantes-de-npc).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `Add(rule)` | Instância | `NPCLoot` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCLoot.js#L7) |
| `constructor(type)` | Construtor | `NPCLoot` | `instância` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCLoot.js#L2) |
| `Get()` | Instância | `NPCLoot` | `Consultar fonte` | Sem as regras globais. | [Código](../../app/src/main/cpp/script/js/mod/NPCLoot.js#L20) |
| `Remove(rule)` | Instância | `NPCLoot` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCLoot.js#L13) |
| `RemoveWhere(predicate)` | Instância | `NPCLoot` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCLoot.js#L25) |

## NPCShop

[Contrato e campos](classes.md#ajudantes-de-npc).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `Add(type, options = {})` | Instância | `NPCShop` | `Consultar fonte` | options: { condition: () => bool, price, currency }. | [Código](../../app/src/main/cpp/script/js/mod/NPCShop.js#L16) |
| `constructor(npcType, name = 'Shop')` | Construtor | `NPCShop` | `instância` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCShop.js#L8) |
| `get(npcType, name)` | Estático | `NPCShop` | `NPCShop.#byKey.get(npcType + '/' + name)` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCShop.js#L39) |
| `Open()` | Instância | `NPCShop` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCShop.js#L32) |
| `Register()` | Instância | `NPCShop` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCShop.js#L21) |

## NPCSpawnInfo

[Contrato e campos](classes.md#ajudantes-de-npc).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `get AboveSurface` | Getter | `NPCSpawnInfo` | `this.Surface \|\| this.Sky` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L48) |
| `get AnyEvent` | Getter | `NPCSpawnInfo` | `this.SlimeRain \|\| this.SolarEclipse \|\| this.PumpkinMoon \|\| this.FrostMoon` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L59) |
| `get AnyTower` | Getter | `NPCSpawnInfo` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L89) |
| `get BelowSurface` | Getter | `NPCSpawnInfo` | `!this.AboveSurface && !this.Underworld` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L49) |
| `get BloodMoon` | Getter | `NPCSpawnInfo` | `Terraria.Main.bloodMoon` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L55) |
| `get Cavern` | Getter | `NPCSpawnInfo` | `this.Player.ZoneRockLayerHeight` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L46) |
| `get CommonEnemy` | Getter | `NPCSpawnInfo` | `!this.Invasion && !this.AnyEvent && !this.AnyTower` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L88) |
| `constructor(spawner, tileX, tileY, target, xRange = false)` | Construtor | `NPCSpawnInfo` | `instância` | spawner: o NPC.Spawner do jogo; tileX/tileY: o ponto do spawn (-1 no EditSpawnFlags, antes de o jogo escolher); target: o índice do jogador. xRange: longe o bastante na horizontal (SafeRangeX). | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L25) |
| `get Corruption` | Getter | `NPCSpawnInfo` | `this.Player.ZoneCorrupt && this.AboveSurface` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L65) |
| `get Crimson` | Getter | `NPCSpawnInfo` | `this.Player.ZoneCrimson && this.AboveSurface` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L67) |
| `get Day` | Getter | `NPCSpawnInfo` | `Terraria.Main.dayTime` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L51) |
| `get Desert` | Getter | `NPCSpawnInfo` | `this.Player.ZoneDesert` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L79) |
| `get DesertCave` | Getter | `NPCSpawnInfo` | `this.Player.ZoneUndergroundDesert` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L80) |
| `get Dungeon` | Getter | `NPCSpawnInfo` | `this.Player.ZoneDungeon` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L84) |
| `get Expert` | Getter | `NPCSpawnInfo` | `Terraria.Main.expertMode` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L62) |
| `get FrostMoon` | Getter | `NPCSpawnInfo` | `Terraria.Main.snowMoon` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L58) |
| `get Granite` | Getter | `NPCSpawnInfo` | `this.Player.ZoneGranite` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L82) |
| `get Graveyard` | Getter | `NPCSpawnInfo` | `this.Player.ZoneGraveyard` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L83) |
| `get Hallow` | Getter | `NPCSpawnInfo` | `this.Player.ZoneHallow && this.AboveSurface` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L69) |
| `get HardMode` | Getter | `NPCSpawnInfo` | `Terraria.Main.hardMode` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L61) |
| `get Ice` | Getter | `NPCSpawnInfo` | `this.Player.ZoneSnow && (this.Underground \|\| this.Cavern)` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L72) |
| `get Invasion` | Getter | `NPCSpawnInfo` | `Terraria.Main.invasionType > 0` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L87) |
| `get Jungle` | Getter | `NPCSpawnInfo` | `this.Player.ZoneJungle && this.AboveSurface` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L73) |
| `get Lihzahrd` | Getter | `NPCSpawnInfo` | `this.Player.ZoneLihzhardTemple` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L85) |
| `get Marble` | Getter | `NPCSpawnInfo` | `this.Player.ZoneMarble` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L81) |
| `get Master` | Getter | `NPCSpawnInfo` | `Terraria.Main.masterMode` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L63) |
| `get Meteor` | Getter | `NPCSpawnInfo` | `this.Player.ZoneMeteor` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L78) |
| `get Mushroom` | Getter | `NPCSpawnInfo` | `this.Player.ZoneGlowshroom && this.Cavern` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L76) |
| `get Night` | Getter | `NPCSpawnInfo` | `!Terraria.Main.dayTime` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L52) |
| `get Ocean` | Getter | `NPCSpawnInfo` | `this.Player.ZoneBeach` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L77) |
| `get PumpkinMoon` | Getter | `NPCSpawnInfo` | `Terraria.Main.pumpkinMoon` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L57) |
| `get Rain` | Getter | `NPCSpawnInfo` | `Terraria.Main.raining` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L53) |
| `get Sky` | Getter | `NPCSpawnInfo` | `this.Player.ZoneSkyHeight` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L43) |
| `get SlimeRain` | Getter | `NPCSpawnInfo` | `Terraria.Main.slimeRain` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L54) |
| `get Snow` | Getter | `NPCSpawnInfo` | `this.Player.ZoneSnow && this.AboveSurface` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L71) |
| `get SolarEclipse` | Getter | `NPCSpawnInfo` | `Terraria.Main.eclipse` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L56) |
| `get Surface` | Getter | `NPCSpawnInfo` | `this.Player.ZoneOverworldHeight` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L44) |
| `get SurfaceMushroom` | Getter | `NPCSpawnInfo` | `this.Player.ZoneGlowshroom && this.Surface` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L75) |
| `get Underground` | Getter | `NPCSpawnInfo` | `this.Player.ZoneDirtLayerHeight` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L45) |
| `get UndergroundCorruption` | Getter | `NPCSpawnInfo` | `this.Player.ZoneCorrupt && (this.Underground \|\| this.Cavern)` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L66) |
| `get UndergroundCrimson` | Getter | `NPCSpawnInfo` | `this.Player.ZoneCrimson && (this.Underground \|\| this.Cavern)` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L68) |
| `get UndergroundHallow` | Getter | `NPCSpawnInfo` | `this.Player.ZoneHallow && (this.Underground \|\| this.Cavern)` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L70) |
| `get UndergroundJungle` | Getter | `NPCSpawnInfo` | `this.Player.ZoneJungle && (this.Underground \|\| this.Cavern)` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L74) |
| `get Underworld` | Getter | `NPCSpawnInfo` | `this.Player.ZoneUnderworldHeight` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NPCSpawnInfo.js#L47) |

## NetReader

[Contrato e campos](classes.md#rede).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `constructor(values)` | Construtor | `NetReader` | `instância` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NetReader.js#L2) |
| `get HasMore` | Getter | `NetReader` | `this.index < this.values.length` | Ainda há valores? | [Código](../../app/src/main/cpp/script/js/mod/NetReader.js#L7) |
| `Read()` | Instância | `NetReader` | `Consultar fonte` | O próximo valor, como foi escrito. Lê na ordem escrita pelo NetWriter. Lança RangeError quando a mensagem não tem mais valores. | [Código](../../app/src/main/cpp/script/js/mod/NetReader.js#L9) |
| `Read7BitEncodedInt()` | Instância | `NetReader` | `Number(this.Read()) \| 0` | Alias de NetReader.ReadInt32. | [Código](../../app/src/main/cpp/script/js/mod/NetReader.js#L29) |
| `ReadBoolean()` | Instância | `NetReader` | `!!this.Read()` | O próximo, convertido. | [Código](../../app/src/main/cpp/script/js/mod/NetReader.js#L18) |
| `ReadByte()` | Instância | `NetReader` | `Number(this.Read()) \| 0` | O próximo, convertido. | [Código](../../app/src/main/cpp/script/js/mod/NetReader.js#L29) |
| `ReadDouble()` | Instância | `NetReader` | `Number(this.Read())` | O próximo, convertido. | [Código](../../app/src/main/cpp/script/js/mod/NetReader.js#L32) |
| `ReadFlags()` | Instância | `NetReader` | `this.Read()` | Um array de booleanos; um Vector2. | [Código](../../app/src/main/cpp/script/js/mod/NetReader.js#L20) |
| `ReadInt16()` | Instância | `NetReader` | `Number(this.Read()) \| 0` | Alias de NetReader.ReadInt32. | [Código](../../app/src/main/cpp/script/js/mod/NetReader.js#L29) |
| `ReadInt32()` | Instância | `NetReader` | `Number(this.Read()) \| 0` | O próximo, convertido. | [Código](../../app/src/main/cpp/script/js/mod/NetReader.js#L16) |
| `ReadInt64()` | Instância | `NetReader` | `Number(this.Read())` | Alias de NetReader.ReadSingle. | [Código](../../app/src/main/cpp/script/js/mod/NetReader.js#L31) |
| `ReadSByte()` | Instância | `NetReader` | `Number(this.Read()) \| 0` | Alias de NetReader.ReadInt32. | [Código](../../app/src/main/cpp/script/js/mod/NetReader.js#L29) |
| `ReadSingle()` | Instância | `NetReader` | `Number(this.Read())` | O próximo, convertido. | [Código](../../app/src/main/cpp/script/js/mod/NetReader.js#L17) |
| `ReadString()` | Instância | `NetReader` | `String(this.Read())` | O próximo, convertido. | [Código](../../app/src/main/cpp/script/js/mod/NetReader.js#L19) |
| `ReadUInt16()` | Instância | `NetReader` | `Number(this.Read()) \| 0` | Alias de NetReader.ReadInt32. | [Código](../../app/src/main/cpp/script/js/mod/NetReader.js#L29) |
| `ReadUInt32()` | Instância | `NetReader` | `Number(this.Read()) \| 0` | Alias de NetReader.ReadInt32. | [Código](../../app/src/main/cpp/script/js/mod/NetReader.js#L29) |
| `ReadVector2()` | Instância | `NetReader` | `Consultar fonte` | Um array de booleanos; um Vector2. | [Código](../../app/src/main/cpp/script/js/mod/NetReader.js#L22) |

## NetWriter

[Contrato e campos](classes.md#rede).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `constructor()` | Construtor | `NetWriter` | `instância` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L2) |
| `Write(value)` | Instância | `NetWriter` | `Consultar fonte` | Guarda número, texto, booleano, array, objeto simples ou Vector2. Os nomes com tipo (WriteInt32...) fazem o mesmo. Grava valores JS no protocolo do loader, não bytes de BinaryWriter. As variantes WriteByte, WriteInt32 etc. são aliases de Write. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L6) |
| `Write7BitEncodedInt(value)` | Instância | `NetWriter` | `Consultar fonte` | Alias de NetWriter.Write. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteBoolean(value)` | Instância | `NetWriter` | `Consultar fonte` | Alias de NetWriter.Write. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteByte(value)` | Instância | `NetWriter` | `Consultar fonte` | Alias de NetWriter.Write. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteDouble(value)` | Instância | `NetWriter` | `Consultar fonte` | Alias de NetWriter.Write. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteFlags(...flags)` | Instância | `NetWriter` | `Consultar fonte` | Vários booleanos; um vetor. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L12) |
| `WriteInt16(value)` | Instância | `NetWriter` | `Consultar fonte` | Alias de NetWriter.Write. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteInt32(value)` | Instância | `NetWriter` | `Consultar fonte` | Alias de NetWriter.Write. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteInt64(value)` | Instância | `NetWriter` | `Consultar fonte` | Alias de NetWriter.Write. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteSByte(value)` | Instância | `NetWriter` | `Consultar fonte` | Alias de NetWriter.Write. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteSingle(value)` | Instância | `NetWriter` | `Consultar fonte` | Alias de NetWriter.Write. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteString(value)` | Instância | `NetWriter` | `Consultar fonte` | Alias de NetWriter.Write. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteUInt16(value)` | Instância | `NetWriter` | `Consultar fonte` | Alias de NetWriter.Write. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteUInt32(value)` | Instância | `NetWriter` | `Consultar fonte` | Alias de NetWriter.Write. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L18) |
| `WriteVector2(v)` | Instância | `NetWriter` | `Consultar fonte` | Vários booleanos; um vetor. | [Código](../../app/src/main/cpp/script/js/mod/NetWriter.js#L13) |

## PlayerDrawLayer

[Contrato e campos](classes.md#modplayer).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `AfterParent(layer)` | Estático | `PlayerDrawLayer` | `{ After: layer }` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/PlayerDrawHooks.js#L5) |
| `BeforeParent(layer)` | Estático | `PlayerDrawLayer` | `{ Before: layer }` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/PlayerDrawHooks.js#L4) |
| `constructor(name, method)` | Construtor | `PlayerDrawLayer` | `instância` | Consulte a implementação na fonte. Descritor de uma camada nativa. Hide e posições BeforeParent/AfterParent atuam no desenho atual. Não registra um novo renderer. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/PlayerDrawHooks.js#L2) |
| `Hide()` | Instância | `PlayerDrawLayer` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/PlayerDrawHooks.js#L3) |

## PrefixLoader

[Contrato e campos](classes.md#modprefix).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `AllowPrefix(item, pre)` | Estático | `PrefixLoader` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/PrefixLoader.js#L122) |
| `Categories(item)` | Estático | `PrefixLoader` | `Consultar fonte` | As categorias do item; os prefixos do jogo de uma categoria. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/PrefixLoader.js#L83) |
| `GetPrefix(type)` | Estático | `PrefixLoader` | `PrefixLoader.ByType.get(type)` | O ModPrefix, ou undefined (prefixo do jogo). | [Código](../../app/src/main/cpp/script/js/mod/Loaders/PrefixLoader.js#L26) |
| `GetPrefixesInCategory(category)` | Estático | `PrefixLoader` | `PrefixLoader.#byCategory.get(category) \|\| []` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/PrefixLoader.js#L28) |
| `IsWeaponSubCategory(category)` | Estático | `PrefixLoader` | `category === PrefixCategory.Melee \|\| category === PrefixCategory.Ranged \|\| category === PrefixCategory.Magic` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/PrefixLoader.js#L30) |
| `get PrefixCount` | Getter estático | `PrefixLoader` | `PrefixLoader.VanillaCount + PrefixLoader.ByType.size` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/PrefixLoader.js#L24) |
| `Register(inst)` | Estático | `PrefixLoader` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/PrefixLoader.js#L34) |
| `TooltipLines(item)` | Estático | `PrefixLoader` | `Consultar fonte` | As linhas do GetTooltipLines do prefixo do item, ou null. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/PrefixLoader.js#L132) |
| `get VanillaCount` | Getter estático | `PrefixLoader` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/PrefixLoader.js#L19) |
| `VanillaPrefixes(category)` | Estático | `PrefixLoader` | `Consultar fonte` | As categorias do item; os prefixos do jogo de uma categoria. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/PrefixLoader.js#L109) |
| `WantItemHooks(methods)` | Estático | `PrefixLoader` | `Consultar fonte` | Os métodos de prefixo que um ModItem/GlobalItem sobrescreve (ChoosePrefix, AllowPrefix...). | [Código](../../app/src/main/cpp/script/js/mod/Loaders/PrefixLoader.js#L64) |
| `WantRollable()` | Estático | `PrefixLoader` | `Consultar fonte` | Item de mod ganha os prefixos das categorias dele (MeleePrefix...): sem isto, as tabelas PrefixLegacy.ItemSets do jogo não o conhecem e ele não ganha nenhum. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/PrefixLoader.js#L73) |
| `WantRolling()` | Estático | `PrefixLoader` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/PrefixLoader.js#L77) |

## RarityLoader

[Contrato e campos](classes.md#modrarity).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `Add(inst)` | Estático | `RarityLoader` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/RarityLoader.js#L19) |
| `ColorOf(type)` | Estático | `RarityLoader` | `Consultar fonte` | A cor de uma raridade de mod; branco se o RarityColor falhar. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/RarityLoader.js#L30) |
| `GetRarity(type)` | Estático | `RarityLoader` | `RarityLoader.ByType.get(type) \|\| null` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/RarityLoader.js#L27) |
| `get RarityCount` | Getter estático | `RarityLoader` | `RarityLoader.VanillaCount + RarityLoader.ByType.size` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/Loaders/RarityLoader.js#L17) |

## SoundEngine

[Contrato e campos](classes.md#som-e-música).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `FindActiveSound(style)` | Estático | `SoundEngine` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/SoundEngine.js#L19) |
| `PlaySound(style, position)` | Estático | `SoundEngine` | `Consultar fonte` | Toca agora; som de mod devolve o número (0 = não tocou). | [Código](../../app/src/main/cpp/script/js/mod/SoundEngine.js#L4) |
| `StopSound(stream)` | Estático | `SoundEngine` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/SoundEngine.js#L28) |

## SoundStyle

[Contrato e campos](classes.md#som-e-música).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `constructor(path, options)` | Construtor | `SoundStyle` | `instância` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/SoundStyle.js#L5) |

## SpawnCondition

[Contrato e campos](classes.md#ajudantes-de-npc).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `get Active` | Getter | `SpawnCondition` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/SpawnCondition.js#L37) |
| `Begin(info)` | Estático | `SpawnCondition` | `Consultar fonte` | O spawnInfo do sorteio que começa (o SpawnLoader chama antes dos SpawnChance). | [Código](../../app/src/main/cpp/script/js/mod/SpawnCondition.js#L41) |
| `get BlockWeight` | Getter | `SpawnCondition` | `this.#blockWeight` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/SpawnCondition.js#L38) |
| `get Chance` | Getter | `SpawnCondition` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/SpawnCondition.js#L36) |
| `CheckAll(info)` | Estático | `SpawnCondition` | `Consultar fonte` | Todas as condições no spawnInfo dado, na hora (para testes). | [Código](../../app/src/main/cpp/script/js/mod/SpawnCondition.js#L47) |
| `constructor(parent, test, blockWeight = 1)` | Construtor | `SpawnCondition` | `instância` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/SpawnCondition.js#L24) |
| `Name(all)` | Estático | `SpawnCondition` | `Consultar fonte` | Os nomes, para o aviso (depois de criar todas). | [Código](../../app/src/main/cpp/script/js/mod/SpawnCondition.js#L92) |

## SpawnPool

[Contrato e campos](classes.md#ajudantes-de-npc).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `Add(type, weight)` | Instância | `SpawnPool` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/SpawnPool.js#L9) |
| `Choose()` | Instância | `SpawnPool` | `Consultar fonte` | O tipo sorteado pelo Main.rand, ou null com o total 0 (nada nasce). Peso negativo vale 0. | [Código](../../app/src/main/cpp/script/js/mod/SpawnPool.js#L28) |
| `Clear()` | Instância | `SpawnPool` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/SpawnPool.js#L22) |
| `ContainsKey(type)` | Instância | `SpawnPool` | `Object.prototype.hasOwnProperty.call(this, type)` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/SpawnPool.js#L14) |
| `get Count` | Getter | `SpawnPool` | `Object.keys(this).length` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/SpawnPool.js#L6) |
| `get Keys` | Getter | `SpawnPool` | `Object.keys(this).map(Number)` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/SpawnPool.js#L7) |
| `Remove(type)` | Instância | `SpawnPool` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/SpawnPool.js#L16) |

## StatInheritanceData

[Contrato e campos](classes.md#damageclass).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `constructor(damageInheritance = 0, critChanceInheritance = 0, attackSpeedInheritance = 0, armorPenInheritance = 0, knockbackInheritance = 0)` | Construtor | `StatInheritanceData` | `instância` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/StatInheritanceData.js#L5) |
| `get Full` | Getter estático | `StatInheritanceData` | `new StatInheritanceData(1, 1, 1, 1, 1)` | Um novo a cada leitura: o Full with { attackSpeedInheritance = 0 } do C# vira Object.assign(StatInheritanceData.Full, { attackSpeedInheritance: 0 }). | [Código](../../app/src/main/cpp/script/js/mod/StatInheritanceData.js#L24) |
| `get IsNone` | Getter | `StatInheritanceData` | `!this.damageInheritance && !this.critChanceInheritance && !this.attackSpeedInheritance && !this.armorPenInheritance && !this.knockbackInheritance` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/StatInheritanceData.js#L27) |
| `get None` | Getter estático | `StatInheritanceData` | `new StatInheritanceData()` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/StatInheritanceData.js#L25) |

## StatModifier

[Contrato e campos](classes.md#damageclass).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `ApplyTo(baseValue)` | Instância | `StatModifier` | `(baseValue + this.Base) * this.Additive * this.Multiplicative + this.Flat` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/StatModifier.js#L15) |
| `Clone()` | Instância | `StatModifier` | `new StatModifier(this.Additive, this.Multiplicative, this.Flat, this.Base)` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/StatModifier.js#L33) |
| `CombineWith(m)` | Instância | `StatModifier` | `new StatModifier(this.Additive + m.Additive - 1, this.Multiplicative * m.Multiplicative, this.Flat + m.Flat, this.Base + m.Base)` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/StatModifier.js#L19) |
| `constructor(additive = 1, multiplicative = 1, flat = 0, base = 0)` | Construtor | `StatModifier` | `instância` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/StatModifier.js#L6) |
| `get Default` | Getter estático | `StatModifier` | `new StatModifier()` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/StatModifier.js#L13) |
| `Equals(m)` | Instância | `StatModifier` | `!!m && this.Additive === m.Additive && this.Multiplicative === m.Multiplicative && this.Flat === m.Flat && this.Base === m.Base` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/StatModifier.js#L37) |
| `ForValue(value)` | Estático | `StatModifier` | `Consultar fonte` | O damage dos ModifyWeaponDamage: vale como número (return damage  2, o jeito de antes) e como StatModifier (damage.Additive += 0.1, o do tModLoader). O que o mod devolver em número ganha; senão vale o modificador. | [Código](../../app/src/main/cpp/script/js/mod/StatModifier.js#L45) |
| `Resolve(result, modifier, value)` | Estático | `StatModifier` | `Consultar fonte` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/StatModifier.js#L51) |
| `Scale(scale)` | Instância | `StatModifier` | `new StatModifier(1 + (this.Additive - 1) * scale, 1 + (this.Multiplicative - 1) * scale, this.Flat * scale, this.Base * scale)` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/StatModifier.js#L24) |
| `Undo(currentValue)` | Instância | `StatModifier` | `(currentValue - this.Flat) / (this.Multiplicative * this.Additive) - this.Base` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/StatModifier.js#L29) |

## TagCompound

[Contrato e campos](classes.md#tagcompound).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `Add(key, value)` | Instância | `TagCompound` | `Consultar fonte` | Escrever e apagar. | [Código](../../app/src/main/cpp/script/js/mod/TagCompound.js#L7) |
| `ContainsKey(key)` | Instância | `TagCompound` | `Object.prototype.hasOwnProperty.call(this, key)` | A chave foi salva? | [Código](../../app/src/main/cpp/script/js/mod/TagCompound.js#L4) |
| `get Count` | Getter | `TagCompound` | `Object.keys(this).length` | Quantas chaves. | [Código](../../app/src/main/cpp/script/js/mod/TagCompound.js#L2) |
| `from(data)` | Estático | `TagCompound` | `Object.assign(new TagCompound(), data && typeof data === 'object' ? data : {})` | Um TagCompound com os campos do objeto. | [Código](../../app/src/main/cpp/script/js/mod/TagCompound.js#L17) |
| `Get(key, fallback)` | Instância | `TagCompound` | `this.ContainsKey(key) ? this[key] : fallback` | O valor, ou o padrão. | [Código](../../app/src/main/cpp/script/js/mod/TagCompound.js#L5) |
| `GetBool(key)` | Instância | `TagCompound` | `!!this.Get(key, false)` | O valor já convertido (falso, 0, '', [] se não há). | [Código](../../app/src/main/cpp/script/js/mod/TagCompound.js#L10) |
| `GetCompound(key)` | Instância | `TagCompound` | `TagCompound.from(this[key])` | O valor já convertido (falso, 0, '', [] se não há). | [Código](../../app/src/main/cpp/script/js/mod/TagCompound.js#L15) |
| `GetFloat(key)` | Instância | `TagCompound` | `Number(this.Get(key, 0))` | O valor já convertido (falso, 0, '', [] se não há). | [Código](../../app/src/main/cpp/script/js/mod/TagCompound.js#L12) |
| `GetInt(key)` | Instância | `TagCompound` | `Number(this.Get(key, 0)) \| 0` | O valor já convertido (falso, 0, '', [] se não há). | [Código](../../app/src/main/cpp/script/js/mod/TagCompound.js#L11) |
| `GetList(key)` | Instância | `TagCompound` | `Array.isArray(this[key]) ? this[key] : []` | O valor já convertido (falso, 0, '', [] se não há). | [Código](../../app/src/main/cpp/script/js/mod/TagCompound.js#L14) |
| `GetString(key)` | Instância | `TagCompound` | `String(this.Get(key, ''))` | O valor já convertido (falso, 0, '', [] se não há). | [Código](../../app/src/main/cpp/script/js/mod/TagCompound.js#L13) |
| `Remove(key)` | Instância | `TagCompound` | `Consultar fonte` | Escrever e apagar. | [Código](../../app/src/main/cpp/script/js/mod/TagCompound.js#L8) |
| `Set(key, value)` | Instância | `TagCompound` | `Consultar fonte` | Escrever e apagar. | [Código](../../app/src/main/cpp/script/js/mod/TagCompound.js#L6) |

## TooltipLine

[Contrato e campos](classes.md#tooltipline).

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `colorTag(text, color)` | Estático | `TooltipLine` | `'[c/' + TooltipLoader.Hex(color) + ':' + text + ']'` | '[c/RRGGBB:texto]' para pintar um trecho. | [Código](../../app/src/main/cpp/script/js/mod/TooltipLine.js#L20) |
| `constructor(...args)` | Construtor | `TooltipLine` | `instância` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/TooltipLine.js#L5) |
| `Hide()` | Instância | `TooltipLine` | `Consultar fonte` | Hide() tira a linha do tooltip. | [Código](../../app/src/main/cpp/script/js/mod/TooltipLine.js#L18) |

## UsageException

[Contrato e campos](classes.md#modcommand). Herda de `Error`.

| Método / assinatura | Tipo | Declaração | Retorno base | Contrato / limite | Fonte |
|---|---|---|---|---|---|
| `constructor(msg, color)` | Construtor | `UsageException` | `instância` | Consulte a implementação na fonte. | [Código](../../app/src/main/cpp/script/js/mod/ModCommand.js#L45) |
