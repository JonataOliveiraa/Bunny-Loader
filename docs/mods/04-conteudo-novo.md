# 4. Conteúdo novo: as ideias

Para **criar** o que o jogo não tem (um item, uma arma que atira um projétil
seu, um inimigo com drop e spawn natural, um chefe, um morador, um bloco), o
Bunny Loader traz as classes no formato do **tModLoader**: o mod **estende** a
classe, preenche o que quer e **registra**. O Bunny Loader dá um número ao tipo
novo, põe a textura e o nome no jogo, e instala os hooks por você.

Este guia explica as ideias que valem para todas as classes. Os seguintes
tratam de cada uma:

| Guia | Classes |
|---|---|
| [5. Itens](05-itens.md) | `ModItem`, `ModRecipe`, `ModSystem`, tooltip, vara de pesca. |
| [6. Projéteis](06-projeteis.md) | `ModProjectile`; pets, lacaios e sentinelas. |
| [7. NPCs](07-npcs.md) | `ModNPC`: inimigos, drops, spawn, Bestiário, moradores, chefes. |
| [8. Jogador e buffs](08-jogador-e-buffs.md) | `ModPlayer`, `ModBuff`. |
| [9. Blocos](09-blocos.md) | `ModTile`. |
| [10. Sons e música](10-sons-e-musica.md) | `SoundStyle`, `SoundEngine`, `MusicLoader`. |

A lista completa do que cada classe oferece está na
[referência das classes](../referencia/classes.md).

Pré-requisito: o [guia 1](01-hooks-do-zero.md). Tudo de lá (campos, métodos,
structs) vale dentro das classes.

## O Example Mod

Os guias usam o [`samples/ExampleMod`](../../samples/ExampleMod) do começo ao
fim: abra-o ao lado. Ele é o `ExampleMod` do tModLoader portado para o Bunny
Loader, com itens, armas, munição, projéteis, pets, lacaios, NPCs, um morador
com loja, um chefe com música, blocos, buffs e receitas.

![O Mod Menu com a entrada do Example Mod e as pastas dele](../imagens/mod-menu-example-mod.jpg)

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
    Sounds/, Music/              áudio (guia 10)
```

A árvore de `Content/` e `Textures/` é convenção, não regra: o que liga uma
classe à textura dela é o campo `Texture`.

## Estender e registrar

Uma classe de conteúdo é uma classe JS que estende a base e sobrescreve os
métodos que interessam:

```js
// content/Content/Items/ExampleItem.js
export class ExampleItem extends ModItem {
    constructor() {
        super();
        this.Texture = 'Items/' + this.constructor.name;   // Textures/Items/ExampleItem.png
    }

    SetDefaults() {
        this.Item.maxStack = ModItem.CommonMaxStack;
        this.Item.value = Terraria.Item.buyPrice(0, 0, 1, 0);
    }
}
```

E o `main.js` importa e **registra**:

```js
import { ExampleItem } from './Content/Items/ExampleItem.js';
import { ExampleBulletProjectile } from './Content/Projectiles/ExampleBulletProjectile.js';
import { ExampleSlimeNPC } from './Content/NPCs/ExampleSlimeNPC.js';

