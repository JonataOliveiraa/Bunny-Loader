// Quanto custa um monte de tile de mod na tela (luz, chama, SetDrawPositions,
// animação): o tempo entre quadros com 150 tochas do jogo, depois com 150
// tochas de mod e 20 fogueiras de mod. Não salva. Loga "tileperf ...".
const Main = Terraria.Main;
const W = Terraria.WorldGen;
const { TileID, WallID } = Terraria.ID;

const tileAt = (x, y) => Main.tile['Tile get_Item(int x, int y)'](x, y);
const kill = (x, y) => W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](x, y, false, false, true);
const place = (x, y, type) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, type, true, true, -1, 0);
const placeObject = (x, y, type) => W['bool PlaceObject(int x, int y, int type, bool mute, int style, int alternate, int random, int direction)'](x, y, type, true, 0, 0, -1, -1);

let area = null;
function carve() {
    const cx = Main.spawnTileX, floorY = Main.spawnTileY + 70;
    area = { x0: cx - 20, x1: cx + 20, top: floorY - 22, floorY, cx };
    for (let x = area.x0; x <= area.x1; x++) {
        for (let y = area.top; y < floorY; y++) {
            kill(x, y);
            tileAt(x, y).liquid = 0;
            W['void PlaceWall(int i, int j, int type, bool mute)'](x, y, WallID.Stone, true);
        }
        place(x, floorY, TileID.Stone);
    }
}

// 150 tochas na parede (30 x 5) e, opcional, 20 fogueiras no chão.
function fill(torch, campfire) {
    const placed = [];
    if (torch < 0) return { torches: 0, fires: 0 };
    for (let row = 0; row < 5; row++) {
        for (let col = 0; col < 30; col++) {
            const x = area.x0 + 5 + col, y = area.top + 2 + row * 3;
            if (placeObject(x, y, torch)) placed.push(x);
        }
    }
    let fires = 0;
    if (campfire >= 0) {
        for (let k = 0; k < 10; k++) fires += placeObject(area.x0 + 3 + k * 4, area.floorY - 1, campfire) ? 1 : 0;
    }
    return { torches: placed.length, fires };
}

function clear() {
    for (let x = area.x0 - 3; x <= area.x1 + 3; x++) for (let y = area.top - 3; y <= area.floorY; y++) tileAt(x, y).liquid = 0;
    for (let x = area.x0; x <= area.x1; x++) {
        for (let y = area.top; y < area.floorY; y++) kill(x, y);
    }
}


// Quanto cada método da tocha de mod custa por quadro (o instrumento fica em
// volta do método da instância; o TileLoader chama pela instância).
const cost = {};
function instrument(type) {
    const m = ModContent.GetModTile(type);
    for (const name of ['PostDraw', 'ModifyLight', 'SetDrawPositions', 'EmitParticles', 'AnimateTile', 'PreDraw']) {
        const fn = m[name];
        if (typeof fn !== 'function') continue;
        const c = cost[name] = { ms: 0, calls: 0 };
        m[name] = function (...args) {
            const t0 = Date.now();
            try { return fn.apply(this, args); } finally { c.ms += Date.now() - t0; c.calls++; }
        };
    }
}
let frames = 0, done = false, last = 0;
const phases = [];   // { name, times: [] }
let phase = null;

function begin(name, torch, campfire) {
    bl.log(`tileperf fase ${name}: tocha ${torch}, fogueira ${campfire}`);
    clear();
    const got = fill(torch, campfire);
    phase = { name, times: [], got };
    phases.push(phase);
}

function report() {
    for (const p of phases) {
        const t = p.times.slice(30).sort((a, b) => a - b);   // sem os primeiros quadros
        const avg = t.reduce((s, v) => s + v, 0) / t.length;
        const p95 = t[Math.floor(t.length * 0.95)];
        bl.log(`tileperf ${p.name}: ${p.got.torches} tochas, ${p.got.fires} fogueiras; quadro médio ${avg.toFixed(2)} ms, p95 ${p95.toFixed(2)} ms`);
    }
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

    const T = (name) => ModContent.TileType('examplemod/' + name);
    if (frames === 1) carve();
    if (frames === 60) begin('nada', -1, -1);
    if (frames === 360) begin('tochas do jogo', TileID.Torches, -1);
    if (frames === 660) {
        instrument(T('ExampleTorch'));
        begin('tochas e fogueiras de mod', T('ExampleTorch'), T('ExampleCampfire'));
    }
    if (frames === 460 || frames === 760) {
        const sp = Main.screenPosition;
        bl.log(`tileperf tela ${Math.floor(sp.X / 16)},${Math.floor(sp.Y / 16)} (area ${area.x0}..${area.x1}, ${area.top}..${area.floorY}), morto ${p.dead}`);
    }
    if (frames === 960) {
        done = true;
        phase = null;
        report();
        bl.log('tileperf custo em 300 quadros: ' + Object.entries(cost).map(([n, c]) => `${n} ${c.ms} ms/${c.calls}`).join(', '));
        clear();
        p.position = Vector2.new(Main.spawnTileX * 16, (Main.spawnTileY - 3) * 16);
        bl.log('tileperf FIM');
    }
});
bl.log('tileperf: carregado');

export default class TestTilePerf extends Mod {}
