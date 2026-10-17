// Uma lista vertical que recorta o que passa da borda. No celular rola com o
// arrasto do dedo (e pela barra, com SetScrollbar).
class UIList extends UIElement {
    ListPadding = 5;
    ManualSortMethod = null;
    _items = [];
    _scrollbar = null;
    #inner = null;
    #innerHeight = 0;
    #dragStartY = null;
    #dragStartView = 0;
    #dragging = false;
    static #DRAG_THRESHOLD = 6;

    constructor() {
        super();
        this.#inner = new UIListInner();
        this.#inner.Width.Set(0, 1);
        this.#inner.Height.Set(0, 1);
        this.OverflowHidden = true;
        this.Append(this.#inner);
    }

    get Count() { return this._items.length; }

    [Symbol.iterator]() { return this._items[Symbol.iterator](); }

    GetTotalHeight() { return this.#innerHeight; }

    get ViewPosition() { return this._scrollbar ? this._scrollbar.ViewPosition : this.#ownView; }
    set ViewPosition(value) {
        if (this._scrollbar) this._scrollbar.ViewPosition = value;
        else this.#ownView = Math.min(Math.max(value, 0), Math.max(0, this.#innerHeight - this.GetInnerDimensions().Height));
    }
    #ownView = 0;

    Goto(searchMethod) {
        const item = this._items.find(searchMethod);
        if (item) this.ViewPosition = item.Top.Pixels;
    }

    Add(item) {
        this._items.push(item);
        this.#inner.Append(item);
        this.UpdateOrder();
        this.#inner.Recalculate();
    }

    AddRange(items) {
        for (const item of items) {
            this._items.push(item);
            this.#inner.Append(item);
        }
        this.UpdateOrder();
        this.#inner.Recalculate();
    }

    // Sem argumento é o Remove() do UIElement (tirar a lista do pai): no C#
    // são duas sobrecargas.
    Remove(item) {
        if (item === undefined) return super.Remove();
        this.#inner.RemoveChild(item);
        const i = this._items.indexOf(item);
        if (i >= 0) this._items.splice(i, 1);
        this.UpdateOrder();
        return i >= 0;
    }

    Clear() {
        this.#inner.RemoveAllChildren();
        this._items.length = 0;
    }

    Recalculate() {
        super.Recalculate();
        this.#UpdateScrollbar();
    }

    ScrollWheel(evt) {
        super.ScrollWheel(evt);
        this.ViewPosition -= evt.ScrollWheelValue;
    }

    RecalculateChildren() {
        super.RecalculateChildren();
        let pixels = 0;
        for (const item of this._items) {
            const pad = this._items.length === 1 ? 0 : this.ListPadding;
            item.Top.Set(pixels, 0);
            item.Recalculate();
            pixels += item.GetOuterDimensions().Height + pad;
        }
        this.#innerHeight = pixels;
    }

    #UpdateScrollbar() {
        if (this._scrollbar) this._scrollbar.SetView(this.GetInnerDimensions().Height, this.#innerHeight);
    }

    SetScrollbar(scrollbar) {
        this._scrollbar = scrollbar;
        this.#UpdateScrollbar();
    }

    UpdateOrder() {
        if (this.ManualSortMethod) this.ManualSortMethod(this._items);
        else this._items.sort((a, b) => a.CompareTo(b));
        this.#UpdateScrollbar();
    }

    // Arrasto: o toque que começou na lista e andou mais que o limiar rola.
    Update(gameTime) {
        super.Update(gameTime);
        const Main = Terraria.Main;
        const y = Main.mouseY;
        if (!Main.mouseLeft) {
            this.#dragStartY = null;
            this.#dragging = false;
            return;
        }
        if (this.#dragStartY === null) {
            if (!this.ContainsPoint({ X: Main.mouseX, Y: y })) return;
            this.#dragStartY = y;
            this.#dragStartView = this.ViewPosition;
            return;
        }
        if (!this.#dragging && Math.abs(y - this.#dragStartY) > UIList.#DRAG_THRESHOLD) this.#dragging = true;
        if (this.#dragging) this.ViewPosition = this.#dragStartView - (y - this.#dragStartY);
    }

    // Arrastando, o toque não é clique nos itens.
    get IsDragging() { return this.#dragging; }

    DrawSelf(spriteBatch) {
        this.#inner.Top.Set(-this.ViewPosition, 0);
        this.Recalculate();
    }
}
