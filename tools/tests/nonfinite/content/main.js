// NaN e infinito nos campos do jogo (relato de 2026-10-11: "error in Ocram.AI:
// RangeError: System.Single nao aceita infinito", no Consolaria).
// Progressão: o NewDust devolve 6000 (o pó fantasma, a última vaga de
// Main.dust) quando o pó nasceria fora da tela, com o jogo pausado ou sem vaga;
// ninguém o atualiza nem o zera, e cada código que multiplica a velocidade dele
// acumula até o float estourar (o double cabe, o float vira infinito). Depois,
// o `_v.X *= 0.5` do mod escreve infinito de volta e a ponte recusava.
//   - campo float/double que já guarda infinito (ou NaN) aceita outro, como no C#;
//   - NaN e infinito novos num campo com número continuam recusados;
//   - argumento float de método já aceitava (vai direto ao registrador);
//   - elemento de array (float[]) segue a mesma regra do campo.
// Loga "nonfinite <caso>: ok | FALHOU".
const Main = Terraria.Main;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('nonfinite ' + label + ': ok');
        else { fails++; bl.log('nonfinite ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('nonfinite ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

// O erro da ponte que a escrita deu, ou null se passou.
function refused(write) {
    try {
        write();
        return null;
    } catch (e) {
        return String(e);
    }
}

function run() {
    const ghost = Main.dust[Main.dust.length - 1];   // o 6000
    const keep = { x: ghost.velocity.X, y: ghost.velocity.Y };

    check('o float do pó fantasma estoura sem erro (o double ainda é finito)', () => {
        ghost.velocity.X = 3e38;
        ghost.velocity.X *= 10;
        ghost.velocity.Y = -3e38;
        ghost.velocity.Y *= 10;
        return (ghost.velocity.X === Infinity && ghost.velocity.Y === -Infinity) || `${ghost.velocity.X}, ${ghost.velocity.Y}`;
    });
    check('o código do Ocram no pó fantasma estourado passa (campo infinito aceita infinito)', () => {
        const err = refused(() => {
            const expr_146B6_cp_0 = ghost;
            const _v = expr_146B6_cp_0.velocity;
            _v.X *= 0.5; _v.Y *= 0.1;
            expr_146B6_cp_0.velocity = _v;
        });
        return (err === null && ghost.velocity.X === Infinity && ghost.velocity.Y === -Infinity) || `erro ${err}, ${ghost.velocity.X}, ${ghost.velocity.Y}`;
    });
    check('campo infinito aceita NaN (infinito * 0) e volta a número', () => {
        const err = refused(() => { ghost.velocity.X *= 0; });
        const nan = Number.isNaN(ghost.velocity.X);
        const back = refused(() => { ghost.velocity.X = 1.5; });
        return (err === null && nan && back === null && ghost.velocity.X === 1.5) || `erro ${err}, NaN ${nan}, volta ${back}`;
    });
    check('NaN novo num campo com número continua recusado', () => {
        ghost.velocity.Y = 2;
        const err = refused(() => { ghost.velocity.Y = undefined * 2; });
        return (err && err.includes('NaN') && ghost.velocity.Y === 2) || `erro ${err}, Y ${ghost.velocity.Y}`;
    });
    check('infinito novo num campo com número continua recusado', () => {
        const err = refused(() => { ghost.velocity.Y = 1 / 0; });
        return (err && err.includes('infinito') && ghost.velocity.Y === 2) || `erro ${err}, Y ${ghost.velocity.Y}`;
    });
    check('argumento float de método aceita infinito (já era assim, como no C#)', () => {
        const clamp = Microsoft.Xna.Framework.MathHelper['float Clamp(float value, float min, float max)'];
        let got, err = refused(() => { got = clamp(Infinity, 0, 1); });
        return (err === null && got === 1) || `erro ${err}, ${got}`;
    });
    // Já estourado antes do teste, volta zerado (o Vector2.new não aceita infinito).
    ghost.velocity = Vector2.new(Number.isFinite(keep.x) ? keep.x : 0, Number.isFinite(keep.y) ? keep.y : 0);

    check('elemento de float[]: infinito aceita infinito; número recusa NaN', () => {
        const fade = Main.musicFade, before = fade[0];
        try {
            fade[0] = 3e38;
            fade[0] *= 10;
            const kept = refused(() => { fade[0] *= 0.5; });
            const inf = fade[0];
            fade[0] = 0.25;
            const nan = refused(() => { fade[0] = NaN; });
            return (inf === Infinity && kept === null && nan && fade[0] === 0.25) || `infinito ${inf}, manter ${kept}, NaN ${nan}`;
        } finally {
            fade[0] = before;
        }
    });
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    if (++frames !== 30) return;
    done = true;
    check('roteiro', run);
    bl.log('nonfinite FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
});
bl.log('nonfinite: carregado');

export default class TestNonFinite extends Mod {}
