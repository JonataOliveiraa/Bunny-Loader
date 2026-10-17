// O UserInterface do jogo em JS: um UIState por vez, toque e desenho.
//
// No celular o toque só vira Main.mouseLeft/mouseX dentro de uma região
// registrada no GUIInputRegionManager (é assim que os botões do jogo recebem o
// dedo, e o toque deixa de ir para o mundo). O jogo lê as regiões no meio das
// camadas de interface: a registrada depois (na camada "Mouse Text", onde os
// mods costumam desenhar) se perde. Por isso cada UserInterface desenhado num
// quadro registra a área dos elementos de primeiro nível no começo do
// Main.DrawInterface do quadro seguinte, antes de todas as camadas.
//
// O toque dentro dessas regiões só aparece no Main.mouseX/mouseLeft durante o
// desenho da interface (em unidades de UI); no UpdateUI eles mostram o toque
// do mundo. O começo do DrawInterface guarda o toque, e o Update põe esses
// valores no Main enquanto roda (e devolve os do jogo depois): o Main.mouseX
// e o Main.MouseScreen dos mods funcionam no Update como no PC. Um toque dura
// 1 ou 2 quadros: as mudanças (dedo desce, dedo sobe) vão numa fila, e o
// Update de cada UserInterface passa por todas as que ainda não viu.
class UserInterface {
    static #active = null;
    // O tamanho da tela de UI, lido no desenho de uma camada de interface (no
    // Update o Main.screenWidth pode estar no zoom do mundo).
    static #uiWidth = 0;
    static #uiHeight = 0;
    static #REGION = 'bool RegisterInputRegion(Rectangle rect)';
    static #DOUBLE_CLICK_MS = 500;
    static #STATE_CHANGE_CLICK_DISABLE_MS = 200;
    static #MAX_HISTORY = 32;
    static #drawn = new Set();
    static #regionsHooked = false;
    static #input = { left: false, right: false, x: 0, y: 0 };
    static #events = [];   // { seq, left, right, x, y }: cada mudança do toque
    static #seq = 0;
    static #MAX_EVENTS = 32;

    MousePosition = Vector2.new(0, 0);
    IsVisible = false;
    #currentState = null;
    #lastElementHover = null;
    #clickDisabledUntil = 0;
    #isStateDirty = false;
    #history = [];
    #left = UserInterface.#NewPointer('Left');
    #right = UserInterface.#NewPointer('Right');
    #sizeAtRecalculate = '';
    #seenSeq = UserInterface.#seq;

    constructor() {
        UserInterface.#active = this;
    }

    static get ActiveInstance() {
        if (!UserInterface.#active) UserInterface.#active = new UserInterface();
        return UserInterface.#active;
    }

    static set ActiveInstance(value) {
        UserInterface.#active = value;
    }

    static #NewPointer(side) {
        return { side, wasDown: false, lastDown: null, lastClicked: null, lastTimeDown: 0 };
    }

    get CurrentState() { return this.#currentState; }

    Use() {
        if (UserInterface.#active !== this) {
            UserInterface.#active = this;
            this.Recalculate();
        }
    }

    // ---- toque ----

    #ReadMouse() {
        const Main = Terraria.Main;
        try {
            this.MousePosition = Vector2.new(Main.mouseX, Main.mouseY);
        } catch (e) {
            // No Load do mod o jogo ainda não tem jogador nem mouse.
        }
    }

