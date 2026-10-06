// A etiqueta [i:tipo] na tela. No celular quem desenha a etiqueta é a fonte:
// o SpriteBatch.DrawString chama o ItemTagHandler.PrintInline a cada '['.
// Casos (a conferência é pela captura de tela; o log diz o que está na tela):
//   - texto direto (DrawString) em várias cores, item do jogo e item de mod
//     (o item aparece em toda cor menos o preto, que é a passada da sombra);
//   - tooltip do jogo (item sem gancho de tooltip) e do Bunny Loader
//     (item com ModifyTooltips), item do jogo e de mod;
//   - cabeçalho da Config. dos Mods: a etiqueta no nome da opção e na tradução.
const Main = Terraria.Main;

export class PlainTagItem extends ModItem {
    constructor() {
        super();
        this.DisplayName = 'Tooltip do jogo';
        this.Tooltip = 'Do jogo: [i:101] fim';
    }
    SetDefaults(item) { item.width = item.height = 20; }
}

export class HookTagItem extends ModItem {
    constructor() {
        super();
        this.DisplayName = 'Tooltip do Bunny Loader';
        this.Tooltip = 'Linha normal';
    }
    SetDefaults(item) { item.width = item.height = 20; }
    ModifyTooltips(item, tooltips) {
        tooltips.push(new TooltipLine(this.Mod, 'Vanilla', 'Do jogo: [i:101] fim'));
        tooltips.push(new TooltipLine(this.Mod, 'Modded', `De mod: [i:${this.Type}] fim`));
        const yellow = new TooltipLine(this.Mod, 'Yellow', 'Amarela: [i:101] fim');
        yellow.OverrideColor = Color.new(255, 255, 0);
        tooltips.push(yellow);
    }
}

export class ItemTagConfig extends ModConfig {
    static Options = {
        ['[i:101] Do nome']: ModConfig.Header(),
        Translated: ModConfig.Header(),
        Plain: ModConfig.Toggle(true, { label: 'Opção' }),
    };
}

const newItemAt = Terraria.Item['int NewItem(IEntitySource source, Vector2 center, int type, int stack, int prefix, NewItemOwnership ownership, Nullable<Vector2> velocity, Item.NewItemModifier modifier, bool noBroadcast)'];

const DRAW_STRING = 'void DrawString(SpriteFont spriteFont, string text, Vector2 position, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)';
let frames = 0;

Main['void DrawPendingMouseText(bool worldMouse)'].hook((original, worldMouse) => {
    if (!worldMouse && frames > 60 && !Main.playerInventory) {
        const mod = ModContent.ItemType(HookTagItem);
        const font = Terraria.GameContent.FontAssets.MouseText.Value;
        const lines = [
            ['branco, do jogo: [i:101] fim', Color.new(255, 255, 255, 255)],
            [`branco, de mod (${mod}): [i:${mod}] fim`, Color.new(255, 255, 255, 255)],
            ['amarelo (B = 0): [i:101] fim', Color.new(255, 255, 0, 255)],
            ['vermelho (G = 0): [i:101] fim', Color.new(255, 0, 0, 255)],
            ['verde (R = 0): [i:101] fim', Color.new(0, 255, 0, 255)],
            [`amarelo, de mod: [i:${mod}] fim`, Color.new(255, 255, 0, 255)],
            ['preto (sem item, como a sombra): [i:101] fim', Color.new(0, 0, 0, 255)],
        ];
        lines.forEach(([text, color], k) => {
            Main.spriteBatch[DRAW_STRING](font, text, Vector2.new(40, 160 + k * 34), color, 0, Vector2.new(0, 0), 1, 0, 0);
        });
    }
    return original();
});

Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu) return;
    self.statLife = self.statLifeMax2;
    frames++;
    // Os dois itens soltos em cima do jogador: ele pega do jeito normal, e o
    // inventário mostra (casa mudada por fora não aparece na grade).
    // As sobras de uma rodada anterior saem antes, e os dois itens saem do
    // inventário de novo no quadro 3600 (~1 min): o personagem de teste não
    // fica com item de mod de teste.
    if (frames === 100 || frames === 3600) {
        const ours = [ModContent.ItemType(PlainTagItem), ModContent.ItemType(HookTagItem)];
        const inv = self.inventory;
        let removed = 0;
        for (let k = 0; k < 58; k++) if (ours.includes(inv[k].type)) { inv[k].TurnToAir(); removed++; }
        bl.log('itemtag: ' + removed + ' casa(s) com item de teste esvaziada(s)');
    }
    if (frames === 120) {
        for (const type of [ModContent.ItemType(PlainTagItem), ModContent.ItemType(HookTagItem)]) {
            newItemAt(null, self.Center, type, 1, 0, 0, null, null, false);
        }
        bl.log('itemtag: itens soltos no jogador; item de mod ' + ModContent.ItemType(HookTagItem));
        bl.log('itemtag FIM: conferir pela tela');
    }
});
bl.log('itemtag: carregado');

export default class TestItemTag extends Mod {}
