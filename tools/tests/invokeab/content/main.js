// Chamada direta (salto pelo methodPointer) × runtime_invoke, no mesmo APK e
// na mesma execução: a chave bl.hookFlags 'bridge.runtimeInvoke' manda tudo
// pelo runtime_invoke. Rodadas alternadas (A B, B A, ...) para o ruído do
// aparelho cair nos dois lados; mediana por método, menos o laço vazio.
// O MuMu traduz ARM e não serve para essa comparação: rodar num aparelho.
// Só lê: não cria nada no mundo. Loga "invokeab ..." e o resumo no chat.
const Main = Terraria.Main;
const N = 3000, ROUNDS = 8;

const say = (text) => {
    bl.log(text);
    try {
        Main['void NewText(string newText, byte R, byte G, byte B, bool onlyCurrentPlayer)'](text, 255, 240, 120, false);
    } catch (e) {
        bl.log('invokeab chat: ' + e);
    }
};

function cases() {
    const p = Main.player[Main.myPlayer];
    const inWorld = Terraria.WorldGen['bool InWorld(int x, int y, int fluff)'];
    const clamp = Microsoft.Xna.Framework.MathHelper['float Clamp(float value, float min, float max)'];
    const distance = Microsoft.Xna.Framework.Vector2['float Distance(Vector2 value1, Vector2 value2)'];
    const countBuffs = p['int CountBuffs()'];
    const center = p['Vector2 get_Center()'];
    const a = Vector2.new(1, 2), b = Vector2.new(4, 6);
    return [
        ['laço vazio', () => 0],
        ['static bool InWorld(int, int, int)', (i) => inWorld(100 + (i & 7), 100, 0)],
        ['static float Clamp(float x3)', (i) => clamp(i * 0.5, 0, 10)],
        ['static float Distance(Vector2, Vector2)', () => distance(a, b)],
        ['int Player.CountBuffs()', () => countBuffs()],
        ['Vector2 Player.get_Center()', () => center()],
    ];
}

function time(fn) {
    const t0 = performance.now();
    for (let i = 0; i < N; i++) fn(i);
    return (performance.now() - t0) * 1e6 / N;
}

const median = (xs) => {
    const s = [...xs].sort((x, y) => x - y);
    return s.length % 2 ? s[(s.length - 1) >> 1] : (s[s.length / 2 - 1] + s[s.length / 2]) / 2;
};

function run() {
    const list = cases();
    const ns = { direct: list.map(() => []), runtime: list.map(() => []) };
    try {
        for (let r = 0; r < ROUNDS; r++) {
            const order = r % 2 ? ['runtime', 'direct'] : ['direct', 'runtime'];
            for (const mode of order) {
                bl.hookFlags.set('bridge.runtimeInvoke', mode === 'runtime');
                list.forEach(([, fn], k) => ns[mode][k].push(time(fn)));
            }
        }
    } finally {
        bl.hookFlags.set('bridge.runtimeInvoke', false);
    }
    const empty = { direct: median(ns.direct[0]), runtime: median(ns.runtime[0]) };
    say(`invokeab: ${ROUNDS} rodadas de ${N} chamadas; laço vazio ${empty.direct.toFixed(0)} ns`);
    for (let k = 1; k < list.length; k++) {
        const d = median(ns.direct[k]) - empty.direct;
        const rt = median(ns.runtime[k]) - empty.runtime;
        say(`invokeab ${list[k][0]}: direta ${d.toFixed(0)} ns, runtime_invoke ${rt.toFixed(0)} ns (${(rt / d).toFixed(2)}x)`);
    }
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (done || i !== Main.myPlayer || Main.gameMenu || ++frames < 120) return;
    done = true;
    try { run(); } catch (e) { bl.log('invokeab FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : '')); }
    bl.log('invokeab FIM');
});
bl.log('invokeab: carregado');

export default class TestInvokeAB extends Mod {}
