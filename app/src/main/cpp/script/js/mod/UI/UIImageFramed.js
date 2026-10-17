// Um pedaço (frame, Rectangle) de uma textura, do tamanho do pedaço.
class UIImageFramed extends UIElement {
    static #DRAW = 'void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color)';

    Color = Color.White;
    #texture = null;
    #frame = null;

    constructor(texture, frame) {
        super();
        this.#texture = texture;
        this.SetFrame(frame);
    }

    SetImage(texture, frame) {
        this.#texture = texture;
        this.SetFrame(frame);
    }

    SetFrame(frame) {
        this.#frame = frame;
        this.Width.Set(frame.Width, 0);
        this.Height.Set(frame.Height, 0);
    }

    DrawSelf(spriteBatch) {
        spriteBatch[UIImageFramed.#DRAW](UINative.Texture(this.#texture), this.GetDimensions().Position(), this.#frame, this.Color);
    }
}
