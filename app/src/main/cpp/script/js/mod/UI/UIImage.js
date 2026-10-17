// Uma imagem (Asset<Texture2D> ou Texture2D). Com AllowResizingDimensions o
// elemento fica do tamanho dela; ScaleToFit estica para o elemento.
class UIImage extends UIElement {
    static #DRAW = 'void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)';
    static #DRAW_RECT = 'void Draw(Texture2D texture, Rectangle destinationRectangle, Color color)';

    ImageScale = 1;
    Rotation = 0;
    ScaleToFit = false;
    AllowResizingDimensions = true;
    Color = Color.White;
    NormalizedOrigin = { X: 0, Y: 0 };
    RemoveFloatingPointsFromDrawPosition = false;
    #texture = null;

    constructor(texture) {
        super();
        this.SetImage(texture);
    }

    SetImage(texture) {
        this.#texture = texture;
        if (this.AllowResizingDimensions && texture) {
            this.Width.Set(UINative.Width(texture), 0);
            this.Height.Set(UINative.Height(texture), 0);
        }
    }

    DrawSelf(spriteBatch) {
        const texture = UINative.Texture(this.#texture);
        if (!texture) return;
        const d = this.GetDimensions();
        if (this.ScaleToFit) {
            spriteBatch[UIImage.#DRAW_RECT](texture, d.ToRectangle(), this.Color);
            return;
        }
        const w = texture.Width, h = texture.Height;
        const ox = w * this.NormalizedOrigin.X, oy = h * this.NormalizedOrigin.Y;
        let x = d.X + w * (1 - this.ImageScale) / 2 + ox;
        let y = d.Y + h * (1 - this.ImageScale) / 2 + oy;
        if (this.RemoveFloatingPointsFromDrawPosition) {
            x = Math.floor(x);
            y = Math.floor(y);
        }
        spriteBatch[UIImage.#DRAW](texture, Vector2.new(x, y), null, this.Color, this.Rotation, Vector2.new(ox, oy), this.ImageScale, 0, 0);
    }
}
