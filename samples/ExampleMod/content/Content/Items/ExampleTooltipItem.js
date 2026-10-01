// O tooltip mexido: uma linha nova, a cor de outra (ModifyTooltips), e a linha
// nova desenhada letra a letra, em onda e arco-íris (PreDrawTooltipLine).
// Exemplo do GST378.
export class ExampleTooltipItem extends ModItem {
    constructor() {
        super();
        this.Texture = 'Content/Items/ExampleItem';
    }

    SetDefaults() {
        this.Item.width = 20;
        this.Item.height = 20;
        this.Item.maxStack = ModItem.CommonMaxStack;
        this.Item.rare = ItemRarityID.Blue;
        this.Item.value = Terraria.Item.buyPrice(0, 0, 1, 0);
    }

    ModifyTooltips(item, tooltips) {
        // A linha "Bunny Loader" logo depois do nome.
        tooltips.splice(1, 0, new TooltipLine(this.Mod, 'BunnyLoader', 'Bunny Loader'));

        // A primeira linha da descrição, em outra cor.
        const description = tooltips.find((line) => line.Name === 'Tooltip0');
        if (description) description.OverrideColor = Color.new(255, 215, 90);
    }

    // Só a linha "Bunny Loader": cada letra sobe e desce e tem a sua cor.
    PreDrawTooltipLine(item, line, yOffset) {
        if (line.Name !== 'BunnyLoader') return true;

        const sb = Terraria.Main.spriteBatch;
        const t = Terraria.Main.GlobalTimeWrappedHourly;
        const time = t * 8;
        const amplitude = 3;
        const letterStep = 1.15;     // atraso da onda de uma letra para a outra
        const hueStep = 0.04;        // quanto a cor muda de uma letra para a outra
        const hueSpeed = 0.20;       // a velocidade do arco-íris
        const scale = line.BaseScale.X;

        const text = line.Text;
        const font = line.Font;
        const alpha = line.Color.A;  // o tooltip aparece aos poucos
        const shadow = Color.Multiply(Color.Black, alpha / 255);
        const spread = line.Spread;

        // HSV (s = 1, v = 1) -> RGB
        const rainbow = (h) => {
            h = ((h % 1) + 1) % 1;
            const k = h * 6, i = Math.floor(k), f = k - i;
            const q = (1 - f) * 255 | 0, u = f * 255 | 0;
            switch (i) {
                case 0: return Color.new(255, u, 0, alpha);
                case 1: return Color.new(q, 255, 0, alpha);
                case 2: return Color.new(0, 255, u, alpha);
                case 3: return Color.new(0, q, 255, alpha);
                case 4: return Color.new(u, 0, 255, alpha);
                default: return Color.new(255, 0, q, alpha);
            }
        };

        for (let i = 0; i < text.length; i++) {
            const ch = text[i];
            if (ch === ' ') continue;

            const offX = i === 0 ? 0 : font['Vector2 MeasureString(string text)'](text.substring(0, i)).X * scale;
            const x = line.X + offX;
            const y = line.Y + Math.sin(time + i * letterStep) * amplitude;
            const color = rainbow(t * hueSpeed + i * hueStep);

            // Quatro sombras e a letra, como o jogo desenha o texto.
            for (let pass = 0; pass < 5; pass++) {
                let px = x, py = y;
                if (pass === 0) px -= spread;
                else if (pass === 1) px += spread;
                else if (pass === 2) py -= spread;
                else if (pass === 3) py += spread;

                sb['void DrawString(SpriteFont spriteFont, string text, Vector2 position, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)'](
                    font, ch, Vector2.new(px, py), pass < 4 ? shadow : color, line.Rotation, line.Origin, scale, 0, 0);
            }
        }

        // Já desenhada aqui: o jogo não desenha de novo.
        return false;
    }
}
