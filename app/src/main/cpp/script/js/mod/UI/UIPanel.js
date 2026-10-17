// O painel azul do jogo (cantos de 12 px). BackgroundColor e BorderColor
// mudam a cor; o construtor com texturas troca o fundo e a borda.
class UIPanel extends UIElement {
    static #CUSTOM = 'void .ctor(Asset`1 customBackground, Asset`1 customborder, int customCornerSize, int customBarSize)';

    BackgroundColor = Color.Multiply(Color.new(63, 82, 151), 0.7);
    BorderColor = Color.Black;
    #native = null;
    #custom = null;

    constructor(customBackground = null, customBorder = null, customCornerSize = 12, customBarSize = 4) {
        super();
        this.SetPadding(customCornerSize);
        if (customBackground || customBorder) this.#custom = [customBackground, customBorder, customCornerSize, customBarSize];
    }

    #Renderer() {
        if (this.#native) return this.#native;
        const native = Terraria.GameContent.UI.Elements.UIPanel.new();
        if (this.#custom) native[UIPanel.#CUSTOM](...this.#custom);
        else native['void .ctor()']();
        this.#native = native;
        return native;
    }

    DrawSelf(spriteBatch) {
        const native = this.#Renderer();
        UINative.Place(native, this);
        native.BackgroundColor = this.BackgroundColor;
        native.BorderColor = this.BorderColor;
        UINative.DrawSelf(native, spriteBatch);
    }
}