ModNPC.register(ExampleSlimeNPC);
ModProjectile.register(ExampleBulletProjectile);
ModItem.register(ExampleItem);
```

As classes base são **globais**: `ModItem`, `ModProjectile`, `ModNPC`,
`ModBuff`, `ModTile`, `ModPlayer`, `ModSystem`, `ModRecipe`... Nada de
`import` para elas, nem para os [ajudantes](#ajudantes).

### O que o `register` faz

1. cria o **molde**: uma instância da sua classe;
2. reserva o **número** do tipo novo, na hora (o primeiro item de mod é o
   6147, logo depois dos do jogo), e o devolve;
3. guarda a textura, o nome e a tradução;
4. instala os hooks do jogo por trás dos métodos que a sua classe
   **sobrescreveu** (e só esses).

O jogo só fica sabendo do tipo novo um pouco depois, na tela de título,
quando as tabelas dele existem ([como](../nucleo/conteudo.md)). Por isso cada
classe tem momentos certos para cada coisa:

| Momento | Método | Para quê |
|---|---|---|
| no `register` | (o construtor da classe) | Campos da classe: `Texture`, `DisplayName`... |
| tela de título | `SetStaticDefaults()` | Tabelas por tipo: `ItemID.Sets...[this.Type]`, `Main.npcFrameCount[this.Type]`. |
| cada entidade que nasce | `SetDefaults(entidade)` | Os atributos: dano, vida, tamanho. |
| conteúdo pronto | `AddRecipes()`, `SetBestiary(...)`, `PostSetupContent()` | O que depende de tudo existir: receitas, Bestiário, outros mods. |

### A ordem importa

Registre primeiro o que os outros usam. A bala precisa do número do projétil
no `SetDefaults` dela, então o projétil vem antes. E como os números saem na
ordem de registro, ela também precisa ser a mesma em todo aparelho do
multijogador: é, desde que todos tenham os mesmos mods.

## O tipo de um conteúdo

`register` devolve o número. Depois, dá para pegá-lo de três jeitos:

```js
const tipo = ModItem.register(ExampleItem);           // na hora do registro
ModItem.getTypeByName('ExampleItem');                  // pelo nome, dentro do mod
ModContent.ItemType(ExampleItem);                      // como no tModLoader
```

### ModContent

O `ModContent` do tModLoader, com os mesmos nomes. O tipo de um conteúdo de mod
sai pela **classe** (o `<T>` de lá), pelo **nome**, ou por `'mod/Nome'` (com o
`id` do manifesto de outro mod). Não achou, devolve 0:

```js
const boia = ModContent.ProjectileType(ExampleBobber);          // a classe
const mesmo = ModContent.ProjectileType('ExampleBobber');       // o nome
const deOutro = ModContent.ItemType('outromod/EspadaDeFogo');   // outro mod
```

Pelo nome, vale o do seu mod; se não houver, o único mod que tem um conteúdo
com esse nome (se dois têm, peça por `'mod/Nome'`, e o log avisa). Há
`ItemType`, `ProjectileType`, `NPCType`, `BuffType` e `TileType`.

O **modelo** (a instância do `register`) também:

```js
ModContent.GetInstance(ExampleItem);                  // pela classe
ModContent.Find(ModItem, 'examplemod/ExampleItem');   // pelo nome; lança se não há
const r = new Ref();
if (ModContent.TryFind(ModNPC, 'examplemod/ExampleBoss', r)) bl.log(r.value.Type);
ModContent.GetModItem(tipo);                          // pelo número
```

Todo modelo tem `this.Mod`, o `Mod` de quem registrou.

As texturas do mod carregam na primeira chamada; as seguintes reusam a mesma:

```js
const asset = ModContent.Request('Textures/brilho');     // Asset<Texture2D>, como no tModLoader
const tex = asset.Value;                                  // a Texture2D
const mesma = ModContent.Texture('Textures/brilho.png');  // atalho para o .Value
```

- `Request` devolve o `Asset<Texture2D>` **do jogo**, do mesmo tipo que as
  tabelas `TextureAssets` guardam: dá para pôr numa delas.
- O caminho é a partir da pasta do mod, com ou sem `.png`; `'Items/Espada'`
  também acha `Textures/Items/Espada.png`. `'outromod/...'` pega de outro
  mod. `ModContent.HasAsset(caminho)` diz se existe.
- Carrega sempre na hora (como o `ImmediateLoad` do tModLoader), e só na
  **thread do jogo**: num hook, no `SetStaticDefaults` ou no
  `PostSetupContent`.
- Só textura. Para som, `ModContent.SoundStyle(caminho, opções)`, que é o
  mesmo que `new SoundStyle(caminho, opções)` ([guia 10](10-sons-e-musica.md)).

## Molde e instância

Como no tModLoader, a classe que você registra é um **molde**. Cada item,
projétil ou NPC do jogo daquele tipo ganha a **própria** instância, copiada do
molde, e é nela que os métodos rodam.

```mermaid
flowchart LR
    C["class Carregavel<br/>extends ModItem"] -- "register" --> M["molde<br/>(1 por tipo)"]
    M -- "Clone()" --> I1["instância do item 1<br/>this.carga = 3"]
    M -- "Clone()" --> I2["instância do item 2<br/>this.carga = 0"]
    I1 <--> E1["Item do jogo 1<br/>item.ModItem"]
    I2 <--> E2["Item do jogo 2<br/>item.ModItem"]
