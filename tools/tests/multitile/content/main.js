// Móvel de mod: a Pia de Exemplo (2x2, TileObjectData) do Example Mod.
// Rodada 1: um chão de pedra acima do spawn e três pias:
//   A fica (e é salva);
//   B perde uma célula (KillTile): sai inteira, com UM item;
//   C perde a pedra de baixo: sai inteira, com UM item.
// Rodada 2 (reabrindo o jogo): no 1o quadro a pia A voltou com os quadros
// salvos, e no quadro 90 (o jogo já enquadrou de novo) ela continua inteira.
// Depois tudo sai (pia, chão) e o mundo é salvo limpo.
// Loga "multitile <caso>: ok | FALHOU".
const Main = Terraria.Main;
const W = Terraria.WorldGen;
const STONE = Terraria.ID.TileID.Stone;
const FILE = bl.path.join(bl.mod.dataDirectory, 'sink.json');

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('multitile ' + label + ': ok');
        else { fails++; bl.log('multitile ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('multitile ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const tileAt = (x, y) => Main.tile['Tile get_Item(int x, int y)'](x, y);
const placeTile = (x, y, type) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, type, true, false, -1, 0);
const placeObject = (x, y, type) => W['bool PlaceObject(int x, int y, int type, bool mute, int style, int alternate, int random, int direction)'](x, y, type, true, 0, 0, -1, -1);
const kill = (x, y, noItem = false) => W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](x, y, false, false, noItem);
const save = () => Terraria.IO.WorldFile['void SaveWorld(WorldSaveContext saveContext)'](0);

let SINK = -1, ITEM = -1;

function itemOfClass(name) {
    for (let t = bl.items.vanillaCount; bl.items.isModItem(t); t++) {
        const m = ModItem.getModItem(t);
        if (m && m.constructor.name === name) return t;
    }
    return -1;
}

// A área: 16 de largura, à esquerda e bem acima do spawn, fora do caminho dos
// outros testes (o de projéteis atira para a direita, na altura da cabeça).
function area() {
    const x = Main.spawnTileX - 40, floor = Main.spawnTileY - 22;
    return { x, floor, a: x + 1, b: x + 5, c: x + 9 };
}

// As 4 células da pia com a origem (canto de baixo à esquerda) em (ox, oy).
function cells(ox, oy) {
    const data = Terraria.ObjectData.TileObjectData['TileObjectData GetTileData(int type, int style, int alternate)'](SINK, 0, 0);
    const left = ox - data.Origin.X, top = oy - data.Origin.Y;
    const out = [];
    for (let dy = 0; dy < 2; dy++) for (let dx = 0; dx < 2; dx++) out.push({ x: left + dx, y: top + dy, dx, dy });
    return out;
}

const snap = (cs) => cs.map((c) => { const t = tileAt(c.x, c.y); return { x: c.x, y: c.y, type: bl.tiles.typeAt(c.x, c.y), fx: t.frameX, fy: t.frameY }; });
const show = (ss) => ss.map((s) => `${s.x},${s.y} ${s.type}:${s.fx},${s.fy}`).join(' | ');

// Soma as pias caídas na área e as tira do chão.
function takeDrops() {
    const { x, floor } = area();
    let n = 0;
    for (let i = 0; i < Main.item.length; i++) {
        const it = Main.item[i];
        if (!it || !it.active || it.type !== ITEM) continue;
        if (Math.abs(it.position.X - (x + 8) * 16) > 30 * 16 || Math.abs(it.position.Y - floor * 16) > 12 * 16) continue;
        n += it.stack;
        it['void TurnToAir()']();
    }
    return n;
}

// Tudo em volta da pia salva (o chão e as outras pias da rodada 1), sem drop.
function clearAround(saved) {
    const left = Math.min(...saved.map((c) => c.x)), top = Math.min(...saved.map((c) => c.y));
    for (let i = left - 3; i < left + 18; i++) {
        for (let j = top - 4; j <= top + 3; j++) if (bl.tiles.typeAt(i, j) >= 0) kill(i, j, true);
    }
    takeDrops();
    check('limpou a área', () => {
        const left2 = saved.filter((c) => bl.tiles.typeAt(c.x, c.y) >= 0);
        return left2.length === 0 || show(snap(left2));
    });
}

