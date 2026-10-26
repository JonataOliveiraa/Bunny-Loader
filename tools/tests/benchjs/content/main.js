// Os mesmos casos do tools/tests/benchcs, com os mesmos nomes. Mede no quadro
// 400 dentro do mundo (o benchcs no 120): 3 rodadas de cada caso, mediana,
// menos o laço vazio. Loga "bench js <caso>: <valor> <unidade>".
const Main = Terraria.Main;
const FRAME = 400, ROUNDS = 3, N = 20000;
const MathHelper = Microsoft.Xna.Framework.MathHelper;
const min = MathHelper['float Min(float value1, float value2)'];
min.hook((original, a, b) => original(a, b), { flag: 'benchjs.hook' });

let sink = 0;

// `loop(n)` roda o caso n vezes por dentro: uma chamada por rodada, como no
// benchcs (lá a chamada de delegate por iteração custava mais que o caso).
function perOp(loop, n, empty) {
    sink += loop(16);
    const runs = [];
    for (let r = 0; r < ROUNDS; r++) {
        const t0 = performance.now();
        sink += loop(n);
        runs.push((performance.now() - t0) * 1e6 / n);
    }
    runs.sort((a, b) => a - b);
    return Math.max(0, runs[ROUNDS >> 1] - empty);
}

function total(work) {
    work();
    const runs = [];
    for (let r = 0; r < ROUNDS; r++) {
        const t0 = performance.now();
        work();
        runs.push((performance.now() - t0) * 1e3);
    }
    runs.sort((a, b) => a - b);
    return runs[ROUNDS >> 1];
}

const report = (name, value, unit) => bl.log(`bench js ${name}: ${value.toFixed(2)} ${unit}`);
const na = (name) => bl.log(`bench js ${name}: n/a`);

function fib(n) { return n < 2 ? n : fib(n - 1) + fib(n - 2); }

