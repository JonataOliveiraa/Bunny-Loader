import { FirstItem } from './Content/Items/FirstItem.js';
import { SecondItem } from './Content/Items/SecondItem.js';
import { OnlyBuff } from './Content/Buffs/OnlyBuff.js';

const EXAMPLE = 'examplemod';
let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('getcontent ' + label + ': ok');
        else { fails++; bl.log('getcontent ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) { fails++; bl.log('getcontent ' + label + ': FALHOU com ' + e); }
}

export class Watcher extends ModSystem {}

function findExample() {
    const found = new Ref();
    return ModLoader.TryGetMod(EXAMPLE, found) ? found.value : null;
}

export default class TestGetContent extends Mod {
    PostSetupContent() {
        const items = this.GetContent(ModItem);
        check('os ModItem deste mod, na ordem', () => (items.length === 2 && items[0] instanceof FirstItem && items[1] instanceof SecondItem) ||
            items.map((i) => i.constructor.name).join(','));
        check('os types', () => {
            const types = items.map((i) => i.Type);
            return (types[0] === ModContent.ItemType(FirstItem) && types[1] === ModContent.ItemType(SecondItem) && types[0] > 0) || types.join(',');
        });
        check('outra classe base', () => {
            const buffs = this.GetContent(ModBuff);
            return (buffs.length === 1 && buffs[0] instanceof OnlyBuff && buffs[0].Type === ModContent.BuffType(OnlyBuff)) || buffs.length;
        });
        check('ModSystem também', () => this.GetContent(ModSystem).length === 1 || this.GetContent(ModSystem).length);
        check('nada de outro mod', () => items.every((i) => i.Mod === this) || 'misturou');
        check('ModContent.GetContent junta todos os mods', () => {
            const all = ModContent.GetContent(ModItem);
            const example = findExample();
            const theirs = example ? example.GetContent(ModItem).length : 0;
            return (all.length >= items.length + theirs && items.every((i) => all.includes(i))) || `todos ${all.length}, ExampleMod ${theirs}`;
        });
        check('classe base errada lança', () => {
            try { this.GetContent('ModItem'); return 'não lançou'; } catch (e) { return e instanceof TypeError || String(e); }
        });
        const example = findExample();
        if (example) bl.log('getcontent ExampleMod tem ' + example.GetContent(ModItem).length + ' itens, ' + example.GetContent(ModNPC).length + ' NPCs');
        bl.log('getcontent FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
}
