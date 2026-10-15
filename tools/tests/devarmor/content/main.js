// ItemID.Sets.PreHardmodeLikeBossBag: ao abrir uma bolsa de mod marcada como
// BossBag, o jogo tenta dar um conjunto de desenvolvedor (TryGettingDevArmor);
// com PreHardmodeLikeBossBag, não tenta. Bolsa comum (sem BossBag) também não.
// As bolsas do jogo: as pré-hardmode e a da Rainha Slime no conjunto, a da Plantera fora.
// Loga "devarmor <caso>: ok | FALHOU".
const Main = Terraria.Main;
const { ItemID } = Terraria.ID;
const SET = 'void SetDefaults(int Type, ItemVariant variant)';

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('devarmor ' + label + ': ok');
        else { fails++; bl.log('devarmor ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('devarmor ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

class TestBag extends ModItem {
    Texture = 'Gem';
    SetDefaults() {
        this.Item.maxStack = 99;
        this.Item.consumable = true;
        this.Item.width = this.Item.height = 24;
    }
    CanRightClick(item) { return true; }
}

export class HardBag extends TestBag {
    SetStaticDefaults() { ItemID.Sets.BossBag[this.Type] = true; }
}

export class SoftBag extends TestBag {
    SetStaticDefaults() {
        ItemID.Sets.BossBag[this.Type] = true;
        ItemID.Sets.PreHardmodeLikeBossBag[this.Type] = true;
    }
}

export class PlainBag extends TestBag {}

let tries = 0;
Terraria.Player['void TryGettingDevArmor(IEntitySource source)'].hook((original, self, source) => {
    tries++;
    return original(self, source);
});

function open(p, type) {
    let slot = -1;
    for (let i = 10; i < 50 && slot < 0; i++) if (p.inventory[i].type === 0) slot = i;
    if (slot < 0) throw new Error('mochila cheia');

    p.inventory[slot][SET](type, null);
    p.inventory[slot].stack = 2;
    const before = tries;
    const opened = Terraria.UI.ItemSlot['bool TryOpenContainer(Item[] inv, int context, int slot, Player player)'](p.inventory, 0, slot, p);
    const left = p.inventory[slot].stack;
    p.inventory[slot][SET](0, null);
    return { opened, spent: left === 1, tries: tries - before };
}

function run(p) {
    const Sets = ItemID.Sets;
    check('bolsas do jogo pré-hardmode no conjunto', () =>
        [ItemID.KingSlimeBossBag, ItemID.WallOfFleshBossBag, ItemID.QueenSlimeBossBag, ItemID.DeerclopsBossBag].every((t) => Sets.PreHardmodeLikeBossBag[t] === true) || 'faltou uma');
    check('bolsa da Plantera fora do conjunto', () => Sets.PreHardmodeLikeBossBag[ItemID.PlanteraBossBag] === false || 'dentro');

    const hard = open(p, ModContent.ItemType(HardBag));
    check('BossBag: abre e tenta o conjunto de desenvolvedor', () => (hard.opened && hard.spent && hard.tries === 1) || JSON.stringify(hard));
    const soft = open(p, ModContent.ItemType(SoftBag));
    check('PreHardmodeLikeBossBag: abre e não tenta', () => (soft.opened && soft.spent && soft.tries === 0) || JSON.stringify(soft));
    const plain = open(p, ModContent.ItemType(PlainBag));
    check('bolsa comum: abre e não tenta', () => (plain.opened && plain.spent && plain.tries === 0) || JSON.stringify(plain));
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    if (++frames !== 60) return;

    done = true;
    check('abrir as bolsas', () => { run(self); });
    bl.log('devarmor FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
});
bl.log('devarmor: carregado');

export default class TestDevArmor extends Mod {}