function run(p) {
    const empty = perOp((n) => { let s = 0; for (let k = 0; k < n; k++) s += k; return s; }, N, 0);
    report('empty loop', empty, 'ns/op');

    // ---- cálculo puro ----
    report('compute: int loop 1M', total(() => {
        let s = 0;
        for (let k = 0; k < 1000000; k++) s += (k * 7) ^ (k >> 3);
        sink += s;
    }), 'us');
    report('compute: sin+sqrt 100k', total(() => {
        let s = 0;
        for (let k = 0; k < 100000; k++) s += Math.sin(k * 0.001) + Math.sqrt(k);
        sink += s;
    }), 'us');
    report('compute: fib(20)', total(() => { sink += fib(20); }), 'us');
    report('compute: sort 10k ints', total(() => {
        const arr = new Int32Array(10000);
        let x = 12345;
        for (let k = 0; k < arr.length; k++) { x = (Math.imul(x, 1103515245) + 12345) >>> 0; arr[k] = x >>> 8; }
        arr.sort();
        sink += arr[5000];
    }), 'us');
    report('compute: map 10k set+get', total(() => {
        const map = new Map();
        for (let k = 0; k < 10000; k++) map.set(k * 31, k);
        let s = 0;
        for (let k = 0; k < 10000; k++) s += map.get(k * 31);
        sink += s;
    }), 'us');
    report('compute: string build 1k', total(() => {
        const parts = [];
        for (let k = 0; k < 1000; k++) parts.push('item', k, ',');
        sink += parts.join('').length;
    }), 'us');
    report('compute: allocate 100k objects', total(() => {
        let last = null;
        for (let k = 0; k < 100000; k++) last = { a: k, b: k + 1 };
        sink += last.b;
    }), 'us');

    // ---- campos e estáticos ----
    const deprecated = Terraria.ID.ItemID.Sets.Deprecated;
    report('field: p.head', perOp((n) => { let s = 0; for (let k = 0; k < n; k++) { s += p.head; } return s; }, N, empty), 'ns/op');
    report('field: p.position.X', perOp((n) => { let s = 0; for (let k = 0; k < n; k++) { s += p.position.X; } return s; }, N, empty), 'ns/op');
    report('field write: p.statLife = p.statLife', perOp((n) => { let s = 0; for (let k = 0; k < n; k++) { p.statLife = p.statLife; } return s; }, N, empty), 'ns/op');
    report('static: Main.myPlayer', perOp((n) => { let s = 0; for (let k = 0; k < n; k++) { s += Main.myPlayer; } return s; }, N, empty), 'ns/op');
    report('static: Main.GameUpdateCount', perOp((n) => { let s = 0; for (let k = 0; k < n; k++) { s += Main.GameUpdateCount; } return s; }, N, empty), 'ns/op');
    report('array: Main.player[myPlayer]', perOp((n) => { let s = 0; for (let k = 0; k < n; k++) { s += Main.player[Main.myPlayer] ? 1 : 0; } return s; }, N, empty), 'ns/op');
    report('array: p.inventory[p.selectedItem].type', perOp((n) => { let s = 0; for (let k = 0; k < n; k++) { s += p.inventory[p.selectedItem].type; } return s; }, N, empty), 'ns/op');
    report('array: ItemID.Sets.Deprecated[5]', perOp((n) => { let s = 0; for (let k = 0; k < n; k++) { s += deprecated[5] ? 1 : 0; } return s; }, N, empty), 'ns/op');

    // ---- chamadas ----
    const inWorld = Terraria.WorldGen['bool InWorld(int x, int y, int fluff)'];
    const clamp = MathHelper['float Clamp(float value, float min, float max)'];
    const distance = Microsoft.Xna.Framework.Vector2['float Distance(Vector2 value1, Vector2 value2)'];
    const countBuffs = p['int CountBuffs()'];
    const itemName = Terraria.Lang['string GetItemNameValue(int id)'];
    const a = Vector2.new(1, 2), b = Vector2.new(4, 6);
    report('call: WorldGen.InWorld(int, int, int)', perOp((n) => { let s = 0; for (let k = 0; k < n; k++) { s += inWorld(100 + (k & 7), 100, 0) ? 1 : 0; } return s; }, N, empty), 'ns/op');
    report('call: MathHelper.Clamp(float x3)', perOp((n) => { let s = 0; for (let k = 0; k < n; k++) { s += clamp(k * 0.5, 0, 10); } return s; }, N, empty), 'ns/op');
    report('call: Vector2.Distance(Vector2, Vector2)', perOp((n) => { let s = 0; for (let k = 0; k < n; k++) { s += distance(a, b); } return s; }, N, empty), 'ns/op');
    report('call: p.CountBuffs()', perOp((n) => { let s = 0; for (let k = 0; k < n; k++) { s += countBuffs(); } return s; }, N, empty), 'ns/op');
    report('call: p.Center (Vector2 return)', perOp((n) => { let s = 0; for (let k = 0; k < n; k++) { s += p.Center.X; } return s; }, N, empty), 'ns/op');
    na('call: Matrix.Add(Matrix, Matrix) (64-byte return)');
    report('call: Lang.GetItemNameValue(98) (string)', perOp((n) => { let s = 0; for (let k = 0; k < n; k++) { s += itemName(98).length; } return s; }, N / 4, empty), 'ns/op');
    report('struct: new Vector2(1, 2)', perOp((n) => { let s = 0; for (let k = 0; k < n; k++) { s += Vector2.new(k, 2).X; } return s; }, N, empty), 'ns/op');
    report('struct: new Color(1, 2, 3)', perOp((n) => { let s = 0; for (let k = 0; k < n; k++) { s += Color.new(k & 255, 2, 3).R; } return s; }, N, empty), 'ns/op');

    // ---- hook: o custo de passar por um hook que só chama o original ----
    const plain = perOp((n) => { let s = 0; for (let k = 0; k < n; k++) { s += min(k, 3); } return s; }, N, empty);
    bl.hookFlags.set('benchjs.hook', true);
    const hooked = perOp((n) => { let s = 0; for (let k = 0; k < n; k++) { s += min(k, 3); } return s; }, N, empty);
    bl.hookFlags.set('benchjs.hook', false);
    report('hook: call without hook', plain, 'ns/op');
    report('hook: call through pass-through hook', hooked, 'ns/op');
    report('hook: overhead per call', Math.max(0, hooked - plain), 'ns/op');

    bl.log(`bench js sink ${sink}`);
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (done || i !== Main.myPlayer || Main.gameMenu || ++frames !== FRAME) return;
    done = true;
    Main['void NewText(string newText, byte R, byte G, byte B, bool onlyCurrentPlayer)']('Medindo JS...', 255, 240, 120, false);
    try { run(self); } catch (e) { bl.log('bench js error: ' + e + ' ' + (e && e.stack)); }
    bl.log('bench js FIM');
    const say = Main['void NewText(string newText, byte R, byte G, byte B, bool onlyCurrentPlayer)'];
    say('Medição concluída. Saia do jogo e abra Configurações > Diagnóstico > Ver relatório > Copiar.', 120, 255, 120, false);
});
bl.log('bench js: carregado');

export default class TestBenchJs extends Mod {}
