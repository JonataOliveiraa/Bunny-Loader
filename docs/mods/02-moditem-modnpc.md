# 2. Conteúdo novo: ModItem, ModProjectile, ModNPC

Para **criar** coisas que o jogo não tem — um item, uma arma que atira um
projétil seu, um inimigo com drop e spawn natural — o Bunny Loader traz as
classes no formato do tModLoader: o mod **estende** a
classe, preenche o que quer e **registra**. O Bunny Loader dá um número ao tipo
novo, põe a textura e o nome no jogo e liga os hooks por você.

As classes são globais: `ModItem`, `ModProjectile`, `ModNPC`, `ModBuff`, `ModPlayer`, `ModRecipe`, `ModSystem`,
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
classe: `ModItem.getTypeByName('ExampleItem')`, ou pelo `ModContent`, como no
tModLoader (abaixo).

### ModContent

O `ModContent` do tModLoader, com os mesmos nomes. O tipo de um conteúdo de mod
sai pela **classe** (o `<T>` de lá), pelo **nome** ou por `'mod/Nome'` (o `id`
do manifesto de outro mod); não achou, devolve 0:

```js
const boia = ModContent.ProjectileType(ExampleBobber);          // a classe
const mesmo = ModContent.ProjectileType('ExampleBobber');       // o nome
const deOutro = ModContent.ItemType('outromod/EspadaDeFogo');   // outro mod
```

Pelo nome, vale o do seu mod; se não houver, o único mod que tem um conteúdo com
esse nome (se dois têm, peça por `'mod/Nome'`). Há `ItemType`, `ProjectileType`,
`NPCType`, `BuffType` e `TileType`; `GetInstance(Classe)` devolve o modelo (a
instância do `register`), `Find(ModItem, 'mod/Nome')` e `TryFind(ModItem,
'mod/Nome', ref)` o procuram pelo nome, e `GetModItem(tipo)` (e os outros) pelo
tipo. Todo modelo tem `this.Mod`, o `Mod` de quem registrou.

Texturas do mod carregam na primeira chamada e as seguintes reusam a mesma:

```js
const brilho = ModContent.Texture('Textures/brilho.png');   // a Texture2D
const asset = ModContent.Request('Textures/brilho');        // como no tModLoader: asset.Value
```

O caminho é a partir da pasta do mod (com ou sem `.png`; `'Items/Espada'` também
acha `Textures/Items/Espada.png`); `'outromod/...'` pega de outro mod.
`ModContent.HasAsset(caminho)` diz se existe. Carregue dentro de um hook, no
`SetStaticDefaults` ou no `PostSetupContent` — textura só nasce com o jogo
rodando. `ModContent.SoundStyle(caminho, opções)` é o mesmo que
`new SoundStyle(caminho, opções)` ([guia 5](05-sons-e-musica.md)).

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

`bl.defineMethod` faz o mesmo com um método: `this` é o objeto do jogo. É daí
que sai o `player.GetModPlayer(...)`.

```js
bl.defineMethod(Terraria.Player, 'estaNoChao', function () {
    return this.velocity.Y === 0;
});
if (player.estaNoChao()) { /* ... */ }
```

