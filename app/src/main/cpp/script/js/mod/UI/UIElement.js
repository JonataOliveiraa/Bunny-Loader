// O UIElement do jogo (Terraria.UI) em JS, para os mods estenderem: o layout
// (Recalculate), os eventos de toque, Update e Draw seguem o código do PC.
// Coordenadas em unidades de UI: as do Main.mouseX/Y e do desenho das camadas
// de interface (a tela do celular dividida por ~1,57).
class UIElement {
    static #idCounter = 0;
    // Recortes abertos (OverflowHidden): o do pai volta quando o filho fecha o dele.
    static #clips = [];
    static #CLIP = 'void EnableClipping(Rectangle inner, Rectangle outer, SpriteBatch batcher, bool vertical)';
    static #EVENTS = ['OnLeftMouseDown', 'OnLeftMouseUp', 'OnLeftClick', 'OnLeftDoubleClick',
        'OnRightMouseDown', 'OnRightMouseUp', 'OnRightClick', 'OnRightDoubleClick',
        'OnMouseOver', 'OnMouseOut', 'OnScrollWheel', 'OnUpdate', 'OnDraw'];

    Elements = [];
    Parent = null;
    UniqueId = 0;
    IgnoresMouseInteraction = false;
    PassThroughMouseInteraction = false;
    OverflowHidden = false;
    UseImmediateMode = false;
    PaddingTop = 0;
    PaddingLeft = 0;
    PaddingRight = 0;
    PaddingBottom = 0;
    MarginTop = 0;
    MarginLeft = 0;
    MarginRight = 0;
    MarginBottom = 0;
    HAlign = 0;
    VAlign = 0;
    IsMouseHovering = false;

    #isInitialized = false;
    #dimensions = new CalculatedStyle();
    #innerDimensions = new CalculatedStyle();
    #outerDimensions = new CalculatedStyle();
    #style = {
        Top: StyleDimension.Empty, Left: StyleDimension.Empty, Width: StyleDimension.Empty, Height: StyleDimension.Empty,
        MaxWidth: StyleDimension.Fill, MaxHeight: StyleDimension.Fill, MinWidth: StyleDimension.Empty, MinHeight: StyleDimension.Empty,
    };

    constructor() {
        this.UniqueId = UIElement.#idCounter++;
        for (const name of UIElement.#EVENTS) {
            const event = new UIElementEvent();
            Object.defineProperty(this, name, {
                get: () => event,
                set: (fn) => {
                    event.Clear();
                    if (fn) event.Add(fn);
                },
                enumerable: false,
            });
        }
    }

    // StyleDimension é valor: atribuir copia (dois elementos nunca dividem um).
    get Top() { return this.#style.Top; }
    set Top(v) { this.#style.Top = StyleDimension.From(v); }
    get Left() { return this.#style.Left; }
    set Left(v) { this.#style.Left = StyleDimension.From(v); }
    get Width() { return this.#style.Width; }
    set Width(v) { this.#style.Width = StyleDimension.From(v); }
    get Height() { return this.#style.Height; }
    set Height(v) { this.#style.Height = StyleDimension.From(v); }
    get MaxWidth() { return this.#style.MaxWidth; }
    set MaxWidth(v) { this.#style.MaxWidth = StyleDimension.From(v); }
    get MaxHeight() { return this.#style.MaxHeight; }
    set MaxHeight(v) { this.#style.MaxHeight = StyleDimension.From(v); }
    get MinWidth() { return this.#style.MinWidth; }
    set MinWidth(v) { this.#style.MinWidth = StyleDimension.From(v); }
    get MinHeight() { return this.#style.MinHeight; }
    set MinHeight(v) { this.#style.MinHeight = StyleDimension.From(v); }

    get Children() { return this.Elements; }

    // ---- árvore ----

    Append(element) {
        element.Remove();
        element.Parent = this;
        this.Elements.push(element);
        element.Recalculate();
    }

    Remove() {
        if (this.Parent) this.Parent.RemoveChild(this);
    }

    RemoveChild(child) {
        const i = this.Elements.indexOf(child);
        if (i >= 0) this.Elements.splice(i, 1);
        child.Parent = null;
    }

    RemoveAllChildren() {
        for (const e of this.Elements) e.Parent = null;
        this.Elements.length = 0;
    }

    HasChild(child) {
        return this.Elements.includes(child);
    }

    AddOrRemoveChild(child, add) {
        if (add && !this.HasChild(child)) this.Append(child);
        else if (!add && this.HasChild(child)) this.RemoveChild(child);
    }

    ExecuteRecursively(action) {
        action(this);
        for (const e of this.Elements) e.ExecuteRecursively(action);
    }

    // ---- desenho ----

    DrawSelf(spriteBatch) {}

    DrawChildren(spriteBatch) {
        for (const e of this.Elements) e.Draw(spriteBatch);
    }

    Draw(spriteBatch) {
        this.OnDraw.Invoke(this, spriteBatch);
        this.DrawSelf(spriteBatch);
        if (!this.OverflowHidden) {
            this.DrawChildren(spriteBatch);
            return;
        }
        UIElement.#PushClip(this.GetClippingRectangle(spriteBatch), spriteBatch);
        try {
            this.DrawChildren(spriteBatch);
        } finally {
            UIElement.#PopClip(spriteBatch);
        }
    }

    // A área de dentro, cortada pelo recorte que já estiver aberto.
    GetClippingRectangle(spriteBatch) {
        const d = this.#innerDimensions;
        let x0 = d.X, y0 = d.Y, x1 = d.X + d.Width, y1 = d.Y + d.Height;
        const outer = UIElement.#clips[UIElement.#clips.length - 1];
        if (outer) {
            x0 = Math.max(x0, outer.X);
            y0 = Math.max(y0, outer.Y);
            x1 = Math.min(x1, outer.X + outer.Width);
            y1 = Math.min(y1, outer.Y + outer.Height);
        }
        return { X: Math.trunc(x0), Y: Math.trunc(y0), Width: Math.max(0, Math.trunc(x1 - x0)), Height: Math.max(0, Math.trunc(y1 - y0)) };
    }

    static #PushClip(rect, spriteBatch) {
        UIElement.#clips.push(rect);
        UIElement.#ApplyClip(spriteBatch);
    }

    static #PopClip(spriteBatch) {
        UIElement.#clips.pop();
        UIElement.#ApplyClip(spriteBatch);
    }

    static #ApplyClip(spriteBatch) {
        const Item = Microsoft.Xna.Framework.Graphics.SpriteBatchItem;
        const top = UIElement.#clips[UIElement.#clips.length - 1];
        if (!top) {
            Item['void DisabledClipping()']();
            return;
        }
        const r = Rectangle.new(top.X, top.Y, top.Width, top.Height);
        Item[UIElement.#CLIP](r, r, spriteBatch, true);
    }

    Update(gameTime) {
        this.OnUpdate.Invoke(this);
        for (const e of [...this.Elements]) e.Update(gameTime);
    }

    // ---- layout ----

    Recalculate() {
        const parent = this.Parent ? this.Parent.GetInnerDimensions() : UserInterface.ActiveInstance.GetDimensions();
        if (this.Parent instanceof UIList) parent.Height = Number.MAX_VALUE;

        const d = this.#FromParent(parent);
        this.#outerDimensions = d.Clone();
        d.X += this.MarginLeft;
        d.Y += this.MarginTop;
        d.Width -= this.MarginLeft + this.MarginRight;
        d.Height -= this.MarginTop + this.MarginBottom;
        this.#dimensions = d.Clone();
        d.X += this.PaddingLeft;
        d.Y += this.PaddingTop;
        d.Width -= this.PaddingLeft + this.PaddingRight;
        d.Height -= this.PaddingTop + this.PaddingBottom;
        this.#innerDimensions = d;
        this.RecalculateChildren();
    }

    #FromParent(parent) {
        const clamp = (v, min, max) => Math.min(Math.max(v, min), max);
        const s = this.#style;
        const d = new CalculatedStyle(s.Left.GetValue(parent.Width) + parent.X, s.Top.GetValue(parent.Height) + parent.Y);
        d.Width = clamp(s.Width.GetValue(parent.Width), s.MinWidth.GetValue(parent.Width), s.MaxWidth.GetValue(parent.Width));
        d.Height = clamp(s.Height.GetValue(parent.Height), s.MinHeight.GetValue(parent.Height), s.MaxHeight.GetValue(parent.Height));
        d.Width += this.MarginLeft + this.MarginRight;
        d.Height += this.MarginTop + this.MarginBottom;
        d.X += parent.Width * this.HAlign - d.Width * this.HAlign;
        d.Y += parent.Height * this.VAlign - d.Height * this.VAlign;
        return d;
    }

    RecalculateChildren() {
        for (const e of this.Elements) e.Recalculate();
    }

    // Cópias (no C# é struct): mexer nelas não muda o elemento.
    GetDimensions() { return this.#dimensions.Clone(); }
    GetInnerDimensions() { return this.#innerDimensions.Clone(); }
    GetOuterDimensions() { return this.#outerDimensions.Clone(); }

    SetPadding(pixels) {
        this.PaddingTop = this.PaddingLeft = this.PaddingRight = this.PaddingBottom = pixels;
    }

    CopyStyle(element) {
        for (const k of ['Top', 'Left', 'Width', 'Height', 'MinWidth', 'MaxWidth', 'MinHeight', 'MaxHeight']) this[k] = element[k];
        for (const k of ['PaddingTop', 'PaddingLeft', 'PaddingRight', 'PaddingBottom', 'HAlign', 'VAlign']) this[k] = element[k];
        this.Recalculate();
    }

    // ---- toque ----

    // `point`: { X, Y } (Vector2 do jogo ou objeto JS).
    ContainsPoint(point) {
        return this.#dimensions.Contains(point.X, point.Y);
    }

    GetViewCullingArea() {
        return this.#dimensions.ToRectangle();
    }

    GetElementAt(point) {
        let found = null;
        for (let i = this.Elements.length - 1; i >= 0; i--) {
            const e = this.Elements[i];
            if (e.IgnoresMouseInteraction || !e.ContainsPoint(point)) continue;
            found = e;
            if (!e.PassThroughMouseInteraction) break;
        }
        if (found) return found.GetElementAt(point);
        if (this.IgnoresMouseInteraction) return null;
        return this.ContainsPoint(point) ? this : null;
    }

    LeftMouseDown(evt) { this.OnLeftMouseDown.Invoke(evt, this); if (this.Parent) this.Parent.LeftMouseDown(evt); }
    LeftMouseUp(evt) { this.OnLeftMouseUp.Invoke(evt, this); if (this.Parent) this.Parent.LeftMouseUp(evt); }
    LeftClick(evt) { this.OnLeftClick.Invoke(evt, this); if (this.Parent) this.Parent.LeftClick(evt); }
    LeftDoubleClick(evt) { this.OnLeftDoubleClick.Invoke(evt, this); if (this.Parent) this.Parent.LeftDoubleClick(evt); }
    RightMouseDown(evt) { this.OnRightMouseDown.Invoke(evt, this); if (this.Parent) this.Parent.RightMouseDown(evt); }
    RightMouseUp(evt) { this.OnRightMouseUp.Invoke(evt, this); if (this.Parent) this.Parent.RightMouseUp(evt); }
    RightClick(evt) { this.OnRightClick.Invoke(evt, this); if (this.Parent) this.Parent.RightClick(evt); }
    RightDoubleClick(evt) { this.OnRightDoubleClick.Invoke(evt, this); if (this.Parent) this.Parent.RightDoubleClick(evt); }

    MouseOver(evt) {
        this.IsMouseHovering = true;
        this.OnMouseOver.Invoke(evt, this);
        if (this.Parent) this.Parent.MouseOver(evt);
    }

    MouseOut(evt) {
        this.IsMouseHovering = false;
        this.OnMouseOut.Invoke(evt, this);
        if (this.Parent) this.Parent.MouseOut(evt);
    }

    ScrollWheel(evt) {
        this.OnScrollWheel.Invoke(evt, this);
        if (this.Parent) this.Parent.ScrollWheel(evt);
    }

    // ---- ciclo de vida ----

    Activate() {
        if (!this.#isInitialized) this.Initialize();
        this.OnActivate();
        for (const e of this.Elements) e.Activate();
    }

    OnActivate() {}

    Deactivate() {
        this.OnDeactivate();
        for (const e of this.Elements) e.Deactivate();
    }

    OnDeactivate() {}

    Initialize() {
        this.OnInitialize();
        this.#isInitialized = true;
    }

    OnInitialize() {}

    CompareTo(other) { return 0; }

    // Para os testes e o UserInterface: o retângulo sem cópia.
    static DimensionsOf(element) { return element.#dimensions; }

    // Interno: põe o elemento num retângulo pronto (sem margem nem padding).
    static Place(element, rect) {
        element.#outerDimensions = new CalculatedStyle(rect.X, rect.Y, rect.Width, rect.Height);
        element.#dimensions = element.#outerDimensions.Clone();
        element.#innerDimensions = element.#outerDimensions.Clone();
    }
}
