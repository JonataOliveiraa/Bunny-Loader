class ConfigLoader {
    static List = [];
    static #dirty = new Set();
    static #VALUES = ['toggle', 'range', 'radio', 'dropdown', 'cycle', 'color'];
    static #CHOICES = ['radio', 'dropdown', 'cycle'];
    static #MAX_DROPDOWN = 12;

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
        const entry = { inst, mod, name, file: null, options };
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

        const local = (path) => {
            const texts = Lang.Localized('Configs', owner + '.' + key + '.' + path);
            return texts ? Lang.Pick(texts) : '';
        };
        o.label = local('Label') || o.label || key.replace(/([a-z0-9])([A-Z])/g, '$1 $2');
        if (ConfigLoader.#CHOICES.includes(o.type)) {
            o.choiceLabels = o.choices.map((c, i) => local(String(c)) || (o.labels && o.labels[i]) || String(c));
        }
        if (o.type === 'button' || o.type === 'link') o.text = local('Text') || o.text || (o.type === 'link' ? 'Abrir' : 'OK');
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

    static Set(entry, o, value) {
        if (o.type === 'range') value = ConfigLoader.Snap(o, value);
        if (o.type === 'color' && typeof value === 'string') value = value.toUpperCase();
        if (!ConfigLoader.#Valid(o, value) || entry.inst[o.key] === value) return;

        entry.inst[o.key] = value;
        ConfigLoader.#dirty.add(entry);
        Safe.Run(entry.name + '.OnChanged', () => entry.inst.OnChanged(o.key));
    }

    static Reset(inst) {
        const entry = ConfigLoader.List.find((e) => e.inst === inst);
        if (!entry) return;
        for (const o of entry.options) {
            if (ConfigLoader.HasValue(o)) ConfigLoader.Set(entry, o, o.default);
        }
        ConfigLoader.Flush();
    }

    static SetOption(inst, key, value) {
        const entry = ConfigLoader.List.find((e) => e.inst === inst);
        const o = entry && entry.options.find((x) => x.key === key);
        if (!o || !ConfigLoader.HasValue(o)) throw new TypeError(inst.constructor.name + ": '" + key + "' não é uma opção com valor");
        ConfigLoader.Set(entry, o, value);
    }

    static IsEnabled(entry, o) {
        if (o.enabledWhen === undefined) return true;
        if (typeof o.enabledWhen === 'string') return !!entry.inst[o.enabledWhen];
        return !!Safe.Run(entry.name + '.' + o.key + '.enabledWhen', () => o.enabledWhen.call(entry.inst, entry.inst));
    }

    static Run(entry, o) {
        Safe.Run(entry.name + '.' + o.key, () => {
            const action = typeof o.action === 'string' ? entry.inst[o.action] : o.action;
            if (typeof action !== 'function') throw new TypeError(entry.name + '.' + o.key + ": '" + o.action + "' não é um método da config");
            action.call(entry.inst, entry.inst);
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
