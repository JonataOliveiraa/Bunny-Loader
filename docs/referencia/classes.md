# O que cada classe tem hoje

Esta referência descreve os contratos, campos e atalhos das classes de mod.
O [catálogo de métodos por classe](metodos.md) é o índice completo das
declarações públicas JavaScript, incluindo assinaturas, herança e aliases.
Também está disponível como [planilha Excel](metodos.xlsx) e [CSV](metodos.csv).
Os guias explicam como usar; aqui é para consultar.

Para cada método que você escreve, a tabela diz **quando ele roda** e **qual
método do jogo está por trás**, com o filtro nativo, se houver. Isso é o que
decide o custo (ver o [guia de custo](../mods/03-custo-e-desempenho.md)):

- **filtro `tipo`**: o hook só entra no JS para conteúdo **de mod**; o do jogo
  passa direto, quase de graça;
- **sem filtro**: entra no JS para toda chamada, e a classe confere o tipo lá
  dentro;
- **nativo**: não é um hook JS; o núcleo em C++ chama o método.

Hooks de conteúdo são instalados sob demanda, de acordo com os métodos
sobrescritos. Há infraestrutura compartilhada e hooks permanentes, como
`Player.GetHairSettings`; por isso, a quantidade de métodos disponíveis na
API não corresponde à quantidade de entradas JavaScript por quadro.

