// Benchmark da ponte JS -> IL2CPP. Roda uma vez, dentro do mundo, na thread do
// jogo (dentro de um hook de Player.Update, que e o contexto real de um mod).
const Main = Terraria.Main;
const N = 20000;
const log = (s) => bl.log('bench ' + s);

// Melhor de 7: o MuMu varia ate 70% entre rodadas (medido na linha de base);
// o minimo e o que mais se aproxima do custo sem interferencia.
function time(fn) {
    let best = Infinity;
    for (let r = 0; r < 7; r++) {
        const t0 = performance.now();
        fn();
        const dt = performance.now() - t0;
        if (dt < best) best = dt;
    }
    return best;
}

let sink = 0;
let benching = true;
function runMicro(p) {
    const base = time(() => { let s = 0; for (let i = 0; i < N; i++) { s += i; } sink += s; });
    const r = {};
    r.fieldReadInt = time(() => { let s = 0; for (let i = 0; i < N; i++) { s += p.statLife; } sink += s; });
    r.fieldWriteInt = time(() => { const v = p.statLife; for (let i = 0; i < N; i++) { p.statLife = v; } });
    r.fieldReadFloat = time(() => { let s = 0; for (let i = 0; i < N; i++) { s += p.moveSpeed; } sink += s; });
    r.structView = time(() => { let s = 0; for (let i = 0; i < N; i++) { s += p.position.X; } sink += s; });
    r.staticField = time(() => { let s = 0; for (let i = 0; i < N; i++) { s += Main.maxTilesX; } sink += s; });
    r.propGetter = time(() => { let s = 0; for (let i = 0; i < N; i++) { s += Main.myPlayer; } sink += s; });
    r.methodLookupAndCall = time(() => { let s = 0; for (let i = 0; i < N; i++) { s += p['bool CanBePushedByWind()']() ? 1 : 0; } sink += s; });
    const f = p['bool CanBePushedByWind()'];
    r.methodCached = time(() => { let s = 0; for (let i = 0; i < N; i++) { s += f(p) ? 1 : 0; } sink += s; });
    const g = Main['int DamageVar(float dmg, float luck)'];
    r.staticMethod2Float = time(() => { let s = 0; for (let i = 0; i < N; i++) { s += g(10.0, 0.0); } sink += s; });
    const players = Main.player;
    r.arrayElemObject = time(() => { let s = 0; for (let i = 0; i < N; i++) { s += players[0] ? 1 : 0; } sink += s; });
    // Wrapper NOVO a cada acesso: 400 itens distintos que o JS nao segura.
    // (players[0] acima reaproveita o wrapper do `self` que o bench segura.)
    const items = Main.item;
    r.arrayElemFresh = time(() => { let s = 0; for (let i = 0; i < N; i++) { s += items[i % 400] ? 1 : 0; } sink += s; });
    const bt = p.buffType;
    r.arrayElemInt = time(() => { let s = 0; for (let i = 0; i < N; i++) { s += bt[0]; } sink += s; });
    r.staticArrayAndElem = time(() => { let s = 0; for (let i = 0; i < N; i++) { s += Main.player[0].whoAmI; } sink += s; });
    r.jsCompute10 = time(() => { let s = 0; for (let i = 0; i < N * 10; i++) { s = (s + i * i) % 1000003; } sink += s; });
    r.jsAlloc = time(() => { let s = 0; for (let i = 0; i < N; i++) { const o = { x: i, y: i }; s += o.x; } sink += s; });
    return { r, base, f, g };
}

/** A bateria inteira duas vezes; fica o menor de cada item. */
function runMicroTwice(p) {
    const a = runMicro(p), b = runMicro(p);
    const base = Math.min(a.base, b.base);
    const ns = (ms) => ((ms - base) * 1e6 / N).toFixed(0);
    log('N=' + N + ' base=' + base.toFixed(2) + 'ms');
    for (const k in a.r) {
        const ms = Math.min(a.r[k], b.r[k]);
        log(k + ' = ' + ns(ms) + ' ns/op (' + ms.toFixed(2) + ' ms)');
    }
    return { f: a.f, g: a.g };
}

