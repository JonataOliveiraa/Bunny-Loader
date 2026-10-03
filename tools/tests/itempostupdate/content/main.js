const Main = Terraria.Main;
const SLOT = 48;
let fails = 0;
let frames = 0;
let stage = 0;
let calls = 0;
let centerOk = false;
let dropped = -1;
let type = 0;

function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('itempostupdate ' + label + ': ok');
        else { fails++; bl.log('itempostupdate ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) { fails++; bl.log('itempostupdate ' + label + ': FALHOU com ' + e); }
}

function start() {
    const found = new Ref();
    if (!ModContent.TryFind(ModItem, 'examplemod/ExampleSoul', found)) {
        bl.log('itempostupdate FIM: sem o Example Mod');
        stage = 3;
        return;
    }
    type = found.value.Type;
    const proto = found.value.constructor.prototype;
    const original = proto.PostUpdate;
    proto.PostUpdate = function (item) {
        calls++;
        if (item && item.Center && typeof item.Center.X === 'number') centerOk = true;
        return original.call(this, item);
    };

    const p = Main.player[Main.myPlayer];
    p.inventory[SLOT]['void SetDefaults(int Type, ItemVariant variant)'](type, null);
    const at = Vector2.new(p.Center.X + 480, p.Center.Y - 64);
    dropped = Terraria.Item['int NewItem(IEntitySource source, Vector2 center, int type, int stack, int prefix, NewItemOwnership ownership, Nullable<Vector2> velocity, Item.NewItemModifier modifier, bool noBroadcast)'](
        null, at, type, 1, 0, 0, null, null, false);
    stage = 1;
}

function finish() {
    check('o item no chão chama o PostUpdate', () => calls > 10 || 'chamadas: ' + calls);
    check('o PostUpdate recebe a WorldItem, com Center', () => centerOk || 'sem Center');
    check('a do chão é a Alma', () => (dropped >= 0 && Main.item[dropped].type === type) || 'indice ' + dropped);
    const p = Main.player[Main.myPlayer];
    for (let i = 0; i < 58; i++) {
        if (p.inventory[i].type === type) p.inventory[i].TurnToAir();
    }
    if (dropped >= 0 && Main.item[dropped].type === type) Main.item[dropped].TurnToAir();
    bl.log('itempostupdate FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
}

export class Probe extends ModSystem {
    PostUpdateEverything() {
        if (stage === 3 || Main.gameMenu) return;
        frames++;
        if (stage === 0 && frames >= 60) start();
        else if (stage === 1 && frames >= 180) {
            stage = 3;
            finish();
        }
    }
}

export default class TestItemPostUpdate extends Mod {}
