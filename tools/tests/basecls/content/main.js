// O exemplo de classe base da documentação: a base com Autoload = false fica
// de fora, a filha entra com o que herdou; a base fora de Content/ e Common/
// também não é registrada. Loga "basecls ...".
import { BardItem } from './Content/Items/Bard/BardItem.js';
import { GrandPiano } from './Content/Items/Bard/GrandPiano.js';
import { OutsideBase } from './Bases/OutsideBase.js';
import { Drum } from './Content/Items/Bard/Drum.js';

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('basecls ' + label + ': ok');
        else { fails++; bl.log('basecls ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) { fails++; bl.log('basecls ' + label + ': FALHOU com ' + e); }
}

function sample(type) {
    const it = Terraria.Item.new();
    it['void .ctor()']();
    it['void SetDefaults(int Type, ItemVariant variant)'](type, null);
    return it;
}

export default class TestBaseClass extends Mod {
    PostSetupContent() {
        check('a base com Autoload = false não é registrada', () => ModContent.GetInstance(BardItem) === undefined || 'registrada');
        check('a base fora de Content/ e Common/ não é registrada', () => ModContent.GetInstance(OutsideBase) === undefined || 'registrada');
        const piano = ModContent.ItemType(GrandPiano), drum = ModContent.ItemType(Drum);
        check('as filhas são registradas', () => (piano > 0 && drum > 0) || `piano ${piano}, tambor ${drum}`);
        check('SetDefaults: o da base pelo super, e o da filha', () => {
            const it = sample(piano);
            return (it.useStyle === 5 && it.useTime === 20 && it.noMelee && it.rare === 2 && it.damage === 40 && it.width === 40) ||
                `useStyle ${it.useStyle}, useTime ${it.useTime}, noMelee ${it.noMelee}, rare ${it.rare}, dano ${it.damage}`;
        });
        check('a filha herda o SetDefaults da base de fora', () => sample(drum).damage === 7 || 'dano ' + sample(drum).damage);
        check('instanceof pela base', () => (ModContent.GetModItem(piano) instanceof BardItem && !(ModContent.GetModItem(drum) instanceof BardItem)) || 'não');
        check('o campo da filha ganha do da base', () => ModContent.GetModItem(piano).InspirationCost === 3 || ModContent.GetModItem(piano).InspirationCost);
        check('o método escrito só na base vale na filha (ModifyTooltips)', () => {
            const lines = [];
            ModContent.GetModItem(piano).ModifyTooltips(sample(piano), lines);
            return (lines.length === 1 && lines[0].Text === 'Custa 3 de inspiração') || JSON.stringify(lines.map((l) => l.Text));
        });
        bl.log('basecls FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
}
