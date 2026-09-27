// A estrutura do mod (classe Mod, Content/, Common/, Assets/) e o registro
// automático. As conferências moram em Common/Systems/AutoChecks.js, no
// PostSetupContent. Rode junto com tools/tests/nomodclass. Loga "autoload ...".
import { AutoSword } from './Content/Items/AutoSword.js';

// Classe de conteúdo no próprio arquivo de entrada: registrada também, e a
// textura sai pelo nome (Assets/Textures/Other/MainThing.png).
export class MainThing extends ModItem {}

export const shared = { sword: AutoSword, loadRuns: 0, loadMod: null, swordTypeInLoad: 0, topMod: bl.mod };

export default class AutoloadMod extends Mod {
    Load() {
        shared.loadRuns++;
        shared.loadMod = this;
        shared.swordTypeInLoad = ModContent.ItemType(AutoSword);
    }
}

bl.log('autoload: carregado');
