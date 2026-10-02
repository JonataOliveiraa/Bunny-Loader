// Avaliação de desempenho do loader dos mods (ExMod). Loga "perfloader ...".
//
// 1. Na carga (sem mundo): os padrões que os loaders repetem a cada chamada,
//    em JS puro — Safe.Run com rótulo concatenado e closure nova, dois Ref
//    novos por bloco animado, for-of contra índice.
// 2. No mundo, com o Example Mod: 60 projéteis animados (AI, PreDraw, GetAlpha)
//    e 15 slimes de mod vivos; o custo por entidade de cada hook (bl.hookStats)
//    e o tempo de quadro com e sem elas.
const Main = Terraria.Main;
const log = (s) => bl.log('perfloader ' + s);
const now = () => performance.now();

// ---------------- 1. padrões, em JS puro ----------------
const N = 20000;
function time(fn) {
    let best = Infinity;
    for (let r = 0; r < 5; r++) {
        const t0 = now();
        fn();
        best = Math.min(best, now() - t0);
    }
    return best;
}

const reported = new Set();
// O Safe.Run do loader (Core/Safe.js), igual.
const SafeRun = (label, fn) => {
    try { return fn(); } catch (e) { if (!reported.has(label)) { reported.add(label); bl.log(String(e)); } return undefined; }
};
class Fake { AI(p) { return p + 1; } AnimateTile(f, c) { c.value++; if (c.value > 5) { c.value = 0; f.value = (f.value + 1) % 4; } } }
const inst = new Fake();
const name = 'ExampleAdvancedAnimatedProjectile';
let sink = 0;

function patterns() {
    const base = time(() => { let s = 0; for (let i = 0; i < N; i++) s += inst.AI(i); sink += s; });
    const r = {};
    r['Safe.Run(n + ".AI", () => m.AI(p))'] = time(() => { let s = 0; for (let i = 0; i < N; i++) s += SafeRun(name + '.AI', () => inst.AI(i)); sink += s; });
    const label = name + '.AI';
    r['Safe.Run(rotulo pronto, () => m.AI(p))'] = time(() => { let s = 0; for (let i = 0; i < N; i++) s += SafeRun(label, () => inst.AI(i)); sink += s; });
    r['try { m.AI(p) } catch'] = time(() => {
        let s = 0;
        for (let i = 0; i < N; i++) { try { s += inst.AI(i); } catch (e) { SafeRun(label, () => { throw e; }); } }
        sink += s;
    });
    const f = { value: 0 }, c = { value: 0 };
    r['AnimateTile com 2 Ref novos + closure'] = time(() => {
        for (let i = 0; i < N; i++) {
            const frame = new Ref(f.value), counter = new Ref(c.value);
            SafeRun(label, () => inst.AnimateTile(frame, counter));
            f.value = frame.value | 0; c.value = counter.value | 0;
        }
    });
    const fr = new Ref(0), cr = new Ref(0);
    r['AnimateTile com 2 Ref reusados, sem closure'] = time(() => {
        for (let i = 0; i < N; i++) {
            fr.value = f.value; cr.value = c.value;
            try { inst.AnimateTile(fr, cr); } catch (e) { SafeRun(label, () => { throw e; }); }
            f.value = fr.value | 0; c.value = cr.value | 0;
        }
    });
    const list = [inst, inst, inst, inst, inst, inst, inst, inst];
    r['for-of em 8 (por volta)'] = time(() => { let s = 0; for (let i = 0; i < N / 8; i++) for (const m of list) s += m.AI(1); sink += s; }) ;
    r['for indice em 8 (por volta)'] = time(() => { let s = 0; for (let i = 0; i < N / 8; i++) for (let k = 0; k < list.length; k++) s += list[k].AI(1); sink += s; });
    const byType = new Map([[1155, inst]]);
    r['Map.get(type)'] = time(() => { let s = 0; for (let i = 0; i < N; i++) s += byType.get(1155) ? 1 : 0; sink += s; });
    log('padrões: base (chamada de método JS) ' + (base * 1e6 / N).toFixed(0) + ' ns');
    for (const k in r) log(`padrões: ${k} = ${((r[k] - base) * 1e6 / N).toFixed(0)} ns acima da base`);
}
patterns();

// ---------------- 2. entidades no mundo ----------------
const newProj = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];
const newNPC = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];

function statsByName() {
    const out = new Map();
    for (const h of bl.hookStats()) {
        const m = / \|seg ([\d ]+)$/.exec(h.name);
        out.set(m ? h.name.slice(0, m.index) : h.name, h);
    }
    return out;
}
const short = (n) => n.replace(/Microsoft\.Xna\.Framework\.(Graphics\.)?|System\.|Terraria\./g, '');

