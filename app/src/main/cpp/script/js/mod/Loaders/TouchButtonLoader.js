// Os botões de toque dos mods (ModTouchButton). Cada um vira uma ação nova
// dos controles do jogo, depois das dele: o editor de controles a oferece na
// lista de ações de um espaço (com ícone e nome próprios), ela funciona dentro
// dos botões de grupo, e o layout a guarda.
//
// Os números: a ação de mod N (0, 1, 2...) é
//   - ContolType.Count + 1 + N nos controles do layout (o Count é o "vazio");
//   - QUICK_BASE + N no desenho do botão (QuickActionControl);
//   - ControlType.Count + N nos arrays Pressed/WasPressed/Scale do controle.
class TouchButtonLoader {
    static #entries = [];
    static #byKey = new Map();
    static #installed = false;
    static #ids = null;
    static #tick = -1;

    // O primeiro número de QuickActionControl de mod: longe dos do jogo (35).
    static #QUICK_BASE = 100;

    static RegisterButton(mod, name, options = {}) {
        if (!(mod instanceof Mod)) throw new TypeError('RegisterButton: the first argument must be your Mod (this.Mod)');
        if (typeof name !== 'string' || !name.trim()) throw new TypeError('RegisterButton: the button needs a name');
        if (!options || typeof options !== 'object') throw new TypeError('RegisterButton: options must be an object');

        name = name.trim();
        const key = (mod.id || mod.uuid) + '/' + name;
        let entry = TouchButtonLoader.#byKey.get(key);
        if (!entry) {
            entry = { key, index: TouchButtonLoader.#entries.length, current: false, old: false, justPressed: false, justReleased: false, latch: false };
            entry.button = new ModTouchButton(entry);
            TouchButtonLoader.#entries.push(entry);
            TouchButtonLoader.#byKey.set(key, entry);
        }
        Object.assign(entry, {
            mod, name,
            textKey: 'Mods.' + (mod.id || mod.uuid) + '.TouchButtons.' + name + '.DisplayName',
            texts: Lang.Localized('TouchButtons', name + '.DisplayName') || Lang.Localized('Keybinds', name + '.DisplayName'),
            label: options.label,
            icon: options.icon,
            texture: undefined,
            visible: typeof options.visible === 'function' ? options.visible : null,
            enabled: typeof options.enabled === 'function' ? options.enabled : null,
            onPress: typeof options.onPress === 'function' ? options.onPress : null,
        });

        TouchButtonLoader.#Install();
        // Registrado depois do conteúdo pronto (no PostSetupContent): já entra.
        if (TouchButtonHooks.Applied) TouchButtonHooks.ApplySaved();
        return entry.button;
    }

    // ---- números ----

    static get Ids() {
        if (TouchButtonLoader.#ids) return TouchButtonLoader.#ids;

        const read = (fn, fallback) => {
            const value = Safe.Run('TouchButtons: enum', fn);
            return Number.isInteger(value) ? value : fallback;
        };
        const unbound = read(() => Number(bl.classOf('', 'VirtualControlsHardwareConfigurationMapping_Layout').ContolType.Count), 28);
        TouchButtonLoader.#ids = {
            unbound,
            firstAction: unbound + 1,
            quickUnbound: read(() => Number(bl.classOf('', 'QuickActionButton_Layout').QuickActionControl.Unbound), 35),
            firstQuick: TouchButtonLoader.#QUICK_BASE,
            firstControl: read(() => Number(bl.classOf('', 'GUIVirtualInputController').ControlType.Count), 33),
        };
        return TouchButtonLoader.#ids;
    }

    static get Count() { return TouchButtonLoader.#entries.length; }
    static get All() { return TouchButtonLoader.#entries; }

    static ActionOf(entry) { return TouchButtonLoader.Ids.firstAction + entry.index; }
    static QuickOf(entry) { return TouchButtonLoader.Ids.firstQuick + entry.index; }
    static ControlOf(entry) { return TouchButtonLoader.Ids.firstControl + entry.index; }

    static get LastAction() { return TouchButtonLoader.Ids.firstAction + TouchButtonLoader.Count - 1; }

    static ByKey(key) { return TouchButtonLoader.#byKey.get(key) || null; }

    static ByAction(action) {
        const i = action - TouchButtonLoader.Ids.firstAction;
        return i >= 0 && i < TouchButtonLoader.Count ? TouchButtonLoader.#entries[i] : null;
    }

    static ByQuick(quick) {
        const i = quick - TouchButtonLoader.Ids.firstQuick;
        return i >= 0 && i < TouchButtonLoader.Count ? TouchButtonLoader.#entries[i] : null;
    }

    // Uma ação além das do jogo: de um mod deste boot ou de um que saiu.
    static IsModded(action) { return action >= TouchButtonLoader.Ids.firstAction; }

    // ---- texto, ícone, estado ----

    static Label(entry) {
        const own = Safe.Run(entry.key + ' label', () => (typeof entry.label === 'function' ? entry.label() : entry.label));
        if (own !== undefined && own !== null && own !== '') return String(own);
        if (entry.texts) {
            const text = Lang.Pick(entry.texts);
            if (text) return text;
        }
        return entry.name.replace(/([A-Z])/g, ' $1').trim();
    }

    static Texture(entry) {
        if (entry.texture === undefined) entry.texture = Safe.Run(entry.key + ' icon', () => TouchButtonLoader.#Resolve(entry)) || null;
        return entry.texture;
    }

    static Visible(entry) {
        return !entry.visible || Safe.Run(entry.key + ' visible', () => entry.visible() !== false) !== false;
    }

    static Enabled(entry) {
        return !entry.enabled || Safe.Run(entry.key + ' enabled', () => entry.enabled() !== false) !== false;
    }

    // O estado da atualização em curso do jogo: tirado uma vez por
    // atualização, na primeira leitura (o ProcessTriggers lê toda atualização).
    static State(entry) {
        TouchButtonLoader.Tick();
        return entry;
    }

    static Tick() {
        const Main = Terraria.Main;
        const now = Number(Main.GameUpdateCount);
        if (now === TouchButtonLoader.#tick) return;
        TouchButtonLoader.#tick = now;

        const controller = TouchButtonLoader.Controller();
        const pressed = controller && Safe.Run('TouchButtons: Pressed', () => controller.Pressed);
        const active = !Main.gameMenu;
        for (const entry of TouchButtonLoader.#entries) {
            const control = TouchButtonLoader.ControlOf(entry);
            const held = !!(pressed && control < pressed.length && pressed[control]);
            const current = active && (entry.latch || held) && TouchButtonLoader.Enabled(entry);
            entry.latch = false;
            entry.old = entry.current;
            entry.current = current;
            entry.justPressed = current && !entry.old;
            entry.justReleased = !current && entry.old;
        }
    }

    // Um toque no botão: o que dispara o onPress e garante o JustPressed de
    // um toque mais curto que uma atualização.
    static Press(entry) {
        if (Terraria.Main.gameMenu || !TouchButtonLoader.Enabled(entry)) return;
        entry.latch = true;
        if (entry.onPress) Safe.Run(entry.key + ' onPress', () => entry.onPress(Terraria.Main.player[Terraria.Main.myPlayer]));
    }

    static Controller() {
        return Safe.Run('TouchButtons: controle', () => bl.classOf('', 'GUIInstance').Active.GUIVirtualInputController) || null;
    }

    static Styles() {
        return Safe.Run('TouchButtons: estilos', () => bl.classOf('', 'VirtualControlsStyles_Layout').Instance) || null;
    }

    static IsBound(entry) {
        const action = TouchButtonLoader.ActionOf(entry);
        const profile = Safe.Run('TouchButtons: perfil', () => TouchButtonLoader.Styles().ActiveProfile);
        const controls = profile && profile.Controls;
        for (let i = 0; controls && i < controls.length; i++) {
            const binding = controls[i];
            if (!binding) continue;
            if (binding.Action === action) return true;
            const groups = binding.GroupActions;
            for (let j = 0; groups && j < groups.length; j++) if (groups[j] === action) return true;
        }
        return false;
    }

    // O icon das opções; sem ele, Assets/TouchButtons/<Nome>.png; sem esse,
    // o ícone do pacote.
    static #Resolve(entry) {
        let icon = entry.icon;
        if (typeof icon === 'function') icon = icon();
        if (!icon) {
            const file = ContentLookup.As(entry.mod, () => ContentLookup.FindTexture('Assets/TouchButtons/' + entry.name));
            if (file) return bl.loadTexture(file);
            const packIcon = entry.mod.root && ['icon_small.png', 'icon.png']
                .map(name => bl.path.join(entry.mod.root, name)).find(path => bl.file.exists(path));
            return packIcon ? bl.loadTexture(packIcon) : null;
        }
        if (typeof icon !== 'string') return icon && icon.Value ? icon.Value : icon;

        const file = ContentLookup.As(entry.mod, () => ContentLookup.FindTexture(icon));
        if (!file) {
            Safe.Once('touchbutton.icon.' + entry.key, entry.key + ": icon '" + icon + "' is not in the mod");
            return null;
        }
        return bl.loadTexture(file);
    }

    static #Install() {
        if (TouchButtonLoader.#installed) return;
        TouchButtonLoader.#installed = true;

        TouchButtonHooks.Install();
        // Depois do conteúdo pronto, a lista do editor e o layout salvo.
        Ready.Add(() => TouchButtonHooks.ApplySaved(), 'finish');
    }
}
