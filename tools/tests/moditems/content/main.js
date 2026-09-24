// Teste das tabelas de item de mod (runtime/ModItems.cpp).
//
// O jogo indexa dezenas de arrays pelo tipo do item, todos nascidos com
// ItemID.Count (6147) posicoes, e esta build do IL2CPP NAO confere limite:
// uma tabela esquecida le ou escreve alem do fim sem erro nenhum. O tooltip de
// item de mod sumia por isso (ArmorSetBonuses.SetsContaining).
//
// Confere que cada tabela conhecida cresceu, e poe os dois itens de mod dos
// exemplos no inventario (espacos 1 e 2) para testar o tooltip na mao.
// Loga "moditems <caso>: ok | FALHOU".
const Main = Terraria.Main;
const MOD_TYPES = [6147, 6148];
const EXPECTED = 6147 + MOD_TYPES.length;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('moditems ' + label + ': ok');
        else { fails++; bl.log('moditems ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('moditems ' + label + ': FALHOU com ' + e);
    }
}

/** Tabela com espaco para os itens de mod; `ref` exige elemento nao nulo neles. */
function table(label, get, ref) {
    check(label, () => {
        const t = get();
        if (!t) return 'nula';
        if (t.length < EXPECTED) return 'tamanho ' + t.length;
        if (ref) for (const type of MOD_TYPES) if (t[type] === null) return '[' + type + '] nulo';
    });
}

const Sets = Terraria.ID.ItemID.Sets;
const Prefix = Terraria.GameContent.Prefixes.PrefixLegacy.ItemSets;

function run() {
    // As que ja eram aumentadas antes da correcao (controle).
    table('ItemID.Sets.IsAMaterial', () => Sets.IsAMaterial);
    table('Lang._itemNameCache', () => Terraria.Lang._itemNameCache, true);
    table('TextureAssets.Item', () => Terraria.GameContent.TextureAssets.Item, true);
    // As que ficavam de fora.
    table('ArmorSetBonuses.SetsContaining', () => Terraria.DataStructures.ArmorSetBonuses.SetsContaining, true);
    table('AmmoID.Sets.IsArrow', () => Terraria.ID.AmmoID.Sets.IsArrow);
    table('ItemVariants._variants', () => Terraria.GameContent.Items.ItemVariants._variants);
    table('ItemSorting._layerIndexForItemType', () => Terraria.UI.ItemSorting._layerIndexForItemType);
    table('PrefixLegacy.ItemSets.SwordsHammersAxesPicks', () => Prefix.SwordsHammersAxesPicks);
    table('PrefixLegacy.ItemSets.GunsBows', () => Prefix.GunsBows);
    table('QuickStacking.firstEntryForType',
          () => Terraria.GameContent.QuickStacking.matchingItemTypeScratch.firstEntryForType);

    check('itens de mod no inventario', () => {
        const inv = Main.player[Main.myPlayer].inventory;
        for (let i = 0; i < MOD_TYPES.length; i++) {
            inv[1 + i]['void SetDefaults(int Type, ItemVariant variant)'](MOD_TYPES[i], null);
            if (inv[1 + i].type !== MOD_TYPES[i]) return 'espaco ' + (1 + i) + ': tipo ' + inv[1 + i].type;
        }
    });
    bl.log('moditems FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
}

/**
 * Duas classes cujo construtor estatico o jogo so roda no primeiro uso (de
 * municao, de empilhamento rapido). Tocar num estatico delas pela ponte o roda,
 * como no C#; o runtime aumenta a tabela nova no quadro seguinte.
 */
function touchLazyClasses() {
    const arrows = Terraria.ID.AmmoID.Sets.IsArrow;   // construtor estatico
    Terraria.GameContent.QuickStacking['int GetCategory(int type)'](1);
    bl.log('moditems AmmoID.Sets.IsArrow ao tocar: ' + (arrows === null ? 'null' : arrows.length));
}

// Alguns quadros dentro do mundo: varias tabelas o jogo so cria ao carregar.
let frames = 0;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu) return;
    ++frames;
    if (frames === 60) touchLazyClasses();
    if (frames === 63) run();
});
bl.log('moditems: carregado');
