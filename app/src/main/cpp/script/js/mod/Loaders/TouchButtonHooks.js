// Os ganchos dos botões de toque de mod (TouchButtonLoader). Os arrays do jogo
// indexados por ação (estilo, ação do botão, controle, modo de entrada) só
// têm as ações dele: crescem para caber as de mod, e são conferidos de novo a
// cada segundo (o jogo os refaz ao trocar de layout ou de modo).
class TouchButtonHooks {
    static Applied = false;

    static #frame = 0;
    static #groupCache = { frame: -999, list: [] };
    static #grids = new Map();

    // O tipo de espaço do editor: botão, grupo (o redondo de várias ações).
    static #BUTTON = 1;
    static #GROUP = 2;
    // O índice do centro do grupo (fecha sem escolher).
    static #GROUP_CENTRE = 9;
    // GUIQuickActionButton.InteractionState.
    static #DISABLED = 0;
    static #PRESSED = 1;
    static #NORMAL = 2;

    static #DRAW_ICON = 'void Draw(QuickActionButton_Layout layout, Texture2D buttonTexture, GUIQuickActionButton.InteractionState state, ref float scale)';

    static Install() {
        const T = TouchButtonLoader;
        const ids = T.Ids;
        const Class = (name) => Safe.Run('TouchButtons: classe ' + name, () => bl.classOf('', name));
        const Controller = Class('GUIVirtualInputController');
        const Quick = Class('GUIQuickActionButton');
        const Edit = Class('GUIVirtualInputControllerEdit');
        const Styles = Class('VirtualControlsStyles_Layout');
        const Hook = (cls, signature, fn, options) => {
            if (!cls) return;
            Safe.Run('TouchButtons: hook ' + signature, () => cls[signature].hook(fn, options));
        };

        Hook(Controller, 'void Update(float elapsedTime)', (original, self, elapsedTime) => {
            TouchButtonHooks.#frame++;
            Safe.Run('TouchButtons: arrays', () => {
                TouchButtonHooks.EnsureController(self);
                TouchButtonHooks.Ensure(false);
            });
            const groups = Safe.Run('TouchButtons: grupos', () => TouchButtonHooks.#GroupSlots()) || [];
            const before = groups.length ? groups.map((g) => TouchButtonHooks.#GroupIndex(self, g.id)) : null;
            original(self, elapsedTime);
            if (!before) return;

            // Um grupo solto numa ação de mod: o índice apertado volta a -1.
            groups.forEach((g, i) => {
                const was = before[i];
                if (was < 0 || was === TouchButtonHooks.#GROUP_CENTRE || was >= g.groups.length) return;
                if (TouchButtonHooks.#GroupIndex(self, g.id) !== -1) return;
                const entry = T.ByAction(g.groups[was]);
                if (entry) T.Press(entry);
            });
        });

        Hook(Controller, 'void Draw()', (original, self) => {
            Safe.Run('TouchButtons: arrays', () => TouchButtonHooks.EnsureController(self));
            return original(self);
        });

        Hook(Controller, 'void HandleTouchCursorDown(int slot, Vector2 cursorUIPosition, Cursor cursor)', (original, self, slot, position, cursor) => {
            const at = Safe.Run('TouchButtons: espaço', () => TouchButtonHooks.#SlotAt(slot));
            const entry = at && at.type === TouchButtonHooks.#BUTTON ? T.ByAction(at.binding.Action) : null;
            if (!entry) return original(self, slot, position, cursor);

            Safe.Run('TouchButtons: toque', () => {
                TouchButtonHooks.EnsureController(self);
                const control = T.ControlOf(entry);
                const first = !self.WasPressed[control];
                self.Pressed[control] = true;
                if (first) T.Press(entry);
            });
        });

        Hook(Controller, 'bool DetermineButtonVisible(int slot, VirtualControlsHardwareConfigurationMapping_Layout.ContolType action, VirtualControlsHardwareConfigurationMapping_Layout slotConfig)', (original, self, slot, action, slotConfig) => {
            const entry = T.ByAction(action);
            if (entry && !T.Visible(entry)) return false;
            return original(self, slot, action, slotConfig);
        }, { minType: ids.firstAction, arg: 1 });

        Hook(Quick, 'void Draw(QuickActionButton_Layout.QuickActionControl actionType, QuickActionButton_Layout layout, bool pressed, ref float scale, bool ignoreChecks, bool ignoreIcon)', (original, actionType, layout, pressed, scale, ignoreChecks, ignoreIcon) => {
            const entry = T.ByQuick(actionType);
            const texture = entry && !ignoreIcon ? T.Texture(entry) : null;
            if (!texture || !layout) return original(ids.quickUnbound, layout, pressed, scale, ignoreChecks, ignoreIcon);
            const state = pressed ? TouchButtonHooks.#PRESSED : T.Enabled(entry) ? TouchButtonHooks.#NORMAL : TouchButtonHooks.#DISABLED;
            Quick[TouchButtonHooks.#DRAW_ICON](layout, texture, state, scale);
        }, { minType: ids.firstQuick, arg: 0 });

        Hook(Quick, 'void GetIconFrame(QuickActionButton_Layout.QuickActionControl actionType, out Texture2D iconTexture, out Rectangle frame)', (original, actionType, iconTexture, frame) => {
            const entry = T.ByQuick(actionType);
            const texture = entry ? T.Texture(entry) : null;
            if (!texture) return original(ids.quickUnbound, iconTexture, frame);
            iconTexture.value = texture;
            frame.value = Rectangle.new(0, 0, texture.Width, texture.Height);
        }, { minType: ids.firstQuick, arg: 0 });

        Hook(Edit, 'string GetControlName(VirtualControlsHardwareConfigurationMapping_Layout.ContolType control)', (original, self, control) => {
            const entry = T.ByAction(control);
            return entry ? T.Label(entry) : original(self, control);
        }, { minType: ids.firstAction, arg: 0 });

        Hook(Edit, 'void GetControlTextureFrame(VirtualControlsHardwareConfigurationMapping_Layout.ContolType controlType, out Texture2D tex, out Rectangle frame)', (original, self, controlType, tex, frame) => {
            const entry = T.ByAction(controlType);
            const texture = entry ? T.Texture(entry) : null;
            if (!texture) return original(self, controlType, tex, frame);
            tex.value = texture;
            frame.value = Rectangle.new(0, 0, texture.Width, texture.Height);
        }, { minType: ids.firstAction, arg: 0 });

        Hook(Edit, 'void Draw()', (original, self) => {
            Safe.Run('TouchButtons: editor', () => TouchButtonHooks.Ensure(false));
            return original(self);
        });

        // O arquivo de controles do jogo nunca leva ação de mod: sem o mod, ou
        // sem o Bunny Loader, ele continua válido. Elas vão à parte (TouchButtonLayouts).
        Hook(Styles, 'void SaveConfiguration(Preferences preferences)', (original, self, preferences) =>
            TouchButtonLayouts.Save(self, () => original(self, preferences)));

        Hook(Styles, 'void LoadConfiguration(Preferences preferences)', (original, self, preferences) => {
            original(self, preferences);
            Safe.Run('TouchButtons: layout salvo', () => {
                TouchButtonHooks.Ensure(true);
                TouchButtonLayouts.Apply(self);
            });
        });

        // O código de compartilhar também sai sem elas.
        Hook(Styles, 'string GetShareCode(VirtualControlsProfile_Layout profile)', (original, profile) =>
            TouchButtonLayouts.WithoutMods(profile ? [profile] : [], () => original(profile)));
    }

    static ApplySaved() {
        TouchButtonHooks.Applied = true;
        Safe.Run('TouchButtons: layout salvo', () => {
            TouchButtonHooks.Ensure(true);
            TouchButtonLayouts.Apply(TouchButtonLoader.Styles());
        });
    }

    // ---- arrays ----

    static Ensure(force) {
        if (!TouchButtonLoader.Count) return;
        if (!force && (TouchButtonHooks.#frame % 60) !== 0) return;

        Safe.Run('TouchButtons: estáticos', () => TouchButtonHooks.#Statics());
        Safe.Run('TouchButtons: modo de entrada', () => TouchButtonHooks.#InputState());
        Safe.Run('TouchButtons: lista do editor', () => TouchButtonHooks.#Options());
        Safe.Run('TouchButtons: controle', () => TouchButtonHooks.EnsureController(TouchButtonLoader.Controller()));
    }

    static EnsureController(controller) {
        const T = TouchButtonLoader;
        if (!controller || !T.Count) return;
        const size = T.ControlOf(T.All[T.Count - 1]) + 1;
        const pressed = controller.Pressed;
        if (pressed && pressed.length >= size) return;
        controller.Pressed = TouchButtonHooks.#Sized(controller.Pressed, size, () => false);
        controller.WasPressed = TouchButtonHooks.#Sized(controller.WasPressed, size, () => false);
        controller.Scale = TouchButtonHooks.#Sized(controller.Scale, size, () => 0);
    }

    static #Statics() {
        const T = TouchButtonLoader;
        const ids = T.Ids;
        const Styles = bl.classOf('', 'VirtualControlsButtonConfiguration_Layout');
        const Controller = bl.classOf('', 'GUIVirtualInputController');
        const Input = bl.classOf('', 'VirtualControllerInputState');
        TouchButtonHooks.#Put(Styles, 'ButtonStyles', () => 0, () => 0);
        TouchButtonHooks.#Put(Controller, 'QuickActionControlFromLayoutControl', () => ids.quickUnbound, (e) => T.QuickOf(e));
        TouchButtonHooks.#Put(Controller, 'ButtonControlFromLayoutControl', () => 0, (e) => T.ControlOf(e));
        TouchButtonHooks.#Put(Input, 'DefaultMode', (i) => i, (e) => T.ActionOf(e));
        TouchButtonHooks.#Put(Input, 'RemappedMode', (i) => i, (e) => T.ActionOf(e));
    }

    static #InputState() {
        const T = TouchButtonLoader;
        const state = bl.classOf('', 'GUIInstance').Active.VirtualControllerInputState;
        if (!state) return;
        const last = T.LastAction;
        const current = state.RemappedBindings;
        if (current && current.length > last && current[last] === last) return;

        Safe.Run('TouchButtons: RefreshAlternativeModeMappings', () => state['void RefreshAlternativeModeMappings()']());
        const after = state.RemappedBindings;
        if (!after || after.length <= last || after[last] !== last) {
            state.RemappedBindings = TouchButtonHooks.#Mapped(after, (i) => i, (e) => T.ActionOf(e));
        }
    }

    // As ações de mod no fim da lista de ações do editor, e a grade da lista
    // cresce para caber (sem cobrir o "Desvincular").
    static #Options() {
        const T = TouchButtonLoader;
        const config = bl.classOf('', 'VirtualInputControllerControlConfiguration_Layout').Instance;
        let list = config && config.ButtonControls;
        if (!list) return;

        const have = new Set();
        for (let i = 0; i < list.length; i++) have.add(list[i]);
        const missing = T.All.filter((e) => !have.has(T.ActionOf(e)));
        if (missing.length) {
            const out = list.cloneResized(list.length + missing.length);
            missing.forEach((e, i) => { out[list.length + i] = T.ActionOf(e); });
            config.ButtonControls = out;
            list = out;
        }
        const grid = config.PulldownOptionGrid;
        if (grid) TouchButtonHooks.#Fit(grid, list.length);
    }

    static #Fit(grid, count) {
        const key = bl.addressOf(grid);
        let base = TouchButtonHooks.#grids.get(key);
        if (!base) {
            base = { items: grid.ItemCount, line: Math.max(1, grid.ItemLineCount) };
            TouchButtonHooks.#grids.set(key, base);
        }
        let items = base.items, line = base.line;
        if (count > items) {
            if (grid.Horizontal) {
                items = Math.ceil(count / line) * line;
            } else {
                const columns = Math.max(1, Math.ceil(items / line));
                line = Math.ceil(count / columns);
                items = line * columns;
            }
        }
        if (grid.ItemLineCount !== line) grid.ItemLineCount = line;
        if (grid.ItemCount !== items) grid.ItemCount = items;
    }

    static #Sized(array, size, fill) {
        if (!array) return null;
        const old = array.length;
        if (old >= size) return array;
        const out = array.cloneResized(size);
        for (let i = old; i < size; i++) out[i] = fill(i);
        return out;
    }

    static #Mapped(array, fill, value) {
        const T = TouchButtonLoader;
        const out = TouchButtonHooks.#Sized(array, T.LastAction + 1, fill);
        if (!out) return null;
        for (const entry of T.All) {
            const i = T.ActionOf(entry), v = value(entry);
            if (out[i] !== v) out[i] = v;
        }
        return out;
    }

    static #Put(cls, field, fill, value) {
        if (!cls) return;
        const array = cls[field];
        const out = TouchButtonHooks.#Mapped(array, fill, value);
        if (out && out !== array) cls[field] = out;
    }

    // ---- espaços ----

    static #SlotAt(slot) {
        const profile = TouchButtonLoader.Styles().ActiveProfile;
        if (!profile) return null;
        const binding = profile.Controls[slot];
        const hardware = profile.HardwareConfig.Slots[slot];
        if (!binding || !hardware) return null;
        return { binding, type: hardware.Style.ControlType };
    }

    // Os grupos do layout com alguma ação de mod (relidos a cada meio segundo).
    static #GroupSlots() {
        const cache = TouchButtonHooks.#groupCache;
        if (TouchButtonHooks.#frame - cache.frame < 30) return cache.list;

        const list = [];
        const profile = TouchButtonLoader.Styles().ActiveProfile;
        const controls = profile && profile.Controls;
        const slots = profile && profile.HardwareConfig.Slots;
        const n = controls && slots ? Math.min(controls.length, slots.length) : 0;
        for (let i = 0; i < n; i++) {
            const hardware = slots[i], binding = controls[i];
            if (!hardware || !binding || hardware.Style.ControlType !== TouchButtonHooks.#GROUP) continue;
            const groups = binding.GroupActions;
            if (!groups) continue;
            let ours = false;
            for (let j = 0; j < groups.length; j++) if (TouchButtonLoader.ByAction(groups[j])) ours = true;
            if (ours) list.push({ id: String(hardware.SlotId), groups });
        }
        TouchButtonHooks.#groupCache = { frame: TouchButtonHooks.#frame, list };
        return list;
    }

    static #GroupIndex(controller, id) {
        const states = controller.GroupStates;
        if (!states || !states.ContainsKey(id)) return -1;
        return states.get_Item(id).pressedIndex;
    }
}
