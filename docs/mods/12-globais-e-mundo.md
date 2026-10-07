# 12. Globais e o mundo

Até aqui, cada classe de mod criava algo **novo**: um item, um NPC, um buff.
Este guia é sobre mexer no que **já existe**, no que é do mundo inteiro e no
que precisa ir de um aparelho a outro no multijogador:

- `GlobalItem`, `GlobalNPC`, `GlobalProjectile`: código que roda para os
  itens, NPCs e projéteis **do jogo** (e os de mod), como deixar a espada de
  cobre mais forte ou fazer todo slime soltar gel extra;
- os **drops**: mudar a tabela de um NPC do jogo, ou pôr uma regra que vale
  para todos;
- `ModSystem`: o que é do mod inteiro, e o ciclo do mundo (carregar, salvar,
  sair) com dados que ficam gravados junto com ele;
- a **rede**: os dados do mod que o servidor manda aos clientes (e o que um
  cliente manda ao servidor), com `NetSend`/`NetReceive` e pacotes próprios.

Antes, leia as [ideias do guia 4](04-conteudo-novo.md). A lista completa está
na referência: [Globais](../referencia/classes.md#globalitem-globalnpc-e-globalprojectile),
[`GlobalLoot`](../referencia/classes.md#globalloot) e
[`ModSystem`](../referencia/classes.md#modsystem).

## Global ou hook direto?

Tudo o que um Global faz também dá para fazer com um hook direto (o
[guia 1](01-hooks-do-zero.md)). O Global é o mesmo hook, já pronto e com a
forma do tModLoader:

- o código do tModLoader passa quase como está (`AppliesToEntity`,
  `InstancePerEntity`, `ModifyNPCLoot`);
- vários mods podem ter Globais no mesmo método sem se atrapalhar;
- o estado por entidade (um contador por NPC, uma carga por item) vem de
  graça, sem mapa na mão.

Use o hook direto quando o Global não tem o método que você precisa.

## O custo

Um `ModItem` só entra no JS para o **seu** item: o hook tem um filtro nativo
por tipo, e o item do jogo passa direto. Um Global não tem esse filtro, porque
ele existe justamente para os itens do jogo. **Todo** item, NPC ou projétil
que passa pelo método entra no JS, e o `AppliesToEntity` decide lá dentro.

Na prática:

- um método que nenhum Global escreveu não instala hook nenhum e não custa
  nada;
- `SetDefaults`, `OnSpawn`, `OnKill`, `HitEffect`, `ModifyNPCLoot` rodam
  pouco: ao criar, acertar ou matar;
- `PreAI`, `AI` e `PostAI` rodam **a cada quadro para cada entidade**: até 200
  NPCs ou 1000 projéteis. Um `AI` global é o método mais caro que existe. Saia
  cedo e deixe o `AppliesToEntity` barato (comparar o `type`).

As contas do [guia de custo](03-custo-e-desempenho.md#quantas-vezes-o-jogo-chama-cada-método)
valem aqui: um `AI` global que só repassa, com 150 NPCs, fica perto de
0,1 ms por quadro, mais o que o seu código fizer. Um `SetDefaults` global roda
milhares de vezes ao carregar o mundo, e soma segundos se for pesado.

## GlobalItem

Um `GlobalItem` roda para os itens do jogo. O `AppliesToEntity` escolhe
quais; sem ele, vale para todos.

```js
const { ItemID } = Terraria.ID;

export class ShortswordGlobalItem extends GlobalItem {
    AppliesToEntity(item, lateInstantiation) {
        return item.type === ItemID.CopperShortsword;
    }

    SetDefaults(item) {
        item.damage = 50;
    }
}
```

Os métodos são os do `ModItem` (`SetDefaults`, `CanUseItem`, `UseItem`,
`HoldItem`, `Shoot`, `ModifyShootStats`, `UpdateAccessory`, `UpdateInventory`,
`OnCraft`, `ModifyTooltips`...), com o item como primeiro parâmetro. Quando um
item de mod também tem o método, o do `ModItem` roda primeiro e depois os
Globais, como no tModLoader. Também:

- dano, crítico, repulsão e tamanho: `ModifyWeaponDamage`, `ModifyWeaponCrit`,
  `ModifyWeaponKnockback`, `ModifyItemScale`, `UseItemHitbox`, `MeleeEffects`,
  `UseAnimation`, `UseItemFrame`, `HoldItemFrame`;
- munição: `NeedsAmmo`, `CanChooseAmmo`, `CanBeChosenAsAmmo`, `CanConsumeAmmo`,
  `CanBeConsumedAsAmmo`, `OnConsumeAmmo`, `OnConsumedAsAmmo`,
  `PickAmmo(weapon, ammo, player, type, speed, damage, knockback)`;
- cura: `GetHealLife`, `GetHealMana`, `ModifyPotionDelay`, `ApplyPotionDelay`;
- golpes: `CanHitNPC`, `ModifyHitNPC`, `OnHitNPC`, `CanHitPvp`, `ModifyHitPvp`,
  `OnHitPvp` (ver [Golpes](16-golpes.md)).

Nos `Can` de munição, um `false` de qualquer um (o item de mod ou um Global)
veta; nos que aceitam `null`, senão um `true` força.

## GlobalNPC e GlobalProjectile

Mesma ideia, para NPCs e projéteis:

```js
const { NPCID } = Terraria.ID;

export class SlimeGlobalNPC extends GlobalNPC {
    AppliesToEntity(npc, lateInstantiation) {
        return npc.type === NPCID.BlueSlime;
    }

    SetDefaults(npc) {
        npc.lifeMax *= 2;
    }

    OnKill(npc) {
        bl.log('um slime morreu em ' + npc.position);
    }
}
```

- `PreAI` devolvendo `false` pula a IA do jogo **e** o `AI` dos outros
  Globais; o `PostAI` roda de qualquer jeito.
- `PreKill` devolvendo `false` faz o NPC morrer sem drop e sem `OnKill`.
- `GetChat(npc, chat)`: `chat` é um `Ref` com a fala que o jogo escolheu;
  troque o `chat.value`. `CanChat(npc)`: `false` impede a conversa, `true`
  permite, `null` deixa o jogo.
- `FindFrame(npc, frameHeight)` (depois do quadro do jogo), `CheckActive`,
  `CheckDead`, `ApplyDifficultyAndPlayerScaling`, `ModifyNPCHappiness`.
- Desenho: `PreDraw` (os Globais antes do `ModNPC`; `false` não desenha),
  `PostDraw`, `DrawEffects`, `GetAlpha`, `BossHeadSlot`, `BossHeadRotation`,
  `BossHeadSpriteEffects`.
- Golpes: os mesmos do `ModNPC`, também nos NPCs do jogo (ver
  [Golpes](16-golpes.md)).

O `ModNPC` e o `GlobalNPC` passam pelos mesmos hooks do jogo (o
`NPCLoader`): cada hook só entra no JS para os tipos de NPC que alguém pediu,
o de mod que escreve o método ou todos, quando um Global o escreve.

### Lojas

O `GlobalNPC` mexe nas lojas do jogo e nas de outros mods:

```js
const { ItemID, NPCID } = Terraria.ID;

export class LojaDoComerciante extends GlobalNPC {
    // Uma vez por loja: o que o Add põe entra depois dos itens dela.
    ModifyShop(shop) {
        if (shop.NpcType === NPCID.Merchant) shop.Add(ItemID.Wood, { condition: () => Terraria.Main.dayTime });
    }

    // O formato antigo do tModLoader: a casa livre em nextSlot (Ref).
    SetupShop(type, shop, nextSlot) {
        if (type !== NPCID.Merchant) return;
        shop.item[nextSlot.value]['void SetDefaults(int Type, ItemVariant variant)'](ItemID.Torch, null);
        nextSlot.value++;
    }

    // A cada abertura, com os itens prontos.
    ModifyActiveShop(npc, shopName, items) {}

    // A loja do Mercador Viajante: shop é o Main.travelShop (os tipos).
    SetupTravelShop(shop, nextSlot) {
        shop[nextSlot.value++] = ItemID.Gel;
    }
}
```

`NPCShop.get(NPCID.Merchant)` devolve a loja do jogo (a do Pintor de
decoração é `NPCShop.get(NPCID.Painter, 'Decor')`), e `NPCShop.All` lista
todas. O jogo do celular monta todas as lojas ao abrir o mundo e de novo a
cada vez que uma abre: o `SetupShop` roda nas duas.

### O spawn natural

Como no tModLoader, o `GlobalNPC` mexe no spawn de inimigos em volta de cada
jogador. Roda sozinho ou no servidor, e o `player` é o jogador-alvo:

```js
export class SpawnDoBioma extends GlobalNPC {
    // Ref: spawnRate menor = mais spawn; maxSpawns = quantos por perto.
    EditSpawnRate(player, spawnRate, maxSpawns) {
        if (player.InModBiome(MeuBioma)) {
            spawnRate.value = Math.floor(spawnRate.value * 0.5);
            maxSpawns.value = Math.floor(maxSpawns.value * 1.5);
        }
    }

    // O sorteio: pool[tipo] = peso; o 0 é o spawn do jogo (peso 1).
    EditSpawnPool(pool, spawnInfo) {
        if (spawnInfo.Player.InModBiome(MeuBioma)) {
            pool[0] = 0.25;                          // menos inimigos do jogo
            pool[NPCID.GreenSlime] = 0.5;            // e um do jogo a mais
        }
    }
}
```

- `EditSpawnFlags(spawnInfo)`: antes de tudo (taxa, área e ponto), os campos
  do jogador (`noWorms`, `invaders`, `ZoneCorrupt`...). O ponto ainda é -1.
- `EditSpawnRange(player, spawnRangeX, spawnRangeY, safeRangeX, safeRangeY)`:
  `Ref`, em blocos: até onde nasce e o quanto longe do jogador.
- `EditSpawnInfo(spawnInfo)`: com o ponto escolhido, antes do sorteio. Os
  campos do `spawnInfo` (`waterTile`, `nearGranite`...) mudam o que o jogo e
  os `SpawnChance` leem.
- `SpawnNPC(npc, tileX, tileY)`: depois de nascer um NPC sorteado que não é o
  do jogo.

## Um estado por entidade

Por padrão, existe **um** objeto do Global, compartilhado por todas as
entidades. Um campo nele é do mod inteiro, não do item.

Com `InstancePerEntity`, cada entidade ganha a própria cópia, e os campos
passam a ser dela:

```js
export class ChargeGlobalItem extends GlobalItem {
    InstancePerEntity = true;
    charge = 0;

    AppliesToEntity(item, lateInstantiation) {
        return item.type === ItemID.Gel;
    }
}

// em qualquer lugar:
const g = item.GetGlobalItem(ChargeGlobalItem);
g.charge++;
```

- A cópia nasce no `SetDefaults`. Trocar o tipo do item dá uma cópia nova, com
  os campos zerados.
- `item.Clone()` leva a cópia junto, com o estado; a cópia do clone é outro
  objeto.
- `GetGlobalItem(Classe)` (ou `'NomeDaClasse'`) **lança** se o Global não se
  aplica àquele item. Para perguntar, use `item.TryGetGlobalItem(Classe, ref)`,
  que devolve `true` ou `false`.
- `GetGlobalNPC` e `GetGlobalProjectile` funcionam do mesmo jeito.

`InstancePerEntity = true` como campo, ou `get InstancePerEntity() { return true; }`,
os dois valem.

### Desenho, colisão e gancho no GlobalProjectile

Estes métodos recebem projéteis do jogo e de mods. `AppliesToEntity` limita
quais entidades recebem os callbacks, e `InstancePerEntity` mantém o estado
de cada projétil separado.

| Método | Contrato |
|---|---|
| `PreDraw(projectile, lightColor)` | `lightColor` é um `Ref<Color>`: leia ou substitua `.value`. `false` cancela o sprite padrão, preservando correntes e linhas. Todos os Globais recebem o callback; o `PreDraw` local só roda se todos permitirem. |
| `PostDraw(projectile, lightColor)` | Depois do desenho, mesmo com veto no `PreDraw`. A cor é um `Color`, sem `Ref`. Roda depois do `PostDraw` local. |
| `GetAlpha(projectile, lightColor)` | Retorne um `Color` ou deixe `undefined`/`null`. O primeiro Global que retornar uma cor decide; depois vem o método local e o jogo. |
| `Colliding(projectile, projHitbox, targetHitbox)` | Primeiro retorno booleano dos Globais decide a colisão. Sem resposta, consulta o método local e o jogo. |
| `CanDamage(projectile)` | `false` veta dano e corte. Qualquer veto prevalece; um `true` global prevalece sobre o método local. `undefined`/`null` mantém a decisão seguinte. As condições nativas de dano continuam valendo. |
| `ModifyDamageHitbox(projectile, hitbox)` | `hitbox` é um `Ref<Rectangle>`. Pode alterar campos de `.value` ou substituir o retângulo. Roda depois do método local. |
| `OnTileCollide(projectile, oldVelocity)` | Todos os Globais rodam. `false` impede a morte causada pelo choque e pula o método local. Morte por tempo, NPC ou `Kill()` continua funcionando. |
| `TileCollideStyle(projectile, width, height, fallThrough, hitboxCenterFrac)` | Os quatro parâmetros são `Ref`. Método local primeiro; depois Globais até o primeiro `false`, que desliga a colisão neste movimento. |
| `MinionContactDamage(projectile)` | `true` permite contato de um projétil considerado pet; consulta o método local e depois os Globais até um deles permitir. |
| `CanCutTiles(projectile)` | Primeiro retorno booleano dos Globais decide; depois consulta o método local e o jogo. `false` também impede os callbacks de corte. |
| `CutTiles(projectile)` | Corte personalizado, somente quando permitido. Globais primeiro, depois o método local e o corte padrão. |
| `CanUseGrapple(type, player)` | Antes do spawn, no template da classe. Cada resposta booleana global substitui a anterior; a última resposta definida decide. O filtro recebe uma amostra do tipo. |
| `UseGrapple(player, type)` | `type` é um `Ref<number>`. Roda depois do método local, para todos os templates que implementam o callback. Filtre `type.value` dentro do método, se necessário. |
| `GrappleCanLatchOnTo(projectile, player, tile)` | `tile` é o objeto nativo do bloco. Qualquer `false` global veta; um `true` global permite. Sem resposta, mantém a decisão local ou nativa. |

Os `Ref` de `PreDraw`, `ModifyDamageHitbox` e `UseGrapple` seguem o contrato
do Global no tModLoader. Nos métodos equivalentes de `ModProjectile`, a API
existente continua recebendo `Color` e `Rectangle` diretamente, e
`UseGrapple(player, type)` continua retornando o número do tipo. O celular
fornece `Tile` ao teste de fixação do gancho; por isso o callback recebe o
bloco, em vez das coordenadas `x` e `y` da API do tModLoader.

```js
export class FlechaAzul extends GlobalProjectile {
    AppliesToEntity(projectile) {
        return projectile.type === ProjectileID.WoodenArrowFriendly;
    }

    PreDraw(projectile, lightColor) {
        lightColor.value = Color.new(70, 130, 255, 255);
        return true;
    }

    ModifyDamageHitbox(projectile, hitbox) {
        hitbox.value.Width += 4;
        hitbox.value.Height += 4;
    }
}
```

As regras de combinação foram conferidas no
[ProjectileLoader do tModLoader](https://github.com/tModLoader/tModLoader/blob/stable/patches/tModLoader/Terraria/ModLoader/ProjectileLoader.cs).
As assinaturas adaptadas estão na [referência de GlobalProjectile](../referencia/classes.md#globalprojectile).

## Drops

A tabela de drop de um NPC do jogo muda no `ModifyNPCLoot` de um `GlobalNPC`.
Ele roda **uma vez por tipo de NPC**, quando o jogo termina de carregar, com a
amostra daquele tipo (é a mesma do Bestiário):

```js
const { ItemID, NPCID } = Terraria.ID;
const { ItemDropRule } = Terraria.GameContent.ItemDropRules;

export class ExampleNPCLoot extends GlobalNPC {
    ModifyNPCLoot(npc, npcLoot) {
        if (npc.type === NPCID.Demolitionist) {
            npcLoot.Add(ItemDropRule.Common(ItemID.Dynamite, 10, 1, 3));
        }
        if (npc.type === NPCID.FireImp) {
            npcLoot.RemoveWhere((rule) => 'itemId' in rule && rule.itemId === ItemID.ObsidianRose);
        }
    }
}
```

- `npcLoot.Get()` devolve as regras (num array), `Add` e `Remove` mexem uma a
  uma, `RemoveWhere(regra => ...)` tira as que casam.
- Regra é um objeto do jogo de várias classes (`CommonDrop`,
  `DropBasedOnExpertMode`...), e nem toda tem `itemId`: pergunte com `in`
  antes de ler, senão é [erro](01-hooks-do-zero.md#nome-errado-é-erro).
- O Bestiário é refeito para os NPCs cuja tabela mudou: o drop novo aparece lá.

Uma regra que vale para **todo** NPC vai no `ModifyGlobalLoot`, que roda uma
vez:

```js
ModifyGlobalLoot(globalLoot) {
    globalLoot.Add(ItemDropRule.Common(ItemID.Sapphire, 1000));
}
```

Quem vem do ExMod pode usar o `GlobalLoot` do jeito de lá, sem mudar nada:

```js
export class ExampleLoot extends GlobalLoot {
    ModifyGlobalLoot() {
        this.RegisterToNPC(NPCID.Demolitionist, ItemDropRule.Common(ItemID.Dynamite, 10, 1, 3));
    }
}
```

## ModSystem: o mundo

O `ModSystem` é o que é do mod inteiro. Além das receitas
([guia 5](05-itens.md#modsystem)), ele acompanha o **mundo**:

| Método | Quando roda |
|---|---|
| `OnModLoad()` | No registro, uma vez. |
| `ClearWorld()` | Ao entrar em qualquer mundo, antes de carregá-lo (e antes de gerar um). |
| `OnWorldLoad()` | O mundo abriu. No cliente de multijogador, quando o mundo chega do servidor. |
| `LoadWorldData(tag)` | Logo depois, com os dados que o mod salvou neste mundo. |
| `PostWorldLoad()` | Depois dos dados. |
| `PreUpdateWorld()`, `PostUpdateWorld()` | A cada quadro, em volta da atualização do mundo. |
| `PreUpdateTime()`, `PostUpdateTime()` | A cada quadro, em volta do relógio do jogo. |
| `PostUpdateEverything()` | A cada quadro, no fim de tudo. |
| `SaveWorldData(tag)` | A cada save do mundo (sair, autosave). |
| `PreSaveAndQuit()` | Ao clicar em sair, antes de salvar. |
| `OnWorldUnload()` | Depois de sair do mundo. |

### Dados salvos com o mundo

O `tag` é um `TagCompound`: escreva nele como num objeto, e leia com os
métodos do tModLoader. Ele vai para um arquivo ao lado do mundo
(`<mundo>.wld.bl.json`), em JSON: número, texto, booleano, array e objeto
simples.

```js
export class DownedBossSystem extends ModSystem {
    static downedExampleBoss = false;

    ClearWorld() {
        DownedBossSystem.downedExampleBoss = false;
    }

    SaveWorldData(tag) {
        if (DownedBossSystem.downedExampleBoss) tag.downedExampleBoss = true;
    }

    LoadWorldData(tag) {
        DownedBossSystem.downedExampleBoss = tag.ContainsKey('downedExampleBoss');
    }

    NetSend(writer) {
        writer.WriteFlags(DownedBossSystem.downedExampleBoss);
    }

    NetReceive(reader) {
        [DownedBossSystem.downedExampleBoss] = reader.ReadFlags();
    }
}
```

- Zere o estado no `ClearWorld`: sem isso, o chefe derrotado num mundo
  aparece derrotado no próximo.
- Mundo sem dados do mod (novo, ou salvo sem ele) chama o `LoadWorldData` com
  a tag vazia.
- `tag.GetInt('x')`, `GetBool`, `GetFloat`, `GetString`, `GetList`,
  `ContainsKey`, `Get(chave, padrão)`: os mesmos nomes do tModLoader. O
  `ModPlayer` usa o mesmo `TagCompound` no `SaveData`/`LoadData`.
- O arquivo é do servidor: no multijogador, o cliente recebe o estado pelo
  `NetSend`/`NetReceive` (ver [Rede](#rede)).

## Multijogador: quem roda o quê

Cada aparelho roda os mods dele; os métodos rodam onde o jogo faz aquela
coisa. Conferido em host e cliente pelo teste `mpglobals`:

| | Host (ou sozinho) | Cliente |
|---|---|---|
| `SetDefaults` dos Globais | sim | sim (monta o que chega pela rede) |
| `OnSpawn` | de quem criou | de quem criou (a flecha que ele atirou) |
| `AI`, `HitEffect` | sim | sim |
| `PreKill`, `OnKill` (NPC) | sim | não: o drop é do servidor |
| `ModifyNPCLoot` | sim | sim (cada lado monta a tabela) |
| `OnWorldLoad`, `PostUpdateEverything` | sim | sim |
| `LoadWorldData`, `SaveWorldData` | sim | não: o arquivo é do servidor |
| `PreUpdateWorld`, `PostUpdateWorld`, `Pre/PostUpdateTime` | sim | não |
| `NetSend` (mundo, NPC) | manda | recebe no `NetReceive` |
| `NetSend` (projétil) | manda os dele e repassa os dos clientes | manda os dele |

## Rede

No multijogador, cada aparelho tem o **seu** estado: um campo que o servidor
mudou não muda sozinho no cliente. Para levar dados de mod de um lado a outro,
há três caminhos, todos com a forma do tModLoader.

### Junto com o que o jogo já sincroniza

O jogo manda de tempos em tempos o mundo (ao entrar e quando algo muda, como
um chefe derrotado), cada NPC e cada projétil. Os dados do mod vão logo atrás,
pelo mesmo caminho e na mesma ordem:

| Quem | Escreve | Lê | Vai com |
|---|---|---|---|
| `ModSystem` | `NetSend(writer)` | `NetReceive(reader)` | os dados do mundo, do servidor aos clientes |
| `GlobalNPC` | `NetSend(npc, writer)` | `NetReceive(npc, reader)` | cada NPC, do servidor aos clientes |
| `ModNPC` | `SendExtraAI(writer)` | `ReceiveExtraAI(reader)` | idem |
| `GlobalProjectile` | `NetSend(projectile, writer)` | `NetReceive(projectile, reader)` | cada projétil, de quem o controla |
| `ModProjectile` | `SendExtraAI(writer)` | `ReceiveExtraAI(reader)` | idem |

```js
export class NetSlime extends GlobalNPC {
    InstancePerEntity = true;
    mark = 0;

    NetSend(npc, writer) {
        writer.Write(this.mark);
    }

    NetReceive(npc, reader) {
        this.mark = reader.ReadInt32();
    }
}
```

- Leia **na mesma ordem** em que escreveu.
- Mudou e quer mandar agora: `npc.netUpdate = true` (ou `proj.netUpdate`),
  como no jogo. Para o mundo, `Terraria.NetMessage.SendData(7, ...)`, que é o
  que o jogo faz.
- O `writer` guarda valores do JavaScript como são: número, texto, booleano,
  array, objeto simples, e `Vector2` (vira `{X, Y}`; leia com `ReadVector2()`).
  `WriteFlags(a, b, ...)` e `ReadFlags()` (um array) guardam vários booleanos.
- Os nomes com tipo do tModLoader (`Write((byte)x)` vira `Write(x)`;
  `ReadByte`, `ReadInt32`, `ReadSingle`, `ReadString`...) existem para o
  código portado passar sem mudança.

### Pacotes próprios

Para uma mensagem que não acompanha nada do jogo (um pedido do cliente, um
aviso do servidor), o `Mod` do seu pacote manda e recebe:

```js
// content/main.js
export default class MeuMod extends Mod {
    HandlePacket(reader, whoAmI) {
        const pedido = reader.ReadString();
        if (pedido === 'ping') {
            const p = this.GetPacket();
            p.Write('pong');
            p.Send(whoAmI);   // só a quem pediu
        }
    }
}
// no cliente, de qualquer arquivo do mod:
const p = bl.mod.GetPacket();
p.Write('ping');
p.Send();                     // do cliente, vai ao servidor
```

- `Send()` no cliente vai ao servidor. No servidor, `Send(cliente)` vai a um,
  e `Send(-1, ignorar)` vai a todos menos `ignorar`. Sozinho, não vai a lugar
  nenhum.
- `whoAmI` é quem mandou: no servidor, o índice do cliente; no cliente, `256`
  (o servidor).
- O pacote chega no `HandlePacket` do **mesmo mod** do outro lado (pelo uid).

### Como viaja

O tModLoader escreve dentro das mensagens do jogo. Aqui, isso exigiria
reescrever código nativo; o caminho é o dos **NetModules** do próprio jogo (a
mensagem 82), com um número de módulo que o jogo não conhece. O conteúdo é o
que você escreveu, em JSON: até ~64 KB por pacote.

O custo: com algum `NetSend` registrado, as mensagens 7, 23 ou 27 entram no
JS (o filtro nativo deixa as outras de fora); e cada NPC ou projétil que o
jogo sincroniza leva um pacote a mais. Mantenha o que vai na rede
pequeno.

## Ainda não

- `NetSend`/`NetReceive` do `GlobalItem` (itens no chão e no inventário);
- `SaveData`/`LoadData` por entidade (de um item ou de um NPC morador);
- `GlobalTile`, `GlobalBuff`, `GlobalWall`;
- no `GlobalNPC`: `UpdateLifeRegen`, `SaveData`/`LoadData`, os ataques de
  morador (`TownNPCAttack*`) e os botões de conversa;
- no `ModSystem`: `ModifyWorldGenTasks`, `ModifyInterfaceLayers`, os
  `Pre/PostUpdate` de jogadores, NPCs, projéteis e itens separados.