function report(tag, before, after, frames) {
    const rows = [];
    let total = 0;
    for (const [n, h] of after) {
        const b = before.get(n);
        const js = h.js - (b ? b.js : 0), calls = h.calls - (b ? b.calls : 0), ms = h.jsMs - (b ? b.jsMs : 0);
        if (js <= 0) continue;
        total += ms;
        rows.push({ n, js: js / frames, calls: calls / frames, ms: ms / frames, us: ms * 1000 / js });
    }
    rows.sort((x, y) => y.ms - x.ms);
    log(`${tag}: JS nos hooks ${(total / frames).toFixed(2)} ms/quadro em ${rows.length} hooks`);
    for (const r of rows.slice(0, 14)) {
        log(`${tag}: ${r.ms.toFixed(3)} ms/quadro, ${r.js.toFixed(1)}/${r.calls.toFixed(0)} chamadas, ${r.us.toFixed(1)} µs cada: ${short(r.n)}`);
    }
}

let frames = 0, phase = 0, before = null, drawMs = [], updMs = [];
let projType = 0, npcType = 0;
const avg = (a) => (a.slice(30).reduce((t, v) => t + v, 0) / Math.max(1, a.length - 30)).toFixed(2);

Main['void DoUpdate(GameTime gameTime)'].hook((original, main, t) => {
    const t0 = now();
    original(main, t);
    if (phase === 1 || phase === 3) updMs.push(now() - t0);
});
Main['void DoDraw(GameTime gameTime)'].hook((original, main, t) => {
    const t0 = now();
    original(main, t);
    if (phase === 1 || phase === 3) drawMs.push(now() - t0);
});

function keepAlive(p) {
    // Em volta do jogador, parados: a AI do Example mata aos 60 quadros.
    const list = [];
    for (let i = 0; i < 1000; i++) {
        const q = Main.projectile[i];
        if (q.active && q.type === projType) { q.ai[0] = 10; list.push(q); }
    }
    const c = p.Center;
    for (let k = list.length; k < 60; k++) {
        newProj(null, c.X + (k % 10) * 30 - 150, c.Y - 120 - Math.floor(k / 10) * 30, 0.5, 0, projType, 0, 0, Main.myPlayer, 10, 0, 0, null);
    }
}

Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || phase > 4) return;
    self.immune = true; self.immuneTime = 60; self.statLife = self.statLifeMax2;
    frames++;
    if (phase === 0 && frames === 240) {
        projType = ModContent.ProjectileType('ExampleAdvancedAnimatedProjectile');
        npcType = ModContent.NPCType('ExampleSlimeNPC');
        log(`tipos: projétil ${projType}, NPC ${npcType}`);
        before = statsByName(); frames = 0; phase = 1; drawMs = []; updMs = [];
    } else if (phase === 1 && frames === 300) {
        report('sem entidades', before, statsByName(), 300);
        log(`sem entidades: DoUpdate ${avg(updMs)} ms, DoDraw ${avg(drawMs)} ms`);
        if (projType <= 0 || npcType <= 0) { phase = 5; log('FIM: sem o Example Mod (perfloader FIM)'); return; }
        const c = self.Center;
        for (let k = 0; k < 15; k++) newNPC(null, Math.floor(c.X) + (k - 7) * 40, Math.floor(c.Y) - 200, npcType, 0, 0, 0, 0, 0, 255);
        phase = 2; frames = 0;
    } else if (phase === 2) {
        keepAlive(self);
        if (frames === 120) { before = statsByName(); frames = 0; phase = 3; drawMs = []; updMs = []; }
    } else if (phase === 3) {
        keepAlive(self);
        if (frames === 300) {
            report('60 projéteis + 15 slimes', before, statsByName(), 300);
            log(`com entidades: DoUpdate ${avg(updMs)} ms, DoDraw ${avg(drawMs)} ms`);
            let np = 0, nn = 0;
            for (let k = 0; k < 1000; k++) { const q = Main.projectile[k]; if (q.active && q.type === projType) { np++; q.active = false; } }
            for (let k = 0; k < 200; k++) { const n = Main.npc[k]; if (n.active && n.type === npcType) { nn++; n.active = false; } }
            log(`vivos no fim: ${np} projéteis, ${nn} slimes`);
            log("FIM: tudo ok");
            phase = 5;
        }
    }
});

export default class TestPerfLoader extends Mod {}
