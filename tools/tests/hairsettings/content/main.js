const { ArmorIDs, HairID } = Terraria.ID;
const signature = 'void GetHairSettings(out bool fullHair, out bool hatHair, out bool hideHair, out bool backHairDraw, out bool drawsBackHairWithoutHeadgear)';
let done = false, frames = 0, failures = 0;

export class HairSettingsHat extends ModItem {
    Texture = 'Hat';
    SetDefaults(item) { item.width = item.height = 20; }
    SetStaticDefaults() { ArmorIDs.Head.Sets.DrawHatHair[this.Item.headSlot] = true; }
}

export class HairSettingsHair extends ModHair {
    Texture = 'Hair';
    SetStaticDefaults() { HairID.Sets.DrawBackHair[this.Type] = true; }
}

function check(label, run) {
    try {
        if (!run()) throw new Error('resultado falso');
        bl.log('hairsettings ' + label + ': ok');
    } catch (error) {
        failures++;
        bl.log('hairsettings ' + label + ': FALHOU ' + error);
    }
}

function settings(player, values) {
    const old = { head: player.head, face: player.face, faceHead: player.faceHead, hair: player.hair };
    try {
        Object.assign(player, { head: 0, face: -1, faceHead: -1, hair: 0 }, values);
        const refs = Array.from({ length: 5 }, () => new Ref(true));
        player[signature](...refs);
        return refs.map((ref) => ref.value);
    } finally { Object.assign(player, old); }
}

function equal(actual, expected) { return JSON.stringify(actual) === JSON.stringify(expected); }

Terraria.Player['void Update(int i)'].hook((original, player, index) => {
    original(player, index);
    if (done || Terraria.Main.gameMenu || index !== Terraria.Main.myPlayer || ++frames < 60) return;
    done = true;
    const type = ModContent.ItemType(HairSettingsHat);
    const item = Terraria.ID.ContentSamples.ItemsByType.get_Item(type);
    const slot = item.headSlot;
    const hair = ModContent.GetInstance(HairSettingsHair).Type;
    check('SetStaticDefaults marca o slot do chapeu', () => slot >= EquipLoader.VanillaCount(EquipType.Head) && slot < ArmorIDs.Head.Count && ArmorIDs.Head.Sets.DrawHatHair[slot] === true);
    check('chapeu preserva o cabelo', () => equal(settings(player, { head: slot }), [false, true, false, false, false]));
    check('cabelo de mod desenha atras', () => equal(settings(player, { hair }), [false, false, false, true, true]));
    check('padroes vanilla de cabelo completo', () => equal(settings(player, { head: 10, hair: 51 }), [true, false, false, true, false]));
    check('padroes vanilla de chapeu', () => equal(settings(player, { head: 14 }), [false, true, false, false, false]));
    check('mascara oculta cabelo', () => settings(player, { head: slot, face: 3 })[2] === true);
    check('faceHead oculta com chapeu', () => settings(player, { head: slot, faceHead: 0 })[2] === true);
    check('faceHead sem chapeu nao oculta', () => settings(player, { faceHead: 0 })[2] === false);
    check('slots negativos', () => equal(settings(player, { head: -1, hair: -1 }), [false, false, false, false, false]));
    const head = ArmorIDs.Head.Sets;
    try {
        head.DrawHatHair[slot] = false;
        head.DrawFullHair[slot] = true;
        check('alteracao do Set vale na proxima chamada', () => equal(settings(player, { head: slot }), [true, false, false, false, false]));
    } finally {
        head.DrawHatHair[slot] = true;
        head.DrawFullHair[slot] = false;
    }
    bl.log('hairsettings FIM falhas=' + failures);
});

export default class HairSettingsTest extends Mod {}
