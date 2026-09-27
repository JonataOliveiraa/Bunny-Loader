# O que cada classe tem hoje

Esta é a lista completa do que as classes de mod do Bunny Loader oferecem **hoje**:
cada campo, cada método que você pode escrever e cada atalho. Os guias explicam
como usar; aqui é para consultar.

Para cada método que você escreve, a tabela diz **quando ele roda** e **qual
método do jogo está por trás**, com o filtro nativo, se houver. Isso é o que
decide o custo (ver o [guia de custo](../mods/03-custo-e-desempenho.md)):

- **filtro `tipo`**: o hook só entra no JS para conteúdo **de mod**; o do jogo
  passa direto, quase de graça;
- **sem filtro**: entra no JS para toda chamada, e a classe confere o tipo lá
  dentro;
- **nativo**: não é um hook JS; o núcleo em C++ chama o método.

Um método que **nenhuma** classe registrada escreve não instala hook nenhum e
não custa nada.

Todas as classes são **globais**: nada de `import`. Toda classe exportada pelo
arquivo de entrada ou por um arquivo de `Content/` e `Common/` é registrada
sozinha (`static Autoload = false` a deixa de fora); o `register` de cada uma
é para registrar na mão, no `Load()` do mod.

| Classe | Para quê | Guia |
|---|---|---|
| [`ModItem`](#moditem) | Item novo. | [5](../mods/05-itens.md) |
| [`ModProjectile`](#modprojectile) | Projétil novo. | [6](../mods/06-projeteis.md) |
| [`ModNPC`](#modnpc) | NPC, chefe ou morador novo. | [7](../mods/07-npcs.md) |
| [`ModPlayer`](#modplayer) | Dados e comportamento por jogador. | [8](../mods/08-jogador-e-buffs.md) |
| [`ModBuff`](#modbuff) | Buff ou debuff novo. | [8](../mods/08-jogador-e-buffs.md) |
| [`ModTile`](#modtile) | Bloco novo, ou móvel com `TileObjectData`. | [9](../mods/09-blocos.md) |
| [`GlobalItem`, `GlobalNPC`, `GlobalProjectile`](#globalitem-globalnpc-e-globalprojectile) | Mexer nos itens, NPCs e projéteis do jogo. | [12](../mods/12-globais-e-mundo.md) |
| [`GlobalLoot`](#globalloot) | Drops que valem para todo NPC. | [12](../mods/12-globais-e-mundo.md#drops) |
| [`ModSystem`](#modsystem) | O que é do mod inteiro; o ciclo do mundo e os dados salvos nele. | [12](../mods/12-globais-e-mundo.md#modsystem-o-mundo) |
| [`TagCompound`](#tagcompound) | Os dados que o mod salva (mundo, jogador). | [12](../mods/12-globais-e-mundo.md#dados-salvos-com-o-mundo) |
| [`ModPacket`, `NetWriter`, `NetReader`](#rede) | Dados de mod na rede. | [12](../mods/12-globais-e-mundo.md#rede) |
| [`Mod`, `ModLoader`](#mod-e-modloader) | O mod em si; conversa entre mods. | [11](../mods/11-conversa-entre-mods.md) |
| [`ModContent`](#modcontent) | Tipo, modelo e textura pelo nome ou pela classe. | [4](../mods/04-conteudo-novo.md#modcontent) |
| [`ModRecipe`](#modrecipe) | Receitas e grupos de receita. | [5](../mods/05-itens.md#receitas) |
| [`NPCLoot`, `NPCSpawnInfo`, `NPCShop`, `NPCHappiness`, `ModGore`](#ajudantes-de-npc) | Drops, spawn, loja, felicidade, gore. | [7](../mods/07-npcs.md) |
| [`TooltipLine`](#tooltipline) | Linha de tooltip. | [5](../mods/05-itens.md#tooltip-colorido) |
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
| `Texture` | texto | Caminho em `Assets/Textures/`, sem `.png`. Padrão: o caminho do arquivo da classe (`Content/Items/X.js` -> `Items/X`), ou o primeiro PNG com o nome dela. |
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
| `ModifyTooltips(item, tooltips)` | Toda vez que o tooltip aparece. | `Main.MouseText_DrawItemTooltip_GetLinesInfo` (+ `DrawString` com `whileIn`) |
| `OnCraft(item, player, recipe)` | Ao criar o item no menu de criação. | `Main.CraftItem_GrantItem`, filtro `tipo` |
| `CanUseItem(item, player)` | Antes de usar; `false` impede. | `Player.ItemCheck_CheckCanUse_Inner`, filtro `tipo` |
| `UseItem(item, player)` | No quadro em que o uso começa. | `Player.ItemCheck_StartActualUse`, filtro `tipo` |
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
| `UpdateInventory(item, player)` | Todo quadro, no inventário. | `Player.UpdateEquips`, sem filtro (percorre só os itens de mod do jogador) |
| `GetAlpha(item, lightColor)` | No chão: devolva a `Color` do desenho. | `WorldItem.GetAlpha`, sem filtro |
| `ModifyFishingLine(item, bobber, lineOriginOffset, lineColor)` | Vara na mão, a cada boia: de onde a linha sai e a cor (dois `Ref`). | `Main.DrawProj_FishingLine`, sem filtro |

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

Do `ModItem` do tModLoader, entre outros: `CanRightClick`/`RightClick`,
`ModifyHitNPC`, `MeleeEffects`, `PreDrawInWorld`/`PostDrawInInventory`,
`OnPickup`, `GrabRange`, `ModifyWeaponDamage` (há no `ModPlayer`),
`IsArmorSet`/`UpdateArmorSet` e a armadura desenhada no corpo.

---

## ModProjectile

Um projétil novo. Como o item: molde e uma instância por projétil
(`this.Projectile`).

### Campos

| Campo | Tipo | Para quê |
|---|---|---|
| `Projectile` | `Projectile` do jogo | O projétil desta instância. |
| `Type`, `Mod` | | O tipo e o mod. |
| `Texture` | texto | Em `Assets/Textures/`, sem `.png`. |
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
| `PreDraw(proj, lightColor)` | Antes do desenho; `false` não desenha o do jogo. | `Main.DrawProjDirect`, filtro `tipo` |
| `PostDraw(proj, lightColor)` | Depois do desenho do jogo. | idem |
| `CanUseGrapple(player, type)` | No **molde**, antes de lançar o gancho; `false` impede. | `Player.FireGrapple`, filtro pelo `item.shoot` |
| `UseGrapple(player, type)` | No molde: devolva o tipo a lançar. | idem |
| `GrappleCanLatchOnTo(proj, player, tile)` | `true`/`false`: agarra neste bloco. | `Projectile.AI_007_GrapplingHooks_CanTileBeLatchedOnTo`, filtro `tipo` |

Sempre instalado, para todo projétil de mod: o `originalDamage` (o dano base
de lacaio e sentinela) vem do item que criou o projétil
(`Projectile.ApplyStatsFromSource`).

### Atalhos e estáticos

| | |
|---|---|
| `CloneDefaults(tipo)` | Copia os valores de um projétil do jogo. |
| `DefaultToSpear()`, `DefaultToYoyo()`, `DefaultToFlail()`, `DefaultToWhip()`, `DefaultToDrillOrChainsaw()`, `DefaultToKite()` | Os padrões do jogo para cada família de projétil segurado. |
| `ModProjectile.register`, `getTypeByName`, `getModProjectile`, `getByName`, `isModType`, `isModProjectile` | Como no item. |

### Ainda não

`ModifyHitNPC`, `CanHitNPC`, `OnHitNPC` com as informações do golpe,
`TileCollideStyle` (use `decidesManualFallThrough`/`shouldFallThrough`),
`PreDrawExtras`, `SendExtraAI`/`ReceiveExtraAI`.

---

## ModNPC

Um NPC novo: inimigo, chefe ou morador. Molde e uma instância por NPC
(`this.NPC`).

### Campos

| Campo | Tipo | Para quê |
|---|---|---|
| `NPC`, `Type`, `Mod` | | O NPC desta instância, o tipo, o mod. |
| `Texture` | texto | Em `Assets/Textures/`, sem `.png`. Tira vertical de quadros. |
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
| `PreAI(npc)`, `AI(npc)`, `PostAI(npc)` | Todo quadro. `PreAI` → `false` pula a IA do jogo. | `NPC.AI`, filtro `tipo` |
| `FindFrame(npc, frameHeight)` | Animação própria: mude `npc.frame`. | `NPC.FindFrame`, filtro `tipo` |
| `CheckActive(npc)` | `false`: não some quando longe. | `NPC.CheckActive`, filtro `tipo` |
| `PreKill(npc)`, `OnKill(npc)` | Na morte. `PreKill` → `false` cancela o drop. | `NPC.NPCLoot`, filtro `tipo` |
| `SpawnChance(spawnInfo)` | A cada spawn natural: devolva o peso (0 = não nasce). | `NPC.SpawnNPC`, sem filtro |
| `SpawnNPC(x, y)` | Sorteado: como nasce. Padrão: no ponto do sorteio. | idem |
| **Morador** | | |
| `CanTownNPCSpawn(numTownNPCs)` | De tempos em tempos, sem um deste no mundo: `true` e ele se muda. | `Main.UpdateTime_SpawnTownNPCs`, sem filtro |
| `CheckConditions(left, right, top, bottom)` | A sala serve para ele? | `WorldGen.CheckSpecialTownNPCSpawningConditions` |
| `SetNPCNameList()` | Os nomes próprios; um é sorteado. | `NPC.getNewNPCName` |
| `GetChat(npc)` | A fala ao conversar. | `NPC.GetChat`, filtro `tipo` |
| `SetChatButtons(npc, buttons)` | Os botões da conversa (até dois no celular). | `GUINPCDialogue.SetupButtonText` |
| `OnChatButtonClicked(npc, firstButton)` | Tocou num botão; devolva o nome de uma loja para abri-la. | `GUINPCDialogue.Option1Clicked`/`Option2Clicked` |
| `AddShops()` | Uma vez: as lojas (`NPCShop`). | conteúdo pronto |
| `TownNPCAttackProj(npc, attack)` | O projétil do ataque (`projType`, `attackDelay`). | `NPC.AI`, filtro `tipo` |
| `TownNPCAttackStrength(npc, attack)` | `damage`, `knockback`. | idem |
| `TownNPCAttackProjSpeed(npc, attack)` | `speed`, `gravityCorrection`, `randomOffset`. | idem |
| `NPCHeadSlot()` | (para ler) o índice da cabeça, -1 sem cabeça. | |

A música (`Music`) é decidida pelos hooks de `Main.UpdateAudio*`, instalados
uma vez; ver o [guia 10](../mods/10-sons-e-musica.md).

### Estáticos

| | |
|---|---|
| `ModNPC.register`, `getTypeByName`, `getModNPC`, `getByName`, `isModType`, `isModNPC` | Como no item. |
| `ModNPC.NPCValue(pl, ouro, prata, cobre)` | O dinheiro que ele solta. |

### Ainda não

`CanHitPlayer`/`ModifyHitPlayer`/`OnHitPlayer`, `ModifyHitByItem`/`ByProjectile`
e `OnHitByItem`/`ByProjectile`, `PreDraw`/`PostDraw`, ícone de
chefe no mapa, sacola de tesouro, marca de chefe derrotado,
`ModifyNPCHappiness`, `CanGoToStatue` e os ataques de morador além do
projétil.

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
| `UpdateMovement(player)` | Movimento próprio (dash), perto do fim do quadro. | `Player.BordersMovement` |
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
| `SaveData(data)` | A cada save: ponha o que lembrar em `data`. | `Player.InternalSavePlayerFile` |
| `LoadData(data)` | Ao carregar o personagem. | `Player.LoadPlayer` |

Os hooks do `ModPlayer` não têm filtro: rodam uma vez por jogador por
quadro, o que é barato.

### Como achar a instância

| | |
|---|---|
| `player.GetModPlayer(Classe)` ou `player.GetModPlayer('Classe')` | A instância daquele jogador. |
| `Classe.get(player)` | O mesmo. |
| `ModPlayer.getByName('Classe')` | A do jogador **local** (para interface). |
| `ModPlayer.register(Classe)` | Registra. |

### Ainda não

`ModifyHitNPC`/`OnHitNPC` do jogador, `ProcessTriggers` (teclas),
`CatchFish`, `ModifyScreenPosition`, `DrawEffects`, `CopyClientState` e a
sincronização pela rede.

---

## ModBuff

Um buff ou debuff novo. **Uma instância por tipo** (buff não é entidade): os
métodos recebem o jogador ou o NPC e a posição do buff na lista dele.

### Campos

| Campo | Para quê |
|---|---|
| `Type`, `Mod` | |
| `Texture` | 32x32, em `Assets/Textures/`, sem `.png`. |
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

## ModTile

Um bloco novo (terra, pedra, minério) ou um objeto de várias células com
`TileObjectData` (móveis: a Pia de Exemplo, 2x2). Uma instância por tipo; os
métodos recebem a posição `(i, j)` em tiles.

### Campos

| Campo | Para quê |
|---|---|
| `Type`, `Mod` | |
| `Texture` | A folha de quadros (288x270, quadros de 16x16 com 2 px de margem). |
| `DustType` | A poeira ao bater e quebrar (`DustID`). |
| `HitSound` | O som ao bater (`SoundID`); `undefined` = o do jogo. |
| `MinPick` | Força de picareta mínima. |
| `MineResist` | O dano de cada golpe é dividido por ele. |
| `ItemDrop` | O item que cai; `undefined` = o item de mod que coloca o tile. |

### Métodos que você escreve

| Método | Quando roda | Por trás |
|---|---|---|
| `SetStaticDefaults()` | Uma vez: `Main.tileSolid[this.Type]`... | nativo |
| `PostSetupContent()` | | conteúdo pronto |
| `CanKillTile(i, j)` | `false`: a picareta não quebra. | `WorldGen.CanKillTile`, filtro de tile |
| `KillTile(i, j, fail, effectOnly, noItem)` | Antes de o tile sair (`fail`: só o golpe). | `WorldGen.KillTile`, filtro de tile |
| `KillMultiTile(i, j, frameX, frameY)` | Um objeto saiu inteiro; `(i, j)` é o canto de cima à esquerda. | `WorldGen.TileFrameImportant`, filtro de tile |
| `CreateDust(i, j)` | `false`: sem poeira. | `WorldGen.KillTile_MakeTileDust`, filtro de tile |
| `KillSound(i, j, fail)` | `false`: sem som. | `WorldGen.KillTile_PlaySounds`, filtro de tile |

`MinPick` e `MineResist` agem em `Player.GetPickaxeDamage`, e o `ItemDrop` em
`WorldGen.KillTile_GetItemDrops`, ambos com filtro de tile. Os hooks de tile
são instalados no primeiro `ModTile.register`.

### Para chamar

| | |
|---|---|
| `AddMapEntry(cor, nome)` | A cor no mapa (a do jogo mais próxima). |
| `ModTile.register`, `getTypeByName`, `getModTile`, `isModType` | |
| `bl.tiles.typeAt(x, y)` | O tipo do tile ativo numa posição (-1 se não há). |

### Ainda não

Portas, cadeiras, baús e outros móveis com comportamento próprio (só a forma e
o quebrar vêm do `TileObjectData`), paredes (`ModWall`), estação de criação de
mod, `NearbyEffects`, `RandomUpdate`, `PlaceInWorld`, animação de
tile.

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
| `Clone(de, para)` | A cópia no `Item.Clone`. Padrão: os mesmos campos. |
| `NewInstance(entidade)` | A cópia de uma entidade nova. Padrão: os campos do modelo. |
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
| `UseItem(item, player)` | O uso começa. | `Player.ItemCheck_StartActualUse` |
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
| `ModifyTooltips(item, tooltips)` | O tooltip; depois do `ModItem`. | `Main.MouseText_DrawItemTooltip_GetLinesInfo` |

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

### GlobalProjectile

| Método | Quando roda | Método do jogo |
|---|---|---|
| `SetDefaults(projectile)` | O projétil nasce (também no outro lado da rede). | `Projectile.SetDefaults` |
| `OnSpawn(projectile, source)` | Criado por `NewProjectile`, em quem criou. | `Projectile.NewProjectile` |
| `PreAI`, `AI`, `PostAI(projectile)` | A cada quadro. | `Projectile.AI` |
| `PreKill(projectile, timeLeft)`, `OnKill(projectile, timeLeft)` | Morte. `PreKill` `false`: some sem o efeito do jogo. | `Projectile.Kill` |
| `OnHitNPC(projectile, target)` | Acertou um NPC. | `Projectile.StatusNPC` |
| `OnHitPlayer(projectile, target)` | Acertou um jogador. | `Projectile.StatusPlayer` |
| `NetSend(projectile, writer)`, `NetReceive(projectile, reader)` | Rede: junto com cada projétil sincronizado, de quem o controla. | `NetMessage.SendData` (27) |

### Ainda não

`NetSend`/`NetReceive` do `GlobalItem`, `SaveData`/`LoadData` por entidade,
`GlobalTile`, `GlobalBuff`; no `GlobalNPC`, `UpdateLifeRegen`, `EditSpawnRate`/`EditSpawnPool`,
`ModifyActiveShop`, `ModifyHitPlayer`/`OnHitPlayer`; no `GlobalProjectile`,
`GetAlpha`, `PreDraw`/`PostDraw`, `Colliding`.

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
| `NetSend(writer)`, `NetReceive(reader)` | Rede: o servidor manda junto com os dados do mundo (ao entrar e a cada sincronização); o cliente lê. | `NetMessage.SendData` (7) |

Os dados vão para `<mundo>.wld.bl.json`, ao lado do `.wld`, uma entrada por
`ModSystem` (a chave é o uid do mod e o nome da classe). `ModSystem.register(Classe)`
devolve a instância (a de `ModContent.GetInstance`).

**Ainda não**: `ModifyWorldGenTasks`, `ModifyInterfaceLayers`, e os `Pre/PostUpdate` de
jogadores, NPCs, projéteis e itens separados.

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
| `ItemType(x)`, `ProjectileType(x)`, `NPCType(x)`, `BuffType(x)`, `TileType(x)` | O tipo. `x` é a classe, o nome (`'ExampleBobber'`) ou `'outromod/Nome'`. Não achou: 0. |
| `GetInstance(Classe)` | O modelo (a instância do `register`). |
| `Find(ModItem, 'mod/Nome')` | O modelo pelo nome; lança se não há. |
| `TryFind(ModItem, 'mod/Nome', ref)` | O mesmo, no `ref.value`; devolve `true`/`false`. |
| `GetModItem(tipo)`, `GetModProjectile(tipo)`, `GetModNPC(tipo)`, `GetModBuff(tipo)`, `GetModTile(tipo)` | O modelo pelo tipo. |
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
| `NPCSpawnInfo` | Recebido no `SpawnChance`: `SpawnTileX`, `SpawnTileY`, `Player`; altura (`Sky`, `Surface`, `Underground`, `Cavern`, `Underworld`, `AboveSurface`, `BelowSurface`); hora e evento (`Day`, `Night`, `Rain`, `SlimeRain`, `BloodMoon`, `SolarEclipse`, `PumpkinMoon`, `FrostMoon`, `AnyEvent`, `Invasion`, `AnyTower`); mundo (`HardMode`, `Expert`, `Master`); bioma (`Corruption`, `Crimson`, `Hallow` e os `Underground...`, `Snow`, `Ice`, `Jungle`, `UndergroundJungle`, `Mushroom`, `SurfaceMushroom`, `Ocean`, `Desert`, `DesertCave`, `Meteor`, `Marble`, `Granite`, `Graveyard`, `Dungeon`, `Lihzahrd`); `CommonEnemy`. |
| `NPCShop` | `new NPCShop(tipoDoNPC, 'Shop').Add(item, { condition, price, currency }).Register()`; `NPCShop.get(tipo, nome)`, `shop.Open()`. |
| `NPCHappiness` | `this.Happiness.SetNPCAffection(npc, nivel)`, `.SetBiomeAffection('Desert', nivel)`, com `AffectionLevel.Love`, `Like`, `Dislike`, `Hate`. |
| `ModGore` | `ModGore.getTypeByName('Nome')`: o gore de `Assets/Textures/Gores/Nome.png`. |

---

## TooltipLine

| | |
|---|---|
| `new TooltipLine(nome, texto)` | (também `new TooltipLine(mod, nome, texto)`, como no tModLoader). |
| `Name`, `Text` | O nome da linha e o texto. |
| `OverrideColor` | Uma `Color` para a linha inteira. |
| `IsModifier`, `IsModifierBad` | Linha de prefixo: boa, ou ruim. |
| `OneDropLogo` | `true`: a linha é o logo da One Drop. |
| `TooltipLine.colorTag(texto, cor)` | `'[c/RRGGBB:texto]'` para pintar um trecho. |

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
| `SceneEffectPriority.None` ... `BossHigh` | Prioridade da música do `ModNPC`. |

**Ainda não**: `Variants`, `IsLooped`, `ModBiome.Music`, `ModSceneEffect`,
caixa de música.

---

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
- `ModPrefix`, `ModMount`, `ModBiome`, `ModSceneEffect`, `ModWall`, `ModDust`,
  `ModRarity`, `ModWaterStyle` e os estilos de fundo;
- `ModKeybind`, `ModCommand`, `ModConfig`;
- interface própria (`UIState`, `ModifyInterfaceLayers`);
- `ModTile` com comportamento de móvel (porta, cadeira, baú, estação de criação).
