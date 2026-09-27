import AutoloadMod, { shared, MainThing } from '../../main.js';
import { AutoSword } from '../../Content/Items/AutoSword.js';
import { AutoBase, AutoDerived, NotMod } from '../../Content/Items/Bases.js';

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('autoload ' + label + ': ok');
        else { fails++; bl.log('autoload ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('autoload ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const textureOf = (cls) => {
    const inst = ModContent.GetInstance(cls);
    return inst ? inst.Texture : 'sem registro';
};

export class AutoChecks extends ModSystem {
    PostSetupContent() {
        check('a classe Mod: o Load rodou uma vez, e bl.mod virou ela', () =>
            (shared.loadRuns === 1 && bl.mod === shared.loadMod && bl.mod instanceof AutoloadMod &&
             shared.topMod === bl.mod && bl.mod.id === 'test-autoload') ||
            `Load ${shared.loadRuns}, classe ${bl.mod && bl.mod.constructor.name}, topo ${shared.topMod === bl.mod}`);
        check('o conteudo ja estava registrado no Load', () =>
            (shared.swordTypeInLoad > 0 && shared.swordTypeInLoad === ModContent.ItemType(AutoSword)) || 'tipo no Load ' + shared.swordTypeInLoad);
        check('o mesmo modulo pelo import e pelo registro automatico', () =>
            (shared.sword === AutoSword && ModContent.GetInstance(AutoSword) !== undefined) || 'outra classe');
        check('classe exportada pelo arquivo de entrada tambem entra', () => ModContent.ItemType(MainThing) > 0 || 'MainThing fora');
        check('Autoload = false fica de fora, e a filha entra', () =>
            (ModContent.GetInstance(AutoBase) === undefined && ModContent.ItemType(AutoDerived) > 0) ||
            `base ${ModContent.GetInstance(AutoBase) !== undefined}, filha ${ModContent.ItemType(AutoDerived)}`);
        check('exportacao que nao e classe de mod e ignorada', () => ModContent.GetInstance(NotMod) === undefined || 'registrou NotMod');
        check('ordem: arquivo de entrada, depois Content/ pelo caminho', () => {
            const a = ModContent.ItemType(MainThing), b = ModContent.ItemType(AutoSword), c = ModContent.ItemType(AutoDerived);
            return (a < b && b < c) || `MainThing ${a}, AutoSword ${b}, AutoDerived ${c}`;
        });
        check('textura: o espelho do arquivo da classe', () =>
            (textureOf(AutoSword) === 'Items/AutoSword' && textureOf(AutoDerived) === 'Items/AutoDerived') ||
            `${textureOf(AutoSword)}, ${textureOf(AutoDerived)}`);
        check('textura: pelo nome, em qualquer pasta de Assets/Textures', () => textureOf(MainThing) === 'Other/MainThing' || textureOf(MainThing));
        check('textura carregada no jogo', () => {
            const t = Terraria.GameContent.TextureAssets.Item[ModContent.ItemType(AutoSword)];
            return (t && t.Value && t.Value.Width > 0) || 'sem textura';
        });
        check('ModContent.Request em Assets/Textures', () =>
            (ModContent.HasAsset('Items/AutoSword') && ModContent.HasAsset('Textures/Items/AutoSword') &&
             ModContent.HasAsset('Assets/Textures/Items/AutoSword.png') && !ModContent.HasAsset('Items/NaoExiste')) || 'HasAsset');
        check('gore de Assets/Textures/Gores', () => ModGore.getTypeByName('AutoGore') > 0 || 'tipo ' + ModGore.getTypeByName('AutoGore'));
        check('nome de Localization/', () => {
            const n = Terraria.Lang['string GetItemNameValue(int id)'](ModContent.ItemType(AutoSword));
            return n === 'Auto Sword' || n === 'Espada Automática' || n;
        });
        check('mod sem a classe Mod: o topo rodou, mas ele nao carregou', () =>
            (globalThis.__nomodclassTop === true && !ModLoader.HasMod('test-nomodclass')) ||
            `topo ${globalThis.__nomodclassTop}, HasMod ${ModLoader.HasMod('test-nomodclass')} (instale tools/tests/nomodclass)`);
        bl.log('autoload FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
}
