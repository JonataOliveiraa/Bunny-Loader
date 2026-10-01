// Tempo de quadro: o DoUpdate e o DoDraw do jogo (CPU da thread do jogo) e o
// intervalo entre um DoDraw e o seguinte (o quadro inteiro, com a espera da
// GPU). Espera o mundo assentar, mede 300 quadros por zoom e loga a média e o
// percentil 90. Os dois hooks custam um despacho por quadro cada: nada perto
// do que medem.
const Main = Terraria.Main;

const ZOOMS = [1, 2, 0.75];
// O resumo também no chat: no celular, sem adb, é onde dá para ler.
const say = (text) => {
    bl.log(text);
    try {
        Main['void NewText(string newText, byte R, byte G, byte B, bool onlyCurrentPlayer)'](text, 255, 240, 120, false);
    } catch (e) {
        bl.log('frametime: chat: ' + e);
    }
};
const SETTLE = 240, SAMPLES = 300;

let phase = -1, frames = 0;
let update = [], draw = [], gap = [];
let lastDraw = 0;
// Coletas de ciclos do QuickJS: o limiar muda a cada uma.
let gcs = 0, lastThreshold = 0;
const hasGc = typeof bl.gcThreshold === 'function';

// Os hooks que mais recebem chamadas por quadro (bl.hookStats: despacho e JS).
let statsAt = null;
function snapshot() {
    const m = new Map();
    if (typeof bl.hookStats !== 'function') return m;   // app antigo, sem o contador
    for (const h of bl.hookStats()) m.set(h.name, h);
    return m;
}
function logTop(zoom, frames) {
    const now = snapshot();
    const rows = [];
    for (const [name, h] of now) {
        const before = statsAt && statsAt.get(name);
        const calls = (h.calls - (before ? before.calls : 0)) / frames;
        const js = (h.js - (before ? before.js : 0)) / frames;
        const ms = ((h.jsMs || 0) - (before ? before.jsMs || 0 : 0)) / frames;
        if (calls > 0) rows.push({ name: name.replace(/Microsoft\.Xna\.Framework\.(Graphics\.)?|System\.|Terraria\./g, ''), calls, js, ms });
    }
    // Onde o JS gasta (tempo por quadro), e os que só passam pelo despacho.
    const byTime = rows.filter((r) => r.js > 0).sort((x, y) => y.ms - x.ms);
    const total = byTime.reduce((t, r) => t + r.ms, 0);
    say('frametime zoom ' + zoom + ' JS nos hooks: ' + total.toFixed(2) + ' ms/quadro em ' + byTime.length + ' hooks');
    for (const r of byTime.slice(0, 15)) {
        bl.log('frametime zoom ' + zoom + ' js ' + r.ms.toFixed(3) + ' ms, ' + r.js.toFixed(1) + '/' + r.calls.toFixed(0) + ' chamadas: ' + r.name);
    }
    for (const r of rows.filter((r) => r.js === 0).sort((x, y) => y.calls - x.calls).slice(0, 8)) {
        bl.log('frametime zoom ' + zoom + ' só despacho ' + r.calls.toFixed(0) + '/quadro: ' + r.name);
    }
    statsAt = now;
}

const stats = (a) => {
    const s = a.slice().sort((x, y) => x - y);
    const avg = s.reduce((t, v) => t + v, 0) / (s.length || 1);
    return avg.toFixed(2) + ' ms (p90 ' + (s[Math.floor(s.length * 0.9)] || 0).toFixed(2) + ')';
};

Main['void DoUpdate(GameTime gameTime)'].hook((original, main, time) => {
    const t0 = performance.now();
    original(main, time);
    if (phase >= 0) update.push(performance.now() - t0);
});

Main['void DoDraw(GameTime gameTime)'].hook((original, main, time) => {
    const t0 = performance.now();
    original(main, time);
    const t1 = performance.now();
    if (Main.gameMenu) return;
    if (phase >= 0 && phase < ZOOMS.length) {
        draw.push(t1 - t0);
        if (lastDraw) gap.push(t0 - lastDraw);
    }
    lastDraw = t0;
    if (hasGc) {
        const th = bl.gcThreshold();
        if (lastThreshold && th !== lastThreshold) ++gcs;
        lastThreshold = th;
    }

    ++frames;
    if (phase < 0) {
        if (frames < SETTLE) return;
        phase = 0;
        Main.GameZoomTarget = ZOOMS[0];
        statsAt = snapshot();
        frames = 0;
        return;
    }
    if (phase >= ZOOMS.length || frames < SAMPLES) return;
    // Os primeiros 30 quadros de cada zoom são a transição: fora.
    const cut = (a) => a.slice(30);
    say('frametime zoom ' + ZOOMS[phase] + ': quadro ' + stats(cut(gap)) + ', DoDraw ' + stats(cut(draw)) +
           ', DoUpdate ' + stats(cut(update)) + ', fps ' + (1000 / (cut(gap).reduce((t, v) => t + v, 0) / (cut(gap).length || 1))).toFixed(1));
    logTop(ZOOMS[phase], frames);
    if (hasGc) bl.log('frametime zoom ' + ZOOMS[phase] + ' coletas do QuickJS: ' + gcs + ' em ' + frames + ' quadros');
    gcs = 0;
    update = []; draw = []; gap = [];
    frames = 0;
    ++phase;
    if (phase < ZOOMS.length) Main.GameZoomTarget = ZOOMS[phase];
    else {
        Main.GameZoomTarget = 1;
        if (typeof bl.gc === 'function') {
            const ms = [bl.gc(), bl.gc(), bl.gc()];
            bl.log('frametime coleta forçada: ' + ms.map((v) => v.toFixed(2)).join(', ') + ' ms');
        }
        say('frametime FIM: tudo ok');
    }
});
// O custo do próprio relógio (o bl.hookStats mede com ele): 10 mil leituras.
{
    const t0 = performance.now();
    let x = 0;
    for (let i = 0; i < 10000; i++) x += performance.now();
    bl.log('frametime relógio: ' + ((performance.now() - t0) * 100).toFixed(0) + ' ns por leitura' + (x < 0 ? '!' : ''));
}
bl.log('frametime: carregado');

export default class TestFrametime extends Mod {}
