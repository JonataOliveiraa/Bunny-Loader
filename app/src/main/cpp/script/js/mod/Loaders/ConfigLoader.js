// Os ModConfig carregados: as opções de cada um (conferidas no registro), o
// valor atual e o arquivo. A tela é o ModConfigMenu.
//
// Cada config grava em <dados do mod>/<Classe>.json só o que difere do
// padrão. Um valor do arquivo que não serve mais (a opção sumiu, o Radio
// perdeu a escolha, o número saiu da faixa) volta ao padrão em vez de travar o
// mod: a config de uma versão velha do mod não pode impedir a nova de carregar.
class ConfigLoader {
    static List = [];           // { inst, mod, name, file, options: [{ key, ... }] }
    static #dirty = new Set();

    static Add(inst) {
        const cls = inst.constructor;
        const name = cls.name;
        const declared = cls.Options;
        if (!declared || typeof declared !== 'object') {
            throw new TypeError(name + ': declare as opções em `static Options = { ... }`');
        }

        const options = Object.keys(declared).map((key) => ConfigLoader.#Option(name, key, declared[key]));
        const mod = inst.Mod;
        const entry = { inst, mod, name, file: null, options };
        const dir = mod ? ModRegistry.DataDirectory(mod.uuid) : null;
        if (dir) entry.file = bl.path.join(dir, name + '.json');

        const saved = entry.file ? ConfigLoader.#Read(entry.file) : {};
        for (const o of options) inst[o.key] = ConfigLoader.#Valid(o, saved[o.key]) ? saved[o.key] : o.default;
        ConfigLoader.List.push(entry);
        Safe.Run(name + '.OnLoaded', () => inst.OnLoaded());
    }

    // A opção conferida, com o rótulo e os textos das escolhas já resolvidos.
    static #Option(owner, key, raw) {
        if (!raw || typeof raw !== 'object') throw new TypeError(owner + '.' + key + ': use ModConfig.Toggle/Range/Radio');
        const where = owner + '.' + key;
        const o = { ...raw, key };
        switch (o.type) {
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
                if (!Array.isArray(o.choices) || o.choices.length < 2) throw new TypeError(where + ': Radio pede ao menos duas escolhas');
                if (!o.choices.includes(o.default)) throw new RangeError(where + ": o padrão '" + o.default + "' não está nas escolhas");
                break;
            default:
                throw new TypeError(where + ": tipo '" + o.type + "' desconhecido (toggle, range ou radio)");
        }

        // O texto do Localization na cultura do jogo, se o mod tiver.
        const local = (path) => {
            const texts = Lang.Localized('Configs', owner + '.' + key + '.' + path);
            return texts ? Lang.Pick(texts) : '';
        };
        o.label = local('Label') || o.label || key.replace(/([a-z0-9])([A-Z])/g, '$1 $2');
        if (o.type === 'radio') {
            o.choiceLabels = o.choices.map((c, i) => local(String(c)) || (o.labels && o.labels[i]) || String(c));
        }
        return o;
    }

    // O número da faixa no degrau mais perto, dentro dos limites.
    static Snap(o, value) {
        const steps = Math.round((value - o.min) / o.step);
        const snapped = o.min + steps * o.step;
        // Sem o arredondamento, 0.1 + 0.2 aparece como 0.30000000000000004.
        return Math.min(o.max, Math.max(o.min, Number(snapped.toFixed(6))));
    }

    static #Valid(o, value) {
        switch (o.type) {
            case 'toggle': return typeof value === 'boolean';
            case 'range': return typeof value === 'number' && value >= o.min && value <= o.max;
            case 'radio': return o.choices.includes(value);
        }
        return false;
    }

    // Muda o valor, avisa o mod e marca para gravar (o Flush grava; o arrastar
    // de uma faixa não escreve no disco a cada quadro).
    static Set(entry, o, value) {
        if (o.type === 'range') value = ConfigLoader.Snap(o, value);
        if (!ConfigLoader.#Valid(o, value) || entry.inst[o.key] === value) return;

        entry.inst[o.key] = value;
        ConfigLoader.#dirty.add(entry);
        Safe.Run(entry.name + '.OnChanged', () => entry.inst.OnChanged(o.key));
    }

    static Flush() {
        for (const entry of ConfigLoader.#dirty) {
            if (!entry.file) continue;
            const data = {};
            for (const o of entry.options) {
                if (entry.inst[o.key] !== o.default) data[o.key] = entry.inst[o.key];
            }
            Safe.Run('ModConfig: gravar ' + entry.file, () => {
                if (!Object.keys(data).length) return bl.file.delete(entry.file);
                bl.directory.create(bl.path.getParentPath(entry.file));
                bl.file.write(entry.file, JSON.stringify(data, null, 2));
            });
        }
        ConfigLoader.#dirty.clear();
    }

    // As configs de um mod, na ordem do registro.
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
