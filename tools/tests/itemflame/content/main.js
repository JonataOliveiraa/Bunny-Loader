// A chama da tocha de mod na mão (TextureAssets.ItemFlame). A tabela crescia
// copiando a do tipo 0, que o jogo nunca carrega: um quadrado branco na mão.
// FlameTorch tem o FlameTorch_Flame.png (14x16); BareTorch não tem (fica
// transparente, 1x1). No fim, a ExampleTorch fica na mão para o print.
// Loga "itemflame <caso>: ok | FALHOU".
const Main = Terraria.Main;
const TextureAssets = Terraria.GameContent.TextureAssets;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('itemflame ' + label + ': ok');
        else { fails++; bl.log('itemflame ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('itemflame ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

export class FlameTorch extends ModItem {
    SetDefaults(item) { this.Item['void DefaultToTorch(int tileStyleToPlace, bool allowWaterPlacement)'](0, false); }
}

export class BareTorch extends ModItem {
    SetDefaults(item) { this.Item['void DefaultToTorch(int tileStyleToPlace, bool allowWaterPlacement)'](0, false); }
}

function flameSize(type) {
    const tex = TextureAssets.ItemFlame[type].Value;
    return tex.Width + 'x' + tex.Height;
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu) return;
    // Vivo e de dia até o print, com a tocha na mão.
    self.statLife = self.statLifeMax2;
    if (done) return;
    if (++frames < 60) return;
    done = true;
    Main.dayTime = true;
    Main.time = 20000;

    const flame = ModContent.ItemType(FlameTorch), bare = ModContent.ItemType(BareTorch);
    let example = -1;
    for (let t = bl.items.vanillaCount; bl.items.isModItem(t); t++) {
        const m = ModItem.getModItem(t);
        if (m && m.constructor.name === 'ExampleTorch') example = t;
    }
    check('com _Flame: a chama é o PNG (14x16)', () => flameSize(flame) === '14x16' || flameSize(flame));
    check('sem _Flame: transparente (1x1), não o quadrado branco', () => flameSize(bare) === '1x1' || flameSize(bare));
    check('ExampleTorch: a chama do Example Mod (14x16)', () => (example > 0 && flameSize(example) === '14x16') || (example > 0 ? flameSize(example) : 'sem ExampleTorch'));
    // A do jogo carrega só quando alguém a segura: confere pelo nome do asset.
    check('a tocha do jogo continua com a dela', () => {
        const name = TextureAssets.ItemFlame[Terraria.ID.ItemID.Torch].Name;
        return /ItemFlame_8$/.test(name) || name;
    });

    if (example > 0) {
        self.inventory[0]['void SetDefaults(int Type, ItemVariant variant)'](example, null);
        self.selectedItemState.selected = 0;
    }
    bl.log('itemflame FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
});
bl.log('itemflame: carregado');

export default class TestItemFlame extends Mod {}
