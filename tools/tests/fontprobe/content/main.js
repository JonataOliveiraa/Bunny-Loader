// Mede as fontes do jogo no celular: quantas páginas, o tamanho lógico e o da
// textura Unity de cada página, as medidas dos glifos e a escala com que o
// MouseText é desenhado (tools/mods/fonts.py usa estas medidas). Depois deixa
// o tooltip de uma espada de cobre e dois números de dano na tela por uns
// segundos, para comparar a nitidez numa captura. Com um pacote de fonte
// ligado, mede a fonte dele. Loga "fontprobe ...".
const Main = Terraria.Main;
const { FontAssets } = Terraria.GameContent;

function describe(label, asset) {
    try {
        const font = asset.Value;
        const textures = font.Textures, glyphs = font.Glyphs;
        const pages = [];
        for (let i = 0; i < textures.length; i++) {
            const t = textures[i];
            pages.push(`${t.Width}x${t.Height} unity=${t.UnityWidth}x${t.UnityHeight} texel=${t.TexelWidth}`);
        }
        bl.log(`fontprobe ${label}: glifos=${glyphs.length} paginas=${textures.length} lineSpacing=${font.LineSpacing} spacing=${font.Spacing}`);
        for (const i of new Set([0, pages.length - 1])) bl.log(`fontprobe ${label}: pagina ${i} ${pages[i]}`);
        for (const ch of ['A', 'H', 'M', 'E', 'x', 'g', '4']) {
            const g = font['Glyph GetCharacterMetrics(char character)'](ch.charCodeAt(0));
            const b = g.BoundsInTexture, c = g.Cropping;
            bl.log(`fontprobe ${label}: '${ch}' bounds=${b.X},${b.Y},${b.Width},${b.Height} crop=${c.X},${c.Y},${c.Width},${c.Height} ` +
                `L=${g.LeftSideBearing} W=${g.Width} R=${g.RightSideBearing} pagina=${g.TexureIndex}`);
        }
    } catch (e) {
        bl.log(`fontprobe ${label}: FALHOU ${e}`);
    }
}

const scales = new Map();
const DRAW = 'void DrawString(SpriteFont spriteFont, string text, Vector2 position, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)';
let watching = false;
Microsoft.Xna.Framework.Graphics.SpriteBatch[DRAW].hook((original, self, font, text, position, color, rotation, origin, scale, effects, depth) => {
    if (watching && font === FontAssets.MouseText.Value) {
        const key = String(scale);
        scales.set(key, (scales.get(key) || 0) + 1);
    }
    return original(self, font, text, position, color, rotation, origin, scale, effects, depth);
});

let sample = null, frames = 0, done = false;

Main['void DrawPendingMouseText(bool worldMouse)'].hook((original, worldMouse) => {
    if (sample && !done && !worldMouse) {
        Main.HoverItem = sample['Item Clone()']();
        Main.inventoryTooltipTime = 30;
        Main.instance['void MouseText(string cursorText, int rare, byte diff, int hackedMouseX, int hackedMouseY, int hackedScreenWidth, int hackedScreenHeight, int pushWidthX)'](
            sample.Name, sample.rare, 0, -1, -1, -1, -1, 0);
    }
    return original(worldMouse);
});

Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    frames++;
    if (frames === 60) {
        describe('MouseText', FontAssets.MouseText);
        describe('ItemStack', FontAssets.ItemStack);
        describe('DeathText', FontAssets.DeathText);
        describe('CombatText0', FontAssets.CombatText[0]);
        describe('CombatText1', FontAssets.CombatText[1]);
        bl.log(`fontprobe tela: screen=${Main.screenWidth}x${Main.screenHeight} UIScale=${Main.UIScale}`);
        const it = Terraria.Item.new();
        it['void .ctor()']();
        it['void SetDefaults(int Type, ItemVariant variant)'](3507, null);   // Espada Curta de Cobre
        sample = it;
        watching = true;
    }
    // Números de dano (CombatText[0] e [1]) acima do jogador, para a captura.
    if (frames === 172 || frames === 176) {
        const C = Terraria.CombatText;
        const box = Microsoft.Xna.Framework.Rectangle.new();
        box['void .ctor(int x, int y, int width, int height)']((self.position.X | 0) - 320, (self.position.Y | 0) - 20, 20, 20);
        C['int NewText(Rectangle location, Color color, int amount, bool dramatic, bool dot)'](box, C.DamagedHostile, 1234, false, false);
        box['void .ctor(int x, int y, int width, int height)']((self.position.X | 0) + 320, (self.position.Y | 0) - 20, 20, 20);
        const at = C['int NewText(Rectangle location, Color color, int amount, bool dramatic, bool dot)'](box, C.DamagedHostileCrit, 5678, true, false);
        const t = at < 100 ? Main.combatText[at] : null;
        bl.log(`fontprobe combate: indice ${at} ativo=${t && t.active} texto=${t && t.text} vida=${t && t.lifeTime}`);
    }
    if (frames === 180) {
        watching = false;
        bl.log('fontprobe escalas do MouseText: ' + [...scales].map(([k, v]) => `${k} x${v}`).join(', '));
        bl.log('fontprobe SHOT');
    }
    if (frames === 900) {
        done = true;
        bl.log('fontprobe FIM');
    }
});

export default class TestFontProbe extends Mod {}
