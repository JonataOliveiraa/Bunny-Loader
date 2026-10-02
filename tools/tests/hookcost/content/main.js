// O custo de um hook que só repassa ao original(), em métodos que o jogo chama
// uma vez por quadro, medido de dois jeitos: pelo bl.hookStats (o despacho em
// C++, sem o tempo do original) e por dentro do JS (performance.now em volta do
// callback inteiro, menos o original). Loga "hookcost ...".
const Main = Terraria.Main;
const now = () => performance.now();

const HOOKS = [
    ['DoUpdate (longo)', Main['void DoUpdate(GameTime gameTime)']],
    ['UpdateAudio', Main['void UpdateAudio()']],
    ['Player.UpdateBiomes', Terraria.Player['void UpdateBiomes()']],
    ['Player.ResetEffects', Terraria.Player['void ResetEffects()']],
];
const inJs = new Map();   // nome -> { calls, ms, orig }

for (const [name, m] of HOOKS) {
    const s = { calls: 0, ms: 0, orig: 0 };
    inJs.set(name, s);
    m.hook((original, ...args) => {
        const t0 = now();
        const t1 = now();
        const r = original(...args);
        const t2 = now();
        s.calls++;
        s.orig += t2 - t1;
        s.ms += now() - t0;
        return r;
    });
}

function statsByName() {
    const out = new Map();
    for (const h of bl.hookStats()) {
        // Build de medição: o nome traz os trechos do despacho (" |seg a b c d e", ns somados).
        const m = / \|seg ([\d ]+)$/.exec(h.name);
        h.seg = m ? m[1].split(' ').map(Number) : null;
        out.set(m ? h.name.slice(0, m.index) : h.name, h);
    }
    return out;
}

let frames = 0, before = null;
Main['void DoDraw(GameTime gameTime)'].hook((original, main, time) => {
    original(main, time);
    if (Main.gameMenu) return;
    frames++;
    if (frames === 240) {
        before = statsByName();
        for (const s of inJs.values()) { s.calls = 0; s.ms = 0; s.orig = 0; }
    }
    if (frames === 540) {
        const after = statsByName();
        for (const [name, h] of after) {
            const b = before.get(name);
            const calls = h.calls - (b ? b.calls : 0), js = h.js - (b ? b.js : 0);
            const ms = h.jsMs - (b ? b.jsMs : 0);
            if (js <= 0) continue;
            let seg = '';
            if (h.seg && b && b.seg) {
                const us = h.seg.map((v, k) => ((v - b.seg[k]) / js / 1000).toFixed(1));
                seg = ` [pré ${us[0]}, callback ${us[1]}, original() antes ${us[2]} depois ${us[3]}, pós ${us[4]}]`;
            }
            bl.log(`hookcost C++: ${(ms * 1000 / js).toFixed(1)} µs/chamada${seg}, ${js} chamadas: ${name}`);
        }
        for (const [name, s] of inJs) {
            if (!s.calls) continue;
            bl.log(`hookcost JS: ${((s.ms - s.orig) * 1000 / s.calls).toFixed(1)} µs/chamada fora do original ` +
                   `(original ${(s.orig * 1000 / s.calls).toFixed(0)} µs), ${s.calls} chamadas: ${name}`);
        }
        bl.log('hookcost FIM: tudo ok');
    }
});

export default class TestHookCost extends Mod {}
