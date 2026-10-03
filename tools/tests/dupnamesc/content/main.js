const WHO = 'C';
let fails = 0;
let frames = 0;
let done = false;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('dupnames ' + WHO + ' ' + label + ': ok');
        else { fails++; bl.log('dupnames ' + WHO + ' ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) { fails++; bl.log('dupnames ' + WHO + ' ' + label + ': FALHOU com ' + e); }
}
const Main = Terraria.Main;
const me = () => Main.player[Main.myPlayer];

function run() {
    const p = me();
    check('nome ambiguo de outro mod: nada (e aviso no log)', () => p.GetModPlayer('Abc') === undefined || 'veio ' + p.GetModPlayer('Abc').owner);
    check("com 'mod/Abc' acha cada um", () => (p.GetModPlayer('test-dupnames-a/Abc').owner === 'A' && p.GetModPlayer('test-dupnames-b/Abc').owner === 'B') || 'nao');
    check('ItemType ambiguo: 0', () => ModContent.ItemType('Abc') === 0 || ModContent.ItemType('Abc'));
}

export class Probe extends ModSystem {
    PostUpdateEverything() {
        if (done || Main.gameMenu) return;
        if (++frames < 60) return;
        done = true;
        run();
        bl.log('dupnames FIM ' + WHO + ': ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
}

export default class Test extends Mod {}
