// Raridade de mod no drop. O Item.Prefix do jogo prende a raridade em 11 mesmo
// quando o sorteio não dá prefixo nenhum (25% no Prefix(-1) do drop): a espada
// de raridade de mod caía roxa. Casos: Prefix(-1) muitas vezes, NewItem com o
// prefixo sorteado (-1) e o drop do item na mão do jogador.
// Loga "raritydrop <caso>: ok | FALHOU".
const Main = Terraria.Main;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('raritydrop ' + label + ': ok');
        else { fails++; bl.log('raritydrop ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('raritydrop ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

export class DropRarity extends ModRarity {
    get RarityColor() { return Color.new(10, 200, 30); }
}

export class RaritySword extends ModItem {
    SetDefaults(item) {
        item.width = item.height = 20;
        item.melee = true;
        item.damage = 20;
        item.knockBack = 5;
        item.useStyle = 1;
        item.useTime = item.useAnimation = 20;
        item.rare = ModContent.RarityType(DropRarity);
    }
}

const newItem = Terraria.Item['int NewItem(IEntitySource source, int X, int Y, int Width, int Height, int type, int stack, bool noBroadcast, int prefix, NewItemOwnership ownership, Nullable<Vector2> velocity, Item.NewItemModifier modifier)'];

function run() {
    const type = ModContent.ItemType(RaritySword);
    const rare = ModContent.RarityType(DropRarity);
    bl.log(`raritydrop diag: espada ${type}, raridade ${rare}`);

    // Prefix(-1) como no drop: com e sem prefixo sorteado.
    const lost = { none: 0, some: 0 }, rolls = { none: 0, some: 0 };
    for (let k = 0; k < 120; k++) {
        const item = Terraria.Item.new();
        item['void .ctor()']();
        item['void SetDefaults(int Type, ItemVariant variant)'](type, null);
        item['bool Prefix(int prefixWeWant)'](-1);
        const key = item.prefix === 0 ? 'none' : 'some';
        rolls[key]++;
        if (item.rare < rare) lost[key]++;
    }
    bl.log(`raritydrop diag: sem prefixo ${rolls.none} (${lost.none} perderam), com prefixo ${rolls.some} (${lost.some} perderam)`);
    check('Prefix(-1) sem prefixo mantém a raridade de mod', () => (rolls.none > 0 && lost.none === 0) || `${lost.none} de ${rolls.none}`);
    check('Prefix(-1) com prefixo mantém a raridade de mod', () => (rolls.some > 0 && lost.some === 0) || `${lost.some} de ${rolls.some}`);

    // NewItem com prefixo sorteado: o item no chão.
    const p = Main.player[Main.myPlayer];
    let droppedLost = 0;
    const indices = [];
    for (let k = 0; k < 40; k++) {
        const i = newItem(Terraria.DataStructures.EntitySource_DebugCommand.new(), Math.floor(p.position.X), Math.floor(p.position.Y) - 64,
                          16, 16, type, 1, false, -1, 0, null, null);
        indices.push(i);
        if (Main.item[i].inner.rare < rare) droppedLost++;
    }
    check('NewItem(prefixo -1): o item no chão mantém a raridade', () => droppedLost === 0 || `${droppedLost} de 40 perderam`);
    for (const i of indices) Main.item[i].TurnToAir();

    // O item da mão, jogado fora.
    const slot = p.selectedItemState.selected;
    const saved = [p.inventory[slot].type, p.inventory[slot].stack, p.inventory[slot].prefix];
    p.inventory[slot]['void SetDefaults(int Type, ItemVariant variant)'](type, null);
    const before = [];
    for (let i = 0; i < Main.item.length; i++) if (Main.item[i].active) before.push(i);
    p.DropSelectedItem();
    let thrown = -1;
    for (let i = 0; i < Main.item.length && thrown < 0; i++) {
        if (Main.item[i].active && Main.item[i].inner.type === type && !before.includes(i)) thrown = i;
    }
    check('o item jogado da mão mantém a raridade', () =>
        (thrown >= 0 && Main.item[thrown].inner.rare === rare) || (thrown < 0 ? 'não achou o item jogado' : 'rare ' + Main.item[thrown].inner.rare));
    if (thrown >= 0) Main.item[thrown].TurnToAir();
    p.inventory[slot]['void SetDefaults(int Type, ItemVariant variant)'](saved[0], null);
    p.inventory[slot].stack = saved[1];
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    if (++frames < 60) return;

    done = true;
    try { run(); } catch (e) { fails++; bl.log('raritydrop FALHOU com ' + e + ' | ' + (e.stack || '')); }
    bl.log('raritydrop FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
});
bl.log('raritydrop: carregado');

export default class TestRarityDrop extends Mod {}
