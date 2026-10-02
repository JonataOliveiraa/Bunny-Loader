// O custo da chuva com o Example Mod ligado (a água dele tem GetRainTexture):
// chuva forte forçada, primeiro com a água do jogo e depois com a do mod (o
// CalculateWaterStyle devolve o estilo dela). Em cada fase, 300 quadros: o
// DoDraw, o intervalo entre quadros, as gotas ativas e os hooks que mais
// entraram no JS (bl.hookStats). Loga 'rainperf ...'.
const Main = Terraria.Main;
const MOD_WATER = 15;   // a primeira água de mod: a do Example Mod
const PHASES = [
    { name: 'chuva com a água do jogo', water: -1 },
    { name: 'chuva com a água do mod', water: MOD_WATER },
];
const SETTLE = 120, SAMPLES = 300;

let phase = -1, frames = 0, draw = [], gap = [], lastDraw = 0, statsAt = null;

function snapshot() {
    const m = new Map();
    if (typeof bl.hookStats !== 'function') return m;
    for (const h of bl.hookStats()) m.set(h.name, h);
    return m;
}

function logHooks(label) {
    const now = snapshot();
    const rows = [];
    for (const [name, h] of now) {
        const before = statsAt && statsAt.get(name);
        const calls = (h.calls - (before ? before.calls : 0)) / SAMPLES;
        const js = (h.js - (before ? before.js : 0)) / SAMPLES;
        const ms = ((h.jsMs || 0) - (before ? before.jsMs || 0 : 0)) / SAMPLES;
        if (js > 0) rows.push({ name: name.replace(/Microsoft\.Xna\.Framework\.(Graphics\.)?|Terraria\./g, ''), calls, js, ms });
    }
    rows.sort((x, y) => y.ms - x.ms);
    const total = rows.reduce((t, r) => t + r.ms, 0);
    bl.log(`rainperf ${label}: JS nos hooks ${total.toFixed(2)} ms/quadro`);
    for (const r of rows.slice(0, 6)) {
        bl.log(`rainperf ${label}:   ${r.ms.toFixed(3)} ms, ${r.js.toFixed(0)} entradas no JS/quadro: ${r.name}`);
    }
    statsAt = now;
}

const avg = (a) => (a.reduce((t, v) => t + v, 0) / (a.length || 1)).toFixed(2);

function activeDrops() {
    const rain = Main.rain;
    let n = 0;
    for (let i = 0; i < rain.length; i++) if (rain[i].active) n++;
    return n;
}

// A chuva forte, segurada a cada quadro.
Main['void DoUpdate(GameTime gameTime)'].hook((original, main, time) => {
    if (!Main.gameMenu && phase >= 0 && phase < PHASES.length) {
        Main.raining = true;
        if (Main.rainTime < 3600) Main.rainTime = 3600;
        Main.maxRaining = 0.9;
        Main.cloudAlpha = 0.9;
    }
    original(main, time);
});

Main['int CalculateWaterStyle(bool ignoreFountains)'].hook((original, ignoreFountains) => {
    const want = phase >= 0 && phase < PHASES.length ? PHASES[phase].water : -1;
    return want >= 0 ? want : original(ignoreFountains);
});

Main['void DoDraw(GameTime gameTime)'].hook((original, main, time) => {
    const t0 = performance.now();
    original(main, time);
    if (Main.gameMenu) return;
    ++frames;
    if (phase < 0) {
        if (frames < SETTLE) return;
        phase = 0;
        frames = 0;
        bl.log(`rainperf: fase ${PHASES[0].name}`);
        return;
    }
    if (phase >= PHASES.length) return;
    // Os primeiros SETTLE quadros da fase: a chuva enche a tela.
    if (frames === SETTLE) statsAt = snapshot();
    if (frames > SETTLE) {
        draw.push(performance.now() - t0);
        if (lastDraw) gap.push(t0 - lastDraw);
    }
    lastDraw = t0;
    if (frames < SETTLE + SAMPLES) return;

    const label = PHASES[phase].name;
    bl.log(`rainperf ${label}: estilo da água ${Main.waterStyle}, ${activeDrops()} gotas ativas, ` +
           `DoDraw ${avg(draw)} ms, quadro ${avg(gap)} ms (${(1000 / avg(gap)).toFixed(1)} fps)`);
    logHooks(label);
    draw = []; gap = []; lastDraw = 0; frames = 0;
    if (++phase < PHASES.length) bl.log(`rainperf: fase ${PHASES[phase].name}`);
    else {
        Main.raining = false;
        Main.maxRaining = 0;
        Main.rainTime = 0;
        bl.log('rainperf FIM: tudo ok');
    }
});
bl.log('rainperf: carregado');

export default class TestRainperf extends Mod {}