```

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

`ModItem.getModItem(tipo)` e `ModContent.GetInstance(Classe)` devolvem o
**molde**, não a instância de um item.

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

O estado da instância **não** vai para o save nem para a rede, e se perde
quando o item passa por um baú (que guarda só tipo, pilha e prefixo): nesses
casos a entidade nasce de novo pelo `SetDefaults`. Para o que o jogador deve
lembrar, use o `SaveData` do `ModPlayer` ([guia 8](08-jogador-e-buffs.md#dados-salvos)).

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
filhas: um campo em `Terraria.Entity` aparece em `Player`, `NPC` e
`Projectile`.

`bl.defineMethod` faz o mesmo com um método; `this` é o objeto do jogo. É daí
que sai o `player.GetModPlayer(...)`.

```js
bl.defineMethod(Terraria.Player, 'estaNoChao', function () {
    return this.velocity.Y === 0;
});
if (player.estaNoChao()) { /* ... */ }
```

Um nome que a classe já tem (campo ou método do jogo) vence o do mod.

## Texturas

O campo `Texture` é o caminho dentro de `content/Textures/`, **sem** `.png`.
Sem ele, vale o nome da classe (`Textures/ExampleItem.png`).

| Conteúdo | Formato |
|---|---|
| Item | O sprite do item. Com animação (`SetItemAnimation`), os quadros numa tira vertical. |
| Projétil | O sprite. Com `Main.projFrames[this.Type] = n`, `n` quadros numa tira vertical. |
| NPC | `Main.npcFrameCount[this.Type] = n` quadros numa tira vertical. |
| Buff | 32x32. |
| Bloco | A folha de quadros do jogo: 288x270, quadros de 16x16 com 2 px de margem. |

As texturas são desenhadas como pixel art (sem suavização), como as do jogo.

## Tradução

`Localization/<idioma>.json`, um por idioma (`pt-BR`, `en-US`, `es-ES`,
`de-DE`, `fr-FR`, `it-IT`, `ru-RU`, `pl-PL`, `ja-JP`, `ko-KR`, `zh-Hans`,
`zh-Hant`):

```json
{
  "ItemName":       { "ExampleItem": "Exemplo de Item" },
  "ItemTooltip":    { "ExampleItem": "Descrição do item" },
  "ProjectileName": { "ExampleBulletProjectile": "Bala de Exemplo" },
  "NPCName":        { "ExampleSlimeNPC": "Slime de Exemplo" },
  "BuffName":       { "ExampleDefenseBuff": "Defesa de Exemplo" },
  "BuffDescription":{ "ExampleDefenseBuff": "+{0} de defesa" },
  "Bestiary":       { "ExampleSlimeNPC": "Um slime azul-claro." }
}
```

A chave é o nome da **classe**. O jogo mostra o idioma dele; sem esse idioma,
vale o `en-US`, e sem nada, o nome da classe. Os campos `DisplayName` e
`Tooltip` da classe, se preenchidos, vencem o arquivo (texto, ou
`{ 'pt-BR': ..., 'en-US': ... }`).

Para um texto seu (a fala de um morador, a plaquinha do Bestiário):

| | |
|---|---|
| `ModLocalization.GetTextValue('Secao.Chave')` | O **texto**, no idioma do jogo. |
| `ModLocalization.Translate('Secao.Chave')` | Registra o texto e devolve a **chave**, para o que o jogo pede por chave (o Bestiário). |
| `ModLocalization.Register(chave, texto)` | Registra um texto direto. |

## O Mod Menu

Todo item, NPC e buff registrado já aparece no **Mod Menu** (o coelho na tela
do jogo), numa entrada com o nome do mod, nas pastas *Itens*, *NPCs* e
*Buffs*, com o nome traduzido e o sprite:

![A pasta de itens do Example Mod no Mod Menu](../imagens/mod-menu-itens.jpg)

Para separar em mais pastas:

```js
const armas = bl.menu.itemCategory('Armas', 'Textures/Items/ExampleGun.png');
bl.menu.addItem(armas, ModItem.getTypeByName('ExampleGun'));
```

(`bl.menu.npcCategory` e `bl.menu.addNpc` para NPCs.)

Para **esconder** um item, NPC ou buff do Mod Menu (das pastas do mod e das
listas gerais), diga no `SetStaticDefaults`:

```js
SetStaticDefaults() {
    this.HideFromModMenu = true;   // ModItem, ModNPC ou ModBuff
}
```

O jeito do tModLoader também esconde, e o jogo o entende nas listas dele:
`ItemID.Sets.Deprecated[this.Type] = true` para item, e um
`NPCBestiaryDrawModifiers` com `Hide = true` no
`NPCID.Sets.NPCBestiaryDrawOffset` para NPC (que também some do Bestiário).

## Saves: desligar o mod não perde nada

Conteúdo de mod é salvo **pelo nome** (mod + classe), num arquivo ao lado do
save do jogo. O arquivo do jogo nunca leva um tipo de mod. Por isso:

- **desligar o mod não perde nada**: o item vira um "?" no inventário, o bloco
  vira ar, o morador some; ao religar, tudo volta;
- o mundo e o personagem **abrem no Terraria sem o Bunny Loader**;
- a ordem dos mods pode mudar à vontade: o conteúdo volta pelo nome, não pelo
  número.

Detalhes de cada save em [conteúdo por dentro](../nucleo/conteudo.md#os-saves-o-mundo-abre-sem-o-mod).

## Arrays do jogo

Um array do jogo (`Main.recipe`, `ItemID.Sets.IsAMaterial`...) tem índice,
`.length` e `cloneResized(n)`: uma cópia do mesmo tipo com `n` posições. O que
cabe vem do original, e o resto fica 0/null. Para trocar a tabela do jogo,
atribua:

```js
Terraria.Main.recipe = Terraria.Main.recipe.cloneResized(4000);
```

Onde o jogo espera um array (`int[]`, `string[]`...), um array JS também serve:
`new RecipeGroup(nome, [1, 2, 3])` recebe um `int[]` montado na hora.

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
| `ItemRarityID`, `ProjAIStyleID`, `NPCAIStyleID` | Os números que este Terraria não traz: `ItemRarityID.Pink`, `ProjAIStyleID.GolfBall`, `NPCAIStyleID.Slime`... |
| `Terraria.ID.DustID` | Além dos nomes do jogo, os que o tModLoader acrescenta: `DustID.PinkFairy`, `DustID.Firework_Blue`... |

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

## Vindo do tModLoader

O formato é o mesmo, com as diferenças do JavaScript e do celular:

| tModLoader (C#) | Bunny Loader (JS) |
|---|---|
| `public class X : ModItem` | `export class X extends ModItem` + `ModItem.register(X)` |
| carregamento automático | `register` explícito, na ordem certa |
| `Item.damage = 10;` | `this.Item.damage = 10;` |
| `ModContent.ItemType<X>()` | `ModContent.ItemType(X)` |
| `ref int damage` | `Ref` com `.value` ([guia 2](02-ref-e-out.md)) |
| `Texture => "Mod/Items/X"` | `this.Texture = 'Items/X'` |
| `.hjson` | `Localization/<idioma>.json` |
| `player.HasBuff(t)` | `player.FindBuffIndex(t) >= 0` (o celular não tem `HasBuff`) |
| `Main.ActiveNPCs` | percorrer `Terraria.Main.npc` pulando `!npc.active` |
