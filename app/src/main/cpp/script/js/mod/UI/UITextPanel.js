// Um painel com um texto dentro, do tamanho do texto (o UITextPanel do jogo).
// TextHAlign: 0 esquerda, 0,5 centro, 1 direita. DrawPanel = false desenha só o texto.
class UITextPanel extends UIPanel {
    TextHAlign = 0.5;
    DrawPanel = true;
    #text = null;

    constructor(text = '', textScale = 1, large = false) {
        super();
        this.#text = new UIText(text, textScale, large);
        this.#Fit();
    }

    get Text() { return this.#text.Text; }
    get TextColor() { return this.#text.TextColor; }
    set TextColor(value) { this.#text.TextColor = value; }

    SetText(text, textScale, large) {
        this.#text.SetText(text, textScale, large);
        this.#Fit();
    }

    #Fit() {
        this.MinWidth.Set(this.#text.MinWidth.Pixels + this.PaddingLeft + this.PaddingRight, 0);
        this.MinHeight.Set(this.#text.MinHeight.Pixels + this.PaddingTop + this.PaddingBottom, 0);
    }

    Recalculate() {
        this.#Fit();
        super.Recalculate();
        // O texto ocupa a área de dentro, alinhado por TextHAlign.
        UIElement.Place(this.#text, this.GetInnerDimensions());
        this.#text.TextOriginX = this.TextHAlign;
        this.#text.TextOriginY = 0;
    }

    DrawSelf(spriteBatch) {
        if (this.DrawPanel) super.DrawSelf(spriteBatch);
        this.#text.DrawSelf(spriteBatch);
    }
}
