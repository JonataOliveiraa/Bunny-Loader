// A coleta do chão (ItemPickupLoader), num piso de pedra no alto, com o
// jogador parado no meio:
//   - CanPickup false (ModItem e GlobalItem no Gel): encostado, fica no chão;
//   - OnPickup false: o item some sem entrar no inventário;
//   - ItemSpace true: com o inventário cheio, pega (e o OnPickup consome);
//     a madeira, sem ele, fica;
//   - GrabRange +400: puxado de 20 blocos (a madeira à mesma distância fica);
//     o GetItemGrabRange(WorldItem) do jogo (o da reserva) já soma;
//   - GrabStyle: o mod move o item no lugar do jogo;
//   - GrabRange negativo: perto (dentro do alcance do jogo), não é puxado.
// Loga "itempickup <caso>: ok | FALHOU".
const Main = Terraria.Main;
const W = Terraria.WorldGen;
const { ItemID, TileID } = Terraria.ID;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('itempickup ' + label + ': ok');
        else { fails++; bl.log('itempickup ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('itempickup ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const seen = { orb: 0, fullOrb: 0, style: 0, args: '' };

class TestGem extends ModItem {
    Texture = 'Gem';
    SetDefaults() {
        this.Item.width = this.Item.height = 20;
        this.Item.maxStack = 1;
    }
}

export class Untouchable extends TestGem {
    CanPickup(worldItem, player) {
        seen.args = `${worldItem.inner && worldItem.inner.type} ${player.whoAmI}`;
        return false;
    }
}

export class Orb extends TestGem {
    OnPickup(worldItem, player) { seen.orb++; return false; }
}

export class FullOrb extends TestGem {
    ItemSpace(item, player) { return true; }
    OnPickup(worldItem, player) { seen.fullOrb++; return false; }
}

export class Magnet extends TestGem {
    GrabRange(worldItem, player, grabRange) { grabRange.value += 400; }
}

export class Styled extends TestGem {
    GrabRange(worldItem, player, grabRange) { grabRange.value += 400; }
    GrabStyle(worldItem, player) {
        seen.style++;
        const dx = player.Center.X - worldItem.Center.X, dy = player.Center.Y - worldItem.Center.Y;
        const length = Math.hypot(dx, dy) || 1;
        worldItem.velocity = Vector2.new(dx / length * 8, dy / length * 8);
        return true;
    }
}

export class Shy extends TestGem {
    GrabRange(worldItem, player, grabRange) { grabRange.value -= 1000; }
}

export class NoGel extends GlobalItem {
    CanPickup(worldItem, player) { return worldItem.inner.type !== ItemID.Gel; }
}

const newItem = Terraria.Item['int NewItem(IEntitySource source, Vector2 center, int type, int stack, int prefix, NewItemOwnership ownership, Nullable<Vector2> velocity, Item.NewItemModifier modifier, bool noBroadcast)'];
const kill = (x, y) => W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](x, y, false, false, true);
const placeTile = (x, y, type) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, type, true, true, -1, 0);
const me = () => Main.player[Main.myPlayer];

const area = () => ({ x: Main.spawnTileX, floor: Main.spawnTileY - 40 });

function clearFloor() {
    const { x, floor } = area();
    for (let i = x - 30; i <= x + 30; i++) for (let j = floor - 6; j <= floor; j++) if (bl.tiles.typeAt(i, j) >= 0) kill(i, j);
}

function stand() {
    const p = me(), { x, floor } = area();
    p.position = Vector2.new(x * 16 - p.width / 2, floor * 16 - p.height);
    p.velocity = Vector2.new(0, 0);
    p.fallStart = floor - 3;
}

// Um item parado, com o centro dx pixels à direita do centro do jogador (no chão).
function drop(type, dx) {
    const p = me();
    const at = Vector2.new(p.Center.X + dx, p.position.Y + p.height - 12);
    const i = newItem(null, at, type, 1, 0, 0, null, null, false);
    const w = Main.item[i];
    w.velocity = Vector2.new(0, 0);
    return i;
}

function count(type) {
    const inv = me().inventory;
    let n = 0;
    for (let k = 0; k < 58; k++) if (inv[k].type === type) n += inv[k].stack;
    return n;
}

const remove = (i) => { if (i >= 0 && Main.item[i].active) Main.item[i]['void TurnToAir()'](); };

let filled = [];
function fillInventory() {
    const inv = me().inventory;
    for (let k = 0; k < 50; k++) {
        if (inv[k].type !== 0) continue;
        inv[k]['void SetDefaults(int Type, ItemVariant variant)'](ItemID.DirtBlock, null);
        filled.push(k);
    }
}
function emptyFilled() {
    const inv = me().inventory;
    for (const k of filled) if (inv[k].type === ItemID.DirtBlock) inv[k]['void TurnToAir()']();
    filled = [];
}

