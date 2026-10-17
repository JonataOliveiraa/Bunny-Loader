const { DustID } = Terraria.ID;
const Main = Terraria.Main;
const GetColor = Terraria.Lighting['Color GetColor(int x, int y)'];
const GetAlpha = Terraria.Dust['Color GetAlpha(Color newColor)'];
const Draw = Microsoft.Xna.Framework.Graphics.SpriteBatch['void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)'];

// Usa a folha de poeiras do jogo (Texture = null) com a moldura da poeira
// elétrica, e desenha um rastro atrás dela no PreDraw.
export class ExampleCustomDrawDust extends ModDust {
    get Texture() { return null; }

    OnSpawn(dust) {
        // O mesmo que calcular a moldura à mão:
        //   x = tipo * 10 % 1000, y = tipo * 10 / 1000 * 30 + Main.rand.Next(3) * 10
        dust.HackFrame(DustID.Electric);
    }

    // Quase sem a luz do lugar, e meio transparente.
    GetAlpha(dust, lightColor) {
        const c = Color.Lerp(lightColor, Color.White, 0.8);
        return Color.new(c.R, c.G, c.B, 25);
    }

    PreDraw(dust) {
        if (dust.fadeIn !== 0) return true;

        const trail = Math.min((Math.abs(dust.velocity.X) + Math.abs(dust.velocity.Y)) * 3, 10);
        let color = GetColor(Math.floor((dust.position.X + 4) / 16), Math.floor((dust.position.Y + 4) / 16));
        color = GetAlpha(dust, color);
        const texture = this.Texture2D.Value;
        const origin = Vector2.new(4, 4);
        for (let i = 0; i < trail; i++) {
            const at = Vector2.new(dust.position.X - dust.velocity.X * i - Main.screenPosition.X,
                dust.position.Y - dust.velocity.Y * i - Main.screenPosition.Y);
            Draw(Main.spriteBatch, texture, at, dust.frame, color, dust.rotation, origin, dust.scale * (1 - i / 10), 0, 0);
        }
        return true;   // e o desenho normal por cima
    }
}
