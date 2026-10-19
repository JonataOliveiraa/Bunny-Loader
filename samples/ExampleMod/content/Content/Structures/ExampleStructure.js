const { TileID, WallID, ItemID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const { GenVars } = Terraria.WorldBuilding;
const Main = Terraria.Main;
const W = Terraria.WorldGen;

const TileAt = (x, y) => Main.tile['Tile get_Item(int x, int y)'](x, y);
const KillTile = W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'];
const PlaceObject = W['bool PlaceObject(int x, int y, int type, bool mute, int style, int alternate, int random, int direction)'];
const PlaceChest = W['int PlaceChest(int x, int y, ushort type, bool notNearOtherChests, int style)'];
const RangeFrame = W['void RangeFrame(int startX, int startY, int endX, int endY)'];
const InWorld = W['bool InWorld(int x, int y, int fluff)'];
const GetTileData = TileObjectData['TileObjectData GetTileData(int type, int style, int alternate)'];
const NextInt = (min, max) => W.genRand['int Next(int minValue, int maxValue)'](min, max);

// Um castelinho gerado com o mundo (a ExampleStructureSystem põe a etapa na
// geração) ou pelo comando /castle. A estrutura é desenhada em texto, como os
// esquemas do Calamity: um mapa de blocos e um de paredes, do mesmo tamanho,
// lidos de cima para baixo. A última linha é a fundação e fica no nível do chão.
//
// O que o Calamity faz e este exemplo repete:
// - procura o lugar com um número limitado de tentativas, conferindo o chão,
//   a água e o GenVars.structures (as outras estruturas do mundo);
// - limpa a área antes (árvores e móveis cortados ao meio ficariam quebrados);
// - põe blocos e paredes, enquadra tudo, e só depois os móveis, que precisam
//   dos blocos para se apoiar;
// - enche o baú pela função de recheio;
// - protege a área (AddProtectedStructure), para as etapas seguintes não
//   construírem por cima.
export class ExampleStructure {
    // Blocos. Espaço: ar. Letras de móvel marcam o canto de CIMA À ESQUERDA do
    // móvel; o resto dele fica em ar (espaço) no mapa.
    static Tiles = [
        '# # # #                   # # # #',
        '#######                   #######',
        '#     #                   #     #',
        '#     #                   #     #',
        '#  t  #                   #  t  #',
        '#     #                   #     #',
        '#     ## # # # # # # # # ##     #',
        '#=====#####################=====#',
        '#     #  B     L      B   #     #',
        '#     #                   #     #',
        '#==   D     t       t     D   ==#',
        '#   t                       t   #',
        '#                               #',
        '#=====#EEEEEEEE===EEEEEEEE#=====#',
        '#     #                   #     #',
        '#  t  #      t       t    #  t  #',
        'D==   D           ==      D   ==D',
        '        hT  H         C          ',
        '                   W             ',
        '#################################',
    ];

    // Paredes. Espaço: sem parede (o céu aparece). A parede só vai por
    // dentro: ela desenha uma borda de meio bloco na célula vizinha vazia, e
    // atrás dos blocos de fora (laterais, telhados, ameias, fundação) essa
    // borda sairia do castelo.
    static Walls = [
        '                                 ',
        '                                 ',
        ' ggggg                     ggggg ',
        ' ggggg                     ggggg ',
        ' ggggg                     ggggg ',
        ' ggggg                     ggggg ',
        ' ggggg                     ggggg ',
        ' gggggg                   gggggg ',
        ' ggggggggggggggggggggggggggggggg ',
        ' ggggggggggggggggggggggggggggggg ',
        ' ggggggggggggggggggggggggggggggg ',
        ' ggggggggggggggggggggggggggggggg ',
        ' ggggggggggggggggggggggggggggggg ',
        ' ggggggggggggggggggggggggggggggg ',
        ' ggggggeeeeeeeeeeeeeeeeeeegggggg ',
        ' ggggggeeeeeeeeeeeeeeeeeeegggggg ',
        ' ggggggeeeeeeeeeeeeeeeeeeegggggg ',
        ' ggggggeeeeeeeeeeeeeeeeeeegggggg ',
        ' ggggggeeeeeeeeeeeeeeeeeeegggggg ',
        '                                 ',
    ];

    static get Width() { return ExampleStructure.Tiles[0].length; }
    static get Height() { return ExampleStructure.Tiles.length; }

    // Os tipos são lidos na hora: os de mod só existem depois do carregamento.
    static #Blocks() {
        return {
            '#': TileID.GrayBrick,
            'E': ModContent.TileType('ExampleTile'),
            '=': ModContent.TileType('ExamplePlatform'),
        };
    }

    static #WallTypes() {
        return {
            'g': WallID.GrayBrick,
            'e': ModContent.WallType('ExampleWall'),
        };
    }

    // direction: para onde a cadeira olha (1 direita, -1 esquerda).
    static #Furniture() {
        return {
            'D': { type: ModContent.TileType('ExampleDoorClosed') },
            'T': { type: ModContent.TileType('ExampleTable') },
            'h': { type: ModContent.TileType('ExampleChair'), direction: 1 },
            'H': { type: ModContent.TileType('ExampleChair'), direction: -1 },
            'W': { type: ModContent.TileType('ExampleWorkbench') },
            'L': { type: ModContent.TileType('ExampleChandelier') },
            'B': { type: ModContent.TileType('ExampleWideBannerTile') },
            't': { type: ModContent.TileType('ExampleTorch') },
            'C': { type: ModContent.TileType('ExampleChest'), chest: true },
        };
    }

    // A etapa da geração do mundo. Usa o WorldGen.genRand: a mesma semente
    // gera o castelo no mesmo lugar.
    static Generate() {
        const structures = GenVars.structures;
        const margin = 300;
        const top = Math.max(10, Math.floor(GenVars.worldSurfaceLow) - 30);

        for (let attempt = 0; attempt < 1000; attempt++) {
            const x = NextInt(margin, Main.maxTilesX - margin);
            if (Math.abs(x - Main.spawnTileX) < 100) continue;

            const ground = ExampleStructure.FindGround(x, top, Math.floor(Main.worldSurface));
            if (ground < 0 || !ExampleStructure.CanPlace(x, ground, structures)) continue;

            const area = ExampleStructure.Place(x, ground);
            if (structures) structures['void AddProtectedStructure(Rectangle area, int padding)'](area, 10);
            return true;
        }
        return false;
    }

    // O primeiro bloco sólido da coluna, de cima para baixo (plataforma não
    // conta). -1 se não achar entre startY e endY.
    static FindGround(x, startY, endY) {
        for (let y = startY; y <= endY; y++) {
            const tile = TileAt(x, y);
            if (tile['bool active()']() && Main.tileSolid[tile.type] && !Main.tileSolidTop[tile.type]) return y;
        }
        return -1;
    }

    // centerX/groundY: o meio da última linha do mapa, que fica no chão.
    static Bounds(centerX, groundY) {
        const left = centerX - Math.floor(ExampleStructure.Width / 2);
        const top = groundY - ExampleStructure.Height + 1;
        return Rectangle.new(left, top, ExampleStructure.Width, ExampleStructure.Height);
    }

    static CanPlace(centerX, groundY, structures) {
        const area = ExampleStructure.Bounds(centerX, groundY);
        if (!InWorld(area.X, area.Y, 20) || !InWorld(area.X + area.Width, groundY + 20, 20)) return false;
        if (structures && !structures['bool CanPlace(Rectangle area, int padding)'](area, 10)) return false;

        // Chão quase plano e natural: nada de areia (cai), dungeon ou ilha.
        const natural = [TileID.Dirt, TileID.Grass, TileID.Stone, TileID.ClayBlock, TileID.SnowBlock];
        for (let x = area.X; x < area.X + area.Width; x++) {
            const ground = ExampleStructure.FindGround(x, area.Y - 10, groundY + 6);
            if (ground < 0 || Math.abs(ground - groundY) > 4) return false;
            if (!natural.includes(TileAt(x, ground).type)) return false;
        }

        for (let x = area.X; x < area.X + area.Width; x++) {
            for (let y = area.Y; y <= groundY; y++) {
                if (TileAt(x, y).liquid > 0) return false;
            }
        }
        return true;
    }

    // Constrói sem conferir nada (o comando usa direto). Devolve a área.
    static Place(centerX, groundY) {
        const area = ExampleStructure.Bounds(centerX, groundY);
        const left = area.X, top = area.Y;

        ExampleStructure.#Clear(area);
        ExampleStructure.#Foundation(area);

        const blocks = ExampleStructure.#Blocks();
        const walls = ExampleStructure.#WallTypes();
        for (let row = 0; row < ExampleStructure.Height; row++) {
            for (let col = 0; col < ExampleStructure.Width; col++) {
                const tile = TileAt(left + col, top + row);
                const block = blocks[ExampleStructure.Tiles[row][col]];
                const wall = walls[ExampleStructure.Walls[row][col]];

                if (block !== undefined) tile['void ResetToType(ushort type)'](block);
                if (wall !== undefined) tile.wall = wall;
            }
        }
        RangeFrame(left - 1, top - 1, left + area.Width + 1, top + area.Height + 1);

        const furniture = ExampleStructure.#Furniture();
        for (let row = 0; row < ExampleStructure.Height; row++) {
            for (let col = 0; col < ExampleStructure.Width; col++) {
                const piece = furniture[ExampleStructure.Tiles[row][col]];
                if (piece) ExampleStructure.#PlaceFurniture(left + col, top + row, piece);
            }
        }
        return area;
    }

    // Quebra o que estiver na área (sem soltar item) e zera blocos, paredes e
    // água. Quebrar antes de zerar desfaz por inteiro os móveis que passam da
    // borda. Árvores e cactos saem antes, inteiros (o tronco passa do topo da
    // área): quebrar só a base derrubaria o resto soltando madeira.
    static #Clear(area) {
        const isTree = (type) => TileID.Sets.IsATreeTrunk[type] || type === TileID.Cactus;
        for (let x = area.X; x < area.X + area.Width; x++) {
            for (let y = area.Y; y < area.Y + area.Height; y++) {
                const tile = TileAt(x, y);
                if (tile['bool active()']() && isTree(tile.type)) ExampleStructure.#RemoveTree(x, y, isTree);
            }
        }

        for (let x = area.X; x < area.X + area.Width; x++) {
            for (let y = area.Y; y < area.Y + area.Height; y++) {
                if (TileAt(x, y)['bool active()']()) KillTile(x, y, false, false, true);
            }
        }
        for (let x = area.X; x < area.X + area.Width; x++) {
            for (let y = area.Y; y < area.Y + area.Height; y++) {
                TileAt(x, y)['void ClearEverything()']();
            }
        }
    }

    // Da célula para cima, o tronco e os galhos dos lados.
    static #RemoveTree(x, y, isTree) {
        for (let j = y; j > 0; j--) {
            let found = false;
            for (let i = x - 1; i <= x + 1; i++) {
                const tile = TileAt(i, j);
                if (!tile['bool active()']() || !isTree(tile.type)) continue;
                tile.ClearTile();
                found = true;
            }
            if (!found) return;
        }
    }

    // Terra embaixo da fundação até o chão, para o castelo não flutuar.
    static #Foundation(area) {
        const bottom = area.Y + area.Height;
        for (let x = area.X; x < area.X + area.Width; x++) {
            for (let y = bottom; y < bottom + 30; y++) {
                const tile = TileAt(x, y);
                if (tile['bool active()']() && Main.tileSolid[tile.type]) break;
                tile['void ResetToType(ushort type)'](TileID.Dirt);
            }
        }
    }

    // O mapa marca o canto de cima à esquerda; o jogo coloca um móvel pela
    // Origin dele (a mesa 3x2 pelo meio de baixo, a porta pelo topo...).
    static #PlaceFurniture(x, y, piece) {
        const data = GetTileData(piece.type, 0, 0);
        const originX = x + (data ? data.Origin.X : 0);
        const originY = y + (data ? data.Origin.Y : 0);

        if (piece.chest) {
            const index = PlaceChest(originX, originY, piece.type, false, 0);
            if (index >= 0) ExampleStructure.FillChest(Main.chest[index]);
            return index >= 0;
        }
        return PlaceObject(originX, originY, piece.type, true, 0, 0, -1, piece.direction ?? -1);
    }

    // No celular, as casas do baú são ChestItem (tipo e quantidade), não Item:
    // SetDefaults(id) e stack.
    static FillChest(chest) {
        const loot = [
            [ModContent.ItemType('ExampleItem'), NextInt(10, 31)],
            [ModContent.ItemType('ExampleBar'), NextInt(3, 8)],
            [ModContent.ItemType('ExampleBuffPotion'), NextInt(1, 3)],
            [ItemID.LesserHealingPotion, NextInt(3, 6)],
            [ItemID.GoldCoin, NextInt(1, 4)],
        ];
        loot.forEach(([type, stack], slot) => {
            const item = chest.item[slot];
            item.SetDefaults(type);
            item.stack = stack;
        });
    }
}