const T = (cls) => ModContent.ItemType(cls);
const ids = {};
let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    self.statLife = self.statLifeMax2;
    frames++;

    if (frames === 1) {
        clearFloor();
        const { x, floor } = area();
        for (let k = x - 30; k <= x + 30; k++) placeTile(k, floor, TileID.Stone);
    }
    if (frames < 300) stand();

    if (frames === 10) {
        ids.untouchable = drop(T(Untouchable), 0);
        ids.orb = drop(T(Orb), 0);
        ids.gel = drop(ItemID.Gel, 0);
        ids.gelBefore = count(ItemID.Gel);
    }
    if (frames === 60) {
        check('CanPickup false: encostado, o item de mod fica no chão', () =>
            (Main.item[ids.untouchable].active && count(T(Untouchable)) === 0) || `ativo ${Main.item[ids.untouchable].active}, no inventário ${count(T(Untouchable))}`);
        check('CanPickup recebe a WorldItem e o jogador', () => seen.args === `${T(Untouchable)} ${Main.myPlayer}` || seen.args);
        check('GlobalItem.CanPickup false: o Gel fica no chão', () =>
            (Main.item[ids.gel].active && count(ItemID.Gel) === ids.gelBefore) || `ativo ${Main.item[ids.gel].active}, no inventário ${ids.gelBefore} -> ${count(ItemID.Gel)}`);
        check('OnPickup false: o item some sem entrar no inventário', () =>
            (!Main.item[ids.orb].active && seen.orb === 1 && count(T(Orb)) === 0) || `ativo ${Main.item[ids.orb].active}, chamadas ${seen.orb}, no inventário ${count(T(Orb))}`);
        remove(ids.untouchable);
        remove(ids.gel);
    }

    if (frames === 70) {
        fillInventory();
        check('ItemSpace true: o jogo diz que cabe, com o inventário cheio', () => {
            const samples = Terraria.ID.ContentSamples.ItemsByType;
            const status = me()['Player.ItemSpaceStatus ItemSpace(Item newItem)'](samples.get_Item(T(FullOrb)));
            const wood = me()['Player.ItemSpaceStatus ItemSpace(Item newItem)'](samples.get_Item(ItemID.Wood));
            return (status.CanTakeItem === true && wood.CanTakeItem === false) || `orbe ${status.CanTakeItem}, madeira ${wood.CanTakeItem} (${filled.length} casas enchidas)`;
        });
        ids.fullOrb = drop(T(FullOrb), 0);
        ids.wood = drop(ItemID.Wood, 0);
        ids.woodBefore = count(ItemID.Wood);
    }
    if (frames === 120) {
        check('ItemSpace + OnPickup: com o inventário cheio, pega e consome', () =>
            (!Main.item[ids.fullOrb].active && seen.fullOrb === 1) || `ativo ${Main.item[ids.fullOrb].active}, chamadas ${seen.fullOrb}`);
        check('sem ItemSpace, a madeira não cabe e fica', () => (Main.item[ids.wood].active && count(ItemID.Wood) === ids.woodBefore) || `ativo ${Main.item[ids.wood].active}, no inventário ${ids.woodBefore} -> ${count(ItemID.Wood)}`);
        remove(ids.wood);
        emptyFilled();
    }

    if (frames === 130) {
        ids.magnet = drop(T(Magnet), -20 * 16);
        ids.styled = drop(T(Styled), 20 * 16);
        ids.far = drop(ItemID.Wood, 12 * 16);
        ids.farX = Main.item[ids.far].position.X;
    }
    if (frames === 131) {
        check('GrabRange: o GetItemGrabRange(WorldItem) do jogo soma os 400', () => {
            const p = me(), w = Main.item[ids.magnet];
            const base = p['int GetItemGrabRange(Item item)'](w.inner), range = p['int GetItemGrabRange(WorldItem item)'](w);
            return range === base + 400 || `jogo ${base}, com o mod ${range}`;
        });
    }
    if (frames === 250) {
        check('GrabRange +400: puxado de 20 blocos e pego', () =>
            (!Main.item[ids.magnet].active && count(T(Magnet)) === 1) || `ativo ${Main.item[ids.magnet].active}, x ${Math.round(Main.item[ids.magnet].position.X / 16)}, jogador ${Math.round(me().position.X / 16)}`);
        check('GrabStyle: o mod puxou do jeito dele e o item foi pego', () =>
            (seen.style > 0 && count(T(Styled)) === 1) || `chamadas ${seen.style}, no inventário ${count(T(Styled))}`);
        check('a madeira a 12 blocos não é puxada', () => {
            const w = Main.item[ids.far];
            return (w.active && Math.abs(w.position.X - ids.farX) < 2) || `ativo ${w.active}, x ${ids.farX} -> ${w.position.X}`;
        });
        remove(ids.far);
        ids.shy = drop(T(Shy), 30);
        ids.shyX = Main.item[ids.shy].position.X;
    }
    if (frames === 300) {
        check('GrabRange negativo: perto, dentro do alcance do jogo, não é puxado', () => {
            const w = Main.item[ids.shy];
            return (w.active && Math.abs(w.position.X - ids.shyX) < 2) || `ativo ${w.active}, x ${ids.shyX} -> ${w.position.X}`;
        });
        remove(ids.shy);
        // Tira do inventário o que os testes puseram.
        const inv = me().inventory;
        for (let k = 0; k < 58; k++) if ([T(Magnet), T(Styled)].includes(inv[k].type)) inv[k]['void TurnToAir()']();
        clearFloor();
        const p = me();
        p.position = Vector2.new(Main.spawnTileX * 16, (Main.spawnTileY - 3) * 16);
        p.fallStart = Main.spawnTileY - 3;
        done = true;
        bl.log('itempickup FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('itempickup: carregado');

export default class TestItemPickup extends Mod {}
