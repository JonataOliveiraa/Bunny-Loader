// O castelo do Example Mod (Content/Structures/ExampleStructure.js), no jogo:
// leva o jogador a um chão longe do spawn, roda o /castle e confere cada
// célula contra os mapas (blocos, paredes, o canto de cada móvel, o baú com
// o recheio). Fica parado para a captura ("excastle SHOT") e depois desfaz o
// castelo. Loga "excastle ..." e "excastle FIM".
const Main = Terraria.Main;
const W = Terraria.WorldGen;
const { TileID, WallID, ItemID } = Terraria.ID;
const log = (s) => bl.log('excastle ' + s);
let fails = 0;
function check(label, ok, detail) {
    if (!ok) fails++;
    log(label + ': ' + (ok ? 'ok' : 'FALHOU') + (detail === undefined ? '' : ' (' + detail + ')'));
}
const modErrors = [];
const blError = bl.error;
bl.error = (text) => {
    modErrors.push(String(text).split('\n')[0]);
    return blError(text);
};

// Cópia dos mapas do ExampleStructure.
const Tiles = [
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
const Walls = [
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
const Width = 33, Height = 20;

const T = (name) => ModContent.TileType('examplemod/' + name);
const TileAt = (x, y) => Main.tile['Tile get_Item(int x, int y)'](x, y);
const KillTile = W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'];
const player = () => Main.player[Main.myPlayer];
const replies = [];
const caller = { CommandType: CommandType.Chat, get Player() { return player(); }, Reply(text) { replies.push(text); log('resposta: ' + text); } };
const state = {};

function groundAt(x) {
    for (let y = 40; y < Main.worldSurface; y++) {
        const t = TileAt(x, y);
        if (t['bool active()']() && Main.tileSolid[t.type] && !Main.tileSolidTop[t.type]) return y;
    }
    return -1;
}

function verify(left, top) {
    const blocks = { '#': TileID.GrayBrick, 'E': T('ExampleTile'), '=': T('ExamplePlatform') };
    const walls = { 'g': WallID.GrayBrick, 'e': ModContent.WallType('examplemod/ExampleWall') };
    const furniture = {
        'D': T('ExampleDoorClosed'), 'T': T('ExampleTable'), 'h': T('ExampleChair'), 'H': T('ExampleChair'),
        'W': T('ExampleWorkbench'), 'L': T('ExampleChandelier'), 'B': T('ExampleWideBannerTile'),
        't': T('ExampleTorch'), 'C': T('ExampleChest'),
    };
    const bad = { blocks: [], walls: [], furniture: [] };
    let pieces = 0;
    for (let row = 0; row < Height; row++) {
        for (let col = 0; col < Width; col++) {
            const tile = TileAt(left + col, top + row);
            const ch = Tiles[row][col];
            const active = tile['bool active()']();
            if (blocks[ch] !== undefined && (!active || tile.type !== blocks[ch])) bad.blocks.push(ch + '@' + col + ',' + row + '=' + (active ? tile.type : '-'));
            if (furniture[ch] !== undefined) {
                pieces++;
                if (!active || tile.type !== furniture[ch]) bad.furniture.push(ch + '@' + col + ',' + row + '=' + (active ? tile.type : '-'));
            }
            const wall = walls[Walls[row][col]] ?? 0;
            if (tile.wall !== wall) bad.walls.push(col + ',' + row + '=' + tile.wall);
        }
    }
    check('blocos iguais ao mapa', bad.blocks.length === 0, bad.blocks.slice(0, 8).join(' '));
    check('paredes iguais ao mapa', bad.walls.length === 0, bad.walls.slice(0, 8).join(' '));
    check('os ' + pieces + ' móveis no lugar', bad.furniture.length === 0, bad.furniture.join(' '));

    const chestX = left + 22, chestY = top + 17;
    const index = Terraria.Chest['int FindChest(int X, int Y)'](chestX, chestY);
    check('baú criado', index >= 0, index);
    if (index >= 0) {
        const items = Main.chest[index].item;
        check('recheio do baú', items[0].type === ModContent.ItemType('examplemod/ExampleItem') && items[0].stack >= 10 && items[4].type === ItemID.GoldCoin,
            [0, 1, 2, 3, 4].map((i) => items[i].type + 'x' + items[i].stack).join(' '));
        state.chest = index;
    }

    // A fundação não pode ficar no ar.
    let floating = 0;
    for (let col = 0; col < Width; col++) {
        const below = TileAt(left + col, top + Height);
        if (!below['bool active()']()) floating++;
    }
    check('nada flutuando embaixo da fundação', floating === 0, floating);
}

function cleanup(left, top) {
    const chest = Terraria.Chest['int FindChest(int X, int Y)'](left + 22, top + 17);
    if (chest >= 0) {
        const items = Main.chest[chest].item;
        for (let i = 0; i < 40; i++) items[i].Clear();
    }
    for (let x = left - 1; x <= left + Width; x++) {
        for (let y = top - 1; y < top + Height; y++) {
            if (TileAt(x, y)['bool active()']()) KillTile(x, y, false, false, true);
        }
    }
    for (let x = left - 1; x <= left + Width; x++) {
        for (let y = top - 1; y < top + Height; y++) TileAt(x, y)['void ClearEverything()']();
    }
    W['void RangeFrame(int startX, int startY, int endX, int endY)'](left - 2, top - 2, left + Width + 2, top + Height + 2);

    // Quebrar uma célula de móvel solta o item mesmo com noItem.
    for (let i = 0; i < 400; i++) {
        const it = Main.item[i];
        const x = Math.floor(it.Center.X / 16), y = Math.floor(it.Center.Y / 16);
        if (it.active && x >= left - 6 && x <= left + Width + 6 && y >= top - 6 && y <= top + Height + 6) it.TurnToAir();
    }
}

const phases = [
    {
        name: 'ir', frames: 60,
        start() {
            let x = Main.spawnTileX + 140;
            if (x > Main.maxTilesX - 100) x = Main.spawnTileX - 140;
            let ground = groundAt(x);
            // Castelo de uma rodada que não chegou a limpar: o 1o sólido no
            // meio dele é o telhado (linha 7 do mapa).
            if (ground > 0 && TileAt(x, ground).type === TileID.GrayBrick) {
                cleanup(x - 16, ground - 7);
                ground = groundAt(x);
                log('castelo velho removido');
            }
            check('achou chão', ground > 0, x + ',' + ground);
            const p = player();
            p.position = Vector2.new(x * 16 + 8 - p.width / 2, ground * 16 - p.height - 2);
            p.velocity = Vector2.new(0, 0);
            state.x = x;
        },
        tick() { player().fallStart = Math.floor(player().position.Y / 16); },
    },
    {
        name: 'castelo', frames: 30,
        start() {
            const p = player();
            const x = Math.floor(p.Center.X / 16);
            const feet = Math.floor((p.position.Y + p.height) / 16);
            let ground = feet;
            while (ground < feet + 50 && !(TileAt(x, ground)['bool active()']() && Main.tileSolid[TileAt(x, ground).type])) ground++;
            state.left = x - 16;
            state.top = ground - Height + 1;
            check('o comando /castle existe e roda', CommandLoader.Handle('/castle', caller) === true);
            check('o /castle terminou sem erro', !replies.some((r) => String(r).startsWith('Uso')), replies.join(' | '));
            verify(state.left, state.top);
            check('nenhum erro de mod', modErrors.length === 0, modErrors.join(' | '));
        },
    },
    {
        name: 'pose', frames: 600,
        start() { log('SHOT excastle'); },
        tick() { player().fallStart = Math.floor(player().position.Y / 16); },
        end() {
            let dropped = 0;
            for (let i = 0; i < 400; i++) {
                const it = Main.item[i];
                if (!it.active) continue;
                const x = Math.floor(it.Center.X / 16), y = Math.floor(it.Center.Y / 16);
                if (x >= state.left - 4 && x <= state.left + Width + 4 && y >= state.top - 40 && y <= state.top + Height + 4) dropped++;
            }
            check('nenhum item solto em volta (árvores sem madeira)', dropped === 0, dropped);
        },
    },
    {
        name: 'limpar', frames: 10,
        start() { cleanup(state.left, state.top); },
    },
];

// Espera 30 s no mundo: o aviso de controles do começo pausa o jogo, e o
// roteiro o fecha antes.
let frame = 0, index = 0, local = 0, done = false, enteredAt = 0;
function step() {
    if (!enteredAt) enteredAt = Date.now();
    if (Date.now() - enteredAt < 30000) return;
    if (index >= phases.length) {
        log('FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
        done = true;
        return;
    }
    const p = phases[index];
    if (local === 0) {
        log('fase ' + p.name);
        if (p.start) p.start();
    }
    if (p.tick) p.tick(local);
    local++;
    if (local >= p.frames) {
        if (p.end) p.end();
        index++;
        local = 0;
    }
}

Main['void DoDraw(GameTime gameTime)'].hook((original, self, gameTime) => {
    if (!done && !Main.gameMenu) {
        frame++;
        const p = player();
        p.statLife = p.statLifeMax2;
        if (Main.dayTime === false || Main.time > 40000) { Main.dayTime = true; Main.time = 20000; }
        try {
            step();
        } catch (e) {
            fails++;
            log('erro no quadro ' + frame + ' (fase ' + (phases[index] ? phases[index].name : '?') + '): ' + e + (e.stack ? ' ' + e.stack : ''));
            index++;
            local = 0;
        }
    }
    original(self, gameTime);
});
log('carregado');

export default class TestExCastle extends Mod {}
