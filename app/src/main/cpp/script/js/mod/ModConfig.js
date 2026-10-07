class ModConfig {
    // false: a config só aparece na Config. dos Mods do menu principal (a de
    // geração de mundo, que não muda nada com o mundo aberto).
    static VisibleInWorld = true;

    OnLoaded() {}
    OnChanged(key) {}
    OnApply() {}

    ResetToDefaults() {
        ConfigLoader.Reset(this);
    }

    SetOption(key, value) {
        ConfigLoader.SetOption(this, key, value);
    }

    static Header(extra = {}) {
        return { ...extra, type: 'header' };
    }

    static Toggle(defaultValue = false, extra = {}) {
        return { ...extra, type: 'toggle', default: !!defaultValue };
    }

    static Range(defaultValue, { min = 0, max = 100, step = 1, suffix = '', ...extra } = {}) {
        return { ...extra, type: 'range', default: defaultValue, min, max, step, suffix };
    }

    static Radio(defaultValue, choices, extra = {}) {
        return { ...extra, type: 'radio', default: defaultValue, choices: [...choices] };
    }

    static Dropdown(defaultValue, choices, extra = {}) {
        return { ...extra, type: 'dropdown', default: defaultValue, choices: [...choices] };
    }

    static Cycle(defaultValue, choices, extra = {}) {
        return { ...extra, type: 'cycle', default: defaultValue, choices: [...choices] };
    }

    static Color(defaultValue = '#FFFFFF', extra = {}) {
        return { ...extra, type: 'color', default: String(defaultValue).toUpperCase() };
    }

    static Button(action, extra = {}) {
        return { ...extra, type: 'button', action };
    }

    static Link(url, extra = {}) {
        return { ...extra, type: 'link', url: String(url) };
    }

    static register(cls) {
        if (typeof cls !== 'function' || !(cls.prototype instanceof ModConfig)) {
            throw new TypeError('ModConfig.register(Classe): passe a classe, que estende ModConfig');
        }

        const inst = new cls();
        Templates.Adopt(cls, inst);
        ConfigLoader.Add(inst);
        return inst;
    }
}
