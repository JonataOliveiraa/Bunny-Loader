// A bolsa de tesouro do Chefe de Exemplo (precisa do Example Mod ligado):
//   - a ExampleBossBag esta em ItemID.Sets.OpenableBag (o jogo deixa abrir);
//   - abrir pelo caminho do jogo (ItemSlot.TryOpenContainer, o do toque no
//     inventario) gasta uma e da o ItemLoot: 15-30 ExampleItem e as moedas
//     do chefe (3 de ouro, com o bonus aleatorio);
//   - as regras do chefe: a condicao "fora do Expert" e a bolsa (o
//     ItemDropRule.BossBag do celular e um DropBasedOnExpertMode).
const Main = Terraria.Main;
const SET = 'void SetDefaults(int Type, ItemVariant variant)';
const COINS = [[71, 1], [72, 100], [73, 10000], [74, 1000000]];   // cobre, prata, ouro, platina

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('bossbag ' + label + ': ok');
        else { fails++; bl.log('bossbag ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('bossbag ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const BAG = () => ModContent.ItemType('examplemod/ExampleBossBag');
const EXAMPLE_ITEM = () => ModContent.ItemType('examplemod/ExampleItem');
const money = (p) => COINS.reduce((sum, [type, value]) => sum + p.CountItem(type, 0) * value, 0);

let state = null;   // o antes da abertura, conferido depois (os itens caem no chao e sao pegos)

function open() {
    const p = Main.LocalPlayer;
    check('a bolsa existe', () => BAG() >= bl.items.vanillaCount || 'ExampleBossBag = ' + BAG());
    check('a bolsa e abrivel', () => Terraria.ID.ItemID.Sets.OpenableBag[BAG()] === true || 'OpenableBag falso');

    check('as regras do chefe', () => {
        const boss = ModContent.NPCType('examplemod/ExampleBoss');
        const list = Main.ItemDropsDB['List<IItemDropRule> GetRulesForNPCID(int npcNetId, bool includeGlobalDrops)'](boss, false);
        const names = [];
        for (let i = 0; i < list.Count; i++) names.push(String(list.get_Item(i)));
        bl.log('bossbag: regras do chefe: ' + names.join(', '));
        return (names.some((n) => n.includes('LeadingConditionRule')) && names.some((n) => n.includes('DropBasedOnExpertMode'))) ||
            names.join(', ');
    });

    // A bolsa num espaco vazio da mochila; abrir como o toque no inventario.
    let slot = -1;
    for (let i = 10; i < 50 && slot < 0; i++) if (p.inventory[i].type === 0) slot = i;
    if (slot < 0) { check('espaco na mochila', () => 'mochila cheia'); return; }
    p.inventory[slot][SET](BAG(), null);
    p.inventory[slot].stack = 2;
    state = { slot, example: p.CountItem(EXAMPLE_ITEM(), 0), money: money(p) };
    const opened = Terraria.UI.ItemSlot['bool TryOpenContainer(Item[] inv, int context, int slot, Player player)'](p.inventory, 0, slot, p);
    check('TryOpenContainer abriu', () => opened === true || String(opened));
    check('gastou uma bolsa', () => (p.inventory[slot].type === BAG() && p.inventory[slot].stack === 1) ||
        `${p.inventory[slot].type} x${p.inventory[slot].stack}`);
}

function after() {
    const p = Main.LocalPlayer;
    const got = p.CountItem(EXAMPLE_ITEM(), 0) - state.example;
    const coins = money(p) - state.money;
    bl.log(`bossbag: a bolsa deu ${got} ExampleItem e ${coins} em moedas (cobre)`);
    check('ExampleItem 15-30', () => (got >= 15 && got <= 30) || got);
    check('moedas do chefe (~3 de ouro)', () => (coins >= 20000 && coins <= 70000) || coins);
    p.inventory[state.slot][SET](0, null);
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    ++frames;
    if (frames === 90) check('preparo', open);
    if (frames === 180) {
        done = true;
        if (state) check('depois', after);
        bl.log('bossbag FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('bossbag: carregado');

// A classe do mod, obrigatória no arquivo de entrada.
export default class TestBossbag extends Mod {}
