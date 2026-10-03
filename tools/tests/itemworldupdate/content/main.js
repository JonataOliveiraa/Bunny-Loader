// PreUpdateInWorld e PostUpdateInWorld (ModItem e GlobalItem). Um item de mod
// e madeira, soltos 15 blocos acima do jogador:
//   - os dois métodos recebem o Item e a WorldItem (com Center e position);
//   - com o Pre devolvendo false o jogo não atualiza o item: ele fica parado
//     no ar; o Post roda igual;
//   - com o Pre devolvendo true o jogo volta a mexer nele.
// Loga "itemworldupdate <caso>: ok | FALHOU".
const Main = Terraria.Main;
const { ItemID } = Terraria.ID;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('itemworldupdate ' + label + ': ok');
        else { fails++; bl.log('itemworldupdate ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('itemworldupdate ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

let freeze = true;
const seen = { pre: 0, post: 0, args: '', gPre: 0, gPost: 0, gArgs: '' };
const describe = (item, world) =>
    `item ${item && item.type} world ${world && world.inner && world.inner.type} centro ${world && typeof world.Center.X}`;

export class FloatyGem extends ModItem {
    SetDefaults() {
        this.Item.width = this.Item.height = 20;
        this.Item.maxStack = 99;
    }
    PreUpdateInWorld(item, worldItem) {
        seen.pre++;
        seen.args = describe(item, worldItem);
        return !freeze;
    }
    PostUpdateInWorld(item, worldItem) { seen.post++; }
}

export class WoodWatcher extends GlobalItem {
    PreUpdateInWorld(item, worldItem) {
        if (item.type !== ItemID.Wood) return true;
        seen.gPre++;
        seen.gArgs = describe(item, worldItem);
        return !freeze;
    }
    PostUpdateInWorld(item, worldItem) { if (item.type === ItemID.Wood) seen.gPost++; }
}

const newItem = Terraria.Item['int NewItem(IEntitySource source, Vector2 center, int type, int stack, int prefix, NewItemOwnership ownership, Nullable<Vector2> velocity, Item.NewItemModifier modifier, bool noBroadcast)'];

let gem = -1, wood = -1, start = {};
let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    self.statLife = self.statLifeMax2;
    frames++;

    if (frames === 60) {
        const at = (dx) => Vector2.new(self.Center.X + dx, self.Center.Y - 15 * 16);
        gem = newItem(null, at(-64), ModContent.ItemType(FloatyGem), 1, 0, 0, null, null, false);
        wood = newItem(null, at(64), ItemID.Wood, 1, 0, 0, null, null, false);
        start = { gem: Main.item[gem].position.Y, wood: Main.item[wood].position.Y };
    }
    if (frames === 120) {
        const g = Main.item[gem], w = Main.item[wood];
        bl.log(`itemworldupdate diag: mod ${JSON.stringify(seen)}; parado y ${start.gem} -> ${g.position.Y}, madeira ${start.wood} -> ${w.position.Y}`);
        check('ModItem: Pre e Post chamados todo quadro', () => (seen.pre > 30 && seen.post === seen.pre) || `pre ${seen.pre}, post ${seen.post}`);
        check('ModItem: recebe o Item e a WorldItem dele', () =>
            seen.args === `item ${ModContent.ItemType(FloatyGem)} world ${ModContent.ItemType(FloatyGem)} centro number` || seen.args);
        check('GlobalItem: Pre e Post no item do jogo', () => (seen.gPre > 30 && seen.gPost === seen.gPre) || `pre ${seen.gPre}, post ${seen.gPost}`);
        check('GlobalItem: recebe o Item e a WorldItem dele', () => seen.gArgs === `item ${ItemID.Wood} world ${ItemID.Wood} centro number` || seen.gArgs);
        check('Pre false: o jogo não atualiza (os dois ficam parados no ar)', () =>
            (g.position.Y === start.gem && w.position.Y === start.wood) || `mod ${start.gem}->${g.position.Y}, madeira ${start.wood}->${w.position.Y}`);
        freeze = false;
        start = { gem: g.position.Y, wood: w.position.Y };
    }
    if (frames === 160) {
        const g = Main.item[gem], w = Main.item[wood];
        // O NewItem solta com um impulso para cima: primeiro sobe, depois cai.
        check('Pre true: o jogo volta a atualizar (os dois se mexem)', () =>
            (g.position.Y !== start.gem && w.position.Y !== start.wood) || `mod ${start.gem}->${g.position.Y}, madeira ${start.wood}->${w.position.Y}`);
        g.TurnToAir();
        w.TurnToAir();
        done = true;
        bl.log('itemworldupdate FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('itemworldupdate: carregado');

export default class TestItemWorldUpdate extends Mod {}
