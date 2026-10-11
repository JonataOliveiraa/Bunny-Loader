// Móvel de mod sem apoio: a cômoda (3x2, contêiner) e o relógio (2x5) do
// Example Mod ao lado dos do jogo (cômoda 88, relógio 104), num chão de pedra
// no alto. Cada um é posto pelo caminho do jogador e perde o bloco embaixo do
// pé direito de dois jeitos: a picareta (Player.PickTile, que respeita o
// CanKillTile) e o KillTile direto. O de mod tem de acabar igual ao do jogo, e
// nunca pela metade nem flutuando.
// Precisa do Example Mod ligado. Loga "furnsupport <caso>: ok | FALHOU".
const Main = Terraria.Main;
const W = Terraria.WorldGen;
const { TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('furnsupport ' + label + ': ok');
        else { fails++; bl.log('furnsupport ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('furnsupport ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const placeTile = (x, y, type) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, type, true, true, -1, 0);
const killTile = (x, y, noItem) => W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](x, y, false, false, noItem);
const me = () => Main.player[Main.myPlayer];
const area = () => ({ x: Main.spawnTileX + 60, floor: Main.spawnTileY - 30 });

function clear() {
    const { x, floor } = area();
    for (let i = x - 2; i <= x + 34; i++) for (let j = floor - 7; j <= floor + 1; j++) if (bl.tiles.typeAt(i, j) >= 0) killTile(i, j, true);
}

// Como o jogador coloca: o alvo é a origem do objeto.
function placeAsPlayer(ox, oy, type) {
    Terraria.Player.tileTargetX = ox;
    Terraria.Player.tileTargetY = oy;
    const data = new Ref();
    const can = Terraria.TileObject['bool CanPlace(int x, int y, int type, int style, int dir, out TileObject objectData, bool onlyCheck, Nullable<int> forcedRandom)'](
        ox, oy, type, 0, 1, data, false, null);
    if (!can) return false;
    me()['TileObject PlaceThing_Tiles_PlaceIt(bool newObjectType, TileObject data, int tileToCreate)'](true, data.value, type);
    return bl.tiles.typeAt(ox, oy) === type;
}

const shapeOf = (type) => {
    const d = TileObjectData['TileObjectData GetTileData(int type, int style, int alternate)'](type, 0, 0);
    return { w: d.Width, h: d.Height, ox: d.Origin.X, oy: d.Origin.Y };
};

// O objeto com o canto de baixo à esquerda em (left, floor): quantas células são dele.
function cells(left, floor, type) {
    const s = shapeOf(type);
    let n = 0;
    for (let i = left; i < left + s.w; i++) for (let j = floor - s.h + 1; j <= floor; j++) if (bl.tiles.typeAt(i, j) === type) n++;
    return { n, total: s.w * s.h };
}

// O que sobrou depois de tirar o apoio, como texto.
function outcome(left, floor, type, groundX) {
    const { n, total } = cells(left, floor, type);
    const ground = bl.tiles.typeAt(groundX, floor + 1) >= 0;
    const object = n === total ? 'inteiro' : n === 0 ? 'saiu' : `pela metade (${n} de ${total})`;
    return { text: `chão ${ground ? 'ficou' : 'saiu'}, objeto ${object}`, ground, n, total };
}

function placed(left, floor, type) {
    const s = shapeOf(type);
    return placeAsPlayer(left + s.ox, floor - (s.h - 1) + s.oy, type);
}

// Tira o bloco embaixo do pé direito: pela picareta ou direto.
function removeSupport(left, floor, type, how) {
    const s = shapeOf(type);
    const gx = left + s.w - 1, gy = floor + 1;
    if (how === 'picareta') {
        for (let k = 0; k < 8 && bl.tiles.typeAt(gx, gy) >= 0; k++) me()['void PickTile(int x, int y, int pickPower, int dealDamageAsIfBaseNumberIs)'](gx, gy, 250, -1);
    } else {
        killTile(gx, gy, true);
    }
    return gx;
}

function run() {
    const { x, floor } = area();
    clear();
    for (let i = x - 2; i <= x + 34; i++) placeTile(i, floor + 1, TileID.Stone);

    const dresser = ModContent.TileType('examplemod/ExampleDresser');
    const clock = ModContent.TileType('examplemod/ExampleClock');
    check('tipos do Example Mod', () => (dresser > 0 && clock > 0) || `cômoda ${dresser}, relógio ${clock}`);

    const cases = [
        { name: 'cômoda', mod: dresser, vanilla: TileID.Dressers },
        { name: 'relógio', mod: clock, vanilla: TileID.GrandfatherClocks },
    ];
    let col = x;
    for (const c of cases) {
        for (const how of ['picareta', 'KillTile']) {
            const modLeft = col, vanLeft = col + 4;
            col += 9;
            check(`${c.name} (${how}): os dois colocados pelo jogador`, () =>
                (placed(modLeft, floor, c.mod) && placed(vanLeft, floor, c.vanilla)) || 'não colocou');
            const gm = removeSupport(modLeft, floor, c.mod, how);
            const gv = removeSupport(vanLeft, floor, c.vanilla, how);
            const m = outcome(modLeft, floor, c.mod, gm), v = outcome(vanLeft, floor, c.vanilla, gv);
            bl.log(`furnsupport ${c.name} (${how}): mod: ${m.text}; jogo: ${v.text}`);
            check(`${c.name} (${how}): o de mod nunca fica pela metade`, () => (m.n === 0 || m.n === m.total) || m.text);
            check(`${c.name} (${how}): o de mod não fica flutuando`, () => !(m.n === m.total && !m.ground) || m.text);
            check(`${c.name} (${how}): o de mod acaba como o do jogo`, () => (m.ground === v.ground && (m.n === m.total) === (v.n === v.total)) || `mod: ${m.text}; jogo: ${v.text}`);
        }
    }
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    self.statLife = self.statLifeMax2;
    frames++;
    if (frames === 30) check('preparo', run);
    if (frames === 60) {
        // Tira o que sobrou e os itens que caíram na área.
        clear();
        const { x, floor } = area();
        for (let k = 0; k < 400; k++) {
            const w = Main.item[k];
            if (w.active && w.position.X / 16 > x - 4 && w.position.X / 16 < x + 36 && Math.abs(w.position.Y / 16 - floor) < 12) w['void TurnToAir()']();
        }
        done = true;
        bl.log('furnsupport FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('furnsupport: carregado');

export default class TestFurnSupport extends Mod {}
