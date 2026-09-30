// A ponte depois do metodo preso ao objeto (bl.log com 'bridgebind ...'):
//   - `var f = npc['int FindBuffIndex(int type)']; f(x)` chama no npc;
//   - o mesmo (objeto, metodo) devolve o mesmo GameMethod (tabela de vagas);
//     objetos diferentes, metodos diferentes; estatico segue igual;
//   - metodo de struct continua funcionando;
//   - JSON.stringify de objeto do jogo e de classe para no nome (toJSON), sem
//     descer; Object.keys lista os membros;
//   - tempo de um laco pelos 1000 projeteis lendo e chamando um metodo.
const Main = Terraria.Main;
const FIND = 'int FindBuffIndex(int type)';

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('bridgebind ' + label + ': ok');
        else { fails++; bl.log('bridgebind ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('bridgebind ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

function run() {
    const player = Main.player[Main.myPlayer];
    const npc0 = Main.npc[0], npc1 = Main.npc[1];

    check('metodo solto chama no dono', () => {
        const f = player[FIND];
        const got = f(1);   // Obsidian Skin: o jogador de teste nao tem
        return got === player[FIND](1) || `solto ${got}, direto ${player[FIND](1)}`;
    });
    check('mesmo (objeto, metodo) -> mesmo objeto', () => player[FIND] === player[FIND] || 'objetos diferentes');
    check('objetos diferentes -> metodos diferentes', () => npc0[FIND] !== npc1[FIND] || 'iguais');
    check('cada um chama no seu', () => {
        const a = npc0[FIND], b = npc1[FIND];
        return a(1) === npc0[FIND](1) && b(1) === npc1[FIND](1) || 'chamada no objeto errado';
    });
    check('estatico: um objeto so', () =>
        Main['bool IsItDay()'] === Main['bool IsItDay()'] || 'estatico sem cache');
    check('metodo de struct', () => {
        const c = player.Center;   // Vector2 do jogo (struct)
        const len = c['float Length()'];
        const want = Math.hypot(c.X, c.Y);
        return Math.abs(len() - want) < 0.5 || `${len()} x ${want}`;
    });

    check('JSON de objeto do jogo para no nome', () => {
        const t0 = Date.now();
        const s = JSON.stringify({ p: player, list: [npc0] });
        const ms = Date.now() - t0;
        bl.log(`bridgebind: JSON ${s} em ${ms} ms`);
        return (s.length < 200 && ms < 50) || `${s.length} caracteres em ${ms} ms`;
    });
    check('JSON de classe para no nome', () => {
        const s = JSON.stringify(Main);
        return (typeof s === 'string' && s.length < 100) || String(s).slice(0, 120);
    });
    check('Object.keys lista os membros', () => {
        const keys = Object.keys(player);
        bl.log(`bridgebind: Object.keys(player): ${keys.length} (ex.: ${keys.slice(0, 5).join(', ')})`);
        return (keys.includes('statLife') && keys.includes('whoAmI')) || keys.length;
    });
    check('bl.log de objeto nao trava', () => {
        const t0 = Date.now();
        bl.log('bridgebind: bl.log', { player }, [npc0]);
        const ms = Date.now() - t0;
        return ms < 50 || ms + ' ms';
    });

    // Tempo: 1000 projeteis, ler o metodo e chamar, 5 voltas.
    const projs = Main.projectile;
    const n = projs.length;
    const METHOD = 'bool IsDamageDodgeable()';
    const t0 = Date.now();
    let calls = 0;
    for (let round = 0; round < 5; round++) {
        for (let i = 0; i < n; i++) {
            projs[i][METHOD]();
            calls++;
        }
    }
    const ms = Date.now() - t0;
    bl.log(`bridgebind: tempo: ${calls} chamadas (${n} projeteis x 5) em ${ms} ms = ${(ms * 1e6 / calls).toFixed(0)} ns/chamada`);
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    if (++frames === 90) {
        done = true;
        check('preparo', run);
        bl.log('bridgebind FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('bridgebind: carregado');

// A classe do mod, obrigatória no arquivo de entrada.
export default class TestBridgebind extends Mod {}