Todas as classes são **globais**: nada de `import`. Toda classe exportada pelo
arquivo de entrada ou por um arquivo de `Content/` e `Common/` é registrada
sozinha (`static Autoload = false` a deixa de fora, como uma
[classe base](../mods/04-conteudo-novo.md#classes-base-do-seu-mod-as-abstract-do-tmodloader)
que outras estendem); o `register` de cada uma
é para registrar na mão, no `Load()` do mod.

| Classe | Para quê | Guia |
|---|---|---|
| [`ModItem`](#moditem) | Item novo. | [5](../mods/05-itens.md) |
| [`ModPrefix`](#modprefix) | Prefixo (modificador) novo. | [5](../mods/05-itens.md#prefixos) |
| [`ModProjectile`](#modprojectile) | Projétil novo. | [6](../mods/06-projeteis.md) |
| [`ModNPC`](#modnpc) | NPC, chefe ou morador novo. | [7](../mods/07-npcs.md) |
| [`ModPlayer`](#modplayer) | Dados e comportamento por jogador. | [8](../mods/08-jogador-e-buffs.md) |
| [`ModBuff`](#modbuff) | Buff ou debuff novo. | [8](../mods/08-jogador-e-buffs.md) |
| [`ModMount`](#modmount) | Montaria nova (carro, carrinho de mina). | — |
| [`ModCommand`](#modcommand) | Comando de chat (`/nome args`). | — |
| [`ModHair`](#modhair) | Penteado novo (criação de personagem e Cabeleireiro). | — |
| [`ModCloud`](#modcloud) | Nuvem nova no céu (comum ou rara). | — |
| [`ModEmoteBubble`](#modemotebubble) | Emote novo (bolha e menu de emotes). | — |
| [`ModAchievement`](#modachievement) | Conquista nova. | — |
| [`ModRarity`](#modrarity) | Raridade nova (a cor do nome do item). | [5](../mods/05-itens.md#raridade-de-mod) |
| [`ModTile`](#modtile) | Bloco novo, ou móvel com `TileObjectData`. | [9](../mods/09-blocos.md) |
| [`ModWall`](#modwall) | Parede nova. | [9](../mods/09-blocos.md#paredes-modwall) |
| [`GlobalItem`, `GlobalNPC`, `GlobalProjectile`](#globalitem-globalnpc-e-globalprojectile) | Mexer nos itens, NPCs e projéteis do jogo. | [12](../mods/12-globais-e-mundo.md) |
| [`GlobalLoot`](#globalloot) | Drops que valem para todo NPC. | [12](../mods/12-globais-e-mundo.md#drops) |
| [`ModSystem`](#modsystem) | O que é do mod inteiro; o ciclo do mundo e os dados salvos nele. | [12](../mods/12-globais-e-mundo.md#modsystem-o-mundo) |
| [`ModBiome`, `ModSceneEffect`](#modbiome-e-modsceneeffect) | Bioma de mod e efeito de cena (a música, os fundos, a água e o fundo do mapa por prioridade). | — |
| [`ModSurfaceBackgroundStyle`, `ModUndergroundBackgroundStyle`](#fundos-de-mod) | Fundos de superfície e de subsolo, com as texturas do `BackgroundTextureLoader`. | — |
| [`ModWaterStyle`, `ModWaterfallStyle`](#água-de-mod) | A água e a cachoeira de um bioma. | — |
| [`ModMenu`](#modmenu) | Tema da tela de título: logo, sol, lua e fundo. | — |
| [`TagCompound`](#tagcompound) | Os dados que o mod salva (mundo, jogador). | [12](../mods/12-globais-e-mundo.md#dados-salvos-com-o-mundo) |
| [`ModPacket`, `NetWriter`, `NetReader`](#rede) | Dados de mod na rede. | [12](../mods/12-globais-e-mundo.md#rede) |
| [`Mod`, `ModLoader`](#mod-e-modloader) | O mod em si; conversa entre mods. | [11](../mods/11-conversa-entre-mods.md) |
| [`ModContent`](#modcontent) | Tipo, modelo e textura pelo nome ou pela classe. | [4](../mods/04-conteudo-novo.md#modcontent) |
| [`ModConfig`](#modconfig) | Opções persistentes do mod, alteradas no menu de pausa. | [14](../mods/14-opcoes-do-mod.md) |
| [`DamageClass`, `StatModifier`, `StatInheritanceData`](#damageclass) | Classes de dano, modificadores de atributos e herança de bônus. | [15](../mods/15-classes-de-dano.md) |
| [`ModRecipe`](#modrecipe) | Receitas e grupos de receita. | [5](../mods/05-itens.md#receitas) |
| [`NPCLoot`, `NPCSpawnInfo`, `NPCShop`, `NPCHappiness`, `ModGore`](#ajudantes-de-npc) | Drops, spawn, loja, felicidade, gore. | [7](../mods/07-npcs.md) |
| [`TooltipLine`, `DrawableTooltipLine`](#tooltipline) | Linha de tooltip, e a linha no desenho. | [5](../mods/05-itens.md#tooltip) |
| [`ModLocalization`](#modlocalization) | Textos traduzidos. | [4](../mods/04-conteudo-novo.md#tradução) |
| [`SoundStyle`, `SoundEngine`, `MusicLoader`](#som-e-música) | Som e música. | [10](../mods/10-sons-e-musica.md) |
| [Ajudantes](#ajudantes) | `Vector2`, `Color`, `Rand`, `Ref`... | [4](../mods/04-conteudo-novo.md#ajudantes) |

---

## ModItem

Um item novo. A classe é o **molde**; cada item do jogo daquele tipo ganha a
própria instância, com `this.Item` apontando para ele.

### Campos

| Campo | Tipo | Para quê |
|---|---|---|
| `Item` | `Item` do jogo | O item desta instância. No molde, `undefined`. |
| `Type` | número | O tipo (`ItemID`) deste item, depois do `register`. |
| `Mod` | `Mod` | O mod que registrou. |
| `Texture` | texto | O caminho no mod, sem `.png` (também `get Texture()`). Padrão: o arquivo da classe (`Content/Items/X.js` -> `Content/Items/X`). Nos mods de antes, o espelho em `Assets/Textures/`. |
| `DisplayName` | texto ou `{ cultura: texto }` | O nome. Vazio: `ItemName.<Classe>` em `Localization/*.json`, e sem isso o nome da classe. |
| `Tooltip` | texto ou `{ cultura: texto }` | A descrição, linhas por `\n`. Vazio: `ItemTooltip.<Classe>`. |
| `TooltipLines` | array de texto | As linhas, para o `ModifyTooltipLines` mexer. |
| `HideFromModMenu` | `boolean` | `true`: fora do Mod Menu. |

### Métodos que você escreve

| Método | Quando roda | Por trás |
|---|---|---|
| `SetStaticDefaults()` | Uma vez, com o tipo já nas tabelas do jogo. | nativo |
| `PostStaticDefaults()` | Logo depois do `SetStaticDefaults`. | nativo |
| `SetDefaults(item)` | Todo item deste tipo que nasce. | nativo (`Item.SetDefaults`) |
| `PostSetDefaults(item)` | Logo depois do `SetDefaults`. | nativo |
| `Clone(newItem)` | O jogo copiou o item (`Item.Clone`); devolva a instância da cópia. | `Item.Clone`, filtro `tipo` |
| `AddRecipeGroups()` | Uma vez, antes de qualquer receita de qualquer mod. | conteúdo pronto |
| `AddRecipes()` | Uma vez, com as receitas do jogo prontas. | conteúdo pronto |
| `PostSetupContent()` | Uma vez, com todo o conteúdo de mod no jogo. | conteúdo pronto |
| `ModifyTooltipLines()` | Uma vez por idioma, com `this.TooltipLines` preenchido. | no `register` |
| `ModifyTooltips(item, tooltips)` | Toda vez que o tooltip aparece, e no popup do guia de criação (sem cor). | `Main.MouseText_DrawItemTooltip` (porte em JS, só para o item que pede; + `DrawString` com `whileIn`); `GetLinesInfo` com `whileIn` no `GUICraftGuidePopup.UpdateText` |
| `PreDrawTooltip(item, lines, x, y)` | Antes das linhas; `x`, `y` são `Ref`; `false` não desenha as linhas. | idem |
| `PostDrawTooltip(item, lines)` | Depois de todas as linhas (`DrawableTooltipLine`). | idem |
| `PreDrawTooltipLine(item, line, yOffset)` | Antes de cada linha; `yOffset` (`Ref`) soma depois de cada linha seguinte; `false` não desenha a linha. | idem |
| `PostDrawTooltipLine(item, line)` | Depois de cada linha (também a não desenhada). | idem |
| `OnCraft(item, player, recipe)` | Ao criar o item no menu de criação. | `Main.CraftItem_GrantItem`, filtro `tipo` |
| `CanUseItem(item, player)` | Antes de usar; `false` impede. | `Player.ItemCheck_CheckCanUse_Inner`, filtro `tipo` |
| `UseItem(item, player)` | No quadro em que o uso começa. `true`: o item conta como usado (tempo de uso aplicado, como o `ApplyItemTime` do tModLoader), e o consumível é gasto. | `Player.ItemCheck_StartActualUse`, filtro `tipo`; o tempo, logo depois do `ItemCheck_OwnerOnlyCode` |
| `CanRightClick(item)` | `true`: o item abre no inventário (bolsa, caixa); entra em `ItemID.Sets.OpenableBag`. No toque, ganha o botão "Abrir" e os botões de usar ficam travados, como nas bolsas do jogo. | `ItemSlot.TryOpenContainer_GrantItems`, `GUIPageOptions.CanBeOpened`, `VirtualControllerInputState.GetItemCategory`, filtro `tipo` |
| `RightClick(item, player)` | Ao abrir, antes do `ItemLoot`. | idem |
| `ModifyItemLoot(itemLoot)` | Uma vez, na primeira abertura: o que sai do item (`itemLoot.Add(regra)`). As regras do jogo e `ItemDropRule.CoinsBasedOnNPCValue(npc)` / `ItemDropRule.Coins(valor)`; o que cairia no NPC vai para o jogador (`QuickSpawnItem`). | idem |
| `ConsumeItem(item, player)` | `false`: abrir não gasta o item. | idem |
| `IsQuestFish()` | `true`: peixe de missão do Pescador (o mesmo que `ItemID.Sets.IsQuestFish[this.Type] = true` no `SetStaticDefaults`). Entra no fim de `Main.anglerQuestItemNetIDs`. | no `Ready` |
| `IsAnglerQuestAvailable()` | `false`: o sorteio do dia não cai nele (sorteia de novo). | `Main.AnglerQuestSwap` |
| `AnglerQuestChat(description, catchLocation)` | A fala do Pescador (`Ref`): `description.value` e `catchLocation.value` (sai entre parênteses embaixo). Sem ela, o nome do item. | `Lang.AnglerQuestChat` |
| `HoldItem(item, player)` | Todo quadro com o item na mão. | `Player.ItemCheck_ApplyUseStyle`/`ApplyHoldStyle`, filtro `tipo` |
| `UseStyle(item, player, mountOffset, frame)` | Todo quadro de uso, depois do estilo do jogo. | idem |
| `HoldStyle(item, player, mountOffset, frame)` | Todo quadro segurando, depois do estilo do jogo. | idem |
| `HoldoutOffset(item, player)` | Devolva `{ X, Y }`: desloca a arma na mão. | idem |
| `CanShoot(item, player)` | `false`: usa, mas não atira. | `Player.ItemCheck_Shoot`, filtro `tipo` |
| `ModifyShootStats(item, player, stats)` | Antes de cada projétil: `stats.position`, `velocity`, `type`, `damage`, `knockBack`. | idem + `Projectile.NewProjectile` (sem filtro; só age durante o tiro) |
| `Shoot(item, player, position, velocity, type, damage, knockBack)` | `false`: o projétil do jogo não nasce. | idem |
| `OnHitNPC(item, player, npc, damageDone, knockBack, crit)` | Acerto corpo a corpo. | `Player.ApplyNPCOnHitEffects`, filtro `tipo` |
| `UpdateEquip(item, player)` | Todo quadro, equipado (armadura ou acessório). | `Player.ApplyEquipFunctional`, `GrantArmorBenefits`, filtro `tipo` |
| `UpdateAccessory(item, player, vanity, hideVisual)` | Todo quadro, acessório equipado (também no slot de vaidade). | `Player.ApplyEquipFunctional`, `ApplyEquipVanity`, filtro `tipo` |
| `IsArmorSet(head, body, legs)` | Para cada peça vestida de mod: `true` = conjunto completo. | `Player.UpdateArmorSets`, sem filtro (zera o `setBonus` antes) |
| `UpdateArmorSet(item, player)` | O bônus do conjunto; o texto vai em `player.setBonus`. | idem |
| `VerticalWingSpeeds(item, player, falling, rising, maxCan, maxAscent, constant)` | As asas deste item no ar (cinco `Ref`). | `Player.WingMovement`, filtro `wingsLogic` ≥ o `Count` do jogo |
| `HorizontalWingSpeeds(item, player, speed, acceleration)` | A corrida no ar com estas asas (dois `Ref`). | depois do `Player.WingAirLogicTweaks`, filtro `wingsLogic` |
| `WingUpdate(player, inUse)` | `true`: o mod anima as asas (o `WingFrame` do jogo não roda). | `Player.WingFrame`, filtro `wings` |
| `IsVanitySet(head, body, legs)` | Os SLOTS desenhados; `true` = conjunto de vaidade (padrão: o `IsArmorSet` dos itens desses slots). | pela textura de cada slot |
| `PreUpdateVanitySet(player)`, `UpdateVanitySet(player)` | Antes e depois do `FrameEffects`, com o conjunto de vaidade. | depois do `Player.PlayerFrame` |
| `ArmorSetShadows(player)` | As sombras do conjunto (`armorEffectDraw*`). | depois do `Player.SetArmorEffectVisuals` |
| `SetMatch(male, equipSlot, robes)` | O slot desenhado desta peça (`Ref`); o manto põe `robes` e as pernas. | depois do `Player.SetMatch` |
| `EquipFrameEffects(player, type)` | A cada quadro, com a textura do item vestida. | depois do `Player.PlayerFrame` |
| `UpdateVanity(item, player)` | Acessório no slot de vaidade. | `Player.ApplyEquipVanity` |
| `Load()` | No registro (o `AddEquipTexture` vai aqui). | registro |
| `UpdateInventory(item, player)` | Todo quadro, no inventário. | `Player.UpdateEquips`, sem filtro (percorre só os itens de mod do jogador) |
| `GetAlpha(item, lightColor)` | Devolva a `Color` do desenho. No chão o `item` é a `WorldItem` (com `Center`); no inventário, o `Item`, sem posição. | `Item.GetAlpha`, filtro `tipo` |
| `PostUpdate(item)` | A cada quadro, com o item no chão (`item` é a `WorldItem`). Luz, poeira. | `WorldItem.UpdateItem`, filtro `inner.type` |
| `ModifyFishingLine(item, bobber, lineOriginOffset, lineColor)` | Vara na mão, a cada boia: de onde a linha sai e a cor (dois `Ref`). | `Main.DrawProj_FishingLine`, sem filtro |
| `MeleePrefix(item)`, `WeaponPrefix(item)`, `RangedPrefix(item)`, `MagicPrefix(item)`, `SummonPrefix(item)` | As categorias de prefixo do item (padrão: `melee` sem `noUseGraphic`, `melee` com, `ranged`, `magic`, `summon`). Só vale para item com dano, sem ser munição nem consumível. Lido uma vez por tipo. | `Item.GetRollablePrefixes`, sem filtro |
| `ChoosePrefix(item, rand)` | Um prefixo forçado ao rolar (> 0), ou -1. | `Item.RollAPrefix`, sem filtro |
| `PrefixChance(item, pre, rand)` | `false` impede, `true` força um prefixo; `null` = o do jogo (`pre`: -1 criar/baú, -2 reforja). | `Item.Prefix`, sem filtro |
| `AllowPrefix(item, pre)` | `false` tira esse prefixo das opções. | `Item.GetRollablePrefixes` |
| `ApplyPrefix(item, pre)` | Depois dos status do prefixo. | `Item.Prefix`, sem filtro |

### Atalhos

| | |
|---|---|
| `CloneDefaults(tipo)` | Copia os valores de um item do jogo. |
| `SetWeaponValues(dano, repulsao, critico)` | |
| `SetDefaultWeaponStyle(useTime, autoReuse)` | `useTime`, `useAnimation`, `autoReuse` e o `useStyle` que combina. |
| `SetShopValues(raridade, preco)` | |
| `DefaultToPlaceableTile(tipoDoTile, estilo)` | Item que coloca um bloco. |
| `DefaultToFood(buff, tempo, gole, animacao)` | Comida. |
| `DefaultToWhip(proj, dano, repulsao, velocidade, animacao)` | Chicote. |
| `DefaultToSpear(proj, velocidade, animacao)` | Lança. |
| `DefaultToGolfBall(proj)` | Bola de golfe. |
| `SetItemAnimation(quadros, ticks, vaiEVolta)` | Item animado (tira vertical). No `SetStaticDefaults`. |
| `CreateRecipe(quantidade)` | Uma `ModRecipe` que dá este item. |
| `CreateRecipeGroup(tipos)` | Grupo com o nome do primeiro item. |
| `SetWingStats(tempo, velocidade, aceleração, pairar, velocidadePairando, aceleraçãoPairando)` | O WingStats das asas do item. No `SetStaticDefaults`. |
| `static AutoloadEquip = [EquipType.Head, ...]` | Só estas texturas vestidas pelo nome; `[]` nenhuma. |

### Estáticos

| | |
|---|---|
| `ModItem.register(Classe)` | Registra na mão (a classe exportada já é registrada sozinha); devolve o tipo. |
| `ModItem.getTypeByName('Classe')` | O tipo de um item **deste** mod; -1 se não há. |
| `ModItem.getModItem(tipo)`, `ModItem.getByName('Classe')` | O molde. |
| `ModItem.isModType(tipo)`, `ModItem.isModItem(item)` | É de mod? |
| `ModItem.CommonMaxStack` | 9999. |
| `ModItem.sellPrice(pl, ouro, prata, cobre)`, `buyPrice(...)` | Preço em cobre. |

### Ainda não

Do `ModItem` do tModLoader, entre outros: `ModifyHitNPC`, `MeleeEffects`,
`PreDrawInWorld`/`PostDrawInInventory`,
`OnPickup`, `GrabRange`, `ModifyWeaponDamage` (há no `ModPlayer`),
`DrawArmorColor`/`ArmorArmGlowMask`, `WingGlidingSpeeds`,
`ModifyEquipTextureDraw` e as camadas de desenho próprias (`PlayerDrawLayer`).

### Texturas vestidas: EquipLoader e EquipTexture

| | |
|---|---|
| `<Textura>_<tipo>.png` | A textura no corpo (`EquipType`: `Head`, `Body`, `Legs`, `HandsOn`, `HandsOff`, `Back`, `Front`, `Shoes`, `Waist`, `Wings`, `Shield`, `Neck`, `Face`, `Beard`, `Balloon`). |
| `EquipLoader.AddEquipTexture([mod,] textura, tipo, item, nome, equipTexture)` | Registra à mão, no `Load()`. |
| `EquipLoader.GetEquipSlot([mod,] nome, tipo)` | O slot; -1 se não há. Do `SetStaticDefaults` em diante. |
| `EquipLoader.GetEquipTexture(tipo, slot)` | A `EquipTexture` do slot. |
| `class X extends EquipTexture` | `FrameEffects`, `IsVanitySet`, `PreUpdateVanitySet`, `UpdateVanitySet`, `ArmorSetShadows`, `SetMatch`, `VerticalWingSpeeds`, `HorizontalWingSpeeds`, `WingUpdate` de uma textura só (padrão: os do item dono). |
| `ArmorIDs.Head.Sets.DrawHead`, `ArmorIDs.Body.Sets.HidesTopSkin`/`HidesBottomSkin`/`HidesHands`/`HidesArms`, `ArmorIDs.Legs.Sets.HidesTopSkin`/`HidesBottomSkin` | As do tModLoader (não existem no jogo daqui), aplicadas no desenho. |

O cabelo é decidido pelos Sets em `Terraria.ID`. O loader substitui
`Player.GetHairSettings` por uma consulta dessas tabelas:

| Set | Efeito |
|---|---|
| `ArmorIDs.Head.Sets.DrawFullHair[headSlot]` | Permite desenhar o cabelo completo com esse equipamento. |
| `ArmorIDs.Head.Sets.DrawHatHair[headSlot]` | Permite desenhar o cabelo de chapéu. |
| `ArmorIDs.Face.Sets.PreventHairDraw[faceSlot]` | Oculta o cabelo por causa do equipamento facial. |
| `ArmorIDs.Head.Sets.DrawsBackHairWithoutHeadgear[headSlot]` | Define a flag correspondente do cabelo traseiro. |
| `HairID.Sets.DrawBackHair[hair]` | Define se o penteado desenha cabelo atrás. |

Para um chapéu que mantém o cabelo de chapéu visível, configure o slot já
registrado no `SetStaticDefaults`:

```js
SetStaticDefaults() {
    Terraria.ID.ArmorIDs.Head.Sets.DrawHatHair[this.Item.headSlot] = true;
}
```

A regra de ocultação também considera `player.faceHead >= 0` com
`player.head !== 0`. Os [testes de cabelo e chapéus](../../tools/tests/hairsettings/check.mjs)
verificam a composição das flags.

---

## ModPrefix

Um prefixo (modificador) novo, como o `ModPrefix` do tModLoader. Uma instância
por prefixo; o `Type` é o número que o item guarda em `item.prefix` (depois
dos 98 do jogo, até 255: o jogo guarda o prefixo em 1 byte). Guia:
[5 · Prefixos](../mods/05-itens.md#prefixos).

### Campos

| Campo | Tipo | O que é |
|---|---|---|
| `Type` | número | O prefixo (só leitura). |
| `Mod`, `Name`, `FullName` | | O mod, o nome da classe, `'<id do mod>/<Classe>'`. |
| `DisplayName` | texto ou `{ cultura: texto }` | O nome. Vazio: `PrefixName.<Classe>` (ou `Prefixes.<Classe>.DisplayName`, como no tModLoader) em `Localization/*.json`, e sem isso o nome da classe. |
| `Category` | `PrefixCategory` | Que itens podem ganhar (padrão `Custom`). Escreva como getter: `get Category() { return PrefixCategory.AnyWeapon; }`. |

`PrefixCategory`: `Melee`, `Ranged`, `Magic`, `Summon`, `AnyWeapon`, `Accessory`,
`Custom` (não rola sozinho: só por `ChoosePrefix` do item ou `item.Prefix(tipo)`).

### Métodos que você escreve

| Método | Quando roda | Por trás |
|---|---|---|
| `SetStaticDefaults()` | Uma vez, com o conteúdo pronto e as tabelas crescidas (`PrefixID.Sets.ReducedNaturalChance[this.Type]`). | conteúdo pronto |
| `RollChance(item)` | O peso na rolagem (cada prefixo do jogo pesa 1). | `Item.RollAPrefix`, sem filtro |
| `CanRoll(item)` | `false`: este item não pode ganhar (padrão: `RollChance > 0`). | `Item.GetRollablePrefixes`, `CanRollPrefix` |
| `SetStats(damageMult, knockbackMult, useTimeMult, scaleMult, shootSpeedMult, manaMult, critBonus, tagDamage, armorPenetration)` | Os status, em `Ref` (`damageMult.value *= 1.2`). Com um parâmetro só, recebe `{ damage, knockBack, speed, size, shootSpeed, mana, crit, tagDamage, armorPenetration }` (a forma do ExMod do TL). O jogo aplica e confere, como os dele: se algum status não mudar de verdade no item, o prefixo não pega. | `Item.TryGetPrefixStatMultipliersForItem`, filtro: prefixo de mod |
| `AllStatChangesHaveEffectOn(item)` | `false`: não pega neste item (para status que não são do jogo). | idem |
| `ModifyValue(valueMult)` | O preço (`Ref`); a raridade sobe ou desce junto. | idem |
| `Apply(item)` | Depois dos status: o que mais o prefixo muda no item. | `Item.Prefix`, sem filtro |
| `ApplyAccessoryEffects(player)` | A cada quadro, com o acessório equipado. | `Player.GrantPrefixBenefits`, filtro: prefixo de mod |
| `GetTooltipLines(item)` | Linhas a mais (array de `TooltipLine`; `IsModifier = true` pinta de verde). As de dano, velocidade, crítico etc. o jogo já escreve. | `Main.MouseText_DrawItemTooltip_GetLinesInfo` |

### Estáticos e o PrefixLoader

| | |
|---|---|
| `ModPrefix.register(Classe)` | Registra na mão; devolve o tipo. |
| `ModPrefix.getTypeByName('Classe')`, `getModPrefix(tipo)`, `isModType(tipo)` | |
| `PrefixLoader.GetPrefix(tipo)` | O `ModPrefix`, ou `undefined` (prefixo do jogo). |
| `PrefixLoader.GetPrefixesInCategory(categoria)`, `PrefixLoader.PrefixCount` | |
| `PrefixLoader.Categories(item)`, `PrefixLoader.VanillaPrefixes(categoria)` | As categorias do item; os prefixos do jogo de uma categoria. |

### Save

O `.plr`/`.wld` guardam o prefixo em 1 byte, e o número de um prefixo de mod
depende dos mods instalados e da ordem de carga. Por isso ele vai também **pelo
nome** no `<personagem>.plr.bl` e no `<mundo>.wld.bl` (inventário, equipamento,
cofres, conjuntos, lixeira e baús), e volta certo com outros mods antes dele.
Sem o mod, o item fica sem o prefixo e o nome segue guardado enquanto o item
não sair do lugar; com o mod de volta, o prefixo volta. Manequim, cabide,
porta-armas e item no chão guardam só o número.

### Ainda não

`ModItem.ReforgePrice`/`CanReforge`/`PreReforge`/`PostReforge`, o prefixo "não
carregado" do tModLoader (`UnloadedPrefix`). A janela de reforja do celular
(`GUIReforgePopup`) monta as linhas dela à parte e não foi conferida: o
`GetTooltipLines` pode não aparecer lá (no inventário aparece).

---

## ModProjectile

Um projétil novo. Como o item: molde e uma instância por projétil
(`this.Projectile`).

### Campos

| Campo | Tipo | Para quê |
|---|---|---|
| `Projectile` | `Projectile` do jogo | O projétil desta instância. |
| `Type`, `Mod` | | O tipo e o mod. |
| `Texture` | texto | O caminho no mod, sem `.png`; padrão, ao lado do arquivo da classe. |
| `DisplayName` | texto ou `{ cultura: texto }` | Vazio: `ProjectileName.<Classe>`. |
| `AIType` | número | Usa a IA deste projétil do jogo (o tipo é trocado só durante a IA). |

### Métodos que você escreve

| Método | Quando roda | Por trás |
|---|---|---|
| `SetStaticDefaults()` | Uma vez. `Main.projFrames[this.Type] = n` aqui vale como os quadros da textura. | nativo |
| `SetDefaults(proj)`, `PostSetDefaults(proj)` | Todo projétil deste tipo que nasce. | nativo (`Projectile.SetDefaults`) |
| `PostStaticDefaults()`, `PostSetupContent()` | Uma vez. | nativo / conteúdo pronto |
| `Clone(newProjectile)` | Cópia da instância. | |
| `OnSpawn(proj)` | Uma vez, no primeiro quadro de vida. | `Projectile.AI`, filtro `tipo` |
| `PreAI(proj)` | Antes da IA; `false` pula a IA do jogo e o `AI`. | idem |
| `AI(proj)` | Todo quadro. | idem |
| `PostAI(proj)` | Depois da IA. | idem |
| `OnTileCollide(proj, oldVelocity)` | Bateu num bloco e ia morrer; `false` o mantém vivo. | `Projectile.HandleMovement` + `Kill`, filtro `tipo` |
| `TileCollideStyle(proj, width, height, fallThrough, hitboxCenterFrac)` | Quatro `Ref`: dimensões da colisão, passagem por plataformas e centro. `false` pula a colisão neste movimento. | `Projectile.HandleMovement`/`GetCollisionParams`, filtro por tipo e sobrescrita |
| `PreKill(proj, timeLeft)` | Antes de morrer; `false` tira os efeitos do jogo. | `Projectile.Kill`, filtro `tipo` |
| `OnKill(proj, timeLeft)` | Ao morrer. | idem |
| `OnHitNPC(proj, npc)` | Acertou um NPC. | `Projectile.StatusNPC`, filtro `tipo` |
| `OnHitPlayer(proj, player)` | Acertou um jogador. | `Projectile.StatusPlayer`, filtro `tipo` |
| `Colliding(proj, projHitbox, targetHitbox)` | `true`/`false` decide o acerto; `undefined`, o do jogo. | `Projectile.Colliding`, filtro `tipo` |
| `CanDamage(proj)` | `false`: não causa dano. | `Projectile.Damage`, filtro `tipo` |
| `MinionContactDamage(proj)` | `true`: lacaio ou pet fere ao encostar. | idem |
| `ModifyDamageHitbox(proj, hitbox)` | Mude o `Rectangle` da área de dano. | `Projectile.Damage_GetHitbox`, filtro `tipo` |
| `CanCutTiles(proj)`, `CutTiles(proj)` | Cortar grama e teia. | `Projectile.CanCutTiles`/`CutTiles`, filtro `tipo` |
| `GetAlpha(proj, lightColor)` | A cor final; `undefined`, a do jogo. | `Projectile.GetAlpha`, filtro `tipo` |
| `PreDrawExtras(proj)` | Antes do `PreDraw`; `false` tira o que o jogo desenha antes do sprite: a corrente do gancho, a linha de pesca, o fio do ioiô, a corrente do mangual. O gancho com corrente própria desenha a dele aqui. | `Main.DrawProjDirect` (no celular a corrente fica dentro dele: até o primeiro desenho com a textura do projétil, os outros não saem) |
| `PreDraw(proj, lightColor)` | Antes do desenho; `false` não desenha o sprite do jogo (os extras do `PreDrawExtras` continuam). | `Main.DrawProjDirect`, filtro `tipo` |
| `PostDraw(proj, lightColor)` | Depois do desenho, mesmo com o `PreDraw` `false` (como no tModLoader). | idem |
| `CanUseGrapple(player, type)` | No **molde**, antes de lançar o gancho; `false` impede. | `Player.FireGrapple`, filtro pelo `item.shoot` |
| `UseGrapple(player, type)` | No molde: devolva o tipo a lançar. | idem |
| `GrappleCanLatchOnTo(proj, player, tile)` | `true`/`false`: agarra neste bloco. | `Projectile.AI_007_GrapplingHooks_CanTileBeLatchedOnTo`, filtro `tipo` |

Sempre instalado, para todo projétil de mod: o `originalDamage` (o dano base
de lacaio e sentinela) vem do item que criou o projétil
(`Projectile.ApplyStatsFromSource`).

`OnTileCollide` retornando `false` conserva o projétil apenas no choque
atual. Ele ainda morre ao expirar ou ao esgotar a penetração em um NPC,
passando por `PreKill` e `OnKill`. Um veto em `PreKill` remove o projétil e
encerra o fluxo antes de `OnKill`. Veja a
[regressão do ciclo de morte](../../tools/tests/projectilekill/check.mjs).

`SendExtraAI(writer)` e `ReceiveExtraAI(reader)` acompanham a sincronização
do projétil. Use os leitores e escritores do loader descritos em [Rede](#rede).

### Atalhos e estáticos

| | |
|---|---|
| `CloneDefaults(tipo)` | Copia os valores de um projétil do jogo. |
| `DefaultToSpear()`, `DefaultToYoyo()`, `DefaultToFlail()`, `DefaultToWhip()`, `DefaultToDrillOrChainsaw()`, `DefaultToKite()` | Os padrões do jogo para cada família de projétil segurado. |
| `ModProjectile.register`, `getTypeByName`, `getModProjectile`, `getByName`, `isModType`, `isModProjectile` | Como no item. |

### Ainda não

`ModifyHitNPC`, `CanHitNPC` e `OnHitNPC` com as informações completas do golpe.

---

## ModNPC

Um NPC novo: inimigo, chefe ou morador. Molde e uma instância por NPC
(`this.NPC`).

### Campos

| Campo | Tipo | Para quê |
|---|---|---|
| `NPC`, `Type`, `Mod` | | O NPC desta instância, o tipo, o mod. |
| `Texture` | texto | O caminho no mod, sem `.png`; padrão, ao lado do arquivo da classe. Tira vertical de quadros. |
| `DisplayName` | texto ou `{ cultura: texto }` | Vazio: `NPCName.<Classe>`. |
| `AnimationType` | número | Anima como este NPC do jogo (0 = não). Vale no `SetDefaults`. |
| `HideFromBestiary` | `boolean` | Sem entrada no Bestiário. |
| `HideFromModMenu` | `boolean` | Fora do Mod Menu. |
| `HeadTexture`, `ShimmerHeadTexture` | texto | Morador: a cabeça no mapa. Padrão: `<Texture>_Head`, `<Texture>_Shimmer_Head`. |
| `Music` | número | A música enquanto ele está perto da tela (`MusicLoader.GetMusicSlot` ou `MusicID`); -1 = a do jogo. |
| `SceneEffectPriority` | número | Entre dois NPCs com música, ganha o maior. |
| `Happiness` | `NPCHappiness` | Gostos do morador (no `SetStaticDefaults`). |

### Métodos que você escreve

| Método | Quando roda | Por trás |
|---|---|---|
| `SetStaticDefaults()` | Uma vez. `Main.npcFrameCount[this.Type] = n`: quadros da textura. | nativo |
| `SetDefaults(npc)` | Todo NPC deste tipo que nasce. A escala de Expert/Mestre vem depois. | nativo (`NPC.SetDefaults`) |
| `PostStaticDefaults()`, `PostSetDefaults(npc)`, `PostSetupContent()` | | nativo / conteúdo pronto |
| `Clone(newNPC)` | Cópia da instância. | |
| `ApplyBuffImmunity(npc)` | Depois do `SetDefaults`: `npc.buffImmune[id] = true`. | nativo |
| `ModifyNPCLoot(npcLoot)` | Uma vez: os drops. | nativo |
| `SetBestiary(database, bestiaryEntry)` | Uma vez: a entrada do Bestiário. | conteúdo pronto |
| `HitEffect(npc, hitDirection, damage)` | A cada golpe, depois do efeito do jogo. | nativo (`NPC.HitEffect`), só se escrito |
| `OnSpawn(npc, source)` | Uma vez após nascer; recebe a fonte nativa. | `NPC.NewNPC`, filtro no argumento Type e por sobrescrita |
| `ResetEffects(npc)` | Depois de zerar os flags de buffs, antes de reaplicar efeitos e executar AI. | entrada de `NPC.UpdateNPC_BuffSetFlags`, filtro por tipo e sobrescrita |
| `CanBeHitByItem(npc, player, item)` | `false` veta; `true` permite no teste de elegibilidade; `null`/`undefined` mantém o jogo. Um veto de ModPlayer prevalece. | fluxo compartilhado de `Player.ProcessHitAgainstNPC` e `CanNPCBeHitByPlayerOrPlayerProjectile` |
| `ModifyIncomingHit(npc, modifiers)` | Altera dano, empurrão, direção e crítico antes do golpe nativo. Não reaplica em `fromNet`. | `NPC.StrikeNPC_Inner`, filtro por tipo e sobrescrita |
| `OnHitByItem(npc, player, item, hit, damageDone)` | Após dano efetivo de item, uma vez por golpe. | fluxo compartilhado de `NPC.StrikeNPC` |
| `OnHitByProjectile(npc, projectile, hit, damageDone)` | Após dano efetivo de projétil, inclusive sem dono jogador. | `Projectile.Damage_PVE` e fluxo compartilhado de `NPC.StrikeNPC` |
| `CanHitPlayer(npc, player, cooldownSlot)` | `false` veta dano; `cooldownSlot.value` altera o slot de imunidade. | fluxo compartilhado de `Player.Hurt` |
| `ModifyHitPlayer(npc, player, modifiers)` | Altera os parâmetros de dano suportados pelo mobile antes de `Hurt`. | idem |
| `OnHitPlayer(npc, player, hurtInfo)` | Após dano efetivo, recebe os dados do golpe e o slot usado. | idem |
| `CheckDead(npc)` | Com NPC ativo e vida ≤ 0: `false` impede a morte; restaure a vida para evitar nova checagem. | `NPC.checkDead`, filtro por tipo e sobrescrita |
| `ApplyDifficultyAndPlayerScaling(npc, numPlayers, balance, bossAdjustment)` | Depois do escalonamento por jogadores. Recebe a contagem usada pelo NPC, balance calculado pelo jogo e ajuste 0,85 em Master ou 1 nos demais modos. | `NPC.ScaleStats_ByPlayerCount`, filtro por tipo e sobrescrita |
| `DrawEffects(npc, drawColor)` | `Ref<Color>` da iluminação do centro; roda antes de `PreDraw`. A cor alterada passa pelo tint dos buffs do jogo. | duas sobrecargas de `Main.DrawNPCDirect`, filtro por NPC e sobrescrita |
| `PreDraw(npc, spriteBatch, screenPos, drawColor)` | `false` pula o desenho nativo. | idem |
| `PostDraw(npc, spriteBatch, screenPos, drawColor)` | Após desenhar, inclusive quando `PreDraw` veta. | idem |
| `DrawBehind(npc, index)` | Após o cache de desenho; adicione o índice às listas `Main.instance.DrawCacheNPCs*`. | `Main.CacheNPCDraws`, só entidades rastreadas com sobrescrita |
| `GetAlpha(npc, drawColor)` | `Color` substitui o resultado, inclusive alfa 0; `null`/`undefined` usa o jogo. | `NPC.GetAlpha`, filtro por tipo e sobrescrita |
| `BossHeadSlot(npc, index)`, `BossHeadRotation(npc, rotation)`, `BossHeadSpriteEffects(npc, spriteEffects)` | Recebem `Ref` inicializada com o resultado do jogo. Índice -1 esconde o ícone. | getters `NPC.GetBossHead*`, filtro por tipo e sobrescrita |
| `PreAI(npc)`, `AI(npc)`, `PostAI(npc)` | Todo quadro. `PreAI` → `false` pula a IA do jogo. | `NPC.AI`, filtro `tipo` |
| `FindFrame(npc, frameHeight)` | Animação própria: mude `npc.frame`. | `NPC.FindFrame`, filtro `tipo` |
| `CheckActive(npc)` | `false`: não some quando longe. | `NPC.CheckActive`, filtro `tipo` |
| `PreKill(npc)`, `OnKill(npc)` | Na morte. `PreKill` → `false` cancela o drop. | `NPC.NPCLoot`, filtro `tipo` |
| `SpawnChance(spawnInfo)` | A cada spawn natural: devolva o peso (o do jogo pesa 1; 0 = não nasce). Só sozinho ou no servidor. | `NPC.Spawner.SpawnAnNPC`, sem filtro |
| `SpawnNPC(tileX, tileY)` | Sorteado: como nasce, no bloco do spawn; devolve o índice. Padrão: em cima do bloco (`tileX * 16 + 8`, `tileY * 16`), como no tModLoader. | idem |
| `SpawnModBiomes` | No `SetDefaults`: os `ModBiome` onde ele nasce (classes, instâncias ou `Type`): o nome, o ícone e o fundo deles na entrada do Bestiário, e o filtro. | ao montar o Bestiário |
| **Morador** | | |
| `CanTownNPCSpawn(numTownNPCs)` | De tempos em tempos, sem um deste no mundo: `true` e ele se muda. | `Main.UpdateTime_SpawnTownNPCs`, sem filtro |
| `CheckConditions(left, right, top, bottom)` | A sala serve para ele? | `WorldGen.CheckSpecialTownNPCSpawningConditions` |
| `SetNPCNameList()` | Os nomes próprios; um é sorteado. | `NPC.getNewNPCName` |
| `GetChat(npc)` | A fala ao conversar. | `NPC.GetChat`, filtro `tipo` |
| `CanChat(npc)` | Decide se pode conversar, inclusive fora de AI 7; retorno indefinido mantém o jogo. | `NPC.get_CanTalk` e `get_CanBeTalkedTo`, filtro por tipo e sobrescrita |
| `TownNPCProfile()` | Uma vez após carregar conteúdo: devolva um `ITownNPCProfile` nativo; `null` mantém a aparência automática. | registro em `TownNPCProfiles.Instance`, sem hook por frame |
| `ModifyActiveShop(npc, shopName, items)` | Ao preencher uma loja registrada deste NPC em conversa, após `NPCShop` montar os itens. Recebe o nome local da loja e o array nativo de itens. | `InventoryStorage.SetupShop` |
| `ModifyNPCHappiness(npc, player, primaryPlayerBiome, shopHelper, nearbyNPCsByType)` | Após as preferências nativas, antes de limitar o preço. Usa a alternativa de ExMod; o método está desativado no tModLoader stable. | `AllPersonalitiesModifier.ModifyShopPrice`, sem filtro nativo, somente cálculos de preço |
| `SetChatButtons(npc, buttons)` | Os botões da conversa (até dois no celular). | `GUINPCDialogue.SetupButtonText` |
| `OnChatButtonClicked(npc, firstButton)` | Tocou num botão; devolva o nome de uma loja para abri-la. | `GUINPCDialogue.Option1Clicked`/`Option2Clicked` |
| `AddShops()` | Uma vez: as lojas (`NPCShop`). | conteúdo pronto |
| `TownNPCAttackProj(npc, attack)` | O projétil do ataque (`projType`, `attackDelay`). | `NPC.AI`, filtro `tipo` |
| `TownNPCAttackStrength(npc, attack)` | `damage`, `knockback`. | idem |
| `TownNPCAttackProjSpeed(npc, attack)` | `speed`, `gravityCorrection`, `randomOffset`. | idem |
| `NPCHeadSlot()` | (para ler) o índice da cabeça, -1 sem cabeça. | |

A música (`Music`) é decidida pelos hooks de `Main.UpdateAudio*`, instalados
uma vez; ver o [guia 10](../mods/10-sons-e-musica.md).

Os modificadores de entrada usam `damage`, `knockBack`, `hitDirection`, `crit`,
`SourceDamage` e `Knockback` (`StatModifier`), além de `SetCrit()` e
`DisableCrit()`. `ModifyHitPlayer` usa `damage`, `hitDirection`, `quiet`,
`crit` e `dodgeable`, como o fluxo de `ModPlayer` do Bunny Loader.
`hit` contém `Damage`, `SourceDamage`, `Knockback`, `HitDirection` e `Crit`;
`damageDone` é o retorno efetivo do golpe nativo. Esses objetos são contratos
JavaScript do Bunny Loader: não incluem os demais campos das estruturas do
tModLoader. `hurtInfo` inclui `DamageSource`, `Damage`, `HitDirection`, `PvP`,
`Quiet`, `Crit`, `CooldownCounter` e `Dodgeable`.

Os métodos sem contrato completo acessível no mobile foram descartados;
as razões e os testes estão em
[ModNPC: contratos e validação](../../tools/tests/modnpchooks/README.md).

No callback de felicidade, o biome primário segue os IDs do ExMod: 0 floresta,
1 subterrâneo, 2 neve, 3 deserto, 4 selva, 5 oceano, 6 sagrado, 7 cogumelo,
8 masmorra, 9 corrupção e 10 carmim. `shopHelper` é nativo e aceita seus
métodos de preferência; o array de vizinhos também vem do `HelperInfo` nativo.

### Estáticos

| | |
|---|---|
| `ModNPC.register`, `getTypeByName`, `getModNPC`, `getByName`, `isModType`, `isModNPC` | Como no item. |
| `ModNPC.NPCValue(pl, ouro, prata, cobre)` | O dinheiro que ele solta. |

### Ainda não

`ModifyHitByItem`/`ModifyHitByProjectile`, `CanBeHitByProjectile`,
`UpdateLifeRegen`, `ModifyDeathMessage`, `DrawHealthBar`, sacola de tesouro,
marca de chefe derrotado, `CanGoToStatue` e os ataques de morador além
do projétil.

---

## ModPlayer

Dados e comportamento de cada jogador. **Uma instância por jogador por
classe**, criada na primeira vez que alguém pergunta por ela. Os métodos
recebem o jogador (`player`), que é o mesmo `this.Player`.

### Campos

| Campo | Para quê |
|---|---|
| `Player` | O jogador desta instância. |
| `CumulativeHealth`, `CumulativeMana` | No `ModifyMaxStats`: somados à vida e à mana máximas. |
| `WeaponDamage` | No `ModifyWeaponDamage`: o dano (alternativa a devolver). |

### Métodos que você escreve, na ordem de um quadro

| Método | Quando roda | Por trás |
|---|---|---|
| `PreUpdate(player)` | Começo do quadro do jogador. | `Player.Update` |
| `ResetEffects(player)` | Logo depois do jogo zerar os efeitos. | `Player.ResetEffects` |
| `ModifyMaxStats(player)` | Depois do `ResetEffects`. | idem |
| `PreUpdateBuffs(player)`, `PostUpdateBuffs(player)` | Em volta dos buffs. | `Player.UpdateBuffs` |
| `UpdateEquips(player)`, `PostUpdateEquips(player)` | Depois dos equipamentos. | `Player.UpdateEquips` |
| `UpdateBadLifeRegen(player)`, `UpdateLifeRegen(player)` | Antes e depois da regeneração de vida. | `Player.UpdateLifeRegen` |
| `UpdateManaRegen(player)` | Depois da regeneração de mana. | `Player.UpdateManaRegen` |
| `PreModifyLuck(player, luck)`, `ModifyLuck(player, luck)` | A sorte, `luck.value`; `PreModifyLuck` false pula a do jogo. | `Player.RecalculateLuck` |
| `UpdateMovement(player)` | Movimento próprio (dash), perto do fim do quadro. | `Player.BordersMovement` |
| `FrameEffects(player)` | Depois de o jogo montar o que se desenha: trocar `player.head`/`body`/`legs` muda o desenho. | depois do `Player.PlayerFrame` |
| `PostUpdate(player)` | Fim do quadro. | `Player.Update` |
| `UpdateDead(player)` | Todo quadro morto. | `Player.UpdateDead` |

### Outros

| Método | Quando roda | Por trás |
|---|---|---|
| `Initialize()` | Uma vez, quando a instância nasce. | |
| `OnEnterWorld(player)` | Entrou no mundo. | `Player.Hooks.EnterWorld` |
| `OnRespawn(player)` | Voltou a viver. | `Player.Spawn` |
| `CanUseItem(player, item)` | `false` impede usar. | `Player.ItemCheck_CheckCanUse_Inner` |
| `ModifyWeaponDamage(player, item, damage)` | Devolva o dano novo. | `Player.GetWeaponDamage` |
| `ImmuneTo(player, source, cooldown, dodgeable)` | `true`: o golpe não acontece. | `Player.Hurt` |
| `FreeDodge(player, source, damage, ...)` | `true`: esquiva. | idem |
| `ModifyHurt(player, modifiers)` | Antes do golpe: `modifiers.damage`, `hitDirection`, `quiet`, `crit`, `dodgeable`. | idem |
| `OnHurt(player, source, damage, ...)` | Depois do golpe. | idem |
| `PostHurt(player, source, damage, ...)` | Depois do golpe, se sobreviveu. | idem |
| `PreKill(player, source, damage, direction, pvp)` | `false` impede a morte. | `Player.KillMe` |
| `Kill(player, source, damage, direction, pvp)` | Morreu. | idem |
| `CatchFish(attempt, itemDrop, npcSpawn, sonar, sonarPosition)` | Pescou, depois do sorteio do jogo: `itemDrop.value` e `npcSpawn.value` (`Ref`, 0 = nada) decidem o que sai; `attempt` tem `questFish`, `common`/`uncommon`/`rare`..., `inLava`... `sonar`/`sonarPosition` ainda não fazem nada. | `Projectile.FishingCheck_RollItemDrop` |
| `SaveData(data)` | A cada save: ponha o que lembrar em `data`. | `Player.InternalSavePlayerFile` |
| `LoadData(data)` | Ao carregar o personagem. | `Player.LoadPlayer` |

Os hooks de `ModPlayer` são compartilhados e instalados conforme as
sobrescritas. Hooks de atualização podem executar a cada tick por jogador;
os de combate, item e desenho acompanham os respectivos eventos. Listas de
callbacks são armazenadas em cache e os despachos frequentes evitam arrays
temporários. O custo depende dos callbacks ativos e do que cada mod executa.
Veja a [revisão e as medições](../../tools/tests/modplayerperf/RESULTADOS.md).

### Como achar a instância

| | |
|---|---|
| `player.GetModPlayer(Classe)`, `GetModPlayer('Classe')` ou `GetModPlayer('mod/Classe')` | A instância daquele jogador. Pelo nome: a do seu mod; se o nome é de outro mod e mais de um mod o tem, `undefined` e aviso no log. Dois mods podem ter `ModPlayer` com o mesmo nome. |
| `Classe.get(player)` | O mesmo. |
| `ModPlayer.getByName('Classe')` | A do jogador **local** (para interface). |
| `ModPlayer.register(Classe)` | Registra. |

### Combate, itens, desenho, inventário e rede

A [referência completa dos hooks de ModPlayer](modplayer-hooks.md) descreve
os hooks adicionais, as referências, os retornos, os descritores de saltos e
camadas, e as adaptações do Terraria nativo.

O teste com host e cliente reais teve 102 verificações sem falhas, incluindo
desconexão e reconexão. Essa execução não cobre todos os hooks nem um
servidor dedicado. Consulte os [resultados multiplayer](../../tools/tests/mpmodplayer/RESULTADOS.md)
e o [roteiro com limites de cobertura](../../tools/tests/mpmodplayer/README.md).

---

## ModBuff

Um buff ou debuff novo. **Uma instância por tipo** (buff não é entidade): os
métodos recebem o jogador ou o NPC e a posição do buff na lista dele.

### Campos

| Campo | Para quê |
|---|---|
| `Type`, `Mod` | |
| `Texture` | 32x32; o caminho no mod, sem `.png` (padrão: ao lado do arquivo da classe). |
| `DisplayName`, `Description` | Texto ou `{ cultura: texto }`. Vazios: `BuffName.<Classe>`, `BuffDescription.<Classe>`. |
| `HideFromModMenu` | Fora do Mod Menu. |

### Métodos que você escreve

| Método | Quando roda | Por trás |
|---|---|---|
| `SetStaticDefaults()` | Uma vez: `Main.debuff[this.Type]`, `buffNoSave`, `BuffID.Sets...`. | nativo |
| `PostStaticDefaults()`, `PostSetupContent()` | | |
| `ModifyDisplayName()`, `ModifyDescription()` | Uma vez por idioma: mexa em `this.DisplayName`/`this.Description`. | no `register` |
| `UpdatePlayer(player, buffIndex)` | Todo quadro, ativo no jogador. | `Player.UpdateBuffs` |
| `UpdateNPC(npc, buffIndex)` | Todo quadro, ativo no NPC. | `NPC.UpdateNPC_BuffSetFlags` |
| `ApplyPlayer(player, time)`, `ApplyNPC(npc, time)` | Quando entra. | `Player.AddBuff_ActuallyTryToAddTheBuff`, `NPC.AddBuff` |
| `ReApplyPlayer(player, time, i)`, `ReApplyNPC(npc, time, i)` | Quando entra de novo; `false` impede renovar o tempo. | `Player.AddBuff_TryUpdatingExistingBuffTime`, `NPC.AddBuff` |
| `CanRemove(player, time, i, debuff)` | Ao tocar no ícone: `true`/`false` decide; `null`, o jogo. | `GUIBuffs.RemoveBuff` |
| `OnRemove(player, time, i)` | Depois de tirado pelo toque. | idem |

### Estáticos

`ModBuff.register`, `getTypeByName`, `getModBuff`, `isModType`.

### Ainda não

`ModifyBuffText`, `PreDraw`/`PostDraw` do ícone, `RightClick`.

---

## ModMount

Uma montaria nova, como o `ModMount` do tModLoader. **Uma instância por
tipo**: o estado de cada jogador fica no `Mount` dele (`player.mount`). O
tipo sai no registro, antes do buff e do item que o pedem
(`ModContent.MountType`); o `MountData` e o `SetStaticDefaults` vêm com o jogo
pronto, quando os tipos de buff e de poeira também existem.

### Campos

| Campo | Para quê |
|---|---|
| `Type`, `Mod` | |
| `MountData` | O `Mount.MountData` do jogo: velocidade, pulo, quadros, deslocamentos, `buff`, `spawnDust`, texturas. Mexa no `SetStaticDefaults`. |
| `Texture` | A base das camadas: `<Texture>_Back`, `_BackGlow`, `_BackExtra`, `_BackExtraGlow`, `_Front`, `_FrontGlow`, `_FrontExtra`, `_FrontExtraGlow` (`MountTextureType`), só as que existirem. Padrão: o espelho do arquivo da classe (`Content/Mounts/X.js` → `Assets/Textures/Mounts/X_Back.png`). |

### Métodos que você escreve

Os `ref` do tModLoader chegam como `Ref` (`.value`).

| Método | Quando roda | Por trás |
|---|---|---|
| `SetStaticDefaults()` | Uma vez, com as texturas já no `MountData`. | no `Ready` |
| `SetMount(player, skipDust)` | Ao montar, depois do `FinalizeMountData`; `skipDust.value = true` pula a poeira do jogo. | `Mount.SetMount`/`DoSpawnDust` |
| `Dismount(player, skipDust)` | Ao desmontar, antes do jogo. | `Mount.Dismount` |
| `UpdateEffects(player)` | Todo quadro montado, antes dos efeitos do jogo. | `Mount.UpdateEffects` |
| `UpdateFrame(player, state, velocity)` | A animação; `false` pula a do jogo. | `Mount.UpdateFrame` |
| `JumpHeight(player, jumpHeight, xVelocity)`, `JumpSpeed(player, jumpSpeed, xVelocity)` | O pulo, com o valor do jogo no `Ref`. | `Mount.JumpHeight`/`JumpSpeed` |
| `UseAbility(player, mouse, toggleOn)`, `AimAbility(player, mouse)` | Habilidade da montaria. | `Mount.UseAbility`/`AimAbility` |
| `Draw(playerDrawData, drawType, player, texture, glowTexture, drawPosition, frame, drawColor, glowColor, rotation, spriteEffects, drawOrigin, drawScale, shadow)` | Uma vez por camada com textura (0 Back, 1 BackExtra, 2 Front, 3 FrontExtra). Os `Ref` trazem o que o jogo desenharia; mudados, desenha-se com os novos. `false`: a camada não é desenhada. | `Mount.Draw` |

### Estáticos

| | |
|---|---|
| `ModMount.GetSpecificData(player)`, `SetSpecificData(player, valor)` | O dado do jogador nesta montaria (o `_mountSpecificData` do tModLoader, que no C# é `object`). Apagado ao montar outra e ao desmontar. |
| `ModMount.NewDrawData(texture, position, sourceRect, color, rotation, origin, scale, effect)` | Um `DrawData`. |
| `ModMount.AddDrawData(playerDrawData, data)` | O `playerDrawData.Add` do tModLoader, dentro do `Draw`. |

Buff com `BuffID.Sets.MountType[this.Type] = tipo` monta o jogador sozinho
(o jogo faz isso no `UpdateBuffs`) e fica sem tempo na tela e sem salvar. O
item aponta a montaria em `Item.mountType`.

### Diferenças do tModLoader

- `MountData.MinecartUpgrade*` não existe no celular: o kit de melhoria do
  carrinho usa os valores do jogo.
- `DelegateMethods.Minecart.SparksMeow` e os sons do Meowmere são privados no
  celular: copie os `delegations` de um carrinho do jogo
  (`Terraria.Mount.mounts[MountID.MeowmereMinecart].delegations`).
- `ModDust` ainda não existe: `spawnDust` com poeira do jogo.

---

## ModCommand

Um comando de chat, como o `ModCommand` do tModLoader. Uma instância por
comando. O texto digitado passa pelo processador do jogo; o que ele não
conhece chega com a barra, e o de mod roda no lugar.

| | |
|---|---|
| `Command` | O nome, sem a barra (getter). |
| `Aliases` | Outros nomes (do ExMod): `get Aliases() { return ['curar']; }`. |
| `Type` | `CommandType.Chat` (no aparelho de quem digitou), `Server` (no servidor, pedido por um jogador), `World` (sozinho no chat; no multijogador, no servidor). `Console` não existe no celular. |
| `Usage`, `Description` | Padrão: `Commands.<Classe>.Usage`/`.Description` do `Localization` (o `/help` mostra). |
| `IsCaseSensitive` | `false`: o texto chega em minúsculas. |
| `Action(caller, input, args)` | O comando. `caller.Player`, `caller.CommandType`, `caller.Reply(texto, cor)`. `throw new UsageException(texto, cor)` responde o erro; `return false` (do ExMod) responde o `Usage`. |

`/mod:nome` escolhe o mod quando dois têm o mesmo nome. O `/help` do jogo
lista os de mod no fim.

---

## ModRarity

Uma raridade, como o `ModRarity` do tModLoader. Tipo depois das 12 do jogo (`ModContent.RarityType(Classe)`); vai em `item.rare`.

| | |
|---|---|
| `get RarityColor()` | A cor do nome (lida a cada desenho: pode piscar). |
| `GetPrefixedRarity(offset, valueMult)` | A raridade quando um prefixo sobe (1, 2) ou desce (-1, -2) o item; padrão `this.Type`. |
| `SetStaticDefaults()` | Uma vez, com o jogo pronto. |

Hooks: `ItemRarity.Initialize` (a tabela das cores), `Item.GetPopupRarityColor`, `Item.Prefix` (filtro `rare`) e
`Main.MouseTextInner` (o item no chão). `ModContent.GetModRarity(tipo)` devolve a instância.

---

## ModHair

Um penteado, como o `ModHair` do tModLoader. Tipo depois dos 228 do jogo.

| | |
|---|---|
| `Texture`, `AltTexture` | A do penteado e a com chapéu (`<Texture>_Alt`; sem ela, a mesma). Padrão: ao lado do arquivo (`Content/Hairs/X.js` → `Content/Hairs/X.png`). |
| `AvailableDuringCharacterCreation` | Na criação de personagem (getter; padrão `true`). |
| `GetUnlockConditions()` | No Cabeleireiro quando todas valem: objetos com `IsMet()` ou funções. |
| `IsUnlocked(naCriacao, noCabeleireiro)` | Decide tudo sozinho (do ExMod), se escrito. |

Save: o arquivo do jogo leva o cabelo 0 e o de mod vai pelo nome no
`<personagem>.plr.bl.json` (`bunny:hair`); sem o mod, o personagem abre com
o cabelo 0. `Terraria.ID.HairID.Sets.DrawBackHair[this.Type] = true` no
`SetStaticDefaults()` permite desenhar o cabelo atrás do jogador. O
`RandomizedCharacterCreationGender` do tModLoader não existe no celular.

---

## ModCloud

Uma nuvem, como o `ModCloud` do tModLoader. Tipo depois do `CloudID.Count`.

| | |
|---|---|
| `Texture` | Padrão: ao lado do arquivo (`Content/Clouds/X.js` → `Content/Clouds/X.png`). |
| `RareCloud` | `true`: sorteada entre as raras (getter). |
| `SpawnChance()` | O peso: as comuns contra as 22 do jogo (peso 1 cada), as raras contra as 18 raras do jogo. |
| `OnSpawn(cloud)` | A nuvem nasceu (o `Cloud` do jogo: `spriteDir`, `scale`...). |

`CloudLoader.AddCloudFromTexture('Clouds/X', peso, rara)`, no `Mod.Load`,
cria uma nuvem só de textura. Todo PNG numa pasta `Clouds/` do mod sem nuvem
com o mesmo nome vira nuvem comum de peso 1 sozinho. O `Draw` do tModLoader
ainda não existe.

---

## ModEmoteBubble

> **Desligado por ora** (`EmoteBubbleLoader.Enabled = false`): no celular do
> usuário os emotes de mod não funcionaram. A classe é ignorada, sem tipo e sem
> hook; o texto abaixo vale quando voltar.

Um emote, como o `ModEmoteBubble` do tModLoader. Tipo depois dos 151 do jogo.
A textura tem os quadros de 34 x 28 lado a lado (dois, no padrão), só o
ícone: a bolha branca vem da folha do jogo. Durante cada método,
`this.EmoteBubble` é a bolha.

| | |
|---|---|
| `AddToCategory(EmoteID.Category.X)` | No menu de emotes (`General`, `RockPaperScissors`, `Items`, `BiomesAndEvents`, `Town`, `CrittersAndMonsters`, `Dangers`). No `SetStaticDefaults`. |
| `IsUnlocked()` | No menu só se liberado. |
| `OnSpawn()` | A bolha nasceu (`EmoteBubble.NewBubble`). |
| `UpdateFrame()` | `false`: a animação é do mod (`this.EmoteBubble.frame`, `frameCounter`). |
| `GetFrame()` | O quadro na textura; `null`, o do jogo (metade da textura). |
| `GetFrameInEmoteMenu(frame, frameCounter)` | O quadro no menu (a animação do menu tem dois). |
| `PreDraw(sb, texture, position, frame, origin, effects)`, `PostDraw(...)` | Em volta do desenho da bolha; `false` não desenha. |

Multijogador: o emote que o jogador manda é recusado pelo jogo acima de 150
(msg 120); o de mod não chega aos outros.

---

## ModAchievement

Uma conquista, como o `ModAchievement` do tModLoader, no gerenciador do jogo
(`Main.Achievements`), com o nome `<mod>/<Classe>`. O progresso fica no
arquivo de conquistas do jogo.

| | |
|---|---|
| `Texture` | Uma folha de 592 x 64: o ícone desbloqueado em x = 0 e o bloqueado em x = 528 (onde o menu do celular procura). A do tModLoader (130 x 64) não serve. |
| `Index` | A posição na folha (colunas de 66). |
| `Category` | `Terraria.Achievements.AchievementCategory.Slayer` (padrão), `Collector`, `Explorer`, `Challenger`. |
| `Hidden` | Nome e descrição viram "???" no menu enquanto não completa. |
| `SetStaticDefaults()` | As condições: `AddCondition(chave)`, `AddIntCondition(chave, máx)`, `AddFloatCondition`, `AddItemCraftCondition(id ou [ids])`, `AddItemPickupCondition`, `AddNPCKilledCondition`, `AddTileDestroyedCondition([ids])`. Devolvem a condição do jogo (`.Value` nas de número, `.Complete()`). |
| `OnCompleted(achievement)` | Completou. |
| `OnNPCKilled(player, npcId)`, `OnItemPickup(player, tipo, qtd)`, `OnItemCraft(tipo, qtd)`, `OnTileDestroyed(player, tile)` | Os avisos do jogo, enquanto não completa (do ExMod; o tModLoader usa os eventos do `AchievementsHelper`). |

Nome e descrição: `Achievements.<Classe>.FriendlyName` (ou `.Name`) e
`.Description` do `Localization`. As posições no menu (`GetDefaultPosition`)
do tModLoader não existem.

---

## ModTile

Um bloco novo (terra, pedra, minério) ou um objeto de várias células com
`TileObjectData` (móveis: pia, cadeira, porta, baú, cama...). Uma instância
por tipo; os métodos recebem a posição `(i, j)` em tiles, e os parâmetros
`ref` do tModLoader chegam como `Ref` (`.value`). A lista completa, com o que
cada móvel do Example Mod usa, está no [guia 9](../mods/09-blocos.md).

### Campos

| Campo | Para quê |
|---|---|
| `Type`, `Mod` | |
| `Texture`, `HighlightTexture` | A folha de quadros; o contorno (`HasOutlines`). |
| `DustType`, `HitSound` | Poeira e som ao bater. |
| `MinPick`, `MineResist` | Picareta mínima; o dano de cada golpe é dividido por ele. |
| `ItemDrop` | O item que cai; `undefined` = o item de mod que coloca o tile (pelo estilo). |
| `AdjTiles` | Estações de criação que ele também é. |
| `AnimationFrameHeight` | A altura de um quadro de animação. |
| `CacheDrawData` | `false`: `SetDrawPositions`/`AnimateIndividualTile`/`SetSpriteEffects` sem cache, em todo desenho (animação por outro contador que não o `Main.tileFrame` do tipo). |

### Métodos que você escreve, e onde entram

| Métodos | Por trás |
|---|---|
| `SetStaticDefaults`, `PostSetDefaults`, `PostSetupContent` | nativo, conteúdo pronto |
| `CanKillTile`, `KillTile`, `NumDust`, `CreateDust`, `KillSound`, `CanDrop`, `GetItemDrops`, `KillMultiTile` | `WorldGen.KillTile*`, `TileFrameImportant`; filtro de tile |
| `TileFrame` | `WorldGen.TileFrame`; marca `tile.frame` |
| `ModifyLight` | `Lighting.LightTiles` (a cada 3 quadros) + `Lighting.AddLight` em lote; marca `tile.light` |
| `AnimateTile` | `Main.AnimateTiles` (só se algum tile o usa) |
| `SetDrawPositions`, `AnimateIndividualTile`, `SetSpriteEffects` | o desenho nativo (`GetTileDrawData`); marca `tile.drawdata` |
| `PreDraw`, `PostDraw`, `DrawEffects`, `SpecialDraw`, `EmitParticles` | passada depois do `TileDrawing.PostDrawTiles`; marca `tile.draw` |
| `RightClick`, `MouseOver`, `MouseOverFar` | `Player.TileInteractions*`; filtro de tile |
| `HitWire` | `Wiring.HitWireSingle`; marca `tile.wire` |
| `RandomUpdate` | `WorldGen.UpdateWorld_*Tile`; marca `tile.random` |
| `NearbyEffects` | `SceneMetrics.Scan` |
| `PlaceInWorld` | `TileObjectData.CallPostPlacementPlayerHook` e `WorldGen.PlaceTile` (colocando) |
| `Slope` | `WorldGen.SlopeTile`/`PoundTile`; marca `tile.slope` |
| `ModifySittingTargetInfo`, `ModifySleepingTargetInfo` | `PlayerSittingHelper`/`PlayerSleepingHelper` |
| `IsLockedChest`, `UnlockChest`, `LockChest` | `Chest.IsLocked`/`Unlock`/`Lock`; marca `tile.chest` |

### Para chamar

| | |
|---|---|
| `AddMapEntry(cor, nome)` | A cor e o nome no mapa; cada chamada é uma opção a mais. |
| `RegisterItemDrop(item, ...estilos)` | O item que cai. |
| `CreateMapEntryName()` | O nome da classe. |
| `ModTile.register`, `getTypeByName`, `getModTile`, `isModType` | |
| `bl.tiles.typeAt(x, y)` | O tipo do tile ativo numa posição (-1 se não há). |
| `MusicLoader.AddMusicBox(mod, slot, item, tile)` | Caixa de música de mod. |

### Ainda não

`ModWall`, `ModTree`/`ModPalmTree`/`ModCactus`, `ModPylon`, o balanço ao vento
(`MultiTileVine`), `ModifyFrameMerge`/`PostTileFrame`, `SwitchTiles`,
`HasSmartInteract`, `GetTorchLuck`; NPC da vila não abre porta de mod.

---

## ModWall

A parede nova. Ver o [guia 9](../mods/09-blocos.md#paredes-modwall).

| | | |
|---|---|---|
| `Type`, `DustType`, `HitSound`, `ItemDrop` | Campos. Sem `ItemDrop`, o item de mod com `createWall` igual ao tipo. | |
| `SetStaticDefaults()` | Com o tipo já nas tabelas (`Main.wallHouse`, `WallID.Sets`). | na instalação |
| `KillWall(i, j, fail)`, `NumDust(i, j, fail, num)` | Ao bater ou quebrar (`fail`, `num`: `Ref`). | `WorldGen.KillWall`, filtro pela parede em `(i, j)` |
| `CreateDust(i, j, type)` | Cada poeira. | `WorldGen.KillWall_MakeWallDust` |
| `KillSound(i, j, fail)` | `false` cala. | `WorldGen.KillWall_PlaySounds` |
| `Drop(i, j, type)` | O item que cai; `false` nada. | `WorldGen.KillWall_DropItems`/`_GetItemDrops` |
| `ModifyLight(i, j, r, g, b)` | A luz da parede (`Ref`), perto da tela. | `Lighting.LightTiles` + `Lighting.AddLight`, marca `wall.light` |
| `AnimateWall(frame, frameCounter)` | O quadro (`Main.wallFrame`), por tipo, a cada quadro. | `Main.DoUpdate_AnimateWalls` |
| `RandomUpdate(i, j)` | Atualização aleatória. | `WorldGen.UpdateWorld_*Tile`, filtro pela parede, marca `wall.random` |
| `AddMapEntry(cor, nome)` | A cor e o nome no mapa. | `MapHelper.wallLookup` |
| `WallFrame(i, j, randomizeFrame, style, frameNumber)` | O quadro da moldura; `false` mantém o anterior. | `Framing.WallFrame`, filtro pela parede, marca `wall.frame` |
| `ModWall.isModType`, `getTypeByName`, `getModWall` | Como no tile. | |
| `item.DefaultToPlaceableWall(tipo)` (`ModItem`) | O item que coloca a parede. | |

---

## GlobalItem, GlobalNPC e GlobalProjectile

Código que roda para as entidades **do jogo** (e as de mod). Os hooks **não
têm filtro nativo**: todo item, NPC ou projétil que passa pelo método entra no
JS, e o `AppliesToEntity` escolhe lá dentro (ver o custo no
[guia 12](../mods/12-globais-e-mundo.md#o-custo)). Um método que nenhum Global
escreve não instala hook.

`X.register(Classe)` (na mão; exportar já registra) devolve o modelo (o de `ModContent.GetInstance`).

### Em todos

| Membro | Para quê |
|---|---|
| `AppliesToEntity(entidade, lateInstantiation)` | `false`: os métodos do Global não rodam para ela. `lateInstantiation` é `false` na amostra do `ModifyNPCLoot`. |
| `InstancePerEntity` | `true` (campo ou getter): cada entidade ganha a própria cópia, nascida no `SetDefaults`. |
| `Clone(from, to)` | Retorna uma cópia dos campos da instância global. No fluxo de itens, é chamado por `Item.Clone`. |
| `NewInstance(target)` | A cópia de uma entidade nova. Padrão: os campos do modelo. |
| `SetStaticDefaults()`, `AddRecipeGroups()`, `AddRecipes()`, `PostSetupContent()` | Uma vez, com o conteúdo pronto. |
| `this.Mod` | O `Mod` de quem registrou. |

Na entidade: `item.GetGlobalItem(Classe)` (ou `'Nome'`) devolve a instância
dela e lança se o Global não se aplica; `item.TryGetGlobalItem(Classe, ref)`
devolve `true`/`false`. `npc.GetGlobalNPC` e `proj.GetGlobalProjectile`, igual.

### GlobalItem

| Método | Quando roda | Método do jogo |
|---|---|---|
| `SetDefaults(item)` | O item nasce ou troca de tipo. | `Item.SetDefaults` |
| `CanUseItem(item, player)` | Antes de usar; `false` impede. | `Player.ItemCheck_CheckCanUse_Inner` |
| `UseItem(item, player)` | O uso começa; `true` como no `ModItem`. | `Player.ItemCheck_StartActualUse` |
| `UseStyle`, `HoldStyle(item, player, mountOffset, heldItemFrame)`, `HoldItem(item, player)` | A cada quadro com o item na mão. | `Player.ItemCheck_ApplyUseStyle`/`ApplyHoldStyle` |
| `ModifyWeaponDamage(item, player, damage)` | O dano da arma; devolva o novo. | `Player.GetWeaponDamage` |
| `CanShoot(item, player)` | Antes do tiro; `false` não atira. | `Player.ItemCheck_Shoot` |
| `ModifyShootStats(item, player, stats)` | `stats = { position, velocity, type, damage, knockBack }`. | `Projectile.NewProjectile` do tiro |
| `Shoot(item, player, position, velocity, type, damage, knockBack, source)` | `false`: o projétil do jogo não sai. | idem |
| `OnHitNPC(item, player, target, damageDone, knockBack, crit)` | Golpe corpo a corpo acertou. | `Player.ApplyNPCOnHitEffects` |
| `UpdateInventory(item, player)` | A cada quadro, para as 58 casas do inventário. | `Player.UpdateEquips` |
| `UpdateEquip(item, player)` | Equipado (acessório ou armadura). | `Player.ApplyEquipFunctional`, `GrantArmorBenefits` |
| `UpdateAccessory(item, player, vanity, hideVisual)` | Acessório equipado (também de vaidade). | `Player.ApplyEquipFunctional`/`ApplyEquipVanity` |
| `OnCraft(item, player, recipe)` | Criado numa receita. | `Main.CraftItem_GrantItem` |
| `ModifyTooltips(item, tooltips)` | O tooltip; depois do `ModItem`. | `Main.MouseText_DrawItemTooltip` (porte em JS) |
| `PreDrawTooltip`, `PostDrawTooltip`, `PreDrawTooltipLine`, `PostDrawTooltipLine` | Os do `ModItem`, com `item` na frente, para qualquer item; depois do `ModItem`. | idem |
| `IsArmorSet(head, body, legs)` → nome, `UpdateArmorSet(player, nome)` | Conjunto de qualquer item (`''` = nenhum). | `Player.UpdateArmorSets` |
| `IsVanitySet(head, body, legs)` → nome, `PreUpdateVanitySet`/`UpdateVanitySet`/`ArmorSetShadows(player, nome)` | Vaidade pelos slots desenhados. | `Player.PlayerFrame`, `SetArmorEffectVisuals` |
| `SetMatch(armorSlot, type, male, equipSlot, robes)` | O slot desenhado de uma parte (0 cabeça, 1 corpo, 2 pernas). | `Player.SetMatch` |
| `VerticalWingSpeeds(item, player, ...)`, `HorizontalWingSpeeds(item, player, speed, acceleration)`, `WingUpdate(wings, player, inUse)` | Asas, também as do jogo. | `Player.WingMovement`, `WingAirLogicTweaks`, `WingFrame` |
| `ChoosePrefix(item, rand)`, `PrefixChance(item, pre, rand)`, `AllowPrefix(item, pre)`, `ApplyPrefix(item, pre)` | Os de prefixo do `ModItem`, para qualquer item (o global vem antes no `ChoosePrefix`). | `Item.RollAPrefix`, `Prefix`, `GetRollablePrefixes` |

### GlobalNPC

| Método | Quando roda | Método do jogo |
|---|---|---|
| `SetDefaults(npc)` | O NPC nasce (também no cliente, ao chegar pela rede). | `NPC.SetDefaults` |
| `OnSpawn(npc, source)` | Criado por `NewNPC` (no servidor ou sozinho). | `NPC.NewNPC` |
| `ResetEffects(npc)` | Começo da atualização do NPC. | `NPC.UpdateNPC` |
| `PreAI(npc)`, `AI(npc)`, `PostAI(npc)` | A cada quadro. `PreAI` `false` pula a IA do jogo e o `AI` dos outros. | `NPC.AI` |
| `HitEffect(npc, hitDirection, damage)` | Levou golpe (sangue, gore). | `NPC.HitEffect` |
| `OnHitByItem(npc, player, item, damageDone, knockBack, crit)` | Golpe corpo a corpo. | `Player.ApplyNPCOnHitEffects` |
| `OnHitByProjectile(npc, projectile)` | Acertado por projétil. | `Projectile.StatusNPC` |
| `PreKill(npc)`, `OnKill(npc)` | A morte com drop, só no servidor ou sozinho. `PreKill` `false`: sem drop e sem `OnKill`. | `NPC.NPCLoot` |
| `GetChat(npc, chat)` | A fala; `chat` é um `Ref`. | `NPC.GetChat` |
| `NetSend(npc, writer)`, `NetReceive(npc, reader)` | Rede: junto com cada NPC que o servidor sincroniza. | `NetMessage.SendData` (23) |
| `ModifyNPCLoot(npc, npcLoot)` | Uma vez por tipo de NPC, com a amostra do jogo. | nativo (ao terminar de carregar) |
| `ModifyGlobalLoot(globalLoot)` | Uma vez. | idem |
| `EditSpawnFlags(spawnInfo)` | Antes da taxa, da área e do ponto: os campos do jogador no `spawnInfo` (`noWorms`, `invaders`, `ZoneCorrupt`...). O ponto ainda não existe (`SpawnTileX` = -1). | `NPC.Spawner.SetSpawnFlags` |
| `EditSpawnRate(player, spawnRate, maxSpawns)` | A cada tentativa de spawn para o jogador. `Ref`: `spawnRate` menor = mais spawn; `maxSpawns`, quantos inimigos por perto. | `NPC.Spawner.GetSpawnRate` |
| `EditSpawnRange(player, spawnRangeX, spawnRangeY, safeRangeX, safeRangeY)` | `Ref`, em blocos: até onde nasce e a distância mínima do jogador. | `NPC.Spawner.GetSpawnArea` |
| `EditSpawnInfo(spawnInfo)` | Com o ponto escolhido, antes do sorteio: os campos do `spawnInfo` (`waterTile`, `nearGranite`...) mudam o que o jogo e o `SpawnChance` leem. | `NPC.Spawner.SetSpawnFlagsForChosenTile` |
| `EditSpawnPool(pool, spawnInfo)` | O sorteio (`SpawnPool`): `pool[tipo] = peso`; o `0` é o spawn do jogo. | `NPC.Spawner.SpawnAnNPC` |
| `SpawnNPC(npc, tileX, tileY)` | Nasceu um NPC sorteado que não é o do jogo (`npc`, o índice). | idem |

O spawn natural só roda sozinho ou no servidor; o `player` e o
`spawnInfo.Player` são o jogador-alvo (no multijogador, não é o
`Main.LocalPlayer`).

```js
export class MaisInimigosNoBioma extends GlobalNPC {
    EditSpawnRate(player, spawnRate, maxSpawns) {
        if (player.InModBiome(MeuBioma)) {
            spawnRate.value = Math.floor(spawnRate.value * 0.5);   // o dobro de spawn
            maxSpawns.value = Math.floor(maxSpawns.value * 1.5);
        }
    }
    EditSpawnPool(pool, spawnInfo) {
        if (spawnInfo.Player.InModBiome(MeuBioma)) pool[0] = 0.25;   // menos inimigos do jogo
    }
}
```

### GlobalProjectile

| Método | Quando roda | Método do jogo |
|---|---|---|
| `SetDefaults(projectile)` | O projétil nasce (também no outro lado da rede). | `Projectile.SetDefaults` |
| `OnSpawn(projectile, source)` | Criado por `NewProjectile`, em quem criou. | `Projectile.NewProjectile` |
| `PreAI`, `AI`, `PostAI(projectile)` | A cada quadro. | `Projectile.AI` |
| `PreKill(projectile, timeLeft)` | Antes da morte. `false` remove o projétil sem o efeito nativo e encerra o fluxo antes de `OnKill`. | `Projectile.Kill` |
| `OnKill(projectile, timeLeft)` | Depois da morte nativa, quando `PreKill` não vetou. | `Projectile.Kill` |
| `OnHitNPC(projectile, target)` | Acertou um NPC. | `Projectile.StatusNPC` |
| `OnHitPlayer(projectile, target)` | Acertou um jogador. | `Projectile.StatusPlayer` |
| `PreDraw(projectile, lightColor)` | Cor em `Ref<Color>`; `false` cancela o sprite. Todos os Globais rodam antes do método local, mantendo extras como correntes. | `Main.DrawProjDirect` |
| `PostDraw(projectile, lightColor)` | Cor direta; roda depois do método local, mesmo com veto no desenho. | `Main.DrawProjDirect` |
| `GetAlpha(projectile, lightColor)` | Primeiro `Color` global decide; `undefined`/`null` consulta o método local e o jogo. | `Projectile.GetAlpha` |
| `Colliding(projectile, projHitbox, targetHitbox)` | Primeiro booleano global decide; `undefined`/`null` consulta o método local e o jogo. | `Projectile.Colliding`, `Projectile.Damage` |
| `CanDamage(projectile)` | `false` global veta; `true` global prevalece sobre o método local. Condições nativas continuam valendo. | `Projectile.Damage` |
| `ModifyDamageHitbox(projectile, hitbox)` | `Ref<Rectangle>`, depois do método local. | `Projectile.Damage_GetHitbox` |
| `OnTileCollide(projectile, oldVelocity)` | `false` impede morte por choque; tempo e NPC continuam chamando `OnKill`. Todos os Globais rodam antes do método local. | `Projectile.HandleMovement`, `UpdatePosition`, `Kill` |
| `TileCollideStyle(projectile, width, height, fallThrough, hitboxCenterFrac)` | Quatro `Ref`, depois do método local; primeiro `false` pula colisão neste movimento. | `Projectile.HandleMovement`, `GetCollisionParams` |
| `MinionContactDamage(projectile)` | `true` permite dano por contato de pet. | `Projectile.Damage` |
| `CanCutTiles(projectile)` | Primeiro booleano global decide; `false` impede corte padrão e personalizado. | `Projectile.CanCutTiles` |
| `CutTiles(projectile)` | Globais, método local e corte padrão, quando permitido. | `Projectile.CutTiles` |
| `CanUseGrapple(type, player)` | No template, filtrado por amostra do tipo; última resposta booleana global decide. | `Player.FireGrapple` |
| `UseGrapple(player, type)` | Tipo em `Ref<number>`, depois do método local, para todos os templates que implementam o método. | `Player.FireGrapple` |
| `GrappleCanLatchOnTo(projectile, player, tile)` | Recebe o bloco nativo; `false` global veta, `true` global permite. | `Projectile.AI_007_GrapplingHooks_CanTileBeLatchedOnTo` |
| `NetSend(projectile, writer)`, `NetReceive(projectile, reader)` | Rede: junto com cada projétil sincronizado, de quem o controla. | `NetMessage.SendData` (27) |

O [catálogo](metodos.md#globalprojectile) inclui as assinaturas completas e
os métodos herdados de `GlobalType`. A
[validação de GlobalProjectile](../../tools/tests/globalprojectilehooks/RESULTADOS.md)
registra 59 verificações JavaScript e 38 verificações no jogo singleplayer,
sem falhas. Os retornos opcionais, a ordem com `ModProjectile` e os quatro
parâmetros de `TileCollideStyle` estão detalhados no
[contrato de testes](../../tools/tests/globalprojectilehooks/README.md).

### Ainda não

`NetSend`/`NetReceive` do `GlobalItem`, `SaveData`/`LoadData` por entidade,
`GlobalTile`, `GlobalBuff`; no `GlobalNPC`, `UpdateLifeRegen`,
`ModifyActiveShop`, `ModifyHitPlayer`/`OnHitPlayer`.

---

## GlobalLoot

As regras de drop que valem para **todo** NPC (o `_globalEntries` do jogo),
e o que o `ModifyGlobalLoot` recebe.

| Membro | Para quê |
|---|---|
| `Add(regra)`, `Remove(regra)` | Pôr ou tirar uma regra global. |
| `Get()` | As regras globais, num array. |
| `RemoveWhere(regra => ...)` | Tira as que casam; devolve quantas. |

Do jeito do ExMod: `class X extends GlobalLoot { ModifyGlobalLoot() {...} }` e
`GlobalLoot.register(X)`. Dentro, `this.RegisterToNPC(id, regra)`,
`this.RemoveFromNPC(id, regra)` e `this.GetRulesForNPCID(id)` (a `List` do jogo)
mexem na tabela de um NPC, e o Bestiário acompanha.

O `NPCLoot` (o do `ModifyNPCLoot`) tem `Add`, `Remove`, `Get()` e
`RemoveWhere`, igual.

---

## ModSystem

O que é do mod inteiro: receitas, o ciclo do mundo e os dados salvos nele.
Cada método de mundo só ganha hook se algum `ModSystem` o escreveu.

| Método | Quando roda | Método do jogo |
|---|---|---|
| `OnModLoad()` | No `register`. | — |
| `AddRecipeGroups()` | Uma vez, antes de qualquer receita. | nativo |
| `AddRecipes()`, `PostAddRecipes()` | Uma vez, com as receitas do jogo prontas. | nativo |
| `PostSetupContent()` | Uma vez, com todo o conteúdo de mod no jogo. | nativo |
| `ClearWorld()` | Ao entrar em qualquer mundo (e antes de gerar um). | `WorldGen.clearWorld` |
| `OnWorldLoad()` | O mundo abriu; no cliente, ao chegar do servidor. | `WorldFile.LoadWorld`; no cliente, `WorldGen.clearWorld` |
| `LoadWorldData(tag)` | Depois do `OnWorldLoad`, com os dados do mundo. Só no servidor ou sozinho. | idem |
| `PostWorldLoad()` | Depois dos dados. | idem |
| `SaveWorldData(tag)` | A cada save do mundo. Só no servidor ou sozinho. | `WorldFile.InternalSaveWorld` |
| `PreSaveAndQuit()` | Ao sair, antes de salvar. | `WorldGen.SaveAndQuit` |
| `OnWorldUnload()` | Depois de sair. | `WorldGen.SaveAndQuitCallBack` |
| `PreUpdateWorld()`, `PostUpdateWorld()` | A cada quadro. Só no servidor ou sozinho. | `WorldGen.UpdateWorld` |
| `PreUpdateTime()`, `PostUpdateTime()` | A cada quadro. Só no servidor ou sozinho. | `Main.UpdateTime` |
| `PostUpdateEverything()` | A cada quadro, em todos. | `Main.DoUpdateInWorld` |
| `ResetNearbyTileEffects()` | Antes de o jogo contar os blocos em volta do jogador local (a cada 5 quadros), e também ao sair do mundo e ao carregar outro (a contagem do anterior não vale no novo). | `SceneMetrics.Reset`, `WorldGen.SaveAndQuit`, `WorldGen.clearWorld` |
| `TileCountsAvailable(tileCounts)` | Com a contagem pronta: `tileCounts[tipo]` é quantos blocos daquele tipo há em volta. Vale durante a chamada: guarde o número, não o array. Só a varredura do jogador local (a dos pilares e a da câmera não chamam). | `SceneMetrics.AggregateTileCounts` |
| `NetSend(writer)`, `NetReceive(reader)` | Rede: o servidor manda junto com os dados do mundo (ao entrar e a cada sincronização); o cliente lê. | `NetMessage.SendData` (7) |

Os dados vão para `<mundo>.wld.bl.json`, ao lado do `.wld`, uma entrada por
`ModSystem` (a chave é o uid do mod e o nome da classe). `ModSystem.register(Classe)`
devolve a instância (a de `ModContent.GetInstance`).

**Ainda não**: `ModifyWorldGenTasks`, `ModifyInterfaceLayers`, e os `Pre/PostUpdate` de
jogadores, NPCs, projéteis e itens separados.

---

## ModBiome e ModSceneEffect

Um **bioma** diz, a cada quadro, se o jogador está nele. A contagem de blocos
em volta vem pelo `ModSystem.TileCountsAvailable`, como no tModLoader:

```js
export class ContagemDoBioma extends ModSystem {
    blocos = 0;
    TileCountsAvailable(tileCounts) {
        this.blocos = tileCounts[ModContent.TileType('ExampleTile')];
    }
}

export class MeuBioma extends ModBiome {
    SetStaticDefaults() {
        this.Music = MusicLoader.GetMusicSlot('Music/MinhaMusica');
    }
    IsBiomeActive(player) {
        return ModContent.GetInstance(ContagemDoBioma).blocos >= 40 && player.ZoneOverworldHeight;
    }
    OnEnter(player) {}
}
```

| `ModBiome` | |
|---|---|
| `IsBiomeActive(player)` | A cada quadro, depois de o jogo atualizar as zonas dele. Um erro vale `false` nesta avaliação. |
| `OnEnter(player)`, `OnLeave(player)` | Uma vez, na troca. Ao sair do mundo dentro do bioma, o `OnLeave` vem na saída. |
| `OnInBiome(player)` | A cada quadro dentro, inclusive no da entrada. |
| `player.InModBiome(Classe)` | Se o jogador está no bioma. Aceita a classe, a instância (`ModContent.GetInstance`) ou o `Type`. |
| `Type` | A posição entre os biomas (a mesma classe em dois mods são dois biomas). |
| `DisplayName`, `TownNPCDialogueName` | Os nomes, como no tModLoader: `Biomes.<Classe>.DisplayName` e `.TownNPCDialogueName` no `Localization/<cultura>.json` do mod (chaves `Mods.<id>.Biomes.<Classe>...`). Sem eles, o nome da classe separado ("My Biome") e "the My Biome". Um `DisplayName` escrito na classe (texto ou `{ cultura: texto }`) ganha do arquivo. Depois do registro, os dois são `{ Key, Value }`. |
| `BestiaryIcon`, `BackgroundPath` | Caminhos no mod: o da classe + `_Icon` (30 x 30, o ícone do filtro) e + `_Background` (115 x 65, o fundo do retrato no Bestiário), como no tModLoader. Sem o arquivo, ou em outro tamanho, os do jogo. O fundo do mapa pode reusar o `BackgroundPath`. |
| `BackgroundColor` | A cor do fundo do retrato no Bestiário (`Color`), ou `null`. |
| No Bestiário | O bioma aparece na entrada de cada NPC com ele no `SpawnModBiomes`, e como filtro (se algum NPC o usa). |
| Na felicidade | `this.Happiness.SetBiomeAffection(MeuBioma, AffectionLevel.Love)` num morador: o preço e a fala dele mudam com o jogador no bioma (a fala usa o `TownNPCDialogueName`). |
| Padrões | `Priority` `BiomeLow` e `Music` `0` (silêncio), como no tModLoader: sem música própria, declare `Music = -1`. |

Um **efeito de cena** (`ModSceneEffect`) é o mesmo sem as flags:
`IsSceneEffectActive(player)` diz se está ativo. Dos efeitos ativos (os biomas
também), o de maior `Priority` + `GetWeight(player)` (0 a 1) dá a música da
cena, em `player.CurrentSceneEffect.music`. `SpecialVisuals(player, isActive)`
roda a cada quadro para todos, ativo ou não.

`Music` e `Priority` podem vir como campo (`this.Music = ...` no
`SetStaticDefaults`) ou como getter (`get Music() { ... }`, como no tModLoader).

A música da cena toca pelas regras de [som e música](#som-e-música).

O jogo avalia os biomas só do jogador local, a cada quadro (também morto). O
teleporte e o renascimento reavaliam na hora. No **multijogador**, cada aparelho
manda as flags do próprio jogador quando mudam, e quem entra recebe as de todos.
Assim, `InModBiome` de outro jogador vale em qualquer aparelho, inclusive no
servidor, onde roda o spawn (`SpawnChance` com `info.Player.InModBiome(...)`). Para
um jogador remoto não há `OnEnter`/`OnInBiome`/`OnLeave`, como no tModLoader.

O efeito de cena também escolhe o fundo e a água: `SurfaceBackgroundStyle`,
`UndergroundBackgroundStyle` e `WaterStyle` devolvem a instância do estilo (ver
[fundos de mod](#fundos-de-mod) e [água de mod](#água-de-mod)), ou `null`.

E o fundo do **mapa em tela cheia**, como no tModLoader:

```js
export class MeuBioma extends ModBiome {
    get MapBackground() { return this.BackgroundPath; }   // ou 'Biomes/MeuFundoDoMapa'
    // ...
}
```

| `ModSceneEffect` (mapa) | |
|---|---|
| `MapBackground` | O caminho de uma textura do mod, ou `null`. Cobre a tela do mapa no lugar do fundo do jogo. O caminho é lido a cada desenho (pode mudar); a textura carrega na primeira vez. |
| `MapBackgroundFullbright` | `true`: sempre branco. O padrão (`false`) é a cor do céu com a tela do mapa na superfície e branco abaixo dela. |
| `MapBackgroundColor(color)` | `Ref` (`.value`, uma `Color`): a cor final, depois das duas acima. |

**Ainda não**: a tocha e a fogueira do bioma (`BiomeTorchItemType`, `BiomeCampfireItemType`).

### Fundos de mod

Como no tModLoader. Todo PNG numa pasta `Backgrounds/` do mod (no Example Mod, `Assets/Textures/Backgrounds`) ganha um número
de textura depois dos 344 do jogo (`TextureAssets.Background`,
`Main.backgroundWidth`/`Height` crescem):

| `BackgroundTextureLoader` | |
|---|---|
| `GetBackgroundSlot(this.Mod, 'Assets/Textures/Backgrounds/Nome')` | O número. Também `GetBackgroundSlot('Assets/...')` (o mod de quem chama) e `GetBackgroundSlot('examplemod/Assets/...')` (o id ou o nome da classe do mod na frente). Lança se não existe. |
| `TryGetBackgroundSlot(caminho, ref)` | `true` e o número em `ref.value`, ou `false`. |
| `AddBackgroundTexture(mod, caminho)` | Um PNG fora de uma pasta `Backgrounds/`; só na carga do mod. |

```js
export class MeuFundo extends ModSurfaceBackgroundStyle {
    ChooseFarTexture() { return BackgroundTextureLoader.GetBackgroundSlot(this.Mod, 'Assets/Textures/Backgrounds/Longe'); }
    ChooseMiddleTexture() { return BackgroundTextureLoader.GetBackgroundSlot(this.Mod, 'Assets/Textures/Backgrounds/Meio'); }
    ChooseCloseTexture(scale, parallax, a, b) { return BackgroundTextureLoader.GetBackgroundSlot(this.Mod, 'Assets/Textures/Backgrounds/Perto'); }
}

export class MeuFundoDeBaixo extends ModUndergroundBackgroundStyle {
    FillTextureArray(slots) {
        for (let i = 0; i < 4; i++) slots[i] = BackgroundTextureLoader.GetBackgroundSlot(this.Mod, 'Assets/Textures/Backgrounds/Caverna' + i);
    }
}

export class MeuBioma extends ModBiome {
    get SurfaceBackgroundStyle() { return ModContent.GetInstance(MeuFundo); }
    get UndergroundBackgroundStyle() { return ModContent.GetInstance(MeuFundoDeBaixo); }
    // ...
}
```

| `ModSurfaceBackgroundStyle` | |
|---|---|
| `Slot` | O número do estilo, depois dos 16 do jogo (`Main.bgStyle` vira ele). |
| `ChooseFarTexture()`, `ChooseMiddleTexture()` | A textura de longe (atrás das montanhas do meio) e a do meio (atrás das árvores do fundo), ou `-1`. A cada quadro: dá para animar. |
| `ChooseCloseTexture(scale, parallax, a, b)` | A da frente, ou `-1`. Os quatro são `Ref` (`.value`): escala 1.25, parallax 0.37, altura `a` 1800 e `b` 1750, como no tModLoader. |
| `PreDrawCloseBackground(spriteBatch)` | `false` não desenha a da frente. |
| `ModifyFarFades(fades, transitionSpeed)` | A transparência de cada estilo (`fades[i]`), a cada quadro com este estilo na tela. O padrão sobe a deste e desce as outras. |

| `ModUndergroundBackgroundStyle` | |
|---|---|
| `Slot` | O número do estilo, depois dos 22 do jogo. |
| `FillTextureArray(slots)` | `slots[0]` a borda com o céu (160x16), `[1]` a terra (160x96), `[2]` a borda da terra com a pedra (160x16), `[3]` a pedra (160x96). `[4]`, a passagem para o inferno, vem com a da caverna comum. |

O fundo entra pela `Priority` da cena, nos degraus do tModLoader. Na superfície,
`BiomeLow` troca só a floresta, `BiomeMedium` também a selva e a neve, e
`BiomeHigh` troca tudo. No subsolo, `BiomeLow` troca só a caverna comum,
`BiomeMedium` também a neve e a selva, e `BiomeHigh` troca tudo. A troca
tem a transição do jogo, e ao sair da cena o fundo volta ao do jogo.

Com o fundo desligado nas opções, o subsolo usa o desenho antigo do jogo e fica
com o estilo do jogo. O fundo da tela de título vem do [`ModMenu`](#modmenu).
**Ainda não**: `GlobalBackgroundStyle`.

### Água de mod

Como no tModLoader. A textura vem do caminho do arquivo da classe
(`Content/Biomes/MinhaAgua.js` -> `Content/Biomes/MinhaAgua.png`, a
superfície da água, 48x1360 como a do jogo), mais `MinhaAgua_Block.png` (o
bloco, 306x16) e `MinhaAgua_Slope.png` (a rampa, 72x16; sem ela, o bloco). A
cachoeira é um PNG de 512x40 com o nome da classe dela.

```js
export class MinhaCachoeira extends ModWaterfallStyle {
    AddLight(i, j) { Terraria.Lighting['void AddLight(int i, int j, float r, float g, float b)'](i, j, 0.5, 0.5, 0.5); }
}

export class MinhaAgua extends ModWaterStyle {
    ChooseWaterfallStyle() { return ModContent.GetInstance(MinhaCachoeira).Slot; }
    GetSplashDust() { return Terraria.ID.DustID.BlueCrystalShard; }
    GetDropletGore() { return ModGore.getTypeByName('MinhaGota'); }
}

export class MeuBioma extends ModBiome {
    get WaterStyle() { return ModContent.GetInstance(MinhaAgua); }
    // ...
}
```

| `ModWaterStyle` | |
|---|---|
| `Slot` | O número do estilo, depois dos 15 do jogo (`Main.waterStyle` vira ele). |
| `ChooseWaterfallStyle()` | O `Slot` de uma `ModWaterfallStyle`, ou uma cachoeira do jogo (0 é a da floresta). |
| `GetSplashDust()` | O pó do respingo (quem cai na água). Padrão: o do jogo. |
| `GetDropletGore()` | A gota que pinga do bloco. Uma de mod (numa pasta `Gores/`) se comporta como a gota d'água do jogo. |
| `LightColorMultiplier(r, g, b)` | `Ref` (`.value`): quanto da luz atravessa a água. `1, 1, 1` não perde luz. |
| `GetRainVariant()` | A variante da chuva: a coluna da textura ÷ 4 (na do jogo, 0 a 2 é a da floresta). O padrão é o sorteio de 0 a 2 do jogo, sem custo; sobrescrever custa uma entrada no JS por gota nova. |
| `GetRainTexture()` | A textura da chuva: o caminho de um PNG no mod (colunas de 4 px, 40 de altura, como a do jogo), ou `null` (a do jogo, o padrão). |
| `BiomeHairColor()` | A cor da tintura de bioma no cabelo com esta água (uma `Color`). Padrão: a da floresta. |

| `ModWaterfallStyle` | |
|---|---|
| `Slot` | O número, depois dos 28 do jogo. |
| `AddLight(i, j)` | Luz na cachoeira, de 3 em 3 tiles da queda (o celular embute a luz e a cor das cachoeiras do jogo no desenho; a de mod roda depois dele). |
| `ColorMultiplier(r, g, b, a)` | A cor de cada pedaço: `r`, `g`, `b` são `Ref` (`.value`, 0 a 255: a luz do lugar vezes a opacidade) e `a`, a opacidade. Para cores que mudam com o tempo. |

A água entra com `Priority` `BiomeLow` ou maior e só onde o jogo usaria a do
fundo comum, como no tModLoader: uma fonte de água ligada, a lua de sangue e os
fundos de bioma do jogo (selva, deserto, neve...) ganham. A troca tem o fade do
jogo, e ao sair a água volta à do jogo.

---

## ModMenu

Um tema da tela de título, como no tModLoader: o logo, o sol, a lua e o fundo
enquanto o jogo está nos menus. Não há troca de tema: vale o do mod carregado
por último (com `IsAvailable` falso, o anterior); sem nenhum, o título do jogo.
A música dos menus é sempre a do jogo: o `Music` do tema é aceito, mas não toca.

```js
export class MeuTema extends ModMenu {
    get SunTexture() { return 'Assets/Textures/Menu/MeuSol'; }
    get MoonTexture() { return 'Assets/Textures/Menu/MinhaLua'; }
    get MenuBackgroundStyle() { return ModContent.GetInstance(MeuFundo); }
    PreDrawLogo(spriteBatch, logoDrawCenter, logoRotation, logoScale, drawColor) {
        drawColor.value = Color.new(255, 120, 120, 255);
        return true;
    }
}
```

| `ModMenu` | |
|---|---|
| `Logo` | A textura do logo: o caminho no mod (`'Assets/Textures/Menu/Logo'`, ou `'outromod/...'`) ou um `Asset` (`ModContent.Request`). `null` (o padrão): o logo do jogo, o de dia e o de noite. |
| `SunTexture`, `MoonTexture` | O sol e a lua, do mesmo jeito. A lua é um quadro só (sem as fases). `null`: os do jogo. |
| `Music` | Aceito (o código do tModLoader roda), sem efeito: a música dos menus é a do jogo. |
| `MenuBackgroundStyle` | Um `ModSurfaceBackgroundStyle` (`ModContent.GetInstance(Classe)`), ou `null`: o fundo do jogo. |
| `DisplayName` | Aceito, sem efeito (não há rodapé de troca). |
| `IsAvailable` | `false` tira o tema: vale o do mod anterior. Pode ser `get IsAvailable()`, para um tema que só vale em certas datas. |
| `IsSelected` | Se é o tema que vale agora (só leitura). |
| `OnSelected()`, `OnDeselected()` | Quando o tema passa a valer (na abertura do jogo, já nos menus) e quando deixa de valer. |
| `Update(isOnTitleScreen)` | Todo quadro nos menus; `isOnTitleScreen` na tela de título (`menuMode` 0). |
| `PreDrawLogo(spriteBatch, logoDrawCenter, logoRotation, logoScale, drawColor)` | Antes do logo. Os quatro últimos são `Ref` (`.value`): o centro, a rotação, a escala e a cor do logo do jogo neste quadro. `false` não desenha o logo. |
| `PostDrawLogo(spriteBatch, logoDrawCenter, logoRotation, logoScale, drawColor)` | Depois do logo, com os valores com que ele foi desenhado. |

Os valores podem ser campos (`this.Logo = ...`) ou `get` (como no
tModLoader), e são lidos a cada quadro. O fundo do tema fica de fora na geração
de mundo com semente especial, como no tModLoader. No celular, o
logo, o sol e a lua do tema só entram no desenho do título; no mundo fica tudo
como é no jogo. **Diferenças do tModLoader**: sem o logo do tModLoader, o
padrão é o do jogo, e não há os temas prontos do jogo antigo (1.3.5.3, Bigger
and Boulder). Também não há o `UserInterface` do tema.

---

## TagCompound

Os dados que um mod salva: o `tag` do `SaveWorldData`/`LoadWorldData` e do
`SaveData`/`LoadData` do `ModPlayer`. Escreva como num objeto
(`tag.chave = valor`); vai para o disco em JSON.

| Membro | Para quê |
|---|---|
| `ContainsKey(chave)` | A chave foi salva? |
| `Get(chave, padrão)` | O valor, ou o padrão. |
| `GetBool`, `GetInt`, `GetFloat`, `GetString`, `GetList`, `GetCompound(chave)` | O valor já convertido (falso, 0, `''`, `[]` se não há). |
| `Set(chave, v)`, `Add(chave, v)`, `Remove(chave)` | Escrever e apagar. |
| `Count` | Quantas chaves. |
| `TagCompound.from(objeto)` | Um `TagCompound` com os campos do objeto. |

---

## Rede

`writer` e `reader` dos `NetSend`/`NetReceive`, e o pacote do `Mod`. Ver o
[guia 12](../mods/12-globais-e-mundo.md#rede).

| Membro | Para quê |
|---|---|
| `writer.Write(valor)` | Guarda número, texto, booleano, array, objeto simples ou `Vector2`. Os nomes com tipo (`WriteInt32`...) fazem o mesmo. |
| `writer.WriteFlags(a, b, ...)`, `writer.WriteVector2(v)` | Vários booleanos; um vetor. |
| `reader.Read()` | O próximo valor, como foi escrito. |
| `reader.ReadInt32()`, `ReadByte`, `ReadSingle`, `ReadDouble`, `ReadBoolean`, `ReadString` | O próximo, convertido. |
| `reader.ReadFlags()`, `reader.ReadVector2()` | Um array de booleanos; um `Vector2`. |
| `reader.HasMore` | Ainda há valores? |
| `mod.GetPacket()` | Um `ModPacket` (um `writer` com `Send`). |
| `packet.Send(toClient = -1, ignoreClient = -1)` | Do cliente: ao servidor. Do servidor: a um cliente, ou a todos menos um. |
| `HandlePacket(reader, whoAmI)` | No `Mod`: o pacote do mesmo mod, do outro lado. `whoAmI`: o cliente, ou `256` (o servidor). |

No `ModNPC` e no `ModProjectile`: `SendExtraAI(writer)` e
`ReceiveExtraAI(reader)`, junto com a sincronização da entidade.

---

## Mod e ModLoader

`Mod` é o mod em si: a classe do `export default` do arquivo de entrada, obrigatória e uma por pacote. `ModLoader` acha os outros.

| `Mod` | |
|---|---|
| `id` | O `id` do manifesto (o `Name` do tModLoader). |
| `uuid`, `name`, `version` | Do manifesto. |
| `path`, `root` | A pasta do `main.js` (`content/`) e a do pacote. |
| `dataDirectory` | `Android/data/com.bunnyloader/mod_data/<uid>`. |
| `Load()` | Na carga, depois do registro do conteúdo. |
| `AddRecipeGroups()`, `AddRecipes()`, `PostSetupContent()` | Como no `ModSystem`. |
| `GetContent(Base)` | Os modelos deste mod que estendem `Base`, na ordem do registro: `this.GetContent(ModItem).map((i) => i.Type)` dá os tipos de todos os itens. Não vale para `ModPlayer` (um por jogador: `player.GetModPlayer`). |
| `Call(...args)` | Quando outro mod chama. |
| `export default class X extends Mod` | Obrigatória, no arquivo de entrada; uma por pacote. O Bunny Loader a cria (é o `bl.mod` do pacote). |

| `ModLoader` | |
|---|---|
| `TryGetMod(id, ref)` | `true` e o `Mod` em `ref.value`; ou `false` e `null`. |
| `GetMod(id)` | O `Mod`; lança se não está. |
| `HasMod(id)` | Está instalado (e carregou)? |
| `Mods` | Todos, na ordem de carga. |

`bl.mod` é o `Mod` de quem pergunta.

---

## ModContent

O `ModContent` do tModLoader: o tipo, o modelo e as texturas de um conteúdo de
mod, pela **classe**, pelo **nome** ou por `'mod/Nome'`.

| | |
|---|---|
| `ItemType(x)`, `ProjectileType(x)`, `NPCType(x)`, `BuffType(x)`, `TileType(x)`, `PrefixType(x)`, `MountType(x)`, `RarityType(x)` | O tipo. `x` é a classe, o nome (`'ExampleBobber'`) ou `'outromod/Nome'`. Não achou: 0. |
| `GetInstance(Classe)` | O modelo (a instância do `register`). |
| `GetContent(Base)` | Os modelos de todos os mods que estendem `Base` (`ModItem`, `ModNPC`, `ModBuff`, `ModSystem`...), na ordem do registro. |
| `Find(ModItem, 'mod/Nome')` | O modelo pelo nome; lança se não há. |
| `TryFind(ModItem, 'mod/Nome', ref)` | O mesmo, no `ref.value`; devolve `true`/`false`. |
| `GetModItem(tipo)`, `GetModProjectile(tipo)`, `GetModNPC(tipo)`, `GetModBuff(tipo)`, `GetModTile(tipo)`, `GetModPrefix(tipo)`, `GetModMount(tipo)`, `GetModRarity(tipo)` | O modelo pelo tipo. |
| `Request(caminho)` | `Asset<Texture2D>` do jogo, carregado uma vez (`.Value` é a textura). Thread do jogo. |
| `Texture(caminho)` | A `Texture2D` (o `.Value` do `Request`). |
| `HasAsset(caminho)` | A textura existe? |
| `SoundStyle(caminho, opções)` | O mesmo que `new SoundStyle(...)`. |

Pelo nome puro vale o do seu mod; se não houver, o único mod que tem um
conteúdo com esse nome (dois: peça por `'mod/Nome'`).

---

## ModRecipe

| | |
|---|---|
| `new ModRecipe()` | Uma receita (dentro do `AddRecipes`). |
| `SetResult(tipo, n)` | O que ela dá. |
| `AddIngredient(tipo, n)` | Até 15. |
| `AddRecipeGroup(grupo, n)` | Aceita qualquer item do grupo (objeto, nome do jogo como `'IronBar'`, ou um criado por mod). |
| `AddTile(tipo)` | A estação (uma por receita). |
| `SetProperty(nome, valor)` | `needWater`, `needLava`, `needHoney`, `needSnowBiome`, `needGraveyardBiome`, `needTorchGodsFavor`, `needMechdusa`, `notDecraftable`, `crimson`, `corruption`, `alchemy`. |
| `AddCustomShimmerResult(tipo, n)` | O que sai no Brilho. |
| `Register()` | Põe no jogo. |
| `ModRecipe.CreateRecipeGroup(nome, tipos)` | Grupo novo (no `AddRecipeGroups`). |
| `ModRecipe.GetGroup(x)` | Um grupo pelo objeto, nome ou número. |

**Ainda não**: `AddCondition` (condições do tModLoader) e estação de mod.

---

## Ajudantes de NPC

| Classe | |
|---|---|
| `NPCLoot` | Recebido no `ModifyNPCLoot`: `npcLoot.Add(regra)`, com as regras do jogo (`ItemDropRule...`). |
| `ItemLoot` | Recebido no `ModifyItemLoot` do `ModItem`: `itemLoot.Add(regra)`, `Remove`, `Get()`, `RemoveWhere`. Guardado pelo Bunny Loader (o jogo não tem drops por item). |
| `NPCSpawnInfo` | Recebido no `SpawnChance`, no `EditSpawnPool` e no `EditSpawnInfo`, como o `NPC.Spawner` do tModLoader: `SpawnTileX`, `SpawnTileY`, `GroundTileY`, `SpawnTileType` e `SpawnWallType` (o bloco e a parede do chão), `SafeRangeX`, `Player` (o jogador-alvo), `Spawner` (o do jogo) e os campos dele, que se leem e escrevem (`waterTile`, `nearGranite`, `nearMarble`, `spawnSpider`, `ZoneCorrupt`...); altura (`Sky`, `Surface`, `Underground`, `Cavern`, `Underworld`, `AboveSurface`, `BelowSurface`); hora e evento (`Day`, `Night`, `Rain`, `SlimeRain`, `BloodMoon`, `SolarEclipse`, `PumpkinMoon`, `FrostMoon`, `AnyEvent`, `Invasion`, `AnyTower`); mundo (`HardMode`, `Expert`, `Master`); bioma (`Corruption`, `Crimson`, `Hallow` e os `Underground...`, `Snow`, `Ice`, `Jungle`, `UndergroundJungle`, `Mushroom`, `SurfaceMushroom`, `Ocean`, `Desert`, `DesertCave`, `Meteor`, `Marble`, `Granite`, `Graveyard`, `Dungeon`, `Lihzahrd`); `CommonEnemy`. |
| `SpawnPool` | O sorteio do `EditSpawnPool`: `pool[tipo] = peso`, `delete pool[tipo]`, ou `Add`, `Remove`, `ContainsKey`, `Clear`, `Keys`, `Count`. O `0` é o spawn do jogo (peso 1). Com o total 0, nada nasce. |
| `NPCShop` | `new NPCShop(tipoDoNPC, 'Shop').Add(item, { condition, price, currency }).Register()`; `NPCShop.get(tipo, nome)`, `shop.Open()`. |
| `NPCHappiness` | `this.Happiness.SetNPCAffection(npc, nivel)`, `.SetBiomeAffection('Desert', nivel)` (do jogo) ou `.SetBiomeAffection(MeuBioma, nivel)` (a classe de um `ModBiome`), com `AffectionLevel.Love`, `Like`, `Dislike`, `Hate`. |
| `SpawnCondition` | As condições do spawn do jogo em objetos, como no tModLoader: `SpawnCondition.OverworldNightMonster.Chance` é a fatia (0 a 1) do sorteio do jogo que cairia ali no ponto do spawn atual; `.Active` diz se foi avaliada. Para o `SpawnChance`: `return SpawnCondition.OverworldDaySlime.Chance * 0.1`. Avaliadas uma vez por sorteio, só se lidas. |
| `ModGore` | `ModGore.getTypeByName('Nome')`: o gore `Nome.png` de uma pasta `Gores/` do mod. |

---

## TooltipLine

| | |
|---|---|
| `new TooltipLine(nome, texto)` | (também `new TooltipLine(mod, nome, texto)`, como no tModLoader). |
| `Mod`, `Name`, `Text` | O mod da linha (`'Terraria'` nas do jogo), o nome (os do tModLoader: `ItemName`, `Damage`, `Tooltip0`...) e o texto. |
| `OverrideColor` | Uma `Color` para a linha inteira. |
| `IsModifier`, `IsModifierBad` | Linha de prefixo: boa, ou ruim. |
| `OneDropLogo` | `true`: a linha é o logo da One Drop. |
| `TooltipLine.colorTag(texto, cor)` | `'[c/RRGGBB:texto]'` para pintar um trecho. |
| `Visible`, `Hide()` | `Hide()` tira a linha do tooltip. |

`DrawableTooltipLine` (nos `Pre/PostDrawTooltipLine` e no `PostDrawTooltip`): tudo da `TooltipLine`, mais

| | |
|---|---|
| `Index` | A posição da linha. |
| `X`, `Y`, `OriginalX`, `OriginalY` | Onde a linha é desenhada (mude `X`, `Y` no `PreDrawTooltipLine`). |
| `Color` | A cor final, com a transparência do tooltip. |
| `Font`, `Rotation`, `Origin` | Como o texto é desenhado. |
| `BaseScale` | A escala; já com a do tooltip do celular (`Settings.Tooltips.Scale`). |
| `Spread` | A distância das quatro sombras (2). |

---

## ModLocalization

| | |
|---|---|
Toda chave de `Localization/<cultura>.json` já está no jogo como
`Mods.<id do mod>.<Secao>.<Chave>` (qualquer profundidade), e segue a troca de idioma.

| | |
|---|---|
| `ModLocalization.Translate('Secao.Chave')` | O **texto** no idioma do jogo (como no TL); sem texto, o próprio caminho. |
| `ModLocalization.TryTranslate('Secao.Chave')` | O mesmo, com `''` quando não há texto. |
| (onde procura) | Nos `Localization/*.json` do mod de quem chama. Chamado de fora de um mod (o console do Editor), a chave completa `Mods.<id>.Secao.Chave` acha o mod dela, e a curta vale se só um mod a tem. `{0}` não é trocado: use `.replace('{0}', x)`. |
| `ModLocalization.GetTextValue(chave)` | Texto do mod ou, sem ele, o do jogo. |
| `ModLocalization.GetText(chave)` | O `LocalizedText` do mod ou do jogo. |
| `ModLocalization.Key('Secao.Chave')` | A **chave** `Mods.<id>.Secao.Chave` (o que o Bestiário e a moeda pedem). |
| `ModLocalization.Exists(chave)` | Se o mod ou o jogo tem o texto. |
| `ModLocalization.Register(chave, texto)` | Um texto (ou `{ cultura: texto }`) sob uma chave qualquer. |
| `ModLocalization.ActiveCultureName` | `'pt-BR'`, `'en-US'`... |

---

## Som e música

| | |
|---|---|
| `new SoundStyle(caminho, { Volume, Pitch, PitchVariance, MaxInstances, SoundLimitBehavior })` | Um som do mod, para `UseSound`, `HitSound`, `DeathSound`. |
| `SoundEngine.PlaySound(estilo, posição?)` | Toca agora; som de mod devolve o número (0 = não tocou). |
| `SoundEngine.FindActiveSound(estilo)`, `SoundEngine.StopSound(n)` | |
| `SoundLimitBehavior.ReplaceOldest`, `.IgnoreNew` | |
| `MusicLoader.GetMusicSlot(caminho)` (ou `(mod, caminho)`) | O número de uma música do mod (0 = não existe). |
| `MusicLoader.MusicExists(caminho)`, `MusicLoader.IsMusicPlaying(slot)`, `MusicLoader.MusicCount` | |
| `SceneEffectPriority.None` ... `BossHigh` | Prioridade da música do `ModNPC` e do `ModSceneEffect`/`ModBiome`. |

**Quem toca.** A caixa de música (de mod ou do jogo) ganha de tudo. Depois,
dos candidatos de mod (o `ModNPC` com `Music` perto da tela e a cena do jogador
local), o de maior prioridade; a cena só troca o do NPC se a dela for maior. Ele
ganha da música do jogo se a prioridade chegar ao degrau dela, como no
tModLoader:

| Degrau da música do jogo | Para ganhar dela |
|---|---|
| créditos | nada ganha |
| desafio da tocha, Senhor da Lua | `BossHigh` |
| Plantera, invasão marciana, pilares celestiais | `BossMedium` |
| os outros chefes | `BossLow` |
| eventos (piratas, goblins, Exército do Antigo, luas, pedra arco-íris) | `Event` |
| eclipse, chuva de slime, brilho, cidade, tempestade de areia, submundo, espaço | `Environment` |
| templo, masmorra, cogumelo, corrupção, carmesim | `BiomeHigh` |
| meteoro, cemitério, deserto, selva, neve | `BiomeMedium` |
| o resto (superfície, subterrâneo, oceano, chuva) | `BiomeLow` |

Vale também no Otherworld. `Priority` `None` não toca.

**Ainda não**: `Variants`, `IsLooped`.

---

## ModConfig

Opções por mod, declaradas em `static Options`. O registro automático cria
uma instância que pode ser obtida com `ModContent.GetInstance(Classe)`.
O [guia de opções](../mods/14-opcoes-do-mod.md) descreve o menu e a persistência.
As [assinaturas completas](metodos.md#modconfig) vêm da implementação.

| Método | Contrato |
|---|---|
| `OnLoaded()` | Depois de carregar a configuração. |
| `OnChanged(key)` | Quando uma opção é alterada na configuração aplicada. |
| `OnApply()` | Notificação da aplicação das opções. |
| `ResetToDefaults()` | Solicita ao loader a restauração dos valores padrão. |
| `SetOption(key, value)` | Altera uma opção pela API de configuração. |
| `ModConfig.Header(extra)`, `Toggle(defaultValue, extra)`, `Range(defaultValue, options)` | Descritores de título, interruptor e barra. |
| `ModConfig.Radio(defaultValue, choices, extra)`, `Dropdown(...)`, `Cycle(...)` | Descritores de seleção. |
| `ModConfig.Color(defaultValue, extra)`, `Button(action, extra)`, `Link(url, extra)` | Descritores de cor, ação e endereço. |
| `ModConfig.register(Classe)` | Registra manualmente uma classe de configuração. |

## DamageClass

Uma classe de dano define herança de bônus, efeitos e prefixos. Ela pode ser
usada em `item.DamageType` e `projectile.DamageType`. As instâncias nativas
adaptadas são acessadas por `DamageClass.Melee`, `Magic`, `Ranged`, `Summon`
e pelos demais getters listados no [catálogo](metodos.md#damageclass).

| Método | Contrato |
|---|---|
| `GetModifierInheritance(damageClass)` | Retorna `StatInheritanceData`; por padrão, herda todos os bônus de `Generic` e nenhum dos demais. |
| `GetEffectInheritance(damageClass)` | Define se herda os efeitos da outra classe; padrão `false`. |
| `GetPrefixInheritance(damageClass)` | Define a herança de prefixos; padrão delega a `GetEffectInheritance`. |
| `SetStaticDefaults()`, `SetDefaultStats(player)` | Inicialização do tipo e dos atributos do jogador. |
| `UseStandardCritCalcs` | Getter com padrão `true`. |
| `ShowStatTooltipLine(player, lineName)` | Controla a linha de atributo no tooltip; padrão `true`. |
| `CountsAsClass(damageClass)`, `GetsPrefixesFor(damageClass)` | Consultam as relações entre classes registradas. |
| `DamageClass.register(Classe)` | Registra manualmente e retorna o tipo. |

`StatModifier` aplica `(valor + Base) * Additive * Multiplicative + Flat`.
Seus campos são mutáveis em JavaScript. `ApplyTo`, `CombineWith`, `Scale`,
`Undo`, `Clone` e `Equals` estão no [catálogo de StatModifier](metodos.md#statmodifier).
`StatInheritanceData` aceita cinco fatores ou um objeto com esses fatores;
`Full` e `None` devolvem instâncias novas. Veja o
[guia de classes de dano](../mods/15-classes-de-dano.md) para compor esses atributos.

## Ajudantes

Globais, com os nomes do tModLoader:

| | |
|---|---|
| `Vector2` | `new(x, y)`, `Add`, `Subtract`, `Multiply`, `Divide`, `Length`, `Distance`, `Normalize`, `SafeNormalize`, `DirectionTo`, `RotatedBy`, `RotatedByRandom`, `ToRotation`, `ToRotationVector2`, `Lerp`, `Dot`, `ToTileCoordinates`, `Clone`. |
| `MathHelper` | `Pi`, `TwoPi`, `PiOver2`, `ToRadians`, `ToDegrees`, `Clamp`, `Lerp`, `SmoothStep`, `WrapAngle`. |
| `Rand` | `Next(max)`, `Next(min, max)`, `NextFloat()`, `NextFloat(max)`, `NextBool(umEm)`, `NextChance(p)`, `NextSign()`, `NextFromList(lista)`, `NextVector2Circular(rx, ry)`, `NextVector2Unit()`. |
| `Color` | `new(r, g, b, a)`, `Color.White`, `Color.SkyBlue`... (sempre uma cópia), `Multiply`, `Lerp`, `ToVector3`. |
| `Rectangle` | `new(x, y, w, h)`, `Size`, `Center`, `Contains`, `Intersects`. |
| `Ref` | `new Ref(valor)`: parâmetro `ref`/`out`, `.value`. |
| `ItemRarityID`, `ProjAIStyleID`, `NPCAIStyleID` | Os números que este Terraria não traz. |
| `Terraria.ID.DustID` | Com os nomes que o tModLoader acrescenta (`DustID.PinkFairy`...). |
| `ProjAI` | Legado: `proj.ai[0]` já funciona direto. |

---

## O que não existe ainda

Classes do tModLoader sem equivalente hoje. Dá para fazer o efeito com hooks
diretos ([guia 1](../mods/01-hooks-do-zero.md)), mas sem atalho:

- `GlobalTile`, `GlobalBuff`, `GlobalWall`;
- `ModDust` e `ModKeybind`;
- interface própria (`UIState`, `ModifyInterfaceLayers`);
- `ModTree`, `ModPalmTree`, `ModCactus` e os pilares (`ModPylon`).
