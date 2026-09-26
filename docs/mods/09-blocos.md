# 9. Blocos

Um bloco novo (terra, pedra, minério) é uma classe que estende `ModTile`, como
o `ModTile` do tModLoader. Este guia mostra como criar o bloco, o item que o
coloca, e como o Bunny Loader salva o mundo para ele continuar abrindo sem o
mod.

Por enquanto, **só blocos de 1x1**. Móveis e objetos maiores (mesas, portas,
estátuas) ainda não.

Antes, leia as [ideias do guia 4](04-conteudo-novo.md). A lista completa está
na [referência](../referencia/classes.md#modtile).

## O minério de exemplo

```js
export class ExampleOre extends ModTile {
    constructor() {
        super();
        this.Texture = 'Tiles/' + this.constructor.name;
        this.DustType = Terraria.ID.DustID.Platinum;
        this.HitSound = Terraria.ID.SoundID.Tink;
        this.MineResist = 4;     // 4 vezes mais golpes
        this.MinPick = 200;      // picareta abaixo de 200 não quebra
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

No `SetStaticDefaults` vão as tabelas do jogo que dizem o que o bloco **é**:
sólido, se bloqueia a luz, se se funde com a terra, se o Espeleólogo o
destaca.

### A textura

A folha de quadros do bloco, no mesmo formato das do jogo: **288x270 px**,
quadros de 16x16 com 2 px de margem. O jogo escolhe o quadro pelos vizinhos (e
pela terra, com `tileMergeDirt`): um bloco sozinho, uma quina, uma borda.
Copiar a folha de um bloco do jogo parecido e repintar é o caminho mais
curto.

### O item que coloca

É um `ModItem` comum:

```js
SetDefaults() {
    this.DefaultToPlaceableTile(ModContent.TileType('ExampleOre'));
}
```

Ao quebrar, cai o item de mod que coloca aquele tile. Para outro item, use
`this.ItemDrop = tipo` no `ModTile`.

## Campos

| Campo | Para quê |
|---|---|
| `Texture` | Relativo a `Textures/`, sem `.png`. |
| `DustType` | A poeira ao bater e quebrar (`Terraria.ID.DustID`). |
| `HitSound` | O som ao bater (`Terraria.ID.SoundID`). Sem ele, o som do jogo. |
| `MinPick` | Força de picareta mínima. |
| `MineResist` | Quanto o tile resiste: o dano de cada golpe é dividido por ele. |
| `ItemDrop` | O item que cai (padrão: o item que coloca o tile). |

## Métodos

| Método | Quando |
|---|---|
| `SetStaticDefaults()` | Uma vez, com o tipo já nas tabelas (`Main.tileSolid[this.Type]`...). |
| `AddMapEntry(cor, nome)` | A cor no mapa (ver abaixo). Sem ela, o tile não aparece no mapa. |
| `CanKillTile(i, j)` | `false`: a picareta não quebra. |
| `KillTile(i, j, fail, effectOnly, noItem)` | Antes de o tile sair (`fail`: só o golpe). |
| `CreateDust(i, j)` | `false`: sem poeira. |
| `KillSound(i, j, fail)` | `false`: sem som. |

`i` e `j` são a posição em **tiles** (pixels / 16). `bl.tiles.typeAt(x, y)`
dá o tipo do tile ativo numa posição (-1 se não há).

Os hooks do `ModTile` só entram no JS para **tile de mod**: bater em terra ou
pedra não passa pelo JS (filtro nativo, que lê o tipo direto do mundo).

## O mundo salvo continua abrindo sem o mod

O `.wld` **nunca** leva tile de mod. Ao salvar, os tiles de mod são gravados à
parte, em `<mundo>.wld.tiles.bl`, pelo nome (`<mod>/<Classe>`), e no `.wld`
ficam como ar (parede, líquido e fios continuam). Ao carregar, eles voltam.

Sem o mod, o mundo abre normalmente no jogo, com ar no lugar, e o arquivo ao
lado guarda os tiles para quando o mod voltar. Se alguém construir no lugar
enquanto isso, vale o que foi construído. A ordem dos mods pode mudar à
vontade: o tile volta pelo nome, não pelo número.

Ao reabrir o mundo, o jogo reenquadra todo bloco comum e **sorteia de novo a
variante** (cada formato tem 3 desenhos). Isso vale para terra, pedra e tile
de mod: um bloco pode aparecer com outro desenho, no mesmo formato. O
tModLoader faz igual: só guarda o quadro de tile com `tileFrameImportant`
(móveis e objetos).

## O mapa

O `.map` do celular guarda o **índice** de cor de cada ponto. Uma cor nova
levaria ao arquivo um índice que o jogo sem o mod não conhece. Por isso o
`AddMapEntry` aponta o tile para a cor **do jogo** mais próxima da pedida. O
mapa mostra quase a mesma cor, e o `.map` só tem cores que o jogo conhece.

## Por trás

O que o Bunny Loader faz para o tile novo não quebrar o jogo (detalhes em
[conteúdo por dentro](../nucleo/conteudo.md)):

- aumenta as ~220 tabelas de tile do jogo (`Main.tile*`, `TileID.Sets` e as
  aninhadas, texturas, mapa, receitas, materiais), com o tipo novo no valor
  padrão de cada uma, e o `Main.tileMerge` (753x753) linha a linha;
- aumenta a contagem de tiles dos biomas (`SceneMetrics`) e a mesa de criação
  por perto (`adjTile`): sem isso, contar um tile de mod escreve fora do array;
- troca os limites compilados no código (`PlaceTile`, `KillTile`,
  `TileFrame`...);
- dá espaço aos tiles de mod na tabela interna de tiles do mundo do celular
  (onde cada tile igual é guardado uma vez só);
- chama os hooks do `ModTile` só para tile de mod.
