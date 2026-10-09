// Auditoria dos hooks: onde o despacho é chamado muitas vezes por quadro.
//
// 1. Parado (300 quadros): TODOS os hooks instalados (bl.hookStats), com as
//    chamadas que chegam ao despacho, as que entram no JS e o tempo de JS.
// 2. Mundo cheio (300 quadros): 15 coelhos, 40 projéteis do jogo, 100 itens no
//    chão e, com o Example Mod, 10 slimes e 30 projéteis de mod. A mesma lista.
// 3. A/B do despacho, no mundo cheio: o DoUpdate sem hook a mais, com 25 e 50
//    hooks barrados pela `flag` empilhados no WorldItem.UpdateItem (400
//    chamadas por quadro) e com um deles entrando no JS. A diferença pelos
//    despachos dá o custo de cada caminho. O MuMu oscila: rodar 2-3 vezes.
// Loga "hookaudit ...".
const Main = Terraria.Main;
const log = (s) => bl.log('hookaudit ' + s);
const now = () => performance.now();
const short = (n) => n.replace(/Microsoft\.Xna\.Framework\.(Graphics\.)?|System\.|Terraria\./g, '');

const newProj = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];
const newNPC = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];
const newItem = Terraria.Item['int NewItem(IEntitySource source, int X, int Y, int Width, int Height, int type, int stack, bool noBroadcast, int prefix, NewItemOwnership ownership, Nullable<Vector2> velocity, Item.NewItemModifier modifier)'];

// Os hooks empilhados do A/B (as flags deles), e quem conta as entradas no JS.
const updateItem = Terraria.WorldItem['void UpdateItem(int i)'];
const stacked = [];
let jsCalls = 0, jsBase = null;
function stack(n) {
    for (let k = 0; k < n; k++) {
        const flag = 'hookaudit.stack' + stacked.length;
        updateItem.hook((original, self, i) => { jsCalls++; original(self, i); }, { flag });
        stacked.push(flag);
    }
}
// Os despachos dos hooks do UpdateItem (o do jogo e os empilhados têm o mesmo
// nome no hookStats: conta todos e tira as 400 do hook do loader).
function countStacked(before, after) {
    let total = 0;
    for (const [n, h] of after) {
        if (!short(n).startsWith('WorldItem.UpdateItem')) continue;
        const b = before.get(n);
        total += h.calls - (b ? b.calls : 0);
    }
    return Math.max(0, total - 400 * FRAMES);
}

// Um registro por hook, não por método: dois loaders no mesmo método são dois
// despachos por chamada. A chave leva a ordem de instalação ("nome#2").
function stats() {
    const out = new Map(), seen = new Map();
    for (const h of bl.hookStats()) {
        const k = (seen.get(h.name) || 0) + 1;
        seen.set(h.name, k);
        out.set(h.name + '#' + k, h);
    }
    return out;
}

