// Item.ResearchUnlockCount (a quantidade que o modo Jornada pede para
// pesquisar): no SetStaticDefaults e no SetDefaults de itens de mod, o padrão
// 1 de todo item de mod, o 0 que tira da pesquisa, e num item do jogo. O jogo
// remonta a tabela no Initialize_AlmostEverything: os valores precisam estar
// lá depois de entrar no mundo.
// Loga "research <caso>: ok | FALHOU".
const Main = Terraria.Main;
const { ItemID } = Terraria.ID;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('research ' + label + ': ok');
        else { fails++; bl.log('research ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('research ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

class TestItem extends ModItem {
    Texture = 'Gem';
    SetDefaults() { this.Item.width = this.Item.height = 20; }
}

let staticSeen = -1;
export class StaticResearch extends TestItem {
    SetStaticDefaults() {
        this.Item.ResearchUnlockCount = 25;
        staticSeen = this.Item.ResearchUnlockCount;
    }
}

export class DefaultsResearch extends TestItem {
    SetDefaults() {
        super.SetDefaults();
        this.Item.ResearchUnlockCount = 7;
    }
}

export class PlainResearch extends TestItem {}

export class NoResearch extends TestItem {
    SetStaticDefaults() { this.Item.ResearchUnlockCount = 0; }
}

const sample = (type) => {
    const item = Terraria.Item.new();
    item['void .ctor()']();
    item['void SetDefaults(int Type, ItemVariant variant)'](type, null);
    return item;
};

function run() {
    const table = Terraria.GameContent.Creative.CreativeItemSacrificesCatalog.Instance.SacrificeCountNeededByItemId;
    const inTable = (type) => table['bool ContainsKey(int key)'](type);

    check('SetStaticDefaults: o getter vê o valor na hora', () => staticSeen === 25 || staticSeen);
    check('SetStaticDefaults: 25 depois de entrar no mundo', () => sample(ModContent.ItemType(StaticResearch)).ResearchUnlockCount === 25 ||
        sample(ModContent.ItemType(StaticResearch)).ResearchUnlockCount);
    check('SetDefaults: 7', () => sample(ModContent.ItemType(DefaultsResearch)).ResearchUnlockCount === 7 ||
        sample(ModContent.ItemType(DefaultsResearch)).ResearchUnlockCount);
    check('item de mod sem nada: 1', () => sample(ModContent.ItemType(PlainResearch)).ResearchUnlockCount === 1 ||
        sample(ModContent.ItemType(PlainResearch)).ResearchUnlockCount);
    check('0: fora da tabela', () => (!inTable(ModContent.ItemType(NoResearch)) && sample(ModContent.ItemType(NoResearch)).ResearchUnlockCount === 0) ||
        sample(ModContent.ItemType(NoResearch)).ResearchUnlockCount);

    const dirt = sample(ItemID.DirtBlock);
    const before = dirt.ResearchUnlockCount;
    check('item do jogo: o valor da tabela (terra = 100)', () => before === 100 || before);
    dirt.ResearchUnlockCount = 42;
    check('item do jogo: mudar vale para o tipo', () => sample(ItemID.DirtBlock).ResearchUnlockCount === 42 || sample(ItemID.DirtBlock).ResearchUnlockCount);
    dirt.ResearchUnlockCount = before;
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    if (++frames !== 60) return;

    done = true;
    check('pesquisa', run);
    bl.log('research FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
});
bl.log('research: carregado');

export default class TestResearch extends Mod {}
