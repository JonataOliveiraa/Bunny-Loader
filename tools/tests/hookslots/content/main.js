// Muitos hooks: bem mais que os limites antigos (128 de int/void, 16 de float
// e de double, 8 por forma de struct). Cada metodo recebe N hooks encadeados;
// chamado uma vez, cada callback roda uma vez, na ordem em que foi
// instalado, e o resultado passa intacto. No fim, mede quantos hooks encadeados
// no MESMO metodo cabem na pilha do motor JS. Loga "hookslots ...".
const Lang = Terraria.Lang;
const PER_METHOD = 25;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('hookslots ' + label + ': ok');
        else { fails++; bl.log('hookslots ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('hookslots ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

// Instala `count` hooks num metodo; cada um anota a sua vez em `trail`.
function chain(method, count) {
    const trail = [];
    for (let k = 0; k < count; k++) {
        method.hook((original, a, b, c, d) => {
            trail.push(k);
            return original(a, b, c, d);
        });
    }
    return { trail, installed: count };
}

// O PRIMEIRO instalado roda primeiro: o `original` dele leva ao seguinte, e
// assim por diante (o mod carregado antes ve a chamada antes).
const inOrder = (trail, count) => trail.length === count && trail.every((k, i) => k === i);

const intMethods = [
    Lang['string GetItemNameValue(int id)'], Lang['string GetNPCNameValue(int netID)'],
    Lang['string GetBuffName(int id)'], Lang['LocalizedText GetItemName(int id)'],
    Lang['LocalizedText GetNPCName(int netID)'], Lang['LocalizedText GetProjectileName(int type)'],
    Lang['string GetMapObjectName(int id)'],
];
const lerp = Terraria.Utils['float GetLerpValue(float from, float to, float t, bool clamped)'];
const pvp = Terraria.Main['double CalculateDamagePlayersTakeInPVP(int Damage, int Defense)'];
const toVec = Terraria.Utils['Vector2 ToVector2(Point p)'];
const probe = Lang['string GetBuffDescription(int id)'];

let intChains = [], fltChain, dblChain, vecChain;
check('instalar 175 de int/objeto, 20 de float, 20 de double e 20 de Vector2', () => {
    intChains = intMethods.map((m) => ({ m, ...chain(m, PER_METHOD) }));
    fltChain = chain(lerp, 20);
    dblChain = chain(pvp, 20);
    vecChain = chain(toVec, 20);
    return true;
});

// Profundidade: 120 hooks num metodo que so o teste chama. Cada um conta a
// entrada; o que nao couber na pilha lanca, e a ponte roda o original por ele.
let depthNow = 0, depthMax = 0, probeEntered = 0;
for (let k = 0; k < 120; k++) {
    probe.hook((original, id) => {
        probeEntered++;
        depthMax = Math.max(depthMax, ++depthNow);
        try { return original(id); } finally { depthNow--; }
    });
}

function run() {
    check('int/objeto: cada callback uma vez, na ordem, resultado intacto', () => {
        const probes = [1, 1, 1, 1, 1, 1, 0];
        for (let c = 0; c < intChains.length; c++) {
            const { m, trail } = intChains[c];
            trail.length = 0;
            const r = m(probes[c]);
            if (!inOrder(trail, PER_METHOD)) return `metodo ${c}: ${trail.length} chamadas, ordem ${trail.slice(0, 5).join(',')}...`;
            if (r === undefined || r === null) return `metodo ${c}: resultado ${r}`;
        }
        return true;
    });
    check('float: 20 callbacks e o valor certo', () => {
        fltChain.trail.length = 0;
        const v = lerp(0, 10, 2.5, false);
        return (Math.abs(v - 0.25) < 1e-6 && inOrder(fltChain.trail, 20)) || `valor ${v}, ${fltChain.trail.length} chamadas`;
    });
    check('double: 20 callbacks e o valor certo', () => {
        dblChain.trail.length = 0;
        const v = pvp(100, 20);
        return (v > 0 && v < 100 && inOrder(dblChain.trail, 20)) || `valor ${v}, ${dblChain.trail.length} chamadas`;
    });
    check('Vector2: 20 callbacks e o struct certo', () => {
        vecChain.trail.length = 0;
        const pt = Microsoft.Xna.Framework.Point.new();
        pt['void .ctor(int x, int y)'](3, 7);
        const v = toVec(pt);
        return (v.X === 3 && v.Y === 7 && inOrder(vecChain.trail, 20)) || `valor ${v.X},${v.Y}, ${vecChain.trail.length} chamadas`;
    });
    check('profundidade: o metodo responde mesmo com hooks demais', () => {
        probeEntered = 0;
        depthMax = 0;
        const r = probe(1);
        bl.log(`hookslots profundidade: ${depthMax} hooks encadeados no mesmo metodo couberam na pilha (de 120)`);
        return (typeof r === 'string' && depthMax >= 20) || `resultado ${r}, profundidade ${depthMax}`;
    });
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Terraria.Main.myPlayer || Terraria.Main.gameMenu || done) return;
    if (++frames === 60) {
        done = true;
        check('preparo', run);
        bl.log('hookslots FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('hookslots: carregado');
