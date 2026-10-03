import { Abc } from './Content/Abc.js';
import { AbcPlayer } from './Content/AbcPlayer.js';

const WHO = 'A';
const OTHER_WHO = 'B';
const UID = 'd1a2b3c4-0001-4e5f-8a9b-0c1d2e3f4a01';
const OTHER = 'test-dupnames-b';
const OTHER_UID = 'd1a2b3c4-0002-4e5f-8a9b-0c1d2e3f4a02';
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

function saveRoundTrip() {
    const p = me();
    const fd = Main.ActivePlayerFileData;
    const file = fd.Path + '.bl.json';
    const mine = p.GetModPlayer(AbcPlayer), theirs = p.GetModPlayer(OTHER + '/Abc');
    mine.value = 7;
    theirs.value = 9;
    const SAVE = 'void SavePlayer(PlayerFileData playerFile, bool skipMapSave, bool forceSave)';
    Terraria.Player[SAVE](fd, false, true);
    check('save: uma chave por mod', () => {
        const all = JSON.parse(bl.file.read(file) || '{}');
        const a = all[UID + '/Abc'], b = all[OTHER_UID + '/Abc'];
        return (a && a.value === 7 && b && b.value === 9) || JSON.stringify({ a, b });
    });
    check('load: cada mod recebe o seu', () => {
        const loaded = Terraria.Player['PlayerFileData LoadPlayer(string playerPath, bool cloudSave)'](fd.Path, false);
        const q = loaded.Player;
        const a = q.GetModPlayer(AbcPlayer), b = q.GetModPlayer(OTHER + '/Abc');
        return (a.loaded && a.loaded.value === 7 && b.loaded && b.loaded.value === 9) || JSON.stringify({ a: a.loaded, b: b.loaded });
    });
    mine.value = 0;
    theirs.value = 0;
    Terraria.Player[SAVE](fd, false, true);
    check('limpeza', () => {
        const all = JSON.parse(bl.file.read(file) || '{}');
        return (!all[UID + '/Abc'] && !all[OTHER_UID + '/Abc']) || 'sobrou';
    });
}

export class Probe extends ModSystem {
    PostUpdateEverything() {
        if (done || Main.gameMenu) return;
        if (++frames < 60) return;
        done = true;
        run();
        saveRoundTrip();
        bl.log('dupnames FIM ' + WHO + ': ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
}

export default class Test extends Mod {}
