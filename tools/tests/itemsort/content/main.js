// O botão "Ordenar" do inventário com item de mod (content/items/ModItemSorting.cpp).
//
// Cada camada do ItemSorting só aceita os tipos da sua lista branca, montada
// até ItemID.Count. Item de mod sobrava no fim do Sort sem contagem de camada,
// o `_sort_counts[0]` lançava depois de o jogo ter esvaziado os slots, e o
// item sumia do inventário.
//
// Favorita o que o personagem já tem (o Sort não mexe em favorito), põe três
// itens de mod e um vanilla nos slots livres, ordena e confere que todos
// continuam lá; depois tira os de teste e desfavorita. Loga "itemsort <caso>: ok | FALHOU".
import { SortSword } from './Content/Items/SortSword.js';
import { SortOre } from './Content/Items/SortOre.js';
import { SortJunk } from './Content/Items/SortJunk.js';

const Main = Terraria.Main;
const { ItemID } = Terraria.ID;
const Sorting = Terraria.UI.ItemSorting;
const FIRST_SLOT = 10, END_SLOT = 50;   // fora a barra de atalho, as moedas e a munição

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('itemsort ' + label + ': ok');
        else { fails++; bl.log('itemsort ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('itemsort ' + label + ': FALHOU com ' + e);
    }
}

const layerOf = (type) => Sorting['string GetSortingLayer(int itemType)'](type);

function run() {
    const inv = Main.player[Main.myPlayer].inventory;
    const sword = ModContent.ItemType(SortSword);
    const ore = ModContent.ItemType(SortOre);
    const junk = ModContent.ItemType(SortJunk);
    const placed = [
        { type: sword, stack: 1 },
        { type: ore, stack: 37 },
        { type: junk, stack: 5 },
        { type: ItemID.Wood, stack: 12 },
    ];

    check('camadas dos itens de mod', () => {
        for (const t of [sword, ore, junk]) if (layerOf(t) === null) return 'tipo ' + t + ' sem camada';
        const melee = layerOf(ItemID.WoodenSword);
        if (layerOf(sword) !== melee) return 'espada em "' + layerOf(sword) + '", nao em "' + melee + '"';
        if (layerOf(junk) !== 'Last - Trash') return 'lixo em "' + layerOf(junk) + '"';
        const index = Sorting._layerIndexForItemType;
        if (index[sword] !== index[ItemID.WoodenSword]) return '_layerIndexForItemType[' + sword + '] = ' + index[sword];
    });

    const pinned = [];
    const free = [];
    for (let i = FIRST_SLOT; i < END_SLOT; i++) {
        const it = inv[i];
        if (it.type !== 0 && it.stack > 0) {
            if (!it.favorited) { it.favorited = true; pinned.push(i); }
        } else free.push(i);
    }
    if (free.length < placed.length) {
        fails++;
        bl.log('itemsort ordenar: FALHOU (so ' + free.length + ' slot(s) livre(s))');
    } else {
        // Nos ULTIMOS slots livres, de tras para frente: o lixo antes da espada.
        for (let k = 0; k < placed.length; k++) {
            const it = inv[free[free.length - 1 - k]];
            it['void SetDefaults(int Type, ItemVariant variant)'](placed[k].type, null);
            it.stack = placed[k].stack;
        }
        check('ordenar', () => {
            Sorting['void SortInventory()']();
        });
        check('nada sumiu', () => {
            for (const p of placed) {
                let total = 0;
                for (let i = 0; i < inv.length; i++) if (!inv[i].favorited && inv[i].type === p.type) total += inv[i].stack;
                if (total !== p.stack) return 'tipo ' + p.type + ': ' + total + ' de ' + p.stack;
            }
        });
        check('favoritos no lugar', () => {
            for (const i of pinned) if (!inv[i].favorited || inv[i].type === 0) return 'slot ' + i;
        });
        check('espada antes do lixo', () => {
            let s = -1, j = -1;
            for (let i = FIRST_SLOT; i < END_SLOT; i++) {
                if (inv[i].type === sword) s = i;
                if (inv[i].type === junk) j = i;
            }
            if (s < 0 || j < 0 || s > j) return 'espada ' + s + ', lixo ' + j;
        });
    }

    // Arruma: os de teste saem, os do personagem voltam a ser como eram.
    const ours = new Set(placed.map(p => p.type));
    for (let i = 0; i < inv.length; i++) if (!inv[i].favorited && ours.has(inv[i].type)) inv[i]['void TurnToAir()']();
    for (const i of pinned) inv[i].favorited = false;
    bl.log('itemsort FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
}

let frames = 0;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu) return;
    if (++frames === 60) run();
});
bl.log('itemsort: carregado');

export default class TestItemsort extends Mod {}
