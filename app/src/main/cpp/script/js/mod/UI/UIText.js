// Texto com a fonte e a sombra do jogo (o UIText de lá desenha). O tamanho
// mínimo segue o texto: o elemento cresce com ele, como no jogo.
class UIText extends UIElement {
    static #CTOR = 'void .ctor(string text, float textScale, bool large)';
    static #SET = 'void SetText(string text, float textScale, bool large)';

    #native = null;
    #text = '';
    #textScale = 1;
    #large = false;

    constructor(text = '', textScale = 1, large = false) {
        super();
        this.#native = Terraria.GameContent.UI.Elements.UIText.new();
        this.#native[UIText.#CTOR]('', textScale, large);
        this.SetText(text, textScale, large);
    }

    // Texto, número, ou o que tiver Value (LocalizedText).
    static #AsString(text) {
        if (text === null || text === undefined) return '';
        if (typeof text === 'object' && 'Value' in text) return String(text.Value);
        return String(text);
    }

    get Text() { return this.#text; }

    SetText(text, textScale = this.#textScale, large = this.#large) {
        this.#text = UIText.#AsString(text);
        this.#textScale = textScale;
        this.#large = large;
        this.#Measure();
    }

    // O UIText do jogo mede (e quebra as linhas, com IsWrapped) pela largura de dentro.
    #Measure() {
        const native = this.#native;
        UINative.Place(native, this);
        try {
            native[UIText.#SET](this.#text, this.#textScale, this.#large);
        } catch (e) {
            // Cedo demais (no Load do mod as fontes ainda não existem): mede no Recalculate.
            return;
        }
        this.MinWidth.Set(native.MinWidth.Pixels + this.PaddingLeft + this.PaddingRight, 0);
        this.MinHeight.Set(native.MinHeight.Pixels + this.PaddingTop + this.PaddingBottom, 0);
    }

    Recalculate() {
        this.#Measure();
        super.Recalculate();
    }

    get TextColor() { return this.#native.TextColor; }
    set TextColor(value) { this.#native.TextColor = value; }
    get ShadowColor() { return this.#native.ShadowColor; }
    set ShadowColor(value) { this.#native.ShadowColor = value; }
    get TextOriginX() { return this.#native.TextOriginX; }
    set TextOriginX(value) { this.#native.TextOriginX = value; }
    get TextOriginY() { return this.#native.TextOriginY; }
    set TextOriginY(value) { this.#native.TextOriginY = value; }
    get IsWrapped() { return this.#native.IsWrapped; }
    set IsWrapped(value) { this.#native.IsWrapped = value; }
    get WrappedTextBottomPadding() { return this.#native.WrappedTextBottomPadding; }
    set WrappedTextBottomPadding(value) { this.#native.WrappedTextBottomPadding = value; }
    get DynamicallyScaleDownToWidth() { return this.#native.DynamicallyScaleDownToWidth; }
    set DynamicallyScaleDownToWidth(value) { this.#native.DynamicallyScaleDownToWidth = value; }

    DrawSelf(spriteBatch) {
        UINative.Place(this.#native, this);
        UINative.DrawSelf(this.#native, spriteBatch);
    }
}
