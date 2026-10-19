const Main = Terraria.Main;
const { TextureAssets, FontAssets } = Terraria.GameContent;
const CoinsSplit = Terraria.Utils['int[] CoinsSplit(long count)'];
const DrawBorderStringFourWay = Terraria.Utils['void DrawBorderStringFourWay(SpriteBatch sb, SpriteFont font, string text, float x, float y, Color textColor, Color borderColor, Vector2 origin, float scale)'];
const Draw = Microsoft.Xna.Framework.Graphics.SpriteBatch['void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)'];

// As moedas pegas desde que a tela abriu, e as moedas por minuto (CPM).
export class UIMoneyDisplay extends UIElement {
    collectedCoins = 0;
    startTime = null;
    coinsTextures = [];
    exampleFont = null;

    constructor() {
        super();
        // A fonte própria do mod (o ModContent.Request<DynamicSpriteFont> do
        // tModLoader): Assets/Fonts/ExampleFont.json, gerada com
        // tools/mods/fonts.py a partir da New Rocker.
        this.exampleFont = ModContent.Request('Assets/Fonts/ExampleFont').Value;
        for (let j = 0; j < 4; j++) {
            Main.instance.LoadItem(74 - j);   // platina, ouro, prata e cobre
            this.coinsTextures[j] = TextureAssets.Item[74 - j].Value;
        }
        this.IgnoresMouseInteraction = true;   // o toque passa para o painel (arrastar)
    }

    AddCoinsPerMinute(coins) {
        this.collectedCoins += coins;
        if (this.startTime === null) this.startTime = Date.now();
    }

    GetCoinsPerMinute() {
        if (this.collectedCoins === 0) return 0;
        const minutes = Math.max(1, (Date.now() - this.startTime) / 60000);
        return Math.trunc(this.collectedCoins / minutes);
    }

    DrawSelf(spriteBatch) {
        const inner = this.GetInnerDimensions();
        const x = inner.X, y = inner.Y;
        this.DrawCoins(spriteBatch, x, y, CoinsSplit(this.collectedCoins));
        this.DrawCoins(spriteBatch, x, y, CoinsSplit(this.GetCoinsPerMinute()), 0, 25);
        DrawBorderStringFourWay(spriteBatch, this.exampleFont, 'CPM', x + 24 * 4, y + 22, Color.Gold, Color.Black, Vector2.new(0.3, 0.3), 0.9);
    }

    DrawCoins(spriteBatch, x, y, coins, xOffset = 0, yOffset = 0) {
        for (let j = 0; j < 4; j++) {
            const texture = this.coinsTextures[j];
            const origin = Vector2.new(texture.Width / 2, texture.Height / 2);
            Draw(spriteBatch, texture, Vector2.new(x + 11 + 24 * j + xOffset, y + yOffset), null, Color.White, 0, origin, 1, 0, 0);
            DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, String(coins[3 - j]), x + 24 * j + xOffset, y + yOffset,
                Color.White, Color.Black, Vector2.new(0.3, 0.3), 0.75);
        }
    }

    ResetCoins() {
        this.collectedCoins = 0;
        this.startTime = Date.now();
    }
}
