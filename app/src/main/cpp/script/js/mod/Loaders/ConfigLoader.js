class ConfigLoader {
    static List = [];
    static #dirty = new Set();
    static #VALUES = ['toggle', 'range', 'radio', 'dropdown', 'cycle', 'color'];
    static #CHOICES = ['radio', 'dropdown', 'cycle'];
    static #MAX_DROPDOWN = 12;
    static #ALIGNS = ['left', 'center', 'right'];

    static Add(inst) {
        const cls = inst.constructor;
        const name = cls.name;
        const declared = cls.Options;
        if (!declared || typeof declared !== 'object') {
            throw new TypeError(name + ': declare as opções em `static Options = { ... }`');
        }

        const options = Object.keys(declared).map((key) => ConfigLoader.#Option(name, key, declared[key]));
        for (const o of options) {
            if (typeof o.enabledWhen === 'string' && !options.some((other) => other.key === o.enabledWhen)) {
                throw new TypeError(name + '.' + o.key + ": enabledWhen '" + o.enabledWhen + "' não é uma opção desta config");
            }
        }
        const mod = inst.Mod;
        const entry = { inst, mod, name, file: null, options, draft: null, visibleInWorld: cls.VisibleInWorld !== false };
        const dir = mod ? ModRegistry.DataDirectory(mod.uuid) : null;
        if (dir) entry.file = bl.path.join(dir, name + '.json');

        const saved = entry.file ? ConfigLoader.#Read(entry.file) : {};
        for (const o of options) {
            if (ConfigLoader.HasValue(o)) inst[o.key] = ConfigLoader.#Valid(o, saved[o.key]) ? saved[o.key] : o.default;
        }
        ConfigLoader.List.push(entry);
        Safe.Run(name + '.OnLoaded', () => inst.OnLoaded());
    }

    static HasValue(o) {
        return ConfigLoader.#VALUES.includes(o.type);
    }

    static #Option(owner, key, raw) {
        const where = owner + '.' + key;
        if (!raw || typeof raw !== 'object') throw new TypeError(where + ': use ModConfig.Toggle, Range, Radio, Dropdown...');
        const o = { ...raw, key };
        switch (o.type) {
            case 'header':
                if (o.color !== undefined) {
                    o.color = String(o.color).toUpperCase();
                    if (!/^#[0-9A-F]{6}$/.test(o.color)) throw new TypeError(where + ": Header pede color '#RRGGBB'");
                }
                if (o.align !== undefined && !ConfigLoader.#ALIGNS.includes(o.align)) {
                    throw new RangeError(where + ": Header pede align 'left', 'center' ou 'right'");
                }
                break;
            case 'toggle':
                o.default = !!o.default;
                break;
            case 'range':
                for (const f of ['min', 'max', 'step', 'default']) {
                    if (typeof o[f] !== 'number' || !Number.isFinite(o[f])) throw new TypeError(where + ': Range pede ' + f + ' numérico');
                }
                if (!(o.max > o.min) || !(o.step > 0)) throw new RangeError(where + ': Range pede min < max e step > 0');
                o.default = ConfigLoader.Snap(o, o.default);
                break;
            case 'radio':
            case 'dropdown':
            case 'cycle':
                if (!Array.isArray(o.choices) || o.choices.length < 2) throw new TypeError(where + ': pede ao menos duas escolhas');
                if (o.type === 'dropdown' && o.choices.length > ConfigLoader.#MAX_DROPDOWN) {
                    throw new RangeError(where + ': Dropdown aceita até ' + ConfigLoader.#MAX_DROPDOWN + ' escolhas');
                }
                if (!o.choices.includes(o.default)) throw new RangeError(where + ": o padrão '" + o.default + "' não está nas escolhas");
                break;
            case 'color':
                if (!/^#[0-9A-F]{6}$/.test(o.default)) throw new TypeError(where + ": Color pede '#RRGGBB'");
                break;
            case 'button':
                if (typeof o.action !== 'function' && typeof o.action !== 'string') {
                    throw new TypeError(where + ": Button pede uma função ou o nome de um método da config");
                }
                break;
            case 'link':
                if (!/^https?:\/\//i.test(o.url)) throw new TypeError(where + ': Link pede um endereço http(s)');
                break;
            default:
                throw new TypeError(where + ": tipo '" + o.type + "' desconhecido");
        }
        if (o.enabledWhen !== undefined && typeof o.enabledWhen !== 'string' && typeof o.enabledWhen !== 'function') {
            throw new TypeError(where + ': enabledWhen pede o nome de uma opção ou uma função');
        }

        const local = (target, property, path, fallback) => {
            const name = 'Configs.' + owner + '.' + key + '.' + path;
            if (!ModLocalization.Exists(name)) {
                target[property] = fallback;
                return;
            }
            const text = ModLocalization.GetText(name);
            Object.defineProperty(target, property, {
                enumerable: true,
                configurable: true,
                get: () => text.Value || fallback,
            });
        };
        local(o, 'label', 'Label', o.label || key.replace(/([a-z0-9])([A-Z])/g, '$1 $2'));
        if (ConfigLoader.#CHOICES.includes(o.type)) {
            o.choiceLabels = [];
            for (let i = 0; i < o.choices.length; i++) {
                const choice = o.choices[i];
                local(o.choiceLabels, i, String(choice), o.labels && o.labels[i] || String(choice));
            }
        }
        if (o.type === 'button' || o.type === 'link') {
            local(o, 'text', 'Text', o.text || (o.type === 'link' ? 'Abrir' : 'OK'));
        }
        return o;
    }

    static Snap(o, value) {
        const steps = Math.round((value - o.min) / o.step);
        const snapped = o.min + steps * o.step;
        return Math.min(o.max, Math.max(o.min, Number(snapped.toFixed(6))));
    }

    static #Valid(o, value) {
        switch (o.type) {
            case 'toggle': return typeof value === 'boolean';
            case 'range': return typeof value === 'number' && value >= o.min && value <= o.max;
            case 'radio':
            case 'dropdown':
            case 'cycle': return o.choices.includes(value);
            case 'color': return typeof value === 'string' && /^#[0-9A-F]{6}$/.test(value);
        }
        return false;
    }

    // Com a tela aberta, cada config tem um rascunho: um clone da instância
    // onde a tela lê e grava. Nada chega à config (nem ao OnChanged, nem ao
    // arquivo) antes do Aplicar.
    static Begin() {
        for (const entry of ConfigLoader.List) {
            const draft = Object.create(Object.getPrototypeOf(entry.inst));
            Object.assign(draft, entry.inst);
            entry.draft = draft;
        }
    }

    static View(entry) {
        return entry.draft || entry.inst;
    }

    static Pending(entries = ConfigLoader.List) {
        return entries.some((entry) => entry.draft && entry.options.some((o) =>
            ConfigLoader.HasValue(o) && entry.draft[o.key] !== entry.inst[o.key]));
    }

    static AtDefaults(entries) {
        return entries.every((entry) => entry.options.every((o) =>
            !ConfigLoader.HasValue(o) || ConfigLoader.View(entry)[o.key] === o.default));
    }

    static Defaults(entries) {
        for (const entry of entries) {
            for (const o of entry.options) {
                if (ConfigLoader.HasValue(o)) ConfigLoader.Set(entry, o, o.default);
            }
        }
    }

    static Apply() {
        for (const entry of ConfigLoader.List) {
            const draft = entry.draft;
            if (!draft) continue;
            entry.draft = null;
            const changed = entry.options.filter((o) => ConfigLoader.HasValue(o) && draft[o.key] !== entry.inst[o.key]);
            if (!changed.length) continue;
            for (const o of changed) entry.inst[o.key] = draft[o.key];
            ConfigLoader.#dirty.add(entry);
            for (const o of changed) Safe.Run(entry.name + '.OnChanged', () => entry.inst.OnChanged(o.key));
            Safe.Run(entry.name + '.OnApply', () => entry.inst.OnApply());
        }
        ConfigLoader.Flush();
    }

    static Discard() {
        for (const entry of ConfigLoader.List) entry.draft = null;
    }

    static Set(entry, o, value) {
        if (o.type === 'range') value = ConfigLoader.Snap(o, value);
        if (o.type === 'color' && typeof value === 'string') value = value.toUpperCase();
        const target = ConfigLoader.View(entry);
        if (!ConfigLoader.#Valid(o, value) || target[o.key] === value) return;

        target[o.key] = value;
        if (entry.draft) return;
        ConfigLoader.#dirty.add(entry);
        Safe.Run(entry.name + '.OnChanged', () => entry.inst.OnChanged(o.key));
    }

    static #EntryOf(inst) {
        return ConfigLoader.List.find((e) => e.inst === inst || e.draft === inst);
    }

    static Reset(inst) {
        const entry = ConfigLoader.#EntryOf(inst);
        if (!entry) return;
        ConfigLoader.Defaults([entry]);
        ConfigLoader.Flush();
    }

    static SetOption(inst, key, value) {
        const entry = ConfigLoader.#EntryOf(inst);
        const o = entry && entry.options.find((x) => x.key === key);
        if (!o || !ConfigLoader.HasValue(o)) throw new TypeError(inst.constructor.name + ": '" + key + "' não é uma opção com valor");
        ConfigLoader.Set(entry, o, value);
    }

    static IsEnabled(entry, o) {
        if (o.enabledWhen === undefined) return true;
        const view = ConfigLoader.View(entry);
        if (typeof o.enabledWhen === 'string') return !!view[o.enabledWhen];
        return !!Safe.Run(entry.name + '.' + o.key + '.enabledWhen', () => o.enabledWhen.call(view, view));
    }

    // Com a tela aberta, o método do botão roda no rascunho: lê o que está na
    // tela, e o SetOption dele também espera o Aplicar.
    static Run(entry, o) {
        Safe.Run(entry.name + '.' + o.key, () => {
            const view = ConfigLoader.View(entry);
            const action = typeof o.action === 'string' ? view[o.action] : o.action;
            if (typeof action !== 'function') throw new TypeError(entry.name + '.' + o.key + ": '" + o.action + "' não é um método da config");
            action.call(view, view);
        });
    }

    static Flush() {
        for (const entry of ConfigLoader.#dirty) {
            if (!entry.file) continue;
            const data = {};
            for (const o of entry.options) {
                if (ConfigLoader.HasValue(o) && entry.inst[o.key] !== o.default) data[o.key] = entry.inst[o.key];
            }
            Safe.Run('ModConfig: gravar ' + entry.file, () => {
                if (!Object.keys(data).length) return bl.file.delete(entry.file);
                bl.directory.create(bl.path.getParentPath(entry.file));
                bl.file.write(entry.file, JSON.stringify(data, null, 2));
            });
        }
        ConfigLoader.#dirty.clear();
    }

    static Of(mod) {
        return ConfigLoader.List.filter((e) => e.mod === mod);
    }

    static #Read(file) {
        const text = bl.file.exists(file) ? bl.file.read(file) : null;
        if (!text) return {};
        try {
            const data = JSON.parse(text);
            return data && typeof data === 'object' ? data : {};
        } catch (e) {
            bl.error('ModConfig: ' + file + ' is broken (' + e + '); using the defaults');
            return {};
        }
    }
}
