// O quadro de cada tile ao sair e entrar no mundo. Rodada 1: poe um 3x3 de
// ExampleTile (753, o primeiro tile de mod) e um 3x3 de pedra acima do spawn,
// anota os quadros em dataDirectory e salva. Rodada 2 (reabrindo o jogo):
//   no 1o quadro de jogo, o ExampleTile tem o quadro salvo (o .tiles.bl repos)
//   e a pedra esta em -1 (o jogo nao grava quadro de bloco comum);
//   no quadro 90, o jogo ja enquadrou os dois de novo, sorteando a variante
//   (0..2), como faz com todo bloco comum. Ninguem fica em -1.
const Main = Terraria.Main;
const W = Terraria.WorldGen;
const STONE = Terraria.ID.TileID.Stone;
const FILE = bl.path.join(bl.mod.dataDirectory, 'frames.json');

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('tileframes ' + label + ': ok');
        else { fails++; bl.log('tileframes ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('tileframes ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const tileAt = (x, y) => Main.tile['Tile get_Item(int x, int y)'](x, y);
const place = (x, y, type) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, type, true, false, -1, 0);
const save = () => Terraria.IO.WorldFile['void SaveWorld(WorldSaveContext saveContext)'](0);

function cells() {
    const modType = 753;
    const sx = Main.spawnTileX - 4, sy = Main.spawnTileY - 12;
    const out = [];
    for (let dy = 0; dy < 3; dy++) {
        for (let dx = 0; dx < 3; dx++) {
            out.push({ x: sx + dx, y: sy + dy, type: modType });
            out.push({ x: sx + 5 + dx, y: sy + dy, type: STONE });
        }
    }
    return out;
}

function snapshot() {
    return cells().map((c) => {
        const t = tileAt(c.x, c.y);
        return { x: c.x, y: c.y, type: bl.tiles.typeAt(c.x, c.y), fx: t.frameX, fy: t.frameY };
    });
}

const same = (a, b) => a.type === b.type && a.fx === b.fx && a.fy === b.fy;
const list = (xs) => xs.map((s) => `${s.x},${s.y} ${s.type}:${s.fx},${s.fy}`).join(' | ');

let before = null, first = null;
function start() {
    const txt = bl.file.read(FILE);
    before = txt ? JSON.parse(txt) : null;
    first = snapshot();
    bl.log('tileframes 1o quadro: ' + first.map((s) => `${s.type}:${s.fx},${s.fy}`).join(' '));
}

function finish() {
    const now = snapshot();
    bl.log('tileframes quadro 90: ' + now.map((s) => `${s.type}:${s.fx},${s.fy}`).join(' '));
    if (!before) {
        for (const c of cells()) place(c.x, c.y, c.type);
        const placed = snapshot();
        bl.log('tileframes colocado: ' + placed.map((s) => `${s.type}:${s.fx},${s.fy}`).join(' '));
        check('colocou os dois 3x3', () => placed.every((s, k) => s.type === cells()[k].type) || 'tipos errados');
        save();
        bl.file.write(FILE, JSON.stringify(snapshot()));
        bl.log('tileframes rodada: salvou');
        return;
    }
    const modTile = (k) => before[k].type >= 753;
    check('1o quadro: o tile de mod voltou com o quadro salvo', () => {
        const bad = first.filter((s, k) => modTile(k) && !same(before[k], s));
        return bad.length === 0 || list(bad);
    });
    check('1o quadro: a pedra voltou sem quadro (-1), como no jogo sem mods', () => {
        const bad = first.filter((s, k) => !modTile(k) && (s.type !== before[k].type || s.fx !== -1));
        return bad.length === 0 || list(bad);
    });
    check('quadro 90: tudo enquadrado de novo, com o tipo certo', () => {
        const bad = now.filter((s, k) => s.type !== before[k].type || s.fx < 0 || s.fy < 0);
        return bad.length === 0 || list(bad);
    });
    const rerolled = (pick) => now.filter((s, k) => pick(k) && !same(before[k], s)).length;
    bl.log(`tileframes variante sorteada de novo: ${rerolled(modTile)} de 9 tiles de mod, ` +
           `${rerolled((k) => !modTile(k))} de 9 pedras`);
    bl.file.delete(FILE);
    bl.log('tileframes rodada: conferiu');
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
        bl.log('tileframes FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('tileframes: carregado');