Nome que a classe já tem (campo ou método do jogo) vence o do mod.

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
| `AddRecipeGroups()` | Uma vez, antes de qualquer receita (de todos os mods). |
| `AddRecipes()` | Uma vez, quando as receitas do jogo já existem. |
| `OnCraft(item, player, recipe)` | Ao criar este item no menu; `item` é o que o jogador vai receber, e ainda dá para mudar. |
| `PostSetupContent()` | Uma vez, com todo o conteúdo de mod no jogo. |
| `ModifyTooltipLines()` | Por idioma, com `this.TooltipLines` preenchido — mude as linhas. |
| `ModifyTooltips(item, tooltips)` | Toda vez que o tooltip aparece: mude, pinte, insira e tire linhas (ver [Tooltip colorido](#tooltip-colorido)). |
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
| `UpdateEquip(item, player)`, `UpdateAccessory(item, player, vanity, hideVisual)` | Todo quadro, equipado. No acessório, `vanity` é o slot de vaidade (só o visual) e `hideVisual` o olho fechado. |
| `GetAlpha(item, lightColor)` | No chão: devolva a `Color` com que ele é desenhado (`Color.White` = brilha no escuro). |
| `ModifyFishingLine(item, bobber, lineOriginOffset, lineColor)` | Vara de pesca: de onde a linha sai e a cor dela, por `Ref` (ver [Vara de pesca](#vara-de-pesca)). |

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

### Vara de pesca

O jogo só sabe onde fica a ponta das varas dele; sem o `ModifyFishingLine`, a
linha de uma vara de mod sai do centro do jogador. Como no tModLoader, os dois
últimos parâmetros são `Ref`: `lineOriginOffset.value` é de onde a linha sai,
em pixels a partir do centro do jogador olhando para a direita (o Bunny Loader
espelha para a esquerda e para a gravidade invertida); `lineColor.value` é a
cor, e a linha colorida que o jogador equipou ganha dela.

```js
ModifyFishingLine(item, bobber, lineOriginOffset, lineColor) {
    lineOriginOffset.value = Vector2.new(43, -30);
    lineColor.value = Color.new(255, 215, 0);
}
```

O jeito antigo, com três parâmetros (`item, bobber, line`) e
`line.lineOriginOffset` / `line.lineColor`, continua valendo.

`bobber` é a boia (o projétil): a `ExampleFishingRod` pinta a linha com a cor
que a `ExampleBobber` sorteou ao nascer (`bobber.ModProjectile`).

### Tooltip colorido

`ModifyTooltips(item, tooltips)` roda **toda vez** que o tooltip do item
aparece, com as linhas que o jogo montou: uma lista de `TooltipLine`, cada uma
com `Name`, `Text`, `OverrideColor`, `IsModifier` e `IsModifierBad`. Mude à
vontade: troque o texto, pinte a linha inteira com `OverrideColor`, insira linhas
novas (`splice`) ou tire as que não quer.

```js
ModifyTooltips(item, tooltips) {
    tooltips.splice(1, 0, new TooltipLine('Aviso', 'Logo abaixo do nome'));
    const aviso = tooltips.find((line) => line.Name === 'Aviso');
    aviso.OverrideColor = Color.new(255, 80, 80);
}
```

Para pintar **trechos**, use a tag `[c/RRGGBB:texto]` dentro do texto.
`TooltipLine.colorTag(texto, cor)` monta a tag de um `Color`, de `{ R, G, B }`
ou de `'#RRGGBB'`:

```js
const vermelho = TooltipLine.colorTag('fogo', Color.new(255, 60, 30));
tooltips.push(new TooltipLine('Elemento', 'Dano de ' + vermelho));
```

O `ExampleTooltipItem` do Example Mod escreve "Bunny Loader!" com uma cor por
letra, girando no arco-íris, logo abaixo do nome.

Os nomes das linhas: `ItemName` (a 0), `Material`, `JourneyResearch`,
`SetBonus`, `OneDropLogo`; as outras são `Line1`, `Line2`... pela posição em que o
jogo as montou. Uma linha nova precisa de um nome seu. Até 30 linhas.

O logo da One Drop, o dos ioiôs licenciados do jogo, é uma linha sem texto com
`OneDropLogo = true`. O `ExampleYoyo` põe o dele no fim:

```js
ModifyTooltips(item, tooltips) {
    const logo = new TooltipLine('OneDropLogo', '');
    logo.OneDropLogo = true;
    tooltips.push(logo);
}
```

No guia de criação e na busca de itens, as linhas chegam sem cor: o texto das
tags fica, as tags somem.

Por trás: o celular monta as linhas num método com seis parâmetros `ref`
(`Main.MouseText_DrawItemTooltip_GetLinesInfo`), que a ponte hooka (ver o
[guia 3](03-ref-e-out.md)). O celular desenha o texto cru, sem o leitor de tags
do PC, então o Bunny Loader desenha a linha com tag trecho a trecho, e a caixa
do tooltip é medida sem as tags.

## ModPlayer

Dados e comportamento de cada **jogador**. Cada jogador tem a própria instância
de cada `ModPlayer`, criada na primeira vez que alguém pergunta por ela. No
multijogador, o escudo de um jogador liga o dash só dele, e não o de todos.

```js
export class ExampleDashPlayer extends ModPlayer {
    DashAccessoryEquipped = false;

    ResetEffects(player) {
        this.DashAccessoryEquipped = false;   // o acessório liga de novo, se estiver equipado
    }

    UpdateMovement(player) {
        if (this.DashAccessoryEquipped) { /* ... */ }
    }
}

ModPlayer.register(ExampleDashPlayer);
```

E no acessório:

```js
UpdateAccessory(item, player, vanity, hideVisual) {
    player.GetModPlayer(ExampleDashPlayer).DashAccessoryEquipped = true;
}
```

| | |
|---|---|
| `player.GetModPlayer(Classe)` | A instância daquele jogador (também por nome: `GetModPlayer('ExampleDashPlayer')`). |
| `Classe.get(player)` | O mesmo. |
| `this.Player` | O jogador desta instância. Os métodos também o recebem como primeiro argumento. |
| `ModPlayer.getByName(nome)` | A instância do jogador **local**. Serve para código de uma só tela (interface), não para lógica de jogo. |

Os métodos, na ordem em que rodam num quadro:

| Método | Quando |
|---|---|
| `PreUpdate(player)` | Início do quadro do jogador. |
| `ResetEffects(player)` | Logo depois do jogo zerar os efeitos: zere aqui o que os acessórios ligam. |
| `ModifyMaxStats(player)` | Depois do `ResetEffects`: `this.CumulativeHealth`/`CumulativeMana` somam à vida/mana máxima. |
| `PreUpdateBuffs(player)`, `PostUpdateBuffs(player)` | Em volta dos buffs. |
| `UpdateEquips(player)` (ou `PostUpdateEquips`) | Depois dos equipamentos e acessórios. |
| `UpdateBadLifeRegen(player)`, `UpdateLifeRegen(player)` | Antes e depois da regeneração de vida. |
| `UpdateManaRegen(player)` | Depois da regeneração de mana. |
| `UpdateMovement(player)` | Movimento próprio (dash): perto do fim do quadro. |
| `PostUpdate(player)` | Fim do quadro. |
| `UpdateDead(player)` | Todo quadro morto. |

Outros:

| Método | Quando |
|---|---|
| `Initialize()` | Uma vez, quando a instância nasce. |
| `OnEnterWorld(player)` | O jogador entrou no mundo. |
| `OnRespawn(player)` | Voltou a viver. |
| `CanUseItem(player, item)` | `false` impede usar. |
| `ModifyWeaponDamage(player, item, dano)` | Devolva o dano novo (ou ponha em `this.WeaponDamage`). |
| `ImmuneTo(player, fonte, cooldown, esquivavel)` | `true`: o golpe não acontece. |
| `FreeDodge(player, fonte, dano, ...)` | `true`: esquiva. |
| `ModifyHurt(player, mod)` | Mude `mod.damage`, `mod.hitDirection`, `mod.crit`... antes do golpe. |
| `OnHurt(player, fonte, dano, ...)`, `PostHurt(...)` | Depois do golpe (`PostHurt`, só se sobreviveu). |
| `PreKill(player, fonte, dano, direcao, pvp)` | `false` impede a morte (dê vida ao jogador, senão ele segue com 0). |
| `Kill(player, fonte, dano, direcao, pvp)` | Morreu. |

Mais de um mod com `ModPlayer`: roda na ordem de carga dos mods (pelo uid) e,
dentro de um mod, na ordem do `register`.

### Dash e toque duplo no celular

No celular, o primeiro toque numa direção põe o `player.doubleTapCardinalTimer`
em **30**, e não em 15 como no PC. O segundo toque só conta com o timer abaixo de
30. Um dash copiado do tModLoader com `timer < 15` não sai no aparelho. O
`ExampleDashPlayer` usa 30 e aceita também o botão de dash do jogo
(`player.controlDash`).

No multijogador, **só o dono** decide o dash (`player.whoAmI === Main.myPlayer`).
Os outros aparelhos recebem os controles pela rede com atraso, e o timer de lá
dá toque duplo onde não houve. O movimento chega aos outros pela posição do
jogador.

### Dados salvos

O que o jogador deve lembrar entre sessões vai no `SaveData` e volta no
`LoadData`:

```js
SaveData(data) {
    data.mortes = this.mortes;
}

LoadData(data) {
    this.mortes = data.mortes ?? 0;
}
```

`data` é um objeto JS comum (vira JSON), gravado em `Players/<personagem>.plr.bl.json`.
Com o mod desligado, os dados dele ficam guardados no arquivo e voltam quando ele
é religado. Personagem salvo na nuvem fica de fora.

## ModBuff

Um buff (ou debuff) novo. Uma instância por tipo: os métodos recebem o
jogador ou o NPC e a posição do buff na lista dele.

```js
export class ExampleDefenseBuff extends ModBuff {
    constructor() {
        super();
        this.Texture = 'Buffs/' + this.constructor.name;   // 32x32
        this.DefenseBonus = 10;
    }

    ModifyDescription() {
        this.Description = this.Description.replace('{0}', this.DefenseBonus);
    }

    UpdatePlayer(player, buffIndex) {
        player.statDefense += this.DefenseBonus;
    }
}

ModBuff.register(ExampleDefenseBuff);
```

Um item que dá o buff: `this.Item.buffType = ModBuff.getTypeByName('ExampleDefenseBuff')`
e `this.Item.buffTime = 5400` (em quadros; 60 = 1 segundo). Registre o buff
antes do item.

Nome e descrição vêm de `BuffName.<Classe>` e `BuffDescription.<Classe>` em
`Localization/*.json` (ou de `this.DisplayName`/`this.Description`).
`ModifyDisplayName`/`ModifyDescription` rodam uma vez por idioma.

| Método | Quando |
|---|---|
| `SetStaticDefaults()` | Uma vez, com o tipo nas tabelas: `Terraria.Main.debuff[this.Type] = true`, `buffNoSave`, `buffNoTimeDisplay`, `persistentBuff`, `BuffID.Sets...`. |
| `UpdatePlayer(player, i)` | Todo quadro, com o buff ativo no jogador (depois do `ResetEffects`, como os do jogo). Pode tirar o próprio buff com `player.DelBuff(i)`: o buff que vier para a posição `i` ainda roda no mesmo quadro. |
| `UpdateNPC(npc, i)` | Todo quadro, com o buff ativo no NPC. |
| `ApplyPlayer(player, tempo)`, `ApplyNPC(npc, tempo)` | Quando o buff entra. |
| `ReApplyPlayer(player, tempo, i)`, `ReApplyNPC(npc, tempo, i)` | Quando entra de novo, já ativo. `false` impede o jogo de renovar o tempo. |
| `CanRemove(player, tempo, i, debuff)` | Ao tocar no ícone para tirar. `true`/`false` decide; `null` deixa com o jogo (debuff não sai). |
| `OnRemove(player, tempo, i)` | Depois de tirado pelo toque. |

Buff de mod ativo no personagem é salvo pelo **nome**, como o item. Com o mod
desligado, ele fica guardado no arquivo e volta quando o mod é religado.

## Pets, lacaios e sentinelas

São um buff, um item e um projétil trabalhando juntos, como no tModLoader. O
Example Mod tem os quatro: o **Aviãozinho** (pet), a **Luz Irritante** (pet de
luz), o **Cajado do Lacaio de Exemplo** e o **Cajado da Sentinela de Exemplo**.

### Pet

O buff mantém o pet vivo. O jogo tem o método que cria o pet se faltar e
renova o buff; ele tem um parâmetro `ref bool`, então vai um `Ref` (guia 3):

```js
const SpawnPet = Terraria.Player['void BuffHandle_SpawnPetIfNeededAndSetTime(int buffIndex, ref bool petBool, int petProjID, int buffTimeToGive)'];

export class ExamplePetBuff extends ModBuff {
    SetStaticDefaults() {
        Terraria.Main.buffNoTimeDisplay[this.Type] = true;
        Terraria.Main.vanityPet[this.Type] = true;     // lightPet para pet de luz
    }

    UpdatePlayer(player, buffIndex) {
        SpawnPet(player, buffIndex, new Ref(false), ModProjectile.getTypeByName('ExamplePetProjectile'), 18000);
    }
}
```

No item, `shoot` é o pet e `buffType` é o buff; no `UseItem`, o buff entra com
`player.AddBuff(item.buffType, 3600, false)`. O projétil marca
`Main.projPet[this.Type] = true` e, a cada quadro, fica vivo enquanto o dono
tiver o buff:

```js
AI(proj) {
    const player = Terraria.Main.player[proj.owner];
    if (!player.dead && player.FindBuffIndex(ModBuff.getTypeByName('ExamplePetBuff')) >= 0) proj.timeLeft = 2;
}
```

Com `Main.vanityPet` (ou `Main.lightPet`) no buff, o item também entra no
**slot de pet** (ou de luz) do equipamento, e o jogo põe o buff sozinho. O
Aviãozinho usa a IA do Zephyr Fish: `this.CloneDefaults(ProjectileID.ZephyrFish)`
e `this.AIType = ProjectileID.ZephyrFish`. A IA do Zephyr Fish mantém o pet
vivo pelo `player.zephyrfish`; com ele em `false` no `PreAI`, quem decide é o
buff do mod.

### Lacaio

- No projétil: `Main.projPet[this.Type] = true`,
  `ProjectileID.Sets.MinionSacrificable` (o jogo troca o mais velho quando
  faltam vagas), `ProjectileID.Sets.MinionTargetingFeature` (segue o alvo
  marcado pelo chicote) e, no `SetDefaults`, `minion = true` e `minionSlots = 1`.
- No buff: `buffNoSave` e, no `UpdatePlayer`, `buffTime[i] = 18000` enquanto
  `player.ownedProjectileCounts[lacaio] > 0`; sem lacaio, `player.DelBuff(i)`.
- No item: `summon = true`, `buffType` e `shoot`; o `Shoot` põe o buff
  (`player.AddBuff(item.buffType, 2, false)`) e devolve `true` para o jogo
  criar o lacaio. No `ModifyShootStats`, `stats.position = Main.MouseWorld`
  põe o lacaio onde se tocou.
- `MinionContactDamage(proj)` devolvendo `true`: o lacaio fere ao encostar
  (o jogo não deixa pet nem lacaio ferir por contato).

O dano do lacaio sai do `originalDamage`, que o jogo recalcula a cada quadro
com os bônus de invocação. O Bunny Loader o preenche com o dano do item que
criou o projétil, como o tModLoader.

O contador de lacaios sob o ícone do buff:

```js
const counter = Terraria.DataStructures.CachedProjectileCounterBuffTextHandler.new(ModProjectile.getTypeByName('ExampleMinion'));
Terraria.ID.BuffID.Sets.BuffTextHandlers.Add(this.Type, counter);
```

### Sentinela

No item, `sentry = true`; no projétil, `sentry = true` e `timeLeft = 36000`
(10 minutos). O `Shoot` do Example Mod acha o chão com
`player.FindSentryRestingSpot(type, x, y, empurrao)` (três `out`), cria a
sentinela e chama `player.UpdateMaxTurrets()`, que tira a mais velha quando
passa do limite.

Para a sentinela pousar em plataforma: `decidesManualFallThrough = true` e
`shouldFallThrough = false` no `SetDefaults` (é o `TileCollideStyle` do
tModLoader). E `OnTileCollide` devolvendo `false`, para o chão não matá-la.

### Diferenças do tModLoader

- `player.HasBuff(t)` não existe no celular: `player.FindBuffIndex(t) >= 0`.
- `Main.ActiveNPCs` não existe: percorra `Terraria.Main.npc` (o último é vazio)
  e pule os `!npc.active`.
- `proj.ai`, `proj.localAI`, `proj.oldPos`, `proj.oldRot` não são arrays no
  celular (são structs de tamanho fixo), mas `[i]` funciona igual:
  `proj.ai[0]++`, `proj.localAI[1] = 5`, `proj.oldPos[3].X`. Fora do tamanho
  é `RangeError` (o `ai` tem 3).
- O projétil não tem `DamageType`: `minion` e `sentry` já dizem que o dano é de
  invocação. No item, `summon = true`.

## ModTile

Um bloco novo, como o `ModTile` do tModLoader. Por enquanto, **blocos de 1x1**
(terra, pedra, minério); móveis e objetos maiores ainda não.

```js
export class ExampleOre extends ModTile {
    constructor() {
        super();
        this.Texture = 'Tiles/' + this.constructor.name;
        this.DustType = Terraria.ID.DustID.Platinum;
        this.HitSound = Terraria.ID.SoundID.Tink;
        this.MineResist = 4;     // 4 vezes mais golpes
        this.MinPick = 200;      // picareta abaixo de 200 nao quebra
    }

    SetStaticDefaults() {
        Terraria.Main.tileSolid[this.Type] = true;
        Terraria.Main.tileBlockLight[this.Type] = true;
        Terraria.Main.tileMergeDirt[this.Type] = true;
        Terraria.Main.tileMerge[this.Type][this.Type] = true;
        Terraria.Main.tileSpelunker[this.Type] = true;
        Terraria.ID.TileID.Sets.Ore[this.Type] = true;
        this.AddMapEntry(Color.new(152, 171, 198), this.constructor.name);
    }
}

ModTile.register(ExampleOre);
```

A textura é a folha de quadros do bloco, no mesmo formato das do jogo:
288x270 px, quadros de 16x16 com 2 px de margem. O jogo escolhe o quadro pelos
vizinhos (e pela terra, com `tileMergeDirt`).

O item que coloca o bloco é um `ModItem` comum:

```js
SetDefaults() {
    this.DefaultToPlaceableTile(ModTile.getTypeByName('ExampleOre'));
}
```

Ao quebrar, cai o item de mod que coloca aquele tile. Para outro item, use
`this.ItemDrop = tipo`.

| Campo | Para quê |
|---|---|
| `Texture` | Relativo a `Textures/`, sem `.png`. |
| `DustType` | A poeira ao bater e quebrar (`Terraria.ID.DustID`). |
| `HitSound` | O som ao bater (`Terraria.ID.SoundID`). Sem ele, o som do jogo. |
| `MinPick` | Força de picareta mínima. |
| `MineResist` | Quanto o tile resiste: o dano de cada golpe é dividido por ele. |
| `ItemDrop` | O item que cai (padrão: o item que coloca o tile). |

| Método | Quando |
|---|---|
| `SetStaticDefaults()` | Uma vez, com o tipo já nas tabelas (`Main.tileSolid[this.Type]`...). |
| `AddMapEntry(cor, nome)` | A cor no mapa (ver abaixo). Sem ela, o tile não aparece no mapa. |
| `CanKillTile(i, j)` | `false`: a picareta não quebra. |
| `KillTile(i, j, fail, effectOnly, noItem)` | Antes de o tile sair (`fail`: só o golpe). |
| `CreateDust(i, j)` | `false`: sem poeira. |
| `KillSound(i, j, fail)` | `false`: sem som. |

`bl.tiles.typeAt(x, y)` dá o tipo do tile ativo numa posição (-1 se não há).

### O mundo salvo continua abrindo sem o mod

O `.wld` **nunca** leva tile de mod. Ao salvar, os tiles de mod são gravados à
parte, em `<mundo>.wld.tiles.bl`, pelo nome (`<mod>/<Classe>`), e no `.wld`
ficam como ar (parede, líquido e fios continuam). Ao carregar, eles voltam.

Sem o mod, o mundo abre normalmente no jogo, com ar no lugar, e o arquivo ao
lado guarda os tiles para quando o mod voltar. Se alguém construir no lugar
enquanto isso, vale o que foi construído. A ordem dos mods pode mudar à vontade:
o tile volta pelo nome, não pelo número.

Ao reabrir o mundo, o jogo reenquadra todo bloco comum e **sorteia de novo a
variante** (cada formato tem 3 desenhos). Isso vale para terra, pedra e tile de
mod: um bloco pode aparecer com outro desenho, no mesmo formato. Com uma
textura de variantes bem diferentes, como a do `ExampleTile`, dá para ver. O
tModLoader faz igual: só guarda o quadro de tile com `tileFrameImportant`
(móveis e objetos).

### Mapa

O `.map` do celular guarda o **índice** de cor de cada ponto. Uma cor nova
levaria ao arquivo um índice que o jogo sem o mod não conhece. Por isso o
`AddMapEntry` aponta o tile para a cor **do jogo** mais próxima da pedida. O
mapa mostra quase a mesma cor, e o `.map` só tem cores que o jogo conhece.

### Por trás

O que o Bunny Loader faz para o tile novo não quebrar o jogo:

- aumenta as ~220 tabelas de tile do jogo (`Main.tile*`, `TileID.Sets` e as
  aninhadas, texturas, mapa, receitas, materiais), com o tipo novo no valor
  padrão de cada uma, e o `Main.tileMerge` (753x753) linha a linha;
- aumenta a contagem de tiles dos biomas (`SceneMetrics`) e a mesa de criação
  por perto (`adjTile`): sem isso, contar um tile de mod escreve fora do array;
- troca os limites compilados no código (`PlaceTile`, `KillTile`, `TileFrame`...);
- dá espaço aos tiles de mod na tabela interna de tiles do mundo do celular
  (onde cada tile igual é guardado uma vez só);
- chama os hooks do `ModTile` só para tile de mod: bater em terra ou pedra não
  passa pelo JS.

## Receitas

`this.CreateRecipe(quantidade)` dentro do `AddRecipes`, e uma corrente:

```js
this.CreateRecipe()
    .AddIngredient(Terraria.ID.ItemID.IronBar, 5)
    .AddIngredient(ModItem.getTypeByName('ExampleItem'), 10)
    .AddTile(Terraria.ID.TileID.Anvils)
    .SetProperty('needWater', true)
    .Register();
```

| Método | O que faz |
|---|---|
| `SetResult(tipo, n)` | O que a receita dá (o `CreateRecipe` já chama). |
| `AddIngredient(tipo, n)` | Até 15 ingredientes. |
| `AddRecipeGroup(grupo, n)` | Aceita qualquer item do grupo (veja abaixo). |
| `AddTile(tipo)` | A estação. Uma só por receita (o jogo guarda um número). |
| `SetProperty(nome, valor)` | `needWater`, `needLava`, `needHoney`, `needSnowBiome`, `needGraveyardBiome`, `needTorchGodsFavor`, `needMechdusa`, `notDecraftable`, `crimson`, `corruption`, `alchemy`. |
| `AddCustomShimmerResult(tipo, n)` | O que sai ao jogar o item no Brilho, no lugar dos ingredientes. |
| `Register()` | Põe a receita no jogo. |

Receita para um item **do jogo** também vale:
`new ModRecipe().SetResult(tipo, n).AddIngredient(...).Register()`.

### Grupos de receita

Um grupo faz a receita aceitar qualquer item de uma lista ("Qualquer barra de
ferro"). Os do jogo vão pelo nome, como estão em `Terraria.ID.RecipeGroups`
(`'IronBar'`, `'Wood'`, `'Sand'`, `'Fragment'`...):

```js
new ModRecipe()
    .SetResult(Terraria.ID.ItemID.SpikyBall, 50)
    .AddRecipeGroup('IronBar')          // barra de ferro OU de chumbo
    .AddTile(Terraria.ID.TileID.Anvils)
    .Register();
```

Grupo novo: `ModRecipe.CreateRecipeGroup(nome, [tipos])`, no `AddRecipeGroups`
(que roda antes de qualquer receita). O menu mostra "Qualquer <nome>". O nome
vem da tradução, em `RecipeGroups.<nome>`, e sem ela fica o próprio `nome`:

```js
AddRecipeGroups() {
    this.constructor.Group = ModRecipe.CreateRecipeGroup('ExampleItem', [
        ModItem.getTypeByName('ExampleItem'),
        ModItem.getTypeByName('ExampleSoul'),
    ]);
}
```

`AddRecipeGroup(grupo, n)` aceita o objeto, o nome de um grupo do jogo ou o nome
de um criado por mod. Se nenhum ingrediente já posto é do grupo, ele põe o
primeiro item do grupo com `n`. Se já há um, esse ingrediente passa a aceitar
o grupo todo (`.AddIngredient(ExampleItem, 50).AddRecipeGroup(grupo)`).

### ModSystem

Para o que é do mod inteiro, não de um item (receitas de itens do jogo, grupos):

```js
export class ExampleRecipes extends ModSystem {
    AddRecipeGroups() { /* ModRecipe.CreateRecipeGroup(...) */ }
    AddRecipes() { /* new ModRecipe()... */ }
    PostSetupContent() {}
}

ModSystem.register(ExampleRecipes);
```

### Limite

O jogo tem 3600 posições de receita, e as dele ocupam 3570. Passando disso, a
tabela cresce e a receita existe (Guia, Brilho), mas o menu de criação não a
mostra: o laço do jogo para em 3600, fixo no código. O log avisa quantas
ficaram de fora.

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
| `OnTileCollide(proj, oldVelocity)` | Bateu num bloco e ia morrer: `false` o mantém vivo (para quicar, mude `proj.velocity`). `oldVelocity` é a de antes do choque. |
| `OnHitNPC(proj, npc)`, `OnHitPlayer(proj, player)` | Ao acertar. |
| `Colliding(proj, projHitbox, targetHitbox)` | `true`/`false` decide o acerto; `undefined` deixa o do jogo. |
| `CanDamage(proj)` | `false`: não causa dano. |
| `MinionContactDamage(proj)` | `true`: o lacaio (ou pet) fere ao encostar. |
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
- `proj.ai[0]`, `proj.localAI[2]`: no jogo são structs de 3 floats
  (`Float_FixedArray_3`), e a ponte aceita `[i]` neles como num array; o
  `get_Item(i)`/`set_Item(i, v)` do jogo também. `new ProjAI(proj)` (ou
  `new ProjAI(proj, true)`) continua valendo para código antigo.

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

Item de mod vale em qualquer regra. O drop do jogo (`CommonCode.DropItem*`)
recusava tipo acima dos do jogo, com o limite fixo no código; o Bunny Loader
troca esse limite pelo total de itens, como o tModLoader.

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

## Morador

Um NPC que se muda para uma casa, conversa, vende e tem humor, como os do
jogo. O Example Mod tem a **Pessoa** (`ExamplePerson`), a do ExMod.

```js
export class ExamplePerson extends ModNPC {
    constructor() {
        super();
        this.Texture = 'NPCs/ExamplePerson/' + this.constructor.name;
    }

    SetStaticDefaults() {
        Terraria.Main.npcFrameCount[this.Type] = 25;
        NPCID.Sets.ExtraFramesCount[this.Type] = 9;
        NPCID.Sets.AttackFrameCount[this.Type] = 4;
        NPCID.Sets.DangerDetectRange[this.Type] = 700;
        NPCID.Sets.AttackType[this.Type] = 1;        // 0 arremesso, 1 tiro, 2 magia
        NPCID.Sets.AttackTime[this.Type] = 60;
        NPCID.Sets.AttackAverageChance[this.Type] = 35;
        this.Happiness
            .SetNPCAffection(NPCID.Nurse, AffectionLevel.Love)
            .SetBiomeAffection('Desert', AffectionLevel.Hate);
    }

    SetDefaults() {
        this.NPC.townNPC = true;
        this.NPC.friendly = true;
        this.NPC.aiStyle = 7;                        // a IA de morador do jogo
        this.AnimationType = NPCID.Guide;
        // ...vida, defesa, sons
    }

    CanTownNPCSpawn(numTownNPCs) { /* true: pode se mudar */ }
    CheckConditions(left, right, top, bottom) { return bottom <= Terraria.Main.worldSurface; }
    SetNPCNameList() { return ['Someone', 'Somebody', 'Blocky', 'Colorless']; }
    GetChat(npc) { return ModLocalization.GetTextValue('NPCChat.ExamplePerson_1'); }
}
```

| Método | Quando |
|---|---|
| `CanTownNPCSpawn(numTownNPCs)` | De tempos em tempos, enquanto não há um deste tipo no mundo: `true` e ele se muda para a próxima casa vaga. |
| `CheckConditions(left, right, top, bottom)` | A sala (em tiles) serve para ele? Vale na mudança e no menu de casas. |
| `SetNPCNameList()` | Os nomes próprios; um é sorteado quando ele chega. |
| `GetChat(npc)` | A fala ao conversar (texto). |
| `SetChatButtons(npc, buttons)` | Os botões da conversa: `buttons.button` e `buttons.button2` (o celular mostra até dois). |
| `OnChatButtonClicked(npc, firstButton)` | Tocou num botão. Devolva o nome de uma loja para abri-la. |
| `AddShops()` | As lojas, uma vez (ver abaixo). |
| `TownNPCAttackProj(npc, attack)` | O projétil do ataque: `attack.projType` e `attack.attackDelay`. |
| `TownNPCAttackStrength(npc, attack)` | `attack.damage` e `attack.knockback`. |
| `TownNPCAttackProjSpeed(npc, attack)` | `attack.speed`, `attack.gravityCorrection` (mirar acima) e `attack.randomOffset`. |

`ModLocalization.GetTextValue('Secao.Chave')` dá o **texto** do
`Localization/<cultura>.json` na língua do jogo (o `Translate` dá a chave, que
é o que o Bestiário pede).

### Cabeça e texturas

Ao lado da `Texture`, pelo nome:

| Arquivo | Para quê |
|---|---|
| `_Head.png` | A cabeça no mapa e no menu de casas. **Sem ela o morador nunca se muda.** |
| `_Shimmer_Head.png` | A cabeça depois do shimmer. |
| `_Party.png`, `_Shimmer.png`, `_Shimmer_Party.png` | Com festa, depois do shimmer e os dois. |
| `_Portrait.png`, `_Shimmer_Portrait.png` | O retrato da conversa (200x200). Sem ele, a conversa mostra o quadro do sprite. |

### Loja

```js
SetChatButtons(npc, buttons) {
    buttons.button = Terraria.Localization.Language['string GetTextValue(string key)']('LegacyInterface.28');   // "Loja"
}

OnChatButtonClicked(npc, firstButton) {
    if (firstButton) return 'Shop';
}

AddShops() {
    new NPCShop(this.Type, 'Shop')
        .Add(ModItem.getTypeByName('ExampleGun'))
        .Add(ModItem.getTypeByName('ExampleYoyo'), { condition: () => !Terraria.Main.dayTime })
        .Add(ModItem.getTypeByName('ExampleSwingingEnergySword'), { currency: ExampleCustomCurrency.CurrencyId, price: 10 })
        .Register();
}
```

Opções de cada item: `condition` (uma função; `false` e ele não aparece),
`price` (o preço) e `currency` (uma moeda própria). O preço passa pela
felicidade do morador, como nas lojas do jogo.

Moeda própria, paga com um item (a do Example Mod é o Exemplo de Item):

```js
const { CustomCurrencyManager, CustomCurrencySingleCoin } = Terraria.GameContent.UI;
const currency = CustomCurrencySingleCoin.new();
currency['void .ctor(int coinItemID, long currencyCap)'](ModItem.getTypeByName('ExampleItem'), 999);
currency.CurrencyTextKey = ModLocalization.Translate('CustomCurrency.ExampleItemCurrency');
const id = CustomCurrencyManager.RegisterCurrency(currency);
```

### Felicidade

`this.Happiness` (no `SetStaticDefaults`) grava os gostos no banco de
personalidades do próprio jogo, que calcula o humor e o preço da loja:
`SetNPCAffection(npcDoJogo, nível)` e `SetBiomeAffection('Forest' | 'Desert' |
'Snow' | 'Jungle' | 'Ocean' | 'Underground' | 'Hallow' | 'Mushroom' | 'Dungeon'
| 'Corruption' | 'Crimson', nível)`, com `AffectionLevel.Love`, `Like`,
`Dislike` ou `Hate`.

As falas de humor vêm de `TownNPCMood.<Classe>` no `Localization/<cultura>.json`:
`Content`, `NoHome`, `FarFromHome`, `LoveSpace`, `LikeBiome`, `LoveNPC`,
`DislikeCrowded`... O `{0}` vira o nome do bioma ou do NPC.

### Ataque

Com `NPCID.Sets.AttackType` e os outros sets de ataque, a IA de morador do jogo
leva ele a atacar: escolhe a hora e anima. O projétil vem do
`TownNPCAttackProj` e sai no inimigo à vista mais perto, com o dano escalado
pelo jogo.

### Gore

Todo PNG em `Textures/Gores/` do mod vira um gore, com o nome do arquivo:

```js
const NewGore = Terraria.Gore['int NewGore(Vector2 Position, Vector2 Velocity, int Type, float Scale)'];
NewGore(npc.position, npc.velocity, ModGore.getTypeByName('ExamplePerson_Gore_Head'), 1);
```

### Save

O morador vai para `<mundo>.wld.npcs.bl`, **pelo nome**: posição, casa, nome
próprio, variação, a sala guardada e se passou pelo shimmer. O `.wld` fica sem
ele, então o mundo abre sem o mod, e o morador volta quando o mod volta.

### Diferenças do tModLoader

- O celular mostra até dois botões na conversa (`SetChatButtons`).
- Ainda não há `ModifyNPCHappiness`, `CanGoToStatue` (estátua do rei ou da
  rainha) nem os ataques `TownNPCAttackCooldown`, `Shoot`, `Magic` e `Swing`.
- O alvo do tiro é o inimigo à vista mais perto (o do jogo fica em variáveis
  que o mod não alcança).

## Chefe

O Example Mod tem um chefe, o **Olho de ???** (`ExampleBoss`): `npc.boss = true`,
`aiStyle = -1` e a IA no `AI` (persegue o jogador; na metade da vida acelera e
troca a animação), invocado à noite pelo `ExampleBossSummonItem` com
`Terraria.NPC.SpawnOnPlayer`. Ícone de chefe no mapa, música, sacola de
tesouro e a marca de chefe derrotado ainda não têm atalho no Bunny Loader.

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

## Save

Item de mod no inventário, no cofre e nos baús é salvo pelo **nome** (mod +
classe), num arquivo ao lado do save do jogo. Desligar o mod não perde nada: o
item vira um "?" e volta ao normal quando o mod é religado.

## Arrays do jogo

Um array do jogo (`Main.recipe`, `ItemID.Sets.IsAMaterial`...) tem índice e
`.length`, e `cloneResized(n)`: uma cópia do mesmo tipo com `n` posições. O que
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
| `ProjAI` | `new ProjAI(proj)`: `proj.ai` como vetor. Desnecessário hoje: `proj.ai[0]` já funciona direto (ver ModProjectile). |
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

## O que ainda não existe

Estas partes do tModLoader ainda não têm classe no Bunny Loader — dá para fazer
na mão, com hooks (guia 1), mas não há atalho:

- `ModTile` além de bloco 1x1 (móveis, objetos maiores), `ModPrefix`, `ModMount`, `ModBiome`;
- no `ModSystem`, por enquanto só `AddRecipeGroups`, `AddRecipes` e `PostSetupContent`;
- `GlobalItem`, `GlobalNPC`, `GlobalProjectile`;
- armadura vestida (textura no corpo) e conjuntos (`IsArmorSet`/`UpdateArmorSet`);
- morador: `ModifyNPCHappiness`, `CanGoToStatue` e os ataques além do projétil;
- condições de receita do tModLoader (`AddCondition`) e estação de mod (`ModTile`).