function reportAll(tag, before, after, frames) {
    const rows = [];
    for (const [n, h] of after) {
        const b = before.get(n);
        const calls = (h.calls - (b ? b.calls : 0)) / frames;
        if (calls <= 0) continue;
        const js = (h.js - (b ? b.js : 0)) / frames;
        const ms = (h.jsMs - (b ? b.jsMs : 0)) / frames;
        rows.push({ n: short(n.replace(/#1$/, '')), calls, js, ms });
    }
    rows.sort((x, y) => y.calls - x.calls || y.ms - x.ms);
    const total = rows.reduce((t, r) => t + r.ms, 0);
    const calls = rows.reduce((t, r) => t + r.calls, 0);
    const js = rows.reduce((t, r) => t + r.js, 0);
    log(`${tag}: ${rows.length} hooks chamados; ${calls.toFixed(0)} despachos/quadro, ${js.toFixed(0)} no JS, ${total.toFixed(2)} ms de JS/quadro (de ${after.size} instalados)`);
    for (const r of rows) {
        log(`${tag} | ${r.calls.toFixed(1)} chamadas | ${r.js.toFixed(1)} no JS | ${(r.ms * 1000).toFixed(1)} µs/quadro | ${r.n}`);
    }
}

// ---------------- tempo de quadro ----------------
let upd = [], draw = [], measuring = false;
Main['void DoUpdate(GameTime gameTime)'].hook((original, main, t) => {
    const t0 = now();
    original(main, t);
    if (measuring) upd.push(now() - t0);
});
Main['void DoDraw(GameTime gameTime)'].hook((original, main, t) => {
    const t0 = now();
    original(main, t);
    if (measuring) draw.push(now() - t0);
});
function frameStats() {
    const med = (a) => { const s = [...a].sort((x, y) => x - y); return s.length ? s[s.length >> 1] : NaN; };
    const mean = (a) => a.reduce((t, v) => t + v, 0) / Math.max(1, a.length);
    return { updMean: mean(upd), updMed: med(upd), drawMean: mean(draw), drawMed: med(draw) };
}
const fmt = (f) => `DoUpdate ${f.updMean.toFixed(3)} ms (med ${f.updMed.toFixed(3)}), DoDraw ${f.drawMean.toFixed(3)} ms (med ${f.drawMed.toFixed(3)})`;

// ---------------- entidades ----------------
let modProj = 0, modNpc = 0, modItem = 0;
const VANILLA_PROJ = 1;      // flecha de madeira: reposta parada a cada quadro
// O que o teste criou, para tirar no fim sem mexer no resto do mundo.
const spawnedNpcs = [], spawnedItems = [];
function spawnWorld(p) {
    const c = p.Center;
    const cx = Math.floor(c.X), cy = Math.floor(c.Y);
    for (let k = 0; k < 15; k++) spawnedNpcs.push(newNPC(null, cx + (k - 7) * 48, cy - 160, 46, 0, 0, 0, 0, 0, 255));
    if (modNpc > 0) for (let k = 0; k < 10; k++) spawnedNpcs.push(newNPC(null, cx + (k - 5) * 60, cy - 240, modNpc, 0, 0, 0, 0, 0, 255));
    // Itens longe do jogador (não são pegos), tipos diferentes (não se juntam).
    for (let k = 0; k < 100; k++) {
        const type = modItem > 0 && k % 10 === 0 ? modItem : 2 + k;
        spawnedItems.push(newItem(null, cx - 700 + (k % 25) * 24, cy - 300 - Math.floor(k / 25) * 24, 16, 16, type, 1, false, 0, 0, null, null));
    }
}
function keepAlive(p) {
    const c = p.Center;
    let vanilla = 0, mod = 0;
    for (let i = 0; i < 1000; i++) {
        const q = Main.projectile[i];
        if (!q.active) continue;
        if (q.type === VANILLA_PROJ && q.owner === Main.myPlayer) {
            vanilla++;
            q.timeLeft = 600;
        } else if (modProj > 0 && q.type === modProj) {
            mod++;
            q.ai[0] = 10;
        }
    }
    for (let k = vanilla; k < 40; k++) {
        newProj(null, c.X + (k % 10) * 32 - 160, c.Y - 260 - Math.floor(k / 10) * 32, 0, 0, VANILLA_PROJ, 0, 0, Main.myPlayer, 0, 0, 0, null);
    }
    if (modProj > 0) for (let k = mod; k < 30; k++) {
        newProj(null, c.X + (k % 10) * 30 - 150, c.Y - 120 - Math.floor(k / 10) * 30, 0.5, 0, modProj, 0, 0, Main.myPlayer, 10, 0, 0, null);
    }
}

// ---------------- roteiro ----------------
const FRAMES = 300;
let frames = 0, phase = 'settle', before = null, ab = [], baseline = null;

function startPhase(name) { phase = name; frames = 0; upd = []; draw = []; measuring = true; before = stats(); }

Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || phase === 'done') return;
    self.immune = true; self.immuneTime = 60; self.statLife = self.statLifeMax2;
    frames++;
    if (phase === 'settle' && frames === 240) {
        try { modProj = ModContent.ProjectileType('ExampleAdvancedAnimatedProjectile'); } catch (e) { modProj = 0; }
        try { modNpc = ModContent.NPCType('ExampleSlimeNPC'); } catch (e) { modNpc = 0; }
        try { modItem = ModContent.ItemType('ExampleItem'); } catch (e) { modItem = 0; }
        log(`tipos de mod: projétil ${modProj}, NPC ${modNpc}, item ${modItem}`);
        startPhase('idle');
    } else if (phase === 'idle' && frames === FRAMES) {
        measuring = false;
        reportAll('parado', before, stats(), FRAMES);
        log('parado: ' + fmt(frameStats()));
        spawnWorld(self);
        phase = 'fill'; frames = 0;
    } else if (phase === 'fill') {
        keepAlive(self);
        if (frames === 120) startPhase('busy');
    } else if (phase === 'busy') {
        keepAlive(self);
        if (frames === FRAMES) {
            measuring = false;
            reportAll('cheio', before, stats(), FRAMES);
            baseline = frameStats();
            log('cheio, sem hook de teste (A): ' + fmt(baseline));
            // B: hooks empilhados no WorldItem.UpdateItem (400 chamadas por
            // quadro, sempre), todos barrados pela flag. Cada um é um despacho a
            // mais por chamada: 25 hooks = 10.000 despachos barrados por quadro.
            stack(25);
            startPhase('barred');
        }
    } else if (phase === 'barred' || phase === 'barred50' || phase === 'js') {
        keepAlive(self);
        if (frames !== FRAMES) return;
        measuring = false;
        const f = frameStats();
        const extra = stats(), dispatched = countStacked(before, extra) / FRAMES;
        // Só o DoUpdate: o UpdateItem roda nele, e o DoDraw do MuMu oscila ~1 ms
        // entre fases sozinho (somado, o ruído engolia o sinal).
        const delta = f.updMean - baseline.updMean;
        const deltaMed = f.updMed - baseline.updMed;
        log(`${phase}: ${fmt(f)}`);
        log(`${phase}: ${stacked.length} hooks a mais no UpdateItem, ${dispatched.toFixed(0)} despachos deles/quadro; ΔDoUpdate ${(delta * 1000).toFixed(0)} µs (med ${(deltaMed * 1000).toFixed(0)}) = ${(delta * 1e6 / dispatched).toFixed(0)} ns por despacho (med ${(deltaMed * 1e6 / dispatched).toFixed(0)})`);
        if (phase === 'barred') {
            stack(25);
            startPhase('barred50');
        } else if (phase === 'barred50') {
            // Um só com o JS ligado: 400 entradas no JS por quadro.
            bl.hookFlags.set(stacked[0], true);
            jsBase = f;
            startPhase('js');
        } else {
            bl.hookFlags.set(stacked[0], false);
            const d = f.updMean - jsBase.updMean;
            const dm = f.updMed - jsBase.updMed;
            log(`js: ΔDoUpdate contra barred50 ${(d * 1000).toFixed(0)} µs (med ${(dm * 1000).toFixed(0)}) em 400 entradas no JS = ${(d * 1e6 / 400).toFixed(0)} ns por entrada (med ${(dm * 1e6 / 400).toFixed(0)}); contador do callback ${jsCalls}`);
            for (let k = 0; k < 1000; k++) { const q = Main.projectile[k]; if (q.active && (q.type === VANILLA_PROJ || q.type === modProj)) q.active = false; }
            for (const k of spawnedNpcs) { const n = Main.npc[k]; if (k >= 0 && k < 200 && n.active && (n.type === 46 || n.type === modNpc)) n.active = false; }
            for (const k of spawnedItems) { if (k >= 0 && k < 400 && Main.item[k].active) Main.item[k].TurnToAir(); }
            log('FIM: tudo ok');
            phase = 'done';
        }
    }
});
log('carregado');

export default class TestHookAudit extends Mod {}
