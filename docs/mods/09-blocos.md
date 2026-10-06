# 9. Blocos

Um bloco novo (terra, pedra, minério) é uma classe que estende `ModTile`, como
o `ModTile` do tModLoader. Este guia mostra como criar o bloco, o item que o
coloca, e como o Bunny Loader salva o mundo para ele continuar abrindo sem o
mod.

Objetos de várias células (móveis) também: a forma vem do `TileObjectData`,
como no tModLoader ([Móveis](#móveis-tileobjectdata)), com o que é próprio de
cada um: porta que abre, cadeira que senta, baú, estação de criação
([Móveis com comportamento](#móveis-com-comportamento)).

Antes, leia as [ideias do guia 4](04-conteudo-novo.md). A lista completa está
na [referência](../referencia/classes.md#modtile).

## O minério de exemplo

```js
export class ExampleOre extends ModTile {
    constructor() {
        super();
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
| `Texture` | O caminho no mod, sem `.png`. Padrão: o arquivo da classe (`Content/Tiles/X.js` -> `Content/Tiles/X.png`). |
| `HighlightTexture` | O contorno de `TileID.Sets.HasOutlines` (padrão: `Texture + '_Highlight'`). Sem o arquivo, o contorno sai. |
| `DustType` | A poeira ao bater e quebrar (`Terraria.ID.DustID`); `-1`, nenhuma. |
| `HitSound` | O som ao bater (`Terraria.ID.SoundID`). Sem ele, o som do jogo. |
| `MinPick` | Força de picareta mínima. |
| `MineResist` | Quanto o tile resiste: o dano de cada golpe é dividido por ele. |
| `ItemDrop` | O item que cai (padrão: o item que coloca o tile, pelo estilo). |
| `AdjTiles` | Conta como estas estações de criação: `[TileID.WorkBenches]`. |
| `AnimationFrameHeight` | Altura de um quadro de animação na textura (ver `AnimateTile`). |
| `CacheDrawData` | `false`: o `SetDrawPositions`, o `AnimateIndividualTile` e o `SetSpriteEffects` rodam em todo desenho, sem o cache (ver abaixo). Padrão `true`. |

Um bloco de mod pedido numa receita (`AddTile`) aparece no guia de criação com o nome do mapa (`AddMapEntry`) e o ícone do item que o coloca (`TileID.Sets.CraftingStationItemId`, uma tabela só do celular; o Bunny Loader a preenche no fim das receitas).

## Métodos

Os parâmetros `ref` do tModLoader chegam como `Ref`: leia e escreva
`.value` (`num.value = 3`), como no [guia 2](02-ref-e-out.md).

| Método | Quando |
|---|---|
| `SetStaticDefaults()` | Uma vez, com o tipo já nas tabelas (`Main.tileSolid[this.Type]`...). |
| `PostSetDefaults()` | Logo depois (o `tileNoSunLight` do sólido já foi posto). |
| `AddMapEntry(cor, nome)` | A cor e o nome no mapa (ver abaixo). Sem ela, o tile não aparece no mapa. |
| `RegisterItemDrop(item, ...estilos)` | O item que cai, de todos os estilos ou só dos listados. |
| `CanKillTile(i, j, blockDamaged)` | `false`: a picareta não quebra. |
| `KillTile(i, j, fail, effectOnly, noItem)` | Antes de o tile sair (`fail`: só o golpe). Os três são `Ref`. |
| `KillMultiTile(i, j, frameX, frameY)` | Um móvel saiu inteiro; `(i, j)` é o canto de cima à esquerda. |
| `NumDust(i, j, fail, num)` | Quantas poeiras (no máximo as 10 do jogo). |
| `CreateDust(i, j, type)` | `false`: sem poeira; `type.value` troca a poeira. |
| `KillSound(i, j, fail)` | `false`: sem som. |
| `CanDrop(i, j)` / `GetItemDrops(i, j)` | Se cai item / quais (tipos ou `{ type, stack }`, até dois). |
| `PlaceInWorld(i, j, item)` | O jogador acabou de colocar. |
| `TileFrame(i, j, resetFrame, noBreak)` | `false`: o jogo não enquadra (você enquadrou). |
| `RandomUpdate(i, j)` | A atualização aleatória do mundo (a erva cresce aqui). |
| `NearbyEffects(i, j, closer)` | O tile está perto do jogador (`closer`: na tela). |
| `ModifyLight(i, j, r, g, b)` | A luz, com `Main.tileLighted[tipo]`. Recalculada a cada 3 quadros (vale a última no meio). |
| `AnimateTile(frame, frameCounter)` | Uma vez por quadro, para o tipo todo (`Main.tileFrame`). |
| `AnimateIndividualTile(type, i, j, frameXOffset, frameYOffset)` | O quadro de uma célula. |
| `SetDrawPositions(i, j, width, offsetY, height, tileFrameX, tileFrameY)` | Onde e o que desenhar da célula. |
| `SetSpriteEffects(i, j, spriteEffects)` | Espelhar (`1` = horizontal). |
| `PreDraw(i, j, spriteBatch)` | Você desenha; `false` = nem o desenho padrão. |
| `PostDraw(i, j, spriteBatch)` | Por cima do tile (chamas). |
| `DrawEffects(i, j, spriteBatch, drawData)` | Pede o `SpecialDraw` com `Main.instance.TilesRenderer.AddSpecialLegacyPoint(i, j)`. |
| `SpecialDraw(i, j, spriteBatch)` | O desenho pedido acima (a relíquia flutuando). |
| `EmitParticles(i, j, tile, frameX, frameY, luz, visible)` | Poeira e partículas da célula na tela. Declare só os parâmetros que usa: sem `tile` e `luz` na assinatura, eles nem são buscados. |
| `RightClick(i, j)` | O jogador tocou no tile. `true`: tratou. |
| `MouseOver(i, j)` / `MouseOverFar(i, j)` | O dedo (ou o cursor) sobre o tile. |
| `HitWire(i, j)` | Um fio ligado passou pelo tile. |
| `Slope(i, j)` | O martelo; `false` deixa o tile como está. |
| `ModifySittingTargetInfo(i, j, info)` | Onde e para onde senta (`TileID.Sets.CanBeSatOnForPlayers`). |
| `ModifySleepingTargetInfo(i, j, info)` | Onde deita (`TileID.Sets.CanBeSleptIn`). |
| `IsLockedChest`, `UnlockChest`, `LockChest` | O baú trancado. |

`i` e `j` são a posição em **tiles** (pixels / 16). `bl.tiles.typeAt(x, y)`
dá o tipo do tile ativo numa posição (-1 se não há).

**O toque no celular.** O jogo só trata o toque num tile como interação (e
chama o `RightClick`) quando o ícone do cursor está ligado, o que no PC o
`MouseOver` faz só por enfeite. Tile de mod com `RightClick` cujo `MouseOver`
não liga o ícone ganha o do item que o coloca, sozinho. Para outro ícone,
ligue no `MouseOver`: `player.cursorItemIconEnabled = true`,
`player.cursorItemIconID = item` e `player.noThrow = 2`, como a cômoda do
Example Mod. O `RightClick` roda uma vez por toque.

A cômoda de mod (`TileID.Sets.BasicDresser`) funciona como a do jogo: o baú
continua aberto e a janela de roupas abre pela gaveta de baixo. O jogo do
celular só aceitava a cômoda dele (tipo 88) nas duas coisas.

Os hooks do `ModTile` só entram no JS para **tile de mod** (filtro nativo, que
lê o tipo direto do mundo), e os de todo quadro (luz, desenho, animação) só
para os tipos que **sobrescrevem** aquele método: as marcas do
[núcleo](../nucleo/hooks.md#filtros-nativos).

## Móveis (TileObjectData)

Um móvel é um tile com `tileFrameImportant` e um `TileObjectData`: o tamanho,
onde se apoia e a altura de cada linha da textura. A Pia de Exemplo (2x2), do
Example Mod:

```js
const { TileObjectData } = Terraria.ObjectData;

export class ExampleSink extends ModTile {
    SetStaticDefaults() {
        Terraria.Main.tileSolid[this.Type] = false;
        Terraria.Main.tileFrameImportant[this.Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
        TileObjectData.newTile.CoordinateHeights = [16, 18];
        TileObjectData.addTile(this.Type);

        this.AddMapEntry(Color.new(100, 100, 100), this.constructor.name);
    }
}
```

Igual ao tModLoader: copie um modelo do jogo (`Style2x2`, `Style3x2`,
`Style1x2`...), mude o que precisar **entre** o `CopyFrom` e o `addTile`, e
registre. O item é um `ModItem` com `DefaultToPlaceableTile`, como o de um
bloco.

A textura tem uma célula de 16 de largura por coluna, com 2 px de margem, e a
altura de cada linha do `CoordinateHeights` (`[16, 18]`: a de baixo tem 18,
e os 2 px a mais descem sobre o chão, como nos móveis do jogo).

O que o Bunny Loader faz por você:

- **Colocar**: pelo item, ou `WorldGen.PlaceObject(x, y, tipo)`.
- **Quebrar**: quebrou uma célula, sai o objeto inteiro, com **um** item só.
  Tirou o bloco de baixo (ou a parede, conforme a âncora), idem. O
  `KillMultiTile(i, j, frameX, frameY)` avisa, com o canto de cima à
  esquerda.
- **Salvar**: o quadro de cada célula fica no `.tiles.bl`, e o móvel volta
  como estava.

### Móveis com comportamento

O Example Mod traz os do tModLoader, em `Content/Tiles/Furniture/`. O que cada
um usa:

| Móvel | Como |
|---|---|
| Bancada, mesa | `AdjTiles = [TileID.WorkBenches]`: as receitas do jogo aceitam a de mod. `TileID.Sets.RoomNeeds.CountsAsTable` conta para a casa (o Bunny Loader põe o tipo também na lista `CountsAsTableTypes`, que é a que a checagem de casa do jogo percorre; idem cadeira, tocha e porta). |
| Cadeira, vaso sanitário | `TileID.Sets.CanBeSatOnForPlayers` e, no `RightClick`, `player.sitting.SitDown(player, i, j)`. O `ModifySittingTargetInfo` diz para onde olha e qual célula é a de baixo. |
| Cama | `TileID.Sets.CanBeSleptIn` (deitar: `player.sleeping.StartSleeping`) e `TileID.Sets.IsValidSpawnPoint` (o `Player.CheckSpawn` aceita a cama de mod como ponto de nascimento). |
| Porta | Dois tiles: a fechada com `TileID.Sets.OpenDoorID` e a aberta com `CloseDoorID`. Tocar abre ou fecha; como a do jogo, abre sozinha para o jogador que encosta e fecha depois que ele passa; o morador abre e fecha ao passar, o goblin arromba ou derruba, e o fio abre e fecha (sem `HitWire` próprio). O jogo sincroniza no multijogador. |
| Baú, cômoda | `TileID.Sets.BasicChest` (ou `BasicDresser`) e `Main.tileContainer`; os ganchos de colocar do baú do jogo (`HookPostPlaceMyPlayer`) criam o `Main.chest`. `IsLockedChest`/`UnlockChest` para o trancado. |
| Tocha | `TileID.Sets.Torches` e a forma da tocha do jogo (`GetTileData(TileID.Torches, 0, 0)`): o jogo prende na parede e nos lados. |
| Fogueira | `TileID.Sets.Campfires`; o `NearbyEffects` liga o `Main.SceneMetrics.HasCampfire` (o buff). |
| Caixa de música | `MusicLoader.AddMusicBox(mod, slot, item, tile)`: nasce desligada, como a do jogo; o toque (ou o fio) liga, e ligada na tela, ou equipada como acessório, toca a faixa. |
| Luminária, lustre, estátua, armadilha | `HitWire`: o fio liga, desliga, solta moeda ou atira. |

`TileID.Sets.OpenDoorID`, `CloseDoorID`, `IsValidSpawnPoint`,
`MultiTileSway` e `TileObjectData.GetTileStyle`/`IsTopLeft`/`TopLeft` são do
tModLoader: o jogo do celular não os tem, e o Bunny Loader os põe no lugar
(mesmos nomes). `Point16.new(x, y)` e `AnchorData.new(tipo, quantos, início)`
fazem os structs do `TileObjectData`.

Não há: o balanço ao vento (`MultiTileVine`, `AdjustMultiTileVineParameters`),
as árvores, palmeiras e cactos de mod (`ModTree`, `ModPalmTree`, `ModCactus`),
os pilares (`ModPylon`), `ModifyFrameMerge`/`PostTileFrame`, a placa de
pressão (`SwitchTiles`) e o `HasSmartInteract` (no celular se toca no tile).

### Custo por quadro

Tile é o conteúdo mais numeroso na tela (uma base tem dezenas de tochas), e o
que o mod faz por tile, por quadro, soma. O que ficou barato, e como manter:

- **Luz**: o jogo calcula a luz dos tiles numa cópia interna do código dele,
  então a do tile de mod entra por `Lighting.AddLight`, num lote nativo, com
  o `ModifyLight` recalculado a cada 3 quadros. 150 tochas de mod custam o
  mesmo quadro que 150 tochas do jogo (medido no `tools/tests/tileperf`).
- **Chama**: 7 `spriteBatch.Draw` por tocha no `PostDraw` (o jeito do
  tModLoader) custavam ~16 ms por quadro com 150 tochas. A tocha de exemplo
  usa `Main.tileFlame[tipo] = true`: o jogo desenha a chama da tocha dele,
  sem JS. Desenho no `PostDraw` é para tile que aparece pouco (lâmpada,
  fogueira, relíquia).
- **Sorteio**: `Rand` é uma ida ao jogo; para "1 em 40, por tile, por quadro"
  use `Math.random()`.
- **Parâmetros**: no `EmitParticles`, declare só o que usa.

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

Cada `AddMapEntry` vira uma entrada **própria** do mapa, como no tModLoader:
a cor exata pedida e o nome dela (o que aparece ao passar o dedo no mapa).
Chamar de novo cria outra opção do mesmo tile (baú aberto e trancado, por
exemplo).

O `.map` do celular guarda o **índice** de cor de cada ponto, e o índice de
uma entrada de mod não existe no jogo sem o mod. Por isso, ao salvar, os
pontos de tile de mod saem do `.map` (ficam escuros) e vão para um arquivo ao
lado, o `<mapa>.map.bl`, pelo nome do tile. Ao carregar, eles voltam. Sem o
mod, o jogo vê esses pontos como não explorados, e o `.map.bl` os guarda até
o mod voltar. Isso vale também para o pedaço do mapa que o jogo comprime fora
da gravação, ao descarregar ou reciclar: os pontos de mod ficam guardados à
parte e voltam quando o pedaço volta.

## Paredes (ModWall)

Uma parede nova é como o `ModWall` do tModLoader: uma classe que estende
`ModWall`, exportada de `Content/`, com a textura ao lado (a folha de quadros
das paredes do jogo, 468x180). O item que a coloca usa o
`DefaultToPlaceableWall`. A ExampleWall do Example Mod:

```js
// Content/Walls/ExampleWall.js (+ ExampleWall.png)
const { DustID, WallID } = Terraria.ID;
const { Main } = Terraria;

export class ExampleWall extends ModWall {
    SetStaticDefaults() {
        Main.wallHouse[this.Type] = true;   // vale como parede de casa
        this.DustType = DustID.Stone;
        this.AddMapEntry(Color.new(150, 150, 150));
    }

    NumDust(i, j, fail, num) {
        num.value = fail ? 1 : 3;
    }
}
```

```js
// Content/Items/Placeable/ExampleWall.js (+ ExampleWall.png)
export class ExampleWall extends ModItem {
    SetDefaults() {
        this.DefaultToPlaceableWall(ModContent.WallType('ExampleWall'));
    }
}
```

| Campo | |
|---|---|
| `Type` | O número da parede (depois das 367 do jogo). |
| `DustType` | A poeira ao bater e quebrar. Sem ele, a do jogo. |
| `HitSound` | O som ao quebrar (um `SoundID` ou estilo). Sem ele, o do jogo. |
| `ItemDrop` | O item que cai ao quebrar. Sem ele, o item de mod que coloca esta parede (`createWall`). |

| Método | Quando |
|---|---|
| `SetStaticDefaults()` | Uma vez, com as tabelas do jogo já com o tipo: `Main.wallHouse[this.Type]`, `Main.wallLight`, `WallID.Sets...`. |
| `KillWall(i, j, fail)` | Ao bater ou quebrar. `fail` é um `Ref`: `fail.value = true` só faz a poeira. |
| `NumDust(i, j, fail, num)` | Quantas poeiras (`num.value`). |
| `CreateDust(i, j, type)` | Antes de cada poeira; `type.value` muda o tipo, `false` não cria. |
| `KillSound(i, j, fail)` | `false` não toca o som. |
| `Drop(i, j, type)` | O item que cai (`type.value`); `false` não deixa nada. |
| `ModifyLight(i, j, r, g, b)` | A luz que a parede dá (`r.value`, `g.value`, `b.value`, de 0 a 1). Roda para as paredes perto da tela, a cada 3 quadros. |
| `AnimateWall(frame, frameCounter)` | Uma vez por quadro, por tipo: `frame.value` escolhe a faixa de 180 px da textura (a folha com dois quadros tem 468x360). |
| `RandomUpdate(i, j)` | Na atualização aleatória do mundo, como a do bloco. |
| `AddMapEntry(cor, nome)` | A cor no mapa e o nome ao tocar nele (`MapObject.<Classe>` no Localization; sem ele, o nome da classe). |
| `WallFrame(i, j, randomizeFrame, style, frameNumber)` | Ao enquadrar a parede: `style.value` e `frameNumber.value` escolhem o quadro da moldura (0 a 2 é a variação); `false` deixa a moldura como estava. |

A `ExampleWallAdvanced` do Example Mod usa a animação (dois quadros), a luz
verde à noite e a poeira de esmeralda.

A parede de mod entra na **conversão** dos biomas pelas tabelas do jogo, que
crescem com o tipo novo: `WallID.Sets.Conversion.Stone[this.Type] = true` no
`SetStaticDefaults` faz a corrupção, o carmesim e o consagrado tratá-la como
parede de pedra.

Pelo nome ou pela classe: `ModContent.WallType('ExampleWall')`,
`ModContent.GetModWall(tipo)`, `this.GetContent(ModWall)`.

O mundo salvo continua abrindo sem o mod, como com os blocos: o `.wld` nunca
leva parede de mod. Elas vão para um arquivo ao lado, o
`<mundo>.wld.walls.bl` (posição, tinta e `<uid>/<Classe>`), e voltam ao
carregar, com a tinta. No mapa é igual aos blocos: os pontos de parede de
mod saem do `.map` e vão para o `.map.bl`.

Sem o mod, o lugar recebe a **parede não carregada** do Bunny Loader (a
`UnloadedWall` do tModLoader, quadriculada de magenta). Ela segura o que está
preso na parede, como quadros e tochas, e o arquivo guarda a parede original
até o mod voltar: aí a original volta no lugar dela. Quebrar a parede não
carregada perde a original daquela casa, como no tModLoader.

`VanillaFallbackOnModDeletion` existe na classe, mas ainda não faz nada: no
tModLoader ele só vale ao apagar de vez os dados de um mod removido, comando
que o Bunny Loader ainda não tem.

Quanto custa (MuMu, 2130 paredes na tela, `tools/tests/wallperf`): parede de
mod só com textura custa o mesmo que a do jogo. O `ModifyLight` é o caro: roda
para cada parede perto da tela, um terço das linhas por quadro, e com 2130
paredes iluminadas dá ~1,2 a 1,5 ms de JS por quadro. Colocar com `WallFrame`
custa ~60 µs por parede, só na hora. O save do mundo fica igual (~200 ms).

Não tem `PreDraw`/`PostDraw`: o celular desenha as paredes num laço só,
dividido entre threads, sem chamada por célula.

## Por trás

O que o Bunny Loader faz para o tile novo não quebrar o jogo (detalhes em
[conteúdo por dentro](../nucleo/conteudo.md)):

- aumenta as ~220 tabelas de tile do jogo (`Main.tile*`, `TileID.Sets` e as
  aninhadas, texturas, mapa, receitas, materiais), com o tipo novo no valor
  padrão de cada uma, e o `Main.tileMerge` (754x754) linha a linha;
- aumenta a contagem de tiles dos biomas (`SceneMetrics`) e a mesa de criação
  por perto (`adjTile`): sem isso, contar um tile de mod escreve fora do array;
- troca os limites compilados no código (`PlaceTile`, `KillTile`,
  `TileFrame`...);
- dá espaço aos tiles de mod na tabela interna de tiles do mundo do celular
  (onde cada tile igual é guardado uma vez só);
- chama os hooks do `ModTile` só para tile de mod;
- no desenho, o `SetDrawPositions`, o `AnimateIndividualTile` e o
  `SetSpriteEffects` são chamados pelo próprio desenho nativo (que roda em
  várias threads), e só para os tipos que os sobrescrevem; o que o mod
  desenha (`PreDraw`, `PostDraw`, `SpecialDraw`) sai numa passada depois dos
  tiles, na thread do jogo;
- o resultado desses três fica guardado por célula: a célula que o jogo
  redesenha igual não chama o JS de novo (cada ida custava ~30 a 50 µs no
  emulador, quase tudo esperando o motor JS). A chave é a posição, o tipo, o
  quadro da célula e o `Main.tileFrame` do tipo; sai do cache quando um bloco
  perto muda (o jogo reenquadra) e depois de 5 s. Quem anima por outro
  contador (tempo, `Main.GameUpdateCount`) põe `CacheDrawData = false`;
- porta, cama e ponto de nascimento usam o código do jogo: durante o
  `OpenDoor`/`CloseDoor`/`CheckSpawn`, a porta ou cama de mod vira a do jogo
  (10, 11, 79), e o que o jogo montou volta a ser de mod;
- o mesmo disfarce, em C++, para quem abre porta sozinho: o jogador que
  encosta (`DoorOpeningHelper.Update`) e as IAs de NPC que abrem porta
  (`AI_007_TownEntities`, `AI_003_Fighters`, `AI_107_ImprovedWalkers`). As
  portas de mod a poucos tiles viram 10/11 durante a chamada; o `KillTile` de
  um goblin que derruba a porta desfaz o disfarce antes, para sair o item e o
  pó da porta de mod. Sem porta de mod por perto, custa uma varredura de ~120
  células por NPC que anda.
