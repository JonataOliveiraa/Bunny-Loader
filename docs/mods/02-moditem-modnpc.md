# 2. Conteúdo novo: ModItem, ModProjectile, ModNPC

Para **criar** coisas que o jogo não tem — um item, uma arma que atira um
projétil seu, um inimigo com drop e spawn natural — o Bunny Loader traz as
classes no formato do tModLoader: o mod **estende** a
classe, preenche o que quer e **registra**. O Bunny Loader dá um número ao tipo
novo, põe a textura e o nome no jogo e liga os hooks por você.

As classes são globais: `ModItem`, `ModProjectile`, `ModNPC`, `ModRecipe`,
`NPCLoot`, `NPCSpawnInfo`, `ModLocalization`. Nada de `import` para elas —
nem para os [ajudantes](#ajudantes) (`Vector2`, `Rand`, `ItemRarityID`...).

O guia usa o `samples/ExampleMod` do começo ao fim — abra-o ao lado. Tudo do
[guia 1](01-hooks-do-zero.md) (campos, métodos, structs) vale aqui dentro.

## A estrutura

```
ExampleMod/
  manifest.json
  icon.png
  content/
    main.js                      registra tudo
    Content/
      Items/ExampleItem.js       uma classe por arquivo
      Items/Weapons/Melee/ExampleMeleeWeapon.js
      Projectiles/ExampleBulletProjectile.js
      NPCs/ExampleSlimeNPC.js
    Textures/
      Items/ExampleItem.png      as texturas, na mesma árvore
      Projectiles/ExampleBulletProjectile.png
      NPCs/ExampleSlimeNPC.png
    Localization/
      pt-BR.json                 nomes e descrições, por idioma
      en-US.json
```

A árvore de `Content/` e `Textures/` é convenção, não regra: o que liga uma
classe à textura dela é o campo `Texture`.

`content/main.js` importa as classes e registra:

```js
import { ExampleItem } from './Content/Items/ExampleItem.js';
import { ExampleBulletProjectile } from './Content/Projectiles/ExampleBulletProjectile.js';
import { ExampleSlimeNPC } from './Content/NPCs/ExampleSlimeNPC.js';

ModNPC.register(ExampleSlimeNPC);
ModProjectile.register(ExampleBulletProjectile);
ModItem.register(ExampleItem);
```

**A ordem importa**: registre primeiro o que os outros usam. A bala precisa do
número do projétil no `SetDefaults` dela, então o projétil vem antes. E como os
números saem nessa ordem, ela também precisa ser a mesma em todo aparelho do
multijogador — é, desde que todos tenham os mesmos mods.

`register` devolve o número do tipo novo. Depois dá para pegá-lo pelo nome da
classe: `ModItem.getTypeByName('ExampleItem')`.

### Uma instância por entidade

Como no tModLoader, a classe que você registra é um **molde**. Cada item,
projétil ou NPC do jogo daquele tipo ganha a **própria** instância, copiada do
molde, e é nela que os métodos rodam:

- `this.Item`, `this.Projectile`, `this.NPC` é a entidade daquela instância;
- `item.ModItem`, `proj.ModProjectile`, `npc.ModNPC` é a instância da entidade;
- campo que você puser em `this` é **daquele** item: dá para guardar carga,
  contador, alvo, sem uma variável global indexada por item.

```js
export class Carregavel extends ModItem {
    SetDefaults() {
        this.carga = 0;               // cada item começa com a sua
    }
    UseItem(item, player) {
        this.carga++;                 // só deste item
    }
}

// de fora: o ModItem de um item qualquer
const m = player.inventory[0].ModItem;
if (m instanceof Carregavel) bl.log(m.carga);
```

`ModItem.getModItem(tipo)` devolve o **molde**, não a instância de um item.

Quando o jogo copia um item (`Item.Clone`), a cópia ganha um `Clone()` da
instância do original, com o estado dela. O `Clone` padrão copia os campos
rasos (o `MemberwiseClone` do C#); sobrescreva se tiver array ou objeto seu
que precise de cópia própria:

```js
Clone(newItem) {
    const c = super.Clone(newItem);
    c.historico = [...this.historico];
    return c;
}
```

O estado da instância **não** vai para o save nem para a rede, e se perde quando
o item passa por um baú (que guarda só tipo, pilha e prefixo): nesses casos a
entidade nasce de novo pelo `SetDefaults`.

### Campos em classes do jogo

O `item.ModItem` é um caso de algo geral: `bl.defineField` põe um campo novo
numa classe do jogo, para o mod guardar o que quiser em cada objeto:

```js
bl.defineField(Terraria.Player, 'combo');
const p = Terraria.Main.player[Terraria.Main.myPlayer];
p.combo = (p.combo || 0) + 1;
```

O valor fica numa tabela ao lado do objeto (o jogo não deixa a classe crescer)
e some sozinho quando o jogo descarta o objeto. Vale também para as classes
filhas: um campo em `Terraria.Entity` aparece em `Player`, `NPC` e `Projectile`.

## ModItem

```js
export class ExampleItem extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/' + this.constructor.name;   // Textures/Items/ExampleItem.png
    }

    SetDefaults() {
        this.Item.maxStack = ModItem.CommonMaxStack;        // 9999
        this.Item.value = Terraria.Item.buyPrice(0, 0, 1, 0);
    }

    AddRecipes() {
        this.CreateRecipe(999)
            .AddIngredient(Terraria.ID.ItemID.DirtBlock, 10)
            .AddTile(Terraria.ID.TileID.WorkBenches)
            .Register();
    }
}
```

`this.Item` é o `Item` do jogo desta instância: todo campo do C# está ali
(`damage`, `useTime`, `shoot`, `rare`...). Ele também chega como argumento,
`SetDefaults(item)`.

### Campos da classe

| Campo | |
|---|---|
| `Texture` | Caminho em `Textures/`, sem `.png`. Padrão: o nome da classe. |
| `DisplayName` | Texto, ou `{ 'pt-BR': ..., 'en-US': ... }`. Vazio: vem de `ItemName.<Classe>` em `Localization/*.json`, e sem isso é o nome da classe. |
| `Tooltip` | A descrição, linhas separadas por `\n`. Vazio: `ItemTooltip.<Classe>`. |
| `Item` | O item do jogo desta instância (no molde, `undefined`). |
| `Type` | O número deste item, depois do `register`. |

### Métodos que você pode escrever

| Método | Quando |
|---|---|
| `SetStaticDefaults()` | Uma vez, com o tipo já no jogo. Para tabelas por tipo (`Terraria.ID.ItemID.Sets...[this.Type]`). |
| `SetDefaults(item)` | Todo item deste tipo que nasce. Aqui vão os atributos. |
| `AddRecipes()` | Uma vez, quando as receitas do jogo já existem. |
| `PostSetupContent()` | Uma vez, com todo o conteúdo de mod no jogo. |
| `ModifyTooltipLines()` | Por idioma, com `this.TooltipLines` preenchido — mude as linhas. |
| `CanUseItem(item, player)` | Devolva `false` para impedir o uso. |
| `UseItem(item, player)` | A cada uso. |
| `HoldItem(item, player)` | Todo quadro com o item na mão. |
| `UseStyle(...)`, `HoldStyle(...)` | Todo quadro de uso / de segurar, depois do estilo do jogo. |
| `HoldoutOffset(item, player)` | Desloca a arma na mão: devolva `{ X, Y }` em pixels. |
| `CanShoot(item, player)` | `false`: usa, mas não atira. |
| `ModifyShootStats(item, player, stats)` | Antes de cada projetil: mude `stats.position`, `velocity`, `type`, `damage`, `knockBack`. |
| `Shoot(item, player, position, velocity, type, damage, knockBack)` | `false`: o projétil do jogo não nasce (crie os seus aqui). |
| `OnHitNPC(item, player, npc, damageDone, knockBack, crit)` | Acerto corpo a corpo. |
| `UpdateInventory(item, player)` | Todo quadro, no inventário. |
| `UpdateEquip(item, player)`, `UpdateAccessory(item, player, hideVisual)` | Todo quadro, equipado. |
| `GetAlpha(item, lightColor)` | No chão: devolva a `Color` com que ele é desenhado (`Color.White` = brilha no escuro). |

Só os métodos que você escrever custam alguma coisa: o hook do jogo por trás de
cada um só é instalado quando alguma classe o sobrescreve, e só é chamado para
itens de mod.

### Atalhos

```js
this.SetWeaponValues(50, 6, 6);        // dano, repulsão, crítico
this.SetDefaultWeaponStyle(20, true);  // useTime (e useAnimation), autoReuse
this.SetShopValues(2, Terraria.Item.sellPrice(0, 1, 0, 0));  // raridade, preço
this.CloneDefaults(Terraria.ID.ItemID.Minishark);            // copia um item do jogo
this.DefaultToPlaceableTile(tileType);
this.DefaultToFood(buffType, buffTime);
this.DefaultToGolfBall(projType);                            // tee e taco, como as do jogo
this.SetItemAnimation(4, 6);                                 // no SetStaticDefaults: 4 quadros, 6 ticks cada
ModItem.sellPrice(platina, ouro, prata, cobre);
ModItem.buyPrice(platina, ouro, prata, cobre);
```

Raridade: `ItemRarityID.Green`, `ItemRarityID.Pink`... (ver [ajudantes](#ajudantes)).

### Uma arma que atira

```js
export class ExampleGun extends ModItem {
    SetDefaults() {
        this.Item.ranged = true;
        this.Item.shoot = Terraria.ID.ProjectileID.PurificationPowder;  // trocado pela munição
        this.Item.shootSpeed = 10;
        this.Item.useAmmo = Terraria.ID.AmmoID.Bullet;
        this.SetWeaponValues(20, 5, 0);
        this.SetDefaultWeaponStyle(10, true);
        this.Item.noMelee = true;
        this.Item.UseSound = Terraria.ID.SoundID.Item41;
    }

    HoldoutOffset() {
        return { X: -18, Y: 0 };
    }
}
```

E a munição aponta para o projétil de mod:

```js
this.Item.shoot = ModProjectile.getTypeByName('ExampleBulletProjectile');
this.Item.ammo = Terraria.ID.AmmoID.Bullet;
```

## Receitas

`this.CreateRecipe(quantidade)` dentro do `AddRecipes`, e uma corrente:

```js
this.CreateRecipe()
    .AddIngredient(Terraria.ID.ItemID.IronBar, 5)
    .AddIngredient(ModItem.getTypeByName('ExampleItem'), 10)
    .AddTile(Terraria.ID.TileID.Anvils)
    .SetProperty('needWater', true)   // needLava, needHoney, needSnowBiome, alchemy...
    .Register();
```

Até 15 ingredientes por receita. Receita para um item **do jogo** também vale:
`new ModRecipe().SetResult(tipo, n).AddIngredient(...).Register()`, dentro de um
`AddRecipes`.

## ModProjectile

```js
export class ExampleBulletProjectile extends ModProjectile {
    constructor() {
        super();
        this.Texture = 'Projectiles/' + this.constructor.name;
    }

    SetDefaults() {
        this.Projectile.width = 8;
        this.Projectile.height = 8;
        this.Projectile.aiStyle = 1;          // a IA de bala do jogo
        this.Projectile.friendly = true;
        this.Projectile.ranged = true;
        this.Projectile.penetrate = 5;
        this.Projectile.timeLeft = 600;
    }
}
```

| Método | Quando |
|---|---|
| `SetStaticDefaults()` | Uma vez. `Terraria.Main.projFrames[this.Type] = n` aqui diz quantos quadros a textura tem. |
| `SetDefaults(proj)` | Todo projétil deste tipo que nasce. |
| `OnSpawn(proj)` | Uma vez, no primeiro quadro de vida. |
| `PreAI(proj)` | Antes da IA. `false` pula a IA do jogo. |
| `AI(proj)` | A IA sua, todo quadro. |
| `PostAI(proj)` | Depois da IA. |
| `PreKill(proj, timeLeft)` | `false` tira os efeitos do jogo na morte (poeira, som); o projétil morre igual. |
| `OnKill(proj, timeLeft)` | Ao morrer (poeira, som, fragmentos). |
| `OnTileCollide(proj, oldVelocity)` | Bateu num bloco e ia morrer: `false` o mantém vivo (para quicar, mude `proj.velocity`). |
| `OnHitNPC(proj, npc)`, `OnHitPlayer(proj, player)` | Ao acertar. |
| `Colliding(proj, projHitbox, targetHitbox)` | `true`/`false` decide o acerto; `undefined` deixa o do jogo. |
| `CanDamage(proj)` | `false`: não causa dano. |
| `ModifyDamageHitbox(proj, hitbox)` | Mude o `hitbox` (Rectangle) para o dano usar outra área. |
| `CanCutTiles(proj)`, `CutTiles(proj)` | Cortar grama e teia. |
| `GetAlpha(proj, lightColor)` | A cor final (uma `Color`), ou `undefined` para a do jogo. |
| `PreDraw(proj, lightColor)`, `PostDraw(proj, lightColor)` | Desenho: `false` no `PreDraw` não desenha o do jogo — desenhe o seu com `Main.EntitySpriteDraw`. |
| `CanUseGrapple(player, type)`, `UseGrapple(player, type)` | Gancho de escalar, no **molde**, antes de lançar: `false` impede; `UseGrapple` devolve o tipo a lançar (e pode recolher um gancho velho para limitar quantos ficam presos). |
| `GrappleCanLatchOnTo(proj, player, tile)` | `true`/`false`: o gancho agarra neste bloco; `undefined` = o do jogo (bloco sólido). |

Campos e atalhos:

- `this.AIType = ProjectileID.Sunfury`: usa a IA daquele projétil do jogo (o
  tipo é trocado só durante a IA). É o que faz um mangual de mod balançar
  como o Sol Fundido.
- `this.CloneDefaults(ProjectileID.Spear)`: copia os valores de um projétil do jogo.
- `this.DefaultToSpear()`, `DefaultToYoyo()`, `DefaultToFlail()`, `DefaultToWhip()`,
  `DefaultToDrillOrChainsaw()`, `DefaultToKite()`: os padrões do jogo para cada
  família de projétil segurado. No item, `this.DefaultToWhip(projType, dano,
  repulsao, velocidade)` e `this.DefaultToSpear(projType, velocidade, tempo)`.
- `new ProjAI(proj)` (ou `new ProjAI(proj, true)` para o `localAI`): o vetor
  `ai` como `ai[0]`, `ai[1]`, `ai[2]` — no jogo ele é um struct de 3 floats.

No `Shoot` do item, `position` e `velocity` são `Vector2` do jogo: dá para
repassá-los direto a um `NewProjectile`.

Método de instância guardado numa variável perde o objeto: use uma função que
o chama nele.

```js
const sb = Terraria.Main.spriteBatch;
const Draw = (...args) => sb['void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)'](...args);
```

Com `aiStyle` de um projétil do jogo, ele já se comporta como aquele; com
`aiStyle = 0`, o movimento é todo seu no `AI`.

## ModNPC

O `ExampleSlimeNPC` é um slime com dois quadros de animação, drop, spawn natural
de dia e entrada no Bestiário:

```js
const { SoundID, NPCID, ItemID } = Terraria.ID;
const { ItemDropRule } = Terraria.GameContent.ItemDropRules;
const Common = ItemDropRule['IItemDropRule Common(int itemId, int chanceDenominator, int minimumDropped, int maximumDropped)'];

export class ExampleSlimeNPC extends ModNPC {
    constructor() {
        super();
        this.Texture = 'NPCs/' + this.constructor.name;
    }

    SetStaticDefaults() {
        Terraria.Main.npcFrameCount[this.Type] = 2;   // a textura tem 2 quadros, em pé
    }

    SetDefaults() {
        this.NPC.aiStyle = 1;          // IA de slime do jogo
        this.NPC.damage = 7;
        this.NPC.defense = 2;
        this.NPC.lifeMax = 25;
        this.NPC.HitSound = SoundID.NPCHit1;
        this.NPC.DeathSound = SoundID.NPCDeath1;
        this.NPC.value = ModNPC.NPCValue(0, 0, 0, 25);
        this.AnimationType = NPCID.BlueSlime;   // anima como o slime azul
    }

    SpawnChance(info) {
        if (!info.CommonEnemy || !info.Day || !info.AboveSurface) return 0;
        return info.Rain ? 0.05 : 0.1;
    }

    ModifyNPCLoot(npcLoot) {
        npcLoot.Add(Common(ItemID.Gel, 1, 1, 2));
        npcLoot.Add(Common(ModItem.getTypeByName('ExampleItem'), 3, 5, 10));
    }
}
```

### Campos da classe

| Campo | |
|---|---|
| `Texture`, `DisplayName` | Como no item. O nome vem de `NPCName.<Classe>`. |
| `AnimationType` | Anima como este NPC do jogo (`0` = não anima). Vale no `SetDefaults`. |
| `HideFromBestiary` | `true`: sem entrada no Bestiário. |
| `NPC`, `Type` | O NPC do jogo desta instância, e o número do tipo. |

### Métodos que você pode escrever

| Método | Quando |
|---|---|
| `SetStaticDefaults()` | Uma vez. `Terraria.Main.npcFrameCount[this.Type] = n`: quadros da textura (tira vertical). |
| `SetDefaults(npc)` | Todo NPC deste tipo que nasce. Vida, dano e defesa aqui; a escala de Expert/Mestre é aplicada depois. |
| `ApplyBuffImmunity(npc)` | `npc.buffImmune[id] = true`. |
| `ModifyNPCLoot(npcLoot)` | Os drops: `npcLoot.Add(regra)`. |
| `SetBestiary(database, bestiaryEntry)` | A entrada no Bestiário: bioma, hora, texto. |
| `SpawnChance(info)` | Peso no spawn natural (`0` = não nasce). Ver abaixo. |
| `SpawnNPC(x, y)` | Como nasce quando é sorteado. Padrão: no ponto do sorteio. |
| `HitEffect(npc, hitDirection, damage)` | A cada golpe (poeira, gore). `npc.life <= 0` é o golpe que mata. |
| `PreAI(npc)`, `AI(npc)`, `PostAI(npc)` | A IA, todo quadro. `PreAI` devolvendo `false` pula a do jogo. |
| `FindFrame(npc, frameHeight)` | Animação sua: mude `npc.frame.Y`. |
| `CheckActive(npc)` | `false`: não some quando longe do jogador. |
| `PreKill(npc)`, `OnKill(npc)` | Na morte. `PreKill` devolvendo `false` cancela o drop. |

### Drops

`ItemDropRule` é a classe do jogo; pegue os métodos pela assinatura:

```js
const { ItemDropRule } = Terraria.GameContent.ItemDropRules;
const Common = ItemDropRule['IItemDropRule Common(int itemId, int chanceDenominator, int minimumDropped, int maximumDropped)'];
const NormalvsExpert = ItemDropRule['IItemDropRule NormalvsExpert(int itemId, int chanceDenominatorInNormal, int chanceDenominatorInExpert)'];

npcLoot.Add(Common(ItemID.Gel, 1, 1, 2));              // sempre, 1 a 2
npcLoot.Add(Common(meuItem, 3, 5, 10));                // 1 em 3, 5 a 10
npcLoot.Add(NormalvsExpert(ItemID.SlimeStaff, 10000, 7000));
```

### Spawn natural

`SpawnChance(info)` devolve um peso. Quando o jogo faz nascer um inimigo, o
sorteio é entre o do jogo (peso 1) e os de mod com peso maior que zero. `0.1`
é "de vez em quando"; `1` empata com o do jogo.

`info` é um `NPCSpawnInfo`, com:

- `SpawnTileX`, `SpawnTileY`, `Player`;
- altura: `Sky`, `Surface`, `Underground`, `Cavern`, `Underworld`, `AboveSurface`, `BelowSurface`;
- hora e evento: `Day`, `Night`, `Rain`, `SlimeRain`, `BloodMoon`, `SolarEclipse`, `PumpkinMoon`, `FrostMoon`, `AnyEvent`, `Invasion`, `AnyTower`;
- mundo: `HardMode`, `Expert`, `Master`;
- bioma: `Corruption`, `Crimson`, `Hallow` (e `Underground...` de cada um), `Snow`, `Ice`, `Jungle`, `UndergroundJungle`, `Mushroom`, `SurfaceMushroom`, `Ocean`, `Desert`, `DesertCave`, `Meteor`, `Marble`, `Granite`, `Graveyard`, `Dungeon`, `Lihzahrd`;
- `CommonEnemy`: sem invasão, evento ou pilar — o caso normal.

### Bestiário

```js
const { BestiaryDatabaseNPCsPopulator, FlavorTextBestiaryInfoElement } = Terraria.GameContent.Bestiary;

SetBestiary(database, bestiaryEntry) {
    const { SpawnConditions } = BestiaryDatabaseNPCsPopulator.CommonTags;
    bestiaryEntry.Info.Add(SpawnConditions.Biomes.Surface);
    bestiaryEntry.Info.Add(SpawnConditions.Times.DayTime);

    const texto = FlavorTextBestiaryInfoElement.new();
    texto['void .ctor(string languageKey)'](ModLocalization.Translate('Bestiary.ExampleSlimeNPC'));
    bestiaryEntry.Info.Add(texto);
}
```

O retrato, os drops e a contagem de mortes o Bestiário monta sozinho.

## Tradução

`Localization/<idioma>.json`, um por idioma (`pt-BR`, `en-US`, `es-ES`, `de-DE`,
`fr-FR`, `it-IT`, `ru-RU`, `pl-PL`, `ja-JP`, `ko-KR`, `zh-Hans`, `zh-Hant`):

```json
{
  "ItemName":       { "ExampleItem": "Exemplo de Item" },
  "ItemTooltip":    { "ExampleItem": "Descrição do item" },
  "ProjectileName": { "ExampleBulletProjectile": "Bala de Exemplo" },
  "NPCName":        { "ExampleSlimeNPC": "Slime de Exemplo" },
  "Bestiary":       { "ExampleSlimeNPC": "Um slime azul-claro." }
}
```

A chave é o nome da **classe**. O jogo mostra o idioma dele; sem o idioma, vale
o `en-US`, e sem nada, o nome da classe.

Para um texto seu (como o do Bestiário): `ModLocalization.Translate('Secao.Chave')`
lê dos arquivos e devolve a chave registrada. `ModLocalization.Register(chave, texto)`
registra um texto direto.

## O Mod Menu

Todo item e NPC registrado já aparece no Mod Menu, dentro de uma entrada com o
nome do mod, nas pastas *Itens* e *NPCs*. Para separar em mais pastas:

```js
const armas = bl.menu.itemCategory('Armas', 'Textures/Items/ExampleGun.png');
bl.menu.addItem(armas, ModItem.getTypeByName('ExampleGun'));
```

(`bl.menu.npcCategory` e `bl.menu.addNpc` para NPCs.)

## Save

Item de mod no inventário, no cofre e nos baús é salvo pelo **nome** (mod +
classe), num arquivo ao lado do save do jogo. Desligar o mod não perde nada: o
item vira um "?" e volta ao normal quando o mod é religado.

## Genéricos

`List<Vector2>`, `Dictionary<int, int>`: `makeGeneric` na classe genérica, com
as classes dos tipos:

```js
const List = System.Collections.Generic.List.makeGeneric(Vector2.Type);
const pontos = List.new();
pontos['void .ctor()']();

const Dict = System.Collections.Generic.Dictionary.makeGeneric(System.Int32, System.Int32);
```

Só vale para uma combinação que o próprio jogo usa (o código dela precisa
existir no binário); para as outras, `makeGeneric` lança dizendo qual.

## Ajudantes

Globais, com os nomes do tModLoader:

| | |
|---|---|
| `Vector2` | `Vector2.new(x, y)`, `Add`, `Subtract`, `Multiply`, `Divide` (vetor ou número), `Length`, `Distance`, `Normalize`, `SafeNormalize`, `DirectionTo`, `RotatedBy`, `RotatedByRandom`, `ToRotation`, `ToRotationVector2`, `Lerp`, `Dot`, `ToTileCoordinates`, `Clone`. Devolvem o `Vector2` do jogo; aceitam também `{ X, Y }`. |
| `MathHelper` | `Pi`, `TwoPi`, `PiOver2`, `ToRadians`, `ToDegrees`, `Clamp`, `Lerp`, `SmoothStep`, `WrapAngle`. |
| `Rand` | O sorteio do jogo: `Next(max)`, `Next(min, max)`, `NextFloat()`, `NextFloat(max)`, `NextBool(umEm)`, `NextChance(p)`, `NextSign()`, `NextFromList(lista)`, `NextVector2Circular(rx, ry)`, `NextVector2Unit()`. |
| `Color` | `Color.new(r, g, b, a)`, `Color.White`, `Color.SkyBlue`... (qualquer cor do XNA, sempre uma cópia), `Multiply`, `Lerp`, `ToVector3`. |
| `Rectangle` | `Rectangle.new(x, y, w, h)`, `Size`, `Center`, `Contains`, `Intersects`. |
| `ProjAI` | `new ProjAI(proj)`: `proj.ai` como vetor (ver ModProjectile). |
| `ItemRarityID`, `ProjAIStyleID`, `NPCAIStyleID` | Os números que este Terraria não traz: `ItemRarityID.Pink`, `ProjAIStyleID.GolfBall`, `NPCAIStyleID.Slime`... |

```js
Shoot(item, player, position, velocity, type, damage, knockBack) {
    for (let i = 0; i < 8; i++) {
        let v = Vector2.RotatedByRandom(velocity, MathHelper.ToRadians(15));
        v = Vector2.Multiply(v, 1 - Rand.NextFloat(0.3));
        NewProjectile(player.GetProjectileSource_Item(item), position.X, position.Y, v.X, v.Y,
                      type, damage, knockBack, player.whoAmI, 0, 0, 0, null);
    }
    return false;
}
```

## O que ainda não existe

Estas partes do tModLoader ainda não têm classe no Bunny Loader — dá para fazer
na mão, com hooks (guia 1), mas não há atalho:

- `ModBuff`, `ModPlayer`, `ModSystem`, `ModTile`, `ModPrefix`, `ModMount`, `ModBiome`;
- `GlobalItem`, `GlobalNPC`, `GlobalProjectile`;
- armadura vestida (textura no corpo) e conjuntos (`IsArmorSet`/`UpdateArmorSet`);
- morador: conversa, loja e felicidade;
- grupos de receita (`RecipeGroup`) e `OnTileCollide`.
