// A barra de rolagem do jogo (Images/UI/Scrollbar): a alça mostra que parte
// da lista aparece; arrastar a alça (ou tocar na barra) rola.
class UIScrollbar extends UIElement {
    static #DRAW = 'void Draw(Texture2D texture, Rectangle destinationRectangle, Nullable`1 sourceRectangle, Color color)';

    AutoHide = false;
    #viewPosition = 0;
    #viewSize = 1;
    #maxViewSize = 20;
    #isDragging = false;
    #dragYOffset = 0;
    #texture = null;
    #inner = null;

    // theme: 0 azul, 1 ciano (a barra das listas de mundos).
    constructor(theme = 0) {
        super();
        this.Width.Set(20, 0);
        this.MaxWidth.Set(20, 0);
        // As imagens da barra do PC não existem no celular: sem elas, retângulos.
        this.#texture = UIScrollbar.#Vanilla(theme === 1 ? 'Images/UI/Scrollbar2' : 'Images/UI/Scrollbar');
        this.#inner = UIScrollbar.#Vanilla('Images/UI/ScrollbarInner');
        this.PaddingTop = 5;
        this.PaddingBottom = 5;
    }

    get ViewPosition() { return this.#viewPosition; }
    set ViewPosition(value) { this.#viewPosition = UIScrollbar.#Clamp(value, 0, this.#maxViewSize - this.#viewSize); }
    get CanScroll() { return this.#maxViewSize !== this.#viewSize; }

    static #Vanilla(name) {
        try {
            return bl.vanillaTexture(name);
        } catch (e) {
            return null;
        }
    }

    static #Clamp(v, min, max) { return Math.min(Math.max(v, min), Math.max(min, max)); }

    GoToBottom() { this.ViewPosition = this.#maxViewSize - this.#viewSize; }

    SetView(viewSize, maxViewSize) {
        viewSize = UIScrollbar.#Clamp(viewSize, 0, maxViewSize);
        this.#viewPosition = UIScrollbar.#Clamp(this.#viewPosition, 0, maxViewSize - viewSize);
        this.#viewSize = viewSize;
        this.#maxViewSize = maxViewSize;
    }

    GetValue() { return this.#viewPosition; }

    #Handle() {
        const inner = this.GetInnerDimensions();
        if (this.#maxViewSize === 0 && this.#viewSize === 0) {
            this.#viewSize = 1;
            this.#maxViewSize = 1;
        }
        return {
            X: Math.trunc(inner.X),
            Y: Math.trunc(inner.Y + inner.Height * (this.#viewPosition / this.#maxViewSize)) - 3,
            Width: 20,
            Height: Math.trunc(inner.Height * (this.#viewSize / this.#maxViewSize)) + 7,
        };
    }

    static #Contains(r, x, y) {
        return x >= r.X && x < r.X + r.Width && y >= r.Y && y < r.Y + r.Height;
    }

    static #FILL = 'void Draw(Texture2D texture, Rectangle destinationRectangle, Color color)';

    // Sem a imagem: a faixa escura com borda, ou a alça clara.
    #DrawPlain(spriteBatch, r, handle, color) {
        const pixel = Terraria.GameContent.TextureAssets.MagicPixel.Value;
        const fill = (x, y, w, h, c) => spriteBatch[UIScrollbar.#FILL](pixel, Rectangle.new(x, y, w, h), c);
        if (handle) {
            fill(r.X + 4, r.Y, r.Width - 8, r.Height, Color.Multiply(Color.new(220, 225, 245), color.A / 255));
            return;
        }
        fill(r.X, r.Y - 4, r.Width, r.Height + 8, Color.Multiply(Color.Black, 0.6));
        fill(r.X + 2, r.Y - 2, r.Width - 4, r.Height + 4, Color.Multiply(Color.new(43, 56, 101), 0.9));
    }

    #DrawBar(spriteBatch, asset, r, color, handle = false) {
        const texture = UINative.Texture(asset);
        if (!texture) {
            this.#DrawPlain(spriteBatch, r, handle, color);
            return;
        }
        const w = texture.Width, h = texture.Height;
        const draw = (...args) => spriteBatch[UIScrollbar.#DRAW](...args);
        draw(texture, Rectangle.new(r.X, r.Y - 6, r.Width, 6), Rectangle.new(0, 0, w, 6), color);
        draw(texture, Rectangle.new(r.X, r.Y, r.Width, r.Height), Rectangle.new(0, 6, w, 4), color);
        draw(texture, Rectangle.new(r.X, r.Y + r.Height, r.Width, 6), Rectangle.new(0, h - 6, w, 6), color);
    }

    DrawSelf(spriteBatch) {
        if (this.AutoHide && !this.CanScroll) return;
        const inner = this.GetInnerDimensions();
        const mouse = UserInterface.ActiveInstance.MousePosition;
        if (this.#isDragging) {
            this.#viewPosition = UIScrollbar.#Clamp((mouse.Y - inner.Y - this.#dragYOffset) / inner.Height * this.#maxViewSize,
                0, this.#maxViewSize - this.#viewSize);
        }
        const handle = this.#Handle();
        const lit = this.#isDragging || UIScrollbar.#Contains(handle, mouse.X, mouse.Y);
        const d = this.GetDimensions();
        this.#DrawBar(spriteBatch, this.#texture, { X: Math.trunc(d.X), Y: Math.trunc(d.Y), Width: Math.trunc(d.Width), Height: Math.trunc(d.Height) }, Color.White);
        this.#DrawBar(spriteBatch, this.#inner, handle, Color.Multiply(Color.White, lit ? 1 : 0.85), true);
    }

    LeftMouseDown(evt) {
        super.LeftMouseDown(evt);
        if (evt.Target !== this) return;
        const handle = this.#Handle();
        const m = evt.MousePosition;
        if (UIScrollbar.#Contains(handle, m.X, m.Y)) {
            this.#isDragging = true;
            this.#dragYOffset = m.Y - handle.Y;
        } else {
            const inner = this.GetInnerDimensions();
            this.#viewPosition = UIScrollbar.#Clamp((m.Y - inner.Y - (handle.Height >> 1)) / inner.Height * this.#maxViewSize,
                0, this.#maxViewSize - this.#viewSize);
        }
    }

    LeftMouseUp(evt) {
        super.LeftMouseUp(evt);
        this.#isDragging = false;
    }
}
