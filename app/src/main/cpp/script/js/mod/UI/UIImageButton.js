// Um botão de imagem: apagado (0,4) e aceso com o dedo em cima (1), com a
// borda opcional (SetHoverImage) e o "tique" do jogo ao tocar.
class UIImageButton extends UIElement {
    static #DRAW = 'void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color)';

    Color = Color.White;
    BorderColor = Color.White;
    _visibilityActive = 1;
    _visibilityInactive = 0.4;
    #texture = null;
    #frame = null;
    #border = null;
    #borderFrame = null;

    // `frame`: Rectangle do pedaço da textura (opcional).
    constructor(texture, frame = null) {
        super();
        this.#texture = texture;
        this.#frame = frame;
        this.#Resize();
    }

    #Resize() {
        const f = this.#frame;
        this.Width.Set(f ? f.Width : UINative.Width(this.#texture), 0);
        this.Height.Set(f ? f.Height : UINative.Height(this.#texture), 0);
    }

    SetHoverImage(texture, frame = null) {
        this.#border = texture;
        this.#borderFrame = frame;
    }

    SetImage(texture, frame = this.#frame) {
        this.#texture = texture;
        this.#frame = frame;
        this.#Resize();
    }

    SetVisibility(whenActive, whenInactive) {
        const clamp = (v) => Math.min(Math.max(v, 0), 1);
        this._visibilityActive = clamp(whenActive);
        this._visibilityInactive = clamp(whenInactive);
    }

    DrawSelf(spriteBatch) {
        const at = this.GetDimensions().Position();
        const alpha = this.IsMouseHovering ? this._visibilityActive : this._visibilityInactive;
        spriteBatch[UIImageButton.#DRAW](UINative.Texture(this.#texture), at, this.#frame, Color.Multiply(this.Color, alpha));
        if (this.#border && this.IsMouseHovering) {
            spriteBatch[UIImageButton.#DRAW](UINative.Texture(this.#border), at, this.#borderFrame, this.BorderColor);
        }
    }

    MouseOver(evt) {
        super.MouseOver(evt);
        Terraria.Audio.SoundEngine['SoundEffectInstance PlaySound(int type, int x, int y, int Style, float volumeScale, float pitchOffset)'](12, -1, -1, 1, 1, 0);
    }
}