    Update(gameTime) {
        if (!this.#currentState) {
            this.#seenSeq = UserInterface.#seq;
            return;
        }
        const pending = UserInterface.#events.filter((e) => e.seq > this.#seenSeq);
        this.#seenSeq = UserInterface.#seq;
        for (const event of pending) this.#WithInput(event, () => this.#Pointer());
        this.#WithInput(UserInterface.#input, () => {
            this.#Pointer();
            if (this.#currentState) this.#currentState.Update(gameTime);
        });
    }

    // O toque guardado no Main durante `fn`; o do jogo volta depois.
    #WithInput(input, fn) {
        const Main = Terraria.Main;
        const live = { left: Main.mouseLeft, right: Main.mouseRight, x: Main.mouseX, y: Main.mouseY };
        Main.mouseLeft = input.left;
        Main.mouseRight = input.right;
        Main.mouseX = input.x;
        Main.mouseY = input.y;
        try {
            fn();
        } finally {
            Main.mouseLeft = live.left;
            Main.mouseRight = live.right;
            Main.mouseX = live.x;
            Main.mouseY = live.y;
        }
    }

    // O dedo agora: o elemento sob ele e os cliques.
    #Pointer() {
        const state = this.#currentState;
        if (!state) return;
        const Main = Terraria.Main;
        this.#ReadMouse();
        const mouse = { X: this.MousePosition.X, Y: this.MousePosition.Y };
        // No toque não há "passar por cima": o Main.mouseX fica parado no último
        // toque. Só conta com o dedo na tela (e no quadro em que ele sai).
        const touching = !!Main.mouseLeft || this.#left.wasDown;
        const hover = touching ? state.GetElementAt(mouse) : null;
        if (hover !== this.#lastElementHover) {
            if (this.#lastElementHover) this.#lastElementHover.MouseOut(new UIMouseEvent(this.#lastElementHover, this.MousePosition));
            if (hover) hover.MouseOver(new UIMouseEvent(hover, this.MousePosition));
            this.#lastElementHover = hover;
        }
        if (Date.now() >= this.#clickDisabledUntil) {
            this.#HandleClick(this.#left, !!Main.mouseLeft, hover);
            this.#HandleClick(this.#right, !!Main.mouseRight, hover);
        }
    }

    #HandleClick(pointer, isDown, element) {
        const side = pointer.side;
        const now = Date.now();
        if (isDown && !pointer.wasDown && element) {
            pointer.lastDown = element;
            element[side + 'MouseDown'](new UIMouseEvent(element, this.MousePosition));
            if (pointer.lastClicked === element && now - pointer.lastTimeDown < UserInterface.#DOUBLE_CLICK_MS) {
                element[side + 'DoubleClick'](new UIMouseEvent(element, this.MousePosition));
                pointer.lastClicked = null;
            }
            pointer.lastTimeDown = now;
        } else if (!isDown && pointer.wasDown && pointer.lastDown) {
            const down = pointer.lastDown;
            if (down.ContainsPoint(this.MousePosition) && !UserInterface.#InDraggedList(down)) {
                down[side + 'Click'](new UIMouseEvent(down, this.MousePosition));
                pointer.lastClicked = down;
            }
            down[side + 'MouseUp'](new UIMouseEvent(down, this.MousePosition));
            pointer.lastDown = null;
        }
        pointer.wasDown = isDown;
    }

    // O toque que rolou uma UIList não é clique no item.
    static #InDraggedList(element) {
        for (let e = element; e; e = e.Parent) {
            if (e instanceof UIList && e.IsDragging) return true;
        }
        return false;
    }

    // Um elemento segurando o dedo (arrasto começado nele).
    MouseCaptured() {
        return (this.#left.wasDown && !!this.#left.lastDown) || (this.#right.wasDown && !!this.#right.lastDown);
    }

    IsElementUnderMouse() {
        return this.IsVisible && !!this.#lastElementHover && !(this.#lastElementHover instanceof UIState);
    }

    ClearPointers() {
        for (const p of [this.#left, this.#right]) {
            p.lastClicked = null;
            p.lastDown = null;
            p.lastTimeDown = 0;
        }
    }

    ResetLasts() {
        if (this.#lastElementHover) this.#lastElementHover.MouseOut(new UIMouseEvent(this.#lastElementHover, this.MousePosition));
        this.ClearPointers();
        this.#lastElementHover = null;
    }

    EscapeElements() {
        this.ResetLasts();
    }

    #ResetState() {
        const Main = Terraria.Main;
        if (!Main.dedServ) {
            this.#ReadMouse();
            try {
                this.#left.wasDown = !!Main.mouseLeft;
                this.#right.wasDown = !!Main.mouseRight;
            } catch (e) {
                this.#left.wasDown = this.#right.wasDown = false;
            }
            if (this.#lastElementHover) this.#lastElementHover.MouseOut(new UIMouseEvent(this.#lastElementHover, this.MousePosition));
        }
        this.ClearPointers();
        this.#lastElementHover = null;
        this.#clickDisabledUntil = Math.max(this.#clickDisabledUntil, Date.now() + UserInterface.#STATE_CHANGE_CLICK_DISABLE_MS);
    }

    // ---- desenho ----

    Draw(spriteBatch, gameTime) {
        this.Use();
        const state = this.#currentState;
        if (!state) return;
        const Main = Terraria.Main;
        UserInterface.#uiWidth = Main.screenWidth;
        UserInterface.#uiHeight = Main.screenHeight;
        const size = UserInterface.#uiWidth + 'x' + UserInterface.#uiHeight;
        if (this.#isStateDirty || size !== this.#sizeAtRecalculate) {
            state.Recalculate();
            this.#sizeAtRecalculate = size;
            this.#isStateDirty = false;
        }
        UserInterface.#HookRegions();
        UserInterface.#drawn.add(this);
        state.Draw(spriteBatch);
    }

    static #HookRegions() {
        if (UserInterface.#regionsHooked) return;
        UserInterface.#regionsHooked = true;
        const Main = Terraria.Main;
        Main['void DrawInterface(GameTime gameTime)'].hook((original, self, gameTime) => {
            const left = !!Main.mouseLeft, right = !!Main.mouseRight, x = Main.mouseX, y = Main.mouseY;
            const last = UserInterface.#input;
            if (left !== last.left || right !== last.right) {
                const events = UserInterface.#events;
                events.push({ seq: ++UserInterface.#seq, left, right, x, y });
                if (events.length > UserInterface.#MAX_EVENTS) events.shift();
            }
            UserInterface.#input = { left, right, x, y };
            const drawn = [...UserInterface.#drawn];
            UserInterface.#drawn.clear();
            for (const ui of drawn) {
                const state = ui.#currentState;
                if (state) Safe.Run('UserInterface (touch regions)', () => ui.#RegisterRegions(state));
            }
            original(self, gameTime);
        });
    }

    // A área de cada elemento de primeiro nível que aceita toque.
    #RegisterRegions(state) {
        const manager = bl.classOf('', 'GUIInputRegionManager').Instance;
        if (!manager) return;
        for (const e of state.Elements) {
            if (e.IgnoresMouseInteraction) continue;
            const d = UIElement.DimensionsOf(e);
            if (d.Width <= 0 || d.Height <= 0) continue;
            manager[UserInterface.#REGION](Rectangle.new(Math.trunc(d.X), Math.trunc(d.Y), Math.ceil(d.Width), Math.ceil(d.Height)));
        }
    }

    // ---- estados ----

    SetState(state) {
        if (state === this.#currentState) return;
        if (state) this.#AddToHistory(state);
        if (this.#currentState) {
            if (this.#lastElementHover) this.#lastElementHover.MouseOut(new UIMouseEvent(this.#lastElementHover, this.MousePosition));
            this.#currentState.Deactivate();
        }
        this.#currentState = state || null;
        this.#ResetState();
        if (state) {
            this.#isStateDirty = true;
            state.Activate();
            state.Recalculate();
        }
        this.IsVisible = !!this.#currentState;
    }

    GoBack() {
        if (this.#history.length < 2) return;
        const state = this.#history[this.#history.length - 2];
        this.#history.splice(this.#history.length - 2, 2);
        this.SetState(state);
    }

    #AddToHistory(state) {
        this.#history.push(state);
        if (this.#history.length > UserInterface.#MAX_HISTORY) this.#history.splice(0, 4);
    }

    Recalculate() {
        if (this.#currentState) this.#currentState.Recalculate();
    }

    // A tela de UI inteira, em unidades de UI. Antes do primeiro desenho (no
    // Load do mod o jogo ainda nem tem tela) vale 0: o Draw recalcula.
    GetDimensions() {
        if (UserInterface.#uiWidth) return new CalculatedStyle(0, 0, UserInterface.#uiWidth, UserInterface.#uiHeight);
        const Main = Terraria.Main;
        try {
            return new CalculatedStyle(0, 0, Main.screenWidth, Main.screenHeight);
        } catch (e) {
            return new CalculatedStyle(0, 0, 0, 0);
        }
    }
}
