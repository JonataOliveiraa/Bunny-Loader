# Hooks de ModPlayer

As assinaturas abaixo pertencem ao Bunny Loader. `player` é o primeiro
argumento e corresponde a `this.Player`. Os demais parâmetros seguem os
papéis do [ModPlayer do tModLoader](https://docs.tmodloader.net/docs/stable/class_mod_player.html),
adaptados ao Terraria IL2CPP integrado no projeto.

Somente métodos sobrescritos instalam hooks. As instâncias e os dados são
separados por jogador e classe. Exceções de um mod são registradas e não
interrompem os callbacks dos outros mods.

## Retornos e referências

Nos métodos booleanos que permitem uma ação, qualquer `false` veta. Nos
métodos com decisão opcional, `null` mantém a regra do jogo, `true` permite
e `false` tem prioridade. Esquivas e tratamentos de slots param no primeiro
`true`. Multiplicadores positivos e finitos são multiplicados entre mods.

Parâmetros indicados por `Ref<T>` são alterados em `.value`. Outros objetos
mutáveis, como modificadores, itens, tentativas de pesca e informações de
desenho, são alterados diretamente.

## Acertos e dano recebido

| Assinatura | Contrato |
|---|---|
| `CanHitNPC(player, target)` | `true` por padrão; veto geral. |
| `CanHitNPCWithItem(player, item, target)` | `null` por padrão; decisão opcional para o item. |
| `CanHitNPCWithProj(player, projectile, target)` | Decisão opcional para o projétil. |
| `CanHitPvp(player, item, target)` | `false` impede o acerto corpo a corpo em outro jogador. |
| `CanMeleeAttackCollideWithNPC(player, item, hitbox, target)` | Decisão opcional para a interseção do golpe. |
| `ModifyHitNPC(player, target, modifiers)` | Antes do dano nativo, seguido do callback específico da origem. |
| `ModifyHitNPCWithItem(player, item, target, modifiers)` | Modifica o golpe de item. |
| `ModifyHitNPCWithProj(player, projectile, target, modifiers)` | Modifica o golpe de projétil. |
| `OnHitNPC(player, target, hit, damageDone)` | Depois de um acerto com dano positivo. |
| `OnHitNPCWithItem(player, item, target, hit, damageDone)` | Notificação específica do item. |
| `OnHitNPCWithProj(player, projectile, target, hit, damageDone)` | Notificação específica do projétil. |
| `OnHitAnything(player, x, y, victim)` | Depois de `Player.OnHit`. |
| `MeleeEffects(player, item, hitbox)` | Durante os efeitos visuais de uso. |
| `EmitEnchantmentVisualsAt(player, projectile, position, width, height)` | Depois dos efeitos de encantamento do projétil. |
| `CanBeHitByNPC(player, npc, cooldownSlot)` | Veto; `cooldownSlot` é `Ref<int>`. |
| `CanBeHitByProjectile(player, projectile)` | Veto ao dano recebido de projétil. |
| `ModifyHitByNPC(player, npc, modifiers)` | Antes de `ModifyHurt`. |
| `ModifyHitByProjectile(player, projectile, modifiers)` | Antes de `ModifyHurt`. |
| `OnHitByNPC(player, npc, info)` | Depois do dano positivo causado pelo NPC. |
| `OnHitByProjectile(player, projectile, info)` | Depois do dano positivo causado pelo projétil. |
| `ConsumableDodge(player, info)` | `true` evita o dano após `ModifyHurt` e `FreeDodge`. Só para golpes esquiváveis do jogador local. |

Os modificadores de ataque contêm `damage`, `knockBack`, `hitDirection`,
`crit`, `SourceDamage` e `Knockback` (`StatModifier`), além de `SetCrit()` e
`DisableCrit()`. O cálculo de defesa continua no jogo. `hit` contém
`Damage`, `SourceDamage`, `Knockback`, `HitDirection` e `Crit`.

Os modificadores de dano recebido mantêm o contrato existente de
`ModifyHurt`: `damage`, `hitDirection`, `quiet`, `crit` e `dodgeable`.
`info` contém `DamageSource`, `Damage`, `HitDirection`, `PvP`, `Quiet`,
`Crit`, `CooldownCounter` e `Dodgeable`.

## Itens, munição, mana e cura

| Assinatura | Contrato |
|---|---|
| `CanShoot(player, item)` | Veta o disparo, mantendo o tempo de uso. |
| `ModifyShootStats(player, item, position, velocity, type, damage, knockBack)` | Os cinco últimos parâmetros são `Ref`. |
| `Shoot(player, item, source, position, velocity, type, damage, knockBack)` | `false` impede a criação do projétil. |
| `CanConsumeAmmo(player, weapon, ammo)` | `false` preserva a munição; não consulta em chamadas `dontConsume`. |
| `OnConsumeAmmo(player, weapon, ammo)` | Quando a pilha diminui; conserva o tipo da última munição antes de `TurnToAir`. |
| `CanAutoReuseItem(player, item)` | Decisão opcional, preservando os campos permanentes do item. |
| `ModifyWeaponCrit(player, item, crit)` | `crit` é `Ref<int>`. |
| `ModifyWeaponKnockback(player, item, knockback)` | `knockback` é `StatModifier`. |
| `ModifyItemScale(player, item, scale)` | `scale` é `Ref<float>`. |
| `UseSpeedMultiplier(player, item)` | Divisor dos tempos de uso e animação; padrão `1`. |
| `UseTimeMultiplier(player, item)` | Multiplica o tempo de uso; padrão `1`. |
| `UseAnimationMultiplier(player, item)` | Multiplica o tempo da animação; padrão `1`. |
| `ModifyManaCost(player, item, reduce, mult)` | `Ref<float>`: custo atual × `(1 - reduce.value)` × `mult.value`. |
| `OnConsumeMana(player, item, manaConsumed)` | Notifica a mana consumida. |
| `OnMissingMana(player, item, neededMana)` | Antes da tentativa nativa de recuperar mana. |
| `PreItemCheck(player)` | `false` pula a verificação dos itens. |
| `PostItemCheck(player)` | Após a verificação, inclusive quando vetada. |
| `GetHealLife(player, item, quickHeal, healValue)` | `healValue` é `Ref<int>`; também influencia a escolha da cura rápida. |
| `GetHealMana(player, item, quickHeal, healValue)` | `healValue` é `Ref<int>`. |
| `ApplyPotionDelay(player, item, potionDelay)` | Recebe o tempo efetivo, incluindo sorteios; `false` impede o atraso e o buff. |

Crítico, repulsão e tempos compõem com `DamageClass` antes dos modificadores
de `ModPlayer`. Os tempos resultantes têm pelo menos um quadro.

`ModifyShootStats` e `Shoot` rodam para cada criação nativa de projétil
dentro de um disparo. Uma arma que cria vários projéteis pode chamá-los
várias vezes. Projéteis criados por esses próprios callbacks passam
diretamente para o jogo, evitando recursão no mesmo disparo.

## Atualização, controles e saltos

| Assinatura | Ponto de execução |
|---|---|
| `PreUpdateMovement(player)` | Antes da primeira etapa de colisão e deslocamento do quadro. |
| `PostUpdateMiscEffects(player)` | Depois de `CapAttackSpeeds`. |
| `PostUpdateRunSpeeds(player)` | Antes de `HorizontalMovement`. |
| `NaturalLifeRegen(player, regen)` | `Ref<float>` no cálculo nativo da regeneração natural. |
| `UpdateAutopause(player)` | Após a atualização pausada do jogador local. |
| `ProcessTriggers(player, triggersSet)` | Após copiar os controles para o jogador local. |
| `ResetInfoAccessories(player)` | Depois de zerar efeitos e depois de atualizar os acessórios informativos. |
| `ArmorSetBonusActivated(player)` | Duplo toque na direção de ativação configurada. |
| `ArmorSetBonusHeld(player, holdTime)` | Direção de ativação mantida pressionada. |
| `OnEquipmentLoadoutSwitched(player, oldLoadoutIndex, loadoutIndex)` | Depois de uma troca efetiva. |
| `CanStartExtraJump(player, jump)` | Veto ao salto extra disponível. |
| `CanShowExtraJumpVisuals(player, jump)` | Veto aos efeitos nativos do salto ativo. |
| `ExtraJumpVisuals(player, jump)` | Efeitos adicionais do salto permitido. |
| `ModifyExtraJumpDurationMultiplier(player, jump, duration)` | `duration` é `Ref<float>`, inicialmente `1`. |
| `OnExtraJumpStarted(player, jump, playSound)` | `playSound` é `Ref<bool>`; `false` suprime o som do início. |
| `OnExtraJumpEnded(player, jump)` | Ao encerrar o salto ativo. |
| `OnExtraJumpRefreshed(player, jump)` | Após renovar os saltos disponíveis dos equipamentos. |
| `OnExtraJumpCleared(player, jump)` | Ao retirar a disponibilidade de um salto sem equipamento habilitador. |

`ExtraJump` exporta descritores dos nove saltos nativos: `CloudInABottle`,
`SandstormInABottle`, `BlizzardInABottle`, `FartInAJar`, `TsunamiInABottle`,
`UnicornMount`, `SantankMount`, `GoatMount` e `BasiliskMount`. Compare a
identidade do descritor ou leia `jump.Name`. O registro de novos tipos de
salto não faz parte deste conjunto de hooks.

## Desenho e câmera

| Assinatura | Contrato |
|---|---|
| `DrawEffects(player, drawInfo, r, g, b, a, fullBright)` | Multiplicadores `Ref<float>` e `Ref<bool>`; depois da preparação nativa. |
| `DrawPlayer(player, camera)` | Depois do desenho nativo completo. |
| `HideDrawLayers(player, drawInfo)` | Chame `PlayerDrawLayers.Head.Hide()`, por exemplo, para aquele desenho. |
| `ModifyDrawInfo(player, drawInfo)` | Altera o `PlayerDrawSet` antes de construir as camadas. |
| `ModifyDrawLayerOrdering(player, positions)` | `Map` de descritor para `{ Before: descritor }` ou `{ After: descritor }`. |
| `TransformDrawData(player, drawInfo)` | Depois das transformações nativas e antes de renderizar o cache. |
| `ModifyScreenPosition(player)` | Depois de atualizar a câmera; altere `Main.screenPosition`. |
| `ModifyZoom(player, zoom)` | `Ref<float>`; preserva a proporção dos eixos. |

`PlayerDrawLayer.BeforeParent(layer)` e `AfterParent(layer)` criam posições.
A ordenação reorganiza os dados produzidos pelas camadas nativas, preservando
as regras de posição e equipamento do jogo. Ciclos são ignorados com aviso.
O cache é `drawInfo.DrawDataCache`, com `drawInfo.DrawDataCacheCount` entradas
ativas. As visibilidades são restauradas ao terminar cada desenho.

## Pesca, serviços e inventário

| Assinatura | Contrato |
|---|---|
| `GetFishingLevel(player, fishingRod, bait, fishingLevel)` | `Ref<float>`; os itens podem ser `null` quando não encontrados. |
| `ModifyFishingAttempt(player, attempt)` | Antes do sorteio do item; altera a tentativa diretamente. |
| `ModifyCaughtFish(player, fish)` | Altera o item efetivo antes de entregá-lo. |
| `CanConsumeBait(player, bait)` | Decisão opcional depois do sorteio nativo de consumo. |
| `AnglerQuestReward(player, rareMultiplier, rewardItems)` | Lista JS mutável de itens antes da entrega. |
| `GetDyeTraderReward(player, rewardPool)` | Lista JS mutável de IDs antes do sorteio. Lista vazia produz item vazio. |
| `CanBuyItem(player, vendor, shopInventory, item)` | Veto antes do pagamento da compra. |
| `PostBuyItem(player, vendor, shopInventory, item)` | Após compra paga; item recebido pelo cursor. |
| `CanSellItem(player, vendor, shopInventory, item)` | Veto antes da venda. |
| `PostSellItem(player, vendor, shopInventory, item)` | Após venda aceita. |
| `ModifyNurseHeal(player, nurse, health, removeDebuffs, chatText)` | Três `Ref`; `false` impede o atendimento e usa o texto informado. |
| `ModifyNursePrice(player, nurse, health, removeDebuffs, price)` | `price` é `Ref<int>`; consulta também ao mostrar o preço. |
| `PostNurseHeal(player, nurse, health, removeDebuffs, price)` | Depois de um atendimento pago, incluindo preço zero. |
| `CanCatchNPC(player, target, item)` | Decisão opcional para a captura. |
| `OnCatchNPC(player, npc, item, failed)` | Captura concluída ou tentativa que provoca dano ao tentar pegar criatura de lava. |
| `AddStartingItems(player, mediumCoreDeath)` | Retorna uma lista de itens; padrão vazio. |
| `ModifyStartingInventory(player, itemsByMod, mediumCoreDeath)` | `Map` de UUID para listas de itens; itens nativos sob `'Terraria'`. |
| `AddMaterialsForCrafting(player, itemConsumedCallback)` | Retorna itens; ponha uma função `(item, index)` em `itemConsumedCallback.value`. |
| `OnPickup(player, item)` | `false` elimina o item do chão sem pôr no inventário. |
| `HoverSlot(player, inventory, context, slot)` | `true` trata o hover e pula o nativo. |
| `ShiftClickSlot(player, inventory, context, slot)` | Com Shift ativo, `true` trata o clique. |
| `CanBeTeleportedTo(player, teleportPosition, context)` | Veto no teleporte nativo; contexto `'TeleportRod'`, `'TeleportationPotion'`, `'Teleport'` ou `'Wormhole'`. |

Itens de crafting são referências aos itens originais, não cópias. O índice
do callback corresponde à lista devolvida pelo mod. Fontes repetidas são
deduplicadas por endereço e divididas em inventários nativos de até 200 slots.
O jogo usa essas fontes tanto na disponibilidade de receitas quanto no
consumo. Itens iniciais são montados após o inventário nativo, antes do save
do personagem; em morte mediumcore, após o inventário de reposição.

## Save e rede

| Assinatura | Contrato |
|---|---|
| `PreSavePlayer(player)` | Antes do save nativo. |
| `PostSavePlayer(player)` | Ao finalizar o save nativo e os dados auxiliares, inclusive se o save nativo lançar erro. |
| `PreSaveCustomData(player)` | Antes de montar os dados do arquivo `.plr.bl.json`. |
| `CopyClientState(player, targetCopy)` | Copia para outra instância da mesma classe, associada ao clone nativo. |
| `SendClientChanges(player, clientPlayer)` | No cliente, compara com a instância copiada no quadro anterior. |
| `SyncPlayer(player, toWho, fromWho, newPlayer)` | Após sincronizar o jogador nativo. |
| `PlayerConnect(player)` | Após a conexão do jogador. |
| `PlayerDisconnect(player)` | Antes de remover o jogador desconectado. |

As mensagens de estado de mod continuam sendo enviadas explicitamente por
`ModPacket`. Não há serialização automática de campos JS. Copie arrays e
objetos mutáveis no `CopyClientState` para não compartilhar estado com o clone.
`newPlayer` indica que ainda não foi observada uma conexão para aquele índice.
Os hooks de save preservam dados de mods ausentes e mantêm a regra existente
de arquivo auxiliar apenas para personagens locais.

O tModLoader declara `PreSaveCustomData`, mas não o chama no código estável
consultado. Aqui o hook tem o ponto explícito descrito acima.

## Verificação

`node tools/tests/modplayerhooks/check.mjs` confere assinaturas no dump local
e executa testes de composição, vetos, referências, reentrada, cache de
desenho, serviços, crafting, clones e save. Requer `refs/dump.cs`, que é
gerado localmente e não é versionado.

O pacote `tools/tests/modplayerhooks` registra todos os hooks e exercita
os caminhos nativos num personagem e mundo de teste. A ponte de regeneração
natural, iscas, recompensas de tintura e condições de pesca verifica instruções do binário antes
de instalar; um binário incompatível produz erro, sem aplicar o patch.