// Hook: custo do despacho medido como (chamada com hook) - (chamada sem hook).
function runHooks(p, f, g) {
    const moss = Terraria.WorldGen['int GetTileMossColor(int tileType)'];
    const slime = Terraria.NPC['int GetStackForSlimeItemDrop(int item)'];
    const b1 = time(() => { for (let i = 0; i < N; i++) f(p); });
    const b2 = time(() => { for (let i = 0; i < N; i++) g(10.0, 0.0); });
    const b3 = time(() => { for (let i = 0; i < N; i++) moss(1); });
    const b4 = time(() => { for (let i = 0; i < N; i++) slime(1); });
    Terraria.Player['bool CanBePushedByWind()'].hook((o, self) => o(self));
    Terraria.Main['int DamageVar(float dmg, float luck)'].hook((o, dmg, luck) => o(dmg, luck));
    Terraria.WorldGen['int GetTileMossColor(int tileType)'].hook((o, t) => o(t));
    // Devolve fixo SO durante a medicao; depois repassa, para nao mexer no mundo.
    Terraria.NPC['int GetStackForSlimeItemDrop(int item)'].hook((o, it) => benching ? 7 : o(it));
    const a1 = time(() => { for (let i = 0; i < N; i++) f(p); });
    const a2 = time(() => { for (let i = 0; i < N; i++) g(10.0, 0.0); });
    const a3 = time(() => { for (let i = 0; i < N; i++) moss(1); });
    const a4 = time(() => { for (let i = 0; i < N; i++) slime(1); });
    benching = false;
    const d = (a, b) => ((a - b) * 1e6 / N).toFixed(0);
    const c = (b) => (b * 1e6 / N).toFixed(0);
    log('chamada sem hook: instancia ' + c(b1) + ', 2 floats ' + c(b2) + ', 1 int ' + c(b3) + '/' + c(b4) + ' ns (inclui o laco)');
    log('hook estatico 1 int SEM original = +' + d(a4, b4) + ' ns/chamada');
    log('hook estatico 1 int passthrough = +' + d(a3, b3) + ' ns/chamada');
    log('hook estatico 2 floats passthrough = +' + d(a2, b2) + ' ns/chamada');
    log('hook instancia (self) passthrough = +' + d(a1, b1) + ' ns/chamada');
}

// Quadro real: tempo do DoUpdate e do DoDraw, sem hook quente.
let phase = 0, warm = 0, frames = 0, upd = 0, updWorst = 0, drw = 0, drwWorst = 0;
const FRAMES = 300;
function report() {
    log('quadro (' + frames + '): DoUpdate media ' + (upd / frames).toFixed(2) + ' ms pior ' + updWorst.toFixed(2) +
        ' | DoDraw media ' + (drw / frames).toFixed(2) + ' ms pior ' + drwWorst.toFixed(2));
}
Main['void DoUpdate(GameTime gameTime)'].hook((o, self, gt) => {
    const t0 = performance.now();
    o(self, gt);
    const dt = performance.now() - t0;
    if (phase !== 1) return;
    if (warm < 30) { warm++; return; }
    frames++; upd += dt; if (dt > updWorst) updWorst = dt;
    if (frames === FRAMES) { report(); phase = 2; }
});
Main['void DoDraw(GameTime gameTime)'].hook((o, self, gt) => {
    const t0 = performance.now();
    o(self, gt);
    const dt = performance.now() - t0;
    if (phase !== 1 || warm < 30) return;
    drw += dt; if (dt > drwWorst) drwWorst = dt;
});

let started = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (started || i !== Main.myPlayer || Main.gameMenu) return;
    started = true;
    const t0 = performance.now();
    const { f, g } = runMicroTwice(self);
    runHooks(self, f, g);
    log('micro total ' + (performance.now() - t0).toFixed(0) + ' ms; sink=' + (sink % 7));
    phase = 1;
});
log('pronto');
