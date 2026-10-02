// Erro dentro de método de mod tem de chegar ao painel de erro do jogo (nível
// de erro no log), não só ao log. Um erro no ModifyTooltips (pelo tooltip do
// item na tela) e, depois, outro num ModPlayer.PostUpdate: o painel abre no
// primeiro e é atualizado no segundo. Loga "moderrors ...".
const Main = Terraria.Main;
let frames = 0, sample = null, done = false;

function newItem(type) {
    const it = Terraria.Item.new();
    it['void .ctor()']();
    it['void SetDefaults(int Type, ItemVariant variant)'](type, null);
    return it;
}

export class BrokenPlayer extends ModPlayer {
    PostUpdate(player) {
        if (player.whoAmI === Main.myPlayer && frames === 300) {
            bl.log('moderrors: erro no PostUpdate agora');
            outroNomeQueNaoExiste.x = 1;
        }
    }
}

Terraria.Main['void DrawPendingMouseText(bool worldMouse)'].hook((original, worldMouse) => {
    if (sample && !worldMouse && frames < 200) {
        Main.HoverItem = sample['Item Clone()']();
        Main.inventoryTooltipTime = 30;
        Main.instance['void MouseText(string cursorText, int rare, byte diff, int hackedMouseX, int hackedMouseY, int hackedScreenWidth, int hackedScreenHeight, int pushWidthX)'](
            sample.Name, sample.rare, 0, -1, -1, -1, -1, 0);
    }
    return original();
});

Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    frames++;
    if (frames === 120) {
        sample = newItem(ModContent.ItemType('BrokenTooltipItem'));
        bl.log('moderrors: item com o ModifyTooltips quebrado no HoverItem');
    }
    // Um hook JS direto que lança: o erro sai pelo despacho do hook, em C++.
    if (frames === 360) {
        const v = Terraria.Main.player[Main.myPlayer];
        terceiroErro(v);
    }
    if (frames === 420) {
        done = true;
        bl.log('moderrors FIM: confira as linhas E (erro) acima e o painel na tela');
    }
});

export default class TestModErrors extends Mod {}
