import { Abc } from './Content/Abc.js';
import { AbcPlayer } from './Content/AbcPlayer.js';

const WHO = 'B';
const OTHER_WHO = 'A';
const UID = 'd1a2b3c4-0002-4e5f-8a9b-0c1d2e3f4a02';
const OTHER = 'test-dupnames-a';
const OTHER_UID = 'd1a2b3c4-0001-4e5f-8a9b-0c1d2e3f4a01';
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
    check('o ModPlayer deste mod registrou', () => (p.GetModPlayer(AbcPlayer) instanceof AbcPlayer && p.GetModPlayer(AbcPlayer).owner === WHO) || 'nao');
    check('pelo nome, o do proprio mod', () => (p.GetModPlayer('Abc') === p.GetModPlayer(AbcPlayer)) || 'veio o ' + (p.GetModPlayer('Abc') || {}).owner);
    check("o do outro por 'mod/Abc'", () => {
        const other = p.GetModPlayer(OTHER + '/Abc');
        return (other && other.owner === OTHER_WHO && other !== p.GetModPlayer(AbcPlayer)) || 'veio ' + (other && other.owner);
    });
    check('AbcPlayer.get e getByName', () => (AbcPlayer.get(p) === p.GetModPlayer(AbcPlayer) && ModPlayer.getByName('Abc') === p.GetModPlayer(AbcPlayer)) || 'diferentes');
    check('ItemType pelo nome: o do proprio mod', () => {
        const own = ModContent.ItemType(Abc), byName = ModContent.ItemType('Abc'), other = ModContent.ItemType(OTHER + '/Abc');
        return (own > 0 && byName === own && other > 0 && other !== own) || `classe ${own}, nome ${byName}, outro ${other}`;
    });
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
