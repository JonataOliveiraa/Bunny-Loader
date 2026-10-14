const { NPCID } = Terraria.ID;
const Main = Terraria.Main;
const DRAW_STRING = 'void DrawString(SpriteFont spriteFont, string text, Vector2 position, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)';

// O que dá para fazer no desenho das barras de chefe, inclusive as do jogo
// (o ExampleGlobalBossBar do tModLoader). Aqui, a do Olho de Cthulhu.
export class ExampleGlobalBossBar extends GlobalBossBar {
    PreDraw(spriteBatch, npc, drawParams) {
        if (npc.type === NPCID.EyeofCthulhu) {
            drawParams.IconColor = Color.new(Main.DiscoR, Main.DiscoG, Main.DiscoB);
            drawParams.BarColor = Color.Lerp(Color.White, Color.Transparent, Math.sin(Main.GameUpdateCount / 50));
        }

        return true;
    }

    PostDraw(spriteBatch, npc, drawParams) {
        if (npc.type !== NPCID.EyeofCthulhu) return;

        const text = 'GlobalBossBar Showcase';
        const font = Terraria.GameContent.FontAssets.MouseText.Value;
        const size = font['Vector2 MeasureString(string text)'](text);
        // Centrado na barra e abaixo dela: no celular a barra do jogo fica logo
        // embaixo do minimapa (no tModLoader o texto vai acima).
        const position = Vector2.new(drawParams.BarCenter.X - size.X / 2, drawParams.BarCenter.Y - size.Y / 2 + 30);
        spriteBatch[DRAW_STRING](font, text, position, Color.White, 0, Vector2.Zero, 1, 0, 0);
    }
}
