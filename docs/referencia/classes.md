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

Todas as classes são **globais**: nada de `import`.

| Classe | Para quê | Guia |
|---|---|---|
| [`ModItem`](#moditem) | Item novo. | [5](../mods/05-itens.md) |
| [`ModProjectile`](#modprojectile) | Projétil novo. | [6](../mods/06-projeteis.md) |
| [`ModNPC`](#modnpc) | NPC, chefe ou morador novo. | [7](../mods/07-npcs.md) |
| [`ModPlayer`](#modplayer) | Dados e comportamento por jogador. | [8](../mods/08-jogador-e-buffs.md) |
| [`ModBuff`](#modbuff) | Buff ou debuff novo. | [8](../mods/08-jogador-e-buffs.md) |
| [`ModTile`](#modtile) | Bloco novo (1x1). | [9](../mods/09-blocos.md) |
| [`ModSystem`](#modsystem) | O que é do mod inteiro. | [5](../mods/05-itens.md#modsystem) |
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
| `Texture` | texto | Caminho em `Textures/`, sem `.png`. Padrão: o nome da classe. |
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
| `ModItem.register(Classe)` | Registra; devolve o tipo. |
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
| `Texture` | texto | Em `Textures/`, sem `.png`. |
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
| `Texture` | texto | Em `Textures/`, sem `.png`. Tira vertical de quadros. |
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
| `Texture` | 32x32, em `Textures/`, sem `.png`. |
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

Um bloco novo. Por enquanto, **blocos 1x1** (terra, pedra, minério). Uma
instância por tipo; os métodos recebem a posição `(i, j)` em tiles.

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

Móveis e objetos maiores (`TileObjectData`), paredes (`ModWall`), estação de
criação de mod, `NearbyEffects`, `RandomUpdate`, `PlaceInWorld`, animação de
tile.

---

## ModSystem

O que é do mod inteiro, não de um item: grupos de receita, receitas de itens
do jogo, preparação.

| Método | Quando roda |
|---|---|
| `AddRecipeGroups()` | Uma vez, antes de qualquer receita. |
| `AddRecipes()` | Uma vez, com as receitas do jogo prontas. |
| `PostSetupContent()` | Uma vez, com todo o conteúdo de mod no jogo. |

`ModSystem.register(Classe)` devolve a instância.

**Ainda não**: os outros ganchos do `ModSystem` do tModLoader
(`PreUpdateWorld`, `PostUpdateEverything`, `ModifyInterfaceLayers`,
`SaveWorldData`/`LoadWorldData`, `OnWorldLoad`...). Hoje, isso se faz com
hooks diretos.

---

## Mod e ModLoader

`Mod` é o mod em si; um por pacote. `ModLoader` acha os outros.

| `Mod` | |
|---|---|
| `id` | O `id` do manifesto (o `Name` do tModLoader). |
| `uuid`, `name`, `version` | Do manifesto. |
| `path`, `root` | A pasta do `main.js` e a do pacote. |
| `dataDirectory` | `Android/data/com.bunnyloader/mod_data/<uid>`. |
| `Load()` | Na hora do `Mod.register`. |
| `AddRecipeGroups()`, `AddRecipes()`, `PostSetupContent()` | Como no `ModSystem`. |
| `Call(...args)` | Quando outro mod chama. |
| `Mod.register(Classe)` | Um por pacote; devolve o `Mod` (o mesmo objeto de antes). |

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
| `ModGore` | `ModGore.getTypeByName('Nome')`: o gore de `Textures/Gores/Nome.png`. |

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
| `ModLocalization.Translate('Secao.Chave')` | Registra o texto de `Localization/<cultura>.json` e devolve a **chave** (o que o Bestiário pede). |
| `ModLocalization.GetTextValue('Secao.Chave')` | O **texto** no idioma do jogo. |
| `ModLocalization.Register(chave, texto)` | Registra um texto (ou `{ cultura: texto }`) sob a chave. |

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

- `GlobalItem`, `GlobalNPC`, `GlobalProjectile`, `GlobalTile`, `GlobalBuff`;
- `ModPrefix`, `ModMount`, `ModBiome`, `ModSceneEffect`, `ModWall`, `ModDust`,
  `ModRarity`, `ModWaterStyle` e os estilos de fundo;
- `ModKeybind`, `ModCommand`, `ModConfig`, `ModPacket` (mensagens de rede
  próprias);
- interface própria (`UIState`, `ModifyInterfaceLayers`);
- `ModTile` além de 1x1.
