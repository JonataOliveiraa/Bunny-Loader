// Struct com indexador na ponte: o Float_FixedArray_3 do proj.ai/localAI, o
// Vector2_DynamicArray_120 do oldPos (elemento struct = vista), o BitsByte do
// player.hideMisc (pelo get_Item/set_Item do jogo) e o metodo por nome puro
// num struct (localAI.get_Item(1), position.Length()).
const Main = Terraria.Main;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('structindex ' + label + ': ok');
        else { fails++; bl.log('structindex ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('structindex ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

/** fn() tem de lancar um erro cujo nome e `kind` (RangeError, TypeError). */
function throws(kind, fn) {
    try { fn(); } catch (e) { return e.name === kind ? true : 'lancou ' + e; }
    return 'nao lancou';
}

const near = (a, b) => Math.abs(a - b) < 1e-4;

function run() {
    // Um projetil inativo: o struct e o mesmo, e nada no jogo reage.
    let p = null;
    for (let i = Main.projectile.length - 2; i >= 0 && !p; i--) {
        if (!Main.projectile[i].active) p = Main.projectile[i];
    }
    if (!p) return check('projetil inativo', () => 'nenhum');
    const saved = { ai: [p.ai.val0, p.ai.val1, p.ai.val2], local: [p.localAI.val0, p.localAI.val1, p.localAI.val2] };

    check('ai[i] grava nos campos val0..val2', () => {
        p.ai[0] = 1.5; p.ai[1] = -2; p.ai[2] = 3.25;
        return (p.ai.val0 === 1.5 && p.ai.val1 === -2 && p.ai.val2 === 3.25) ||
            `${p.ai.val0},${p.ai.val1},${p.ai.val2}`;
    });
    check('ai[i] le e ++ funciona', () => {
        p.ai[0]++;
        return (p.ai[0] === 2.5 && p.ai[2] === 3.25) || `${p.ai[0]},${p.ai[2]}`;
    });
    check('localAI[2] = 7 nao mexe no ai', () => {
        p.localAI[2] = 7;
        return (p.localAI.val2 === 7 && p.ai.val2 === 3.25) || `${p.localAI.val2},${p.ai.val2}`;
    });
    check('localAI.get_Item(1) por nome puro', () => {
        p.localAI.val1 = 4.5;
        return p.localAI.get_Item(1) === 4.5 || String(p.localAI.get_Item(1));
    });
    check('localAI.set_Item(0, 9) por nome puro', () => {
        p.localAI.set_Item(0, 9);
        return p.localAI.val0 === 9 || String(p.localAI.val0);
    });
    check('localAI.Length (propriedade do jogo) = 3', () => p.localAI.Length === 3 || String(p.localAI.Length));
    check('ai[3] (le) e RangeError', () => throws('RangeError', () => p.ai[3]));
    check('ai[3] = 1 (grava) e RangeError e nao vaza no localAI', () => {
        const before = p.localAI.val0;
        const r = throws('RangeError', () => { p.ai[3] = 1; });
        return r === true && p.localAI.val0 === before ? true : `${r}, localAI.val0 ${before} -> ${p.localAI.val0}`;
    });

    check('oldPos[5].X = 10 e vista (data5)', () => {
        const x = p.oldPos.data5.X, y = p.oldPos.data5.Y;
        p.oldPos[5].X = 10;
        p.oldPos[5].Y = 20;
        const ok = p.oldPos.data5.X === 10 && p.oldPos.data5.Y === 20 && p.oldPos[5].Y === 20;
        const got = `${p.oldPos.data5.X},${p.oldPos.data5.Y}`;
        p.oldPos.data5.X = x; p.oldPos.data5.Y = y;
        return ok || got;
    });
    check('oldRot[119] = 0.5 e oldRot[120] e RangeError', () => {
        const before = p.oldRot.data119;
        p.oldRot[119] = 0.5;
        const ok = near(p.oldRot.data119, 0.5);
        p.oldRot.data119 = before;
        const r = throws('RangeError', () => p.oldRot[120]);
        return ok && r === true ? true : `${p.oldRot.data119}, ${r}`;
    });
    check('oldPos.Length e o campo do jogo', () => typeof p.oldPos.Length === 'number' || typeof p.oldPos.Length);

    const me = Main.player[Main.myPlayer];
    check('hideMisc[i] (BitsByte, pelo get_Item/set_Item do jogo)', () => {
        const orig = me.hideMisc.value;
        me.hideMisc[2] = true;
        const on = me.hideMisc[2] === true && (me.hideMisc.value & 4) !== 0;
        me.hideMisc[2] = false;
        const off = me.hideMisc[2] === false && (me.hideMisc.value & 4) === 0;
        me.hideMisc.value = orig;
        return (on && off) || `on ${on}, off ${off}, value ${me.hideMisc.value}`;
    });
    check('struct sem indexador: position[0] e undefined', () => me.position[0] === undefined || String(me.position[0]));
    check('struct sem indexador: position[0] = 1 e TypeError', () => throws('TypeError', () => { me.position[0] = 1; }));
    check('metodo por nome puro num struct comum (position.Length())', () => {
        const v = me.position;
        const want = Math.sqrt(v.X * v.X + v.Y * v.Y);
        return Math.abs(v.Length() - want) < 1 || `${v.Length()} x ${want}`;
    });

    p.ai.val0 = saved.ai[0]; p.ai.val1 = saved.ai[1]; p.ai.val2 = saved.ai[2];
    p.localAI.val0 = saved.local[0]; p.localAI.val1 = saved.local[1]; p.localAI.val2 = saved.local[2];
}

let frames = 0;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer) return;
    if (++frames !== 60) return;
    check('preparo', run);
    // Linha maior que os 4 KB de antes: no arquivo de sessao tem de chegar
    // inteira (conferir com `awk '/structindex linha longa/ {print length}'`).
    bl.log('structindex linha longa ' + 'x'.repeat(6000) + ' fim');
    bl.log('structindex FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
});
