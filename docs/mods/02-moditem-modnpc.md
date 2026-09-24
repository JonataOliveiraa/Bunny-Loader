# 2. Conteúdo novo: ModItem, ModProjectile, ModNPC

Para **criar** coisas que o jogo não tem — um item, uma arma que atira um
projétil seu, um inimigo com drop e spawn natural — o Bunny Loader traz as
classes no formato do tModLoader: o mod **estende** a
classe, preenche o que quer e **registra**. O Bunny Loader dá um número ao tipo
novo, põe a textura e o nome no jogo e liga os hooks por você.

As classes são globais: `ModItem`, `ModProjectile`, `ModNPC`, `ModRecipe`,
`NPCLoot`, `NPCSpawnInfo`, `ModLocalization`. Nada de `import` para elas.

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

`this.Item` é o `Item` do jogo durante o `SetDefaults`: todo campo do C# está
ali (`damage`, `useTime`, `shoot`, `rare`...). Ele também chega como argumento,
`SetDefaults(item)`.

### Campos da classe

| Campo | |
|---|---|
| `Texture` | Caminho em `Textures/`, sem `.png`. Padrão: o nome da classe. |
| `DisplayName` | Texto, ou `{ 'pt-BR': ..., 'en-US': ... }`. Vazio: vem de `ItemName.<Classe>` em `Localization/*.json`, e sem isso é o nome da classe. |
| `Tooltip` | A descrição, linhas separadas por `\n`. Vazio: `ItemTooltip.<Classe>`. |
| `Item` | O item do jogo, durante `SetDefaults`. |
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
ModItem.sellPrice(platina, ouro, prata, cobre);
ModItem.buyPrice(platina, ouro, prata, cobre);
```

Raridade é número (`2` é verde): `ItemRarityID` não existe neste Terraria.

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
| `PreAI(proj)` | Antes da IA. `false` pula a IA do jogo. |
| `AI(proj)` | A IA sua, todo quadro. |
| `PostAI(proj)` | Depois da IA. |
| `PreKill(proj, timeLeft)` | `false` tira os efeitos do jogo na morte (poeira, som); o projétil morre igual. |
| `OnKill(proj, timeLeft)` | Ao morrer (poeira, som, fragmentos). |
| `OnHitNPC(proj, npc)`, `OnHitPlayer(proj, player)` | Ao acertar. |

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
| `NPC`, `Type` | O NPC do jogo durante o `SetDefaults`, e o número do tipo. |

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

## O que ainda não existe

Estas partes do tModLoader ainda não têm classe no Bunny Loader — dá para fazer
na mão, com hooks (guia 1), mas não há atalho:

- `ModBuff`, `ModPlayer`, `ModSystem`, `ModTile`, `ModPrefix`, `ModMount`, `ModBiome`;
- `GlobalItem`, `GlobalNPC`, `GlobalProjectile`;
- armadura vestida (textura no corpo) e conjuntos (`IsArmorSet`/`UpdateArmorSet`);
- morador: conversa, loja e felicidade;
- grupos de receita (`RecipeGroup`) e `OnTileCollide`.