function firstRound() {
    const { x, floor, a, b, c } = area();
    check('forma do tile (TileObjectData)', () => {
        const d = Terraria.ObjectData.TileObjectData['TileObjectData GetTileData(int type, int style, int alternate)'](SINK, 0, 0);
        if (!d) return 'sem TileObjectData';
        const h = d.CoordinateHeights;
        return (Main.tileFrameImportant[SINK] === true && d.Width === 2 && d.Height === 2 && h[0] === 16 && h[1] === 18) ||
            `frameImportant ${Main.tileFrameImportant[SINK]}, ${d.Width}x${d.Height}, alturas ${h[0]},${h[1]}`;
    });
    // Limpa a área e faz o chão.
    for (let i = x - 1; i < x + 16; i++) {
        for (let j = floor - 4; j <= floor + 1; j++) if (bl.tiles.typeAt(i, j) >= 0) kill(i, j, true);
        placeTile(i, floor + 1, STONE);
    }
    takeDrops();
    check('PlaceObject das três pias', () => {
        const r = [a, b, c].map((ox) => placeObject(ox, floor, SINK));
        return r.every((v) => v === true) || 'PlaceObject: ' + r.join(', ');
    });
    const quadros = (ox) => {
        const s = snap(cells(ox, floor));
        const bad = s.filter((v, k) => { const cc = cells(ox, floor)[k]; return v.type !== SINK || v.fx !== cc.dx * 18 || v.fy !== cc.dy * 18; });
        return bad.length === 0 || show(s);
    };
    check('pia A: quatro células com os quadros certos', () => quadros(a));
    check('pia B: quatro células com os quadros certos', () => quadros(b));

    // B: quebrar a célula de cima à direita leva a pia inteira.
    const cb = cells(b, floor);
    kill(cb[1].x, cb[1].y);
    check('pia B: quebrar uma célula tira a pia inteira', () => {
        const left = snap(cb).filter((s) => s.type >= 0);
        return left.length === 0 || 'sobrou ' + show(left);
    });
    check('pia B: um item só', () => { const n = takeDrops(); return n === 1 || n + ' pias caídas'; });

    // C: sem a pedra de baixo, a pia cai inteira.
    kill(c, floor + 1, true);
    check('pia C: sem o chão, a pia sai inteira', () => {
        const left = snap(cells(c, floor)).filter((s) => s.type >= 0);
        return left.length === 0 || 'sobrou ' + show(left);
    });
    check('pia C: um item só', () => { const n = takeDrops(); return n === 1 || n + ' pias caídas'; });
    check('pia A: continua inteira', () => quadros(a));

    // D: pelo caminho do jogador — colocar como o item coloca, quebrar a picaretadas.
    const d = x + 13;
    const player = Main.player[Main.myPlayer];
    check('pia D: colocada pelo jogador (CanPlace + PlaceThing_Tiles_PlaceIt)', () => {
        const data = new Ref();
        const can = Terraria.TileObject['bool CanPlace(int x, int y, int type, int style, int dir, out TileObject objectData, bool onlyCheck, Nullable<int> forcedRandom)'](
            d, floor, SINK, 0, 1, data, false, null);
        if (!can) return 'CanPlace recusou';
        player['TileObject PlaceThing_Tiles_PlaceIt(bool newObjectType, TileObject data, int tileToCreate)'](true, data.value, SINK);
        return quadros(d);
    });
    check('pia D: a picareta quebra a pia inteira, com um item só', () => {
        const cd = cells(d, floor);
        let hits = 0;
        while (hits < 30 && snap(cd).some((s) => s.type === SINK)) {
            player['void PickTile(int x, int y, int pickPower, int dealDamageAsIfBaseNumberIs)'](cd[0].x, cd[0].y, 100, -1);
            hits++;
        }
        const left = snap(cd).filter((s) => s.type >= 0);
        if (left.length) return `sobrou depois de ${hits} golpe(s): ${show(left)}`;
        const n = takeDrops();
        bl.log(`multitile pia D saiu com ${hits} golpe(s)`);
        return n === 1 || n + ' pias caídas';
    });

    save();
    bl.file.write(FILE, JSON.stringify(snap(cells(a, floor))));
    bl.log('multitile rodada: salvou');
}

let before = null, first = null;
function start() {
    SINK = ModContent.TileType('examplemod/ExampleSink');
    ITEM = itemOfClass('ExampleSinkItem');
    check('tipos', () => (bl.tiles.isModTile(SINK) && ITEM > 0) || `tile ${SINK}, item ${ITEM}`);
    const txt = bl.file.read(FILE);
    before = txt ? JSON.parse(txt) : null;
    if (before) first = snap(before);
}

function finish() {
    if (!before) return firstRound();
    const same = (s, k) => s.type === SINK && s.fx === before[k].fx && s.fy === before[k].fy;
    check('1o quadro: a pia voltou com os quadros salvos', () => first.every(same) || show(first));
    const now = snap(before);
    check('quadro 90: a pia continua inteira depois de o jogo enquadrar', () => now.every(same) || show(now));
    check('quadro 90: nada caiu', () => { const n = takeDrops(); return n === 0 || n + ' pias caídas'; });
    clearAround(before);
    save();
    bl.file.delete(FILE);
    bl.log('multitile rodada: conferiu e limpou');
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    frames++;
    if (frames === 1) check('primeiro quadro', start);
    if (frames === 90) {
        done = true;
        check('preparo', finish);
        bl.log('multitile FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('multitile: carregado');

export default class TestMultitile extends Mod {}
