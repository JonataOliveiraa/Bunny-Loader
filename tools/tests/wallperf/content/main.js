const Main = Terraria.Main;
const W = Terraria.WorldGen;
const WF = Terraria.IO.WorldFile;
const { TileID, WallID } = Terraria.ID;

const tileAt = (x, y) => Main.tile['Tile get_Item(int x, int y)'](x, y);
const kill = (x, y) => W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](x, y, false, false, true);
const place = (x, y, type) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, type, true, true, -1, 0);
const placeWall = W['void PlaceWall(int i, int j, int type, bool mute)'];
const save = () => WF['void SaveWorld(WorldSaveContext saveContext)'](0);

const HALF_W = 35, HEIGHT = 30;
let area = null;

function carve() {
    const cx = Main.spawnTileX, floorY = Main.spawnTileY + 70;
    area = { x0: cx - HALF_W, x1: cx + HALF_W, top: floorY - HEIGHT, floorY, cx };
    for (let x = area.x0; x <= area.x1; x++) {
        for (let y = area.top; y < floorY; y++) {
            kill(x, y);
            tileAt(x, y).liquid = 0;
        }
        place(x, floorY, TileID.Stone);
    }
    clearWalls();
}

function clearWalls() {
    for (let x = area.x0; x <= area.x1; x++) {
        for (let y = area.top; y < area.floorY; y++) tileAt(x, y)['void set_wall(ushort value)'](0);
    }
}

function fillWalls(type) {
    clearWalls();
    if (type <= 0) return { cells: 0, ms: 0 };
    const t0 = Date.now();
    let cells = 0;
    for (let x = area.x0; x <= area.x1; x++) {
        for (let y = area.top; y < area.floorY; y++) {
            placeWall(x, y, type, true);
            cells++;
        }
    }
    return { cells, ms: Date.now() - t0 };
}

const cost = {};
function instrument(name) {
    const m = ModContent.Find(ModWall, 'examplemod/' + name);
    for (const method of ['ModifyLight', 'AnimateWall', 'WallFrame']) {
        const fn = m[method];
        if (typeof fn !== 'function') continue;
        const c = cost[method] = cost[method] || { ms: 0, calls: 0 };
        m[method] = function (...args) {
            const t0 = Date.now();
            try { return fn.apply(this, args); } finally { c.ms += Date.now() - t0; c.calls++; }
        };
    }
    return m.Type;
}

let frames = 0, done = false, last = 0;
const phases = [];
let phase = null;

function snapshot() {
    const m = new Map();
    if (typeof bl.hookStats !== 'function') return m;
    for (const h of bl.hookStats()) m.set(h.name, h);
    return m;
}

function hookCost(p) {
    const rows = [];
    for (const [name, h] of p.statsEnd) {
        const b = p.statsStart.get(name);
        const calls = (h.calls - (b ? b.calls : 0)) / p.frames;
        const ms = ((h.jsMs || 0) - (b ? b.jsMs || 0 : 0)) / p.frames;
        if (calls > 0) rows.push({ name: name.replace(/Microsoft\.Xna\.Framework\.(Graphics\.)?|System\.|Terraria\./g, ''), calls, ms });
    }
    rows.sort((a, b) => b.ms - a.ms);
    const total = rows.reduce((sum, r) => sum + r.ms, 0);
    return `JS ${total.toFixed(3)} ms/quadro; ` + rows.slice(0, 4).map((r) => `${r.name} ${r.ms.toFixed(3)} ms (${r.calls.toFixed(1)}x)`).join('; ');
}

function closePhase() {
    if (!phase) return;
    phase.statsEnd = snapshot();
    phase.frames = Math.max(1, frames - phase.startFrame);
}

function begin(name, type) {
    closePhase();
    const got = fillWalls(type);
    phase = { name, times: [], got, statsStart: snapshot(), startFrame: frames };
    phases.push(phase);
    bl.log(`wallperf fase ${name}: ${got.cells} parede(s) colocada(s) em ${got.ms} ms`);
    last = 0;
}

function stats(p) {
    const t = p.times.slice(30).sort((a, b) => a - b);
    const avg = t.reduce((s, v) => s + v, 0) / Math.max(1, t.length);
    const p95 = t[Math.floor(t.length * 0.95)] || 0;
    return `quadro médio ${avg.toFixed(2)} ms, p95 ${p95.toFixed(2)} ms (${t.length} quadros)`;
}

function timeSave(label) {
    const t0 = Date.now();
    save();
    bl.log(`wallperf save com ${label}: ${Date.now() - t0} ms`);
    last = 0;
}

Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;

    const now = Date.now();
    if (phase && last) phase.times.push(now - last);
    last = now;
    frames++;

    const p = Main.player[Main.myPlayer];
    if (area) {
        p.position = Vector2.new(area.cx * 16, (area.floorY - 3) * 16);
        p.velocity = Vector2.new(0, 0);
        p.statLife = p.statLifeMax2;
        p.breath = p.breathMax;
        p.fallStart = Math.floor(p.position.Y / 16);
    }

    if (frames === 1) carve();
    if (frames === 60) begin('sem parede', 0);
    if (frames === 360) begin('parede do jogo (pedra)', WallID.Stone);
    if (frames === 650) timeSave('2130 paredes do jogo');
    if (frames === 660) begin('ExampleWall', instrument('ExampleWall'));
    if (frames === 950) timeSave('2130 ExampleWall');
    if (frames === 960) {
        begin('ExampleWallAdvanced (luz, animacao, WallFrame)', instrument('ExampleWallAdvanced'));
        let zero = 0, all = 0;
        for (let x = area.x0; x <= area.x1; x++) {
            for (let y = area.top; y < area.floorY; y++) {
                all++;
                if (tileAt(x, y)['byte wallFrameNumber()']() === 0) zero++;
            }
        }
        const pct = 100 * zero / Math.max(1, all);
        bl.log(`wallperf WallFrame: quadro 0 em ${pct.toFixed(1)}% das paredes (sem o mod seria ~33%): ${pct < 20 ? 'ok' : 'FALHOU'}`);
    }
    if (frames === 1260) {
        done = true;
        closePhase();
        phase = null;
        for (const ph of phases) bl.log(`wallperf ${ph.name}: ${stats(ph)}`);
        for (const ph of phases) bl.log(`wallperf ${ph.name}: ${hookCost(ph)}`);
        bl.log('wallperf custo dos metodos na ultima fase (+ WallFrame ao colocar): ' +
            Object.entries(cost).map(([n, c]) => `${n} ${c.ms} ms/${c.calls}`).join(', '));
        clearWalls();
        save();
        p.position = Vector2.new(Main.spawnTileX * 16, (Main.spawnTileY - 3) * 16);
        bl.log('wallperf FIM');
    }
});
bl.log('wallperf: carregado');

export default class TestWallPerf extends Mod {}
