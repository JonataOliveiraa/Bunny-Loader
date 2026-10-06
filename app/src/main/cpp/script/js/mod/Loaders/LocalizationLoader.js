// Os textos de Localization/<cultura>.json, como no tModLoader: cada chave do
// mod vira 'Mods.<id>.<Secao>.<Chave>' no LanguageManager do jogo, e ganha de
// novo o texto do idioma ativo a cada troca de idioma. Entram pelo mesmo
// caminho dos arquivos do jogo (LoadLanguageFromFileText), com os {$chave}
// já resolvidos.
class LocalizationLoader {
    static #mods = new Map();    // uuid -> { prefix, texts: Map<cultura, Map<caminho, texto>>, paths }
    static #extra = new Map();   // chave do jogo -> texto ou { cultura: texto } (Register)
    static #scheduled = false;
    static #templates = new Map();
    static #culture;

    static get CultureName() {
        return LocalizationLoader.#culture || ModLocalization.ActiveCultureName;
    }

    // Na carga do mod: lê os JSONs uma vez. Os textos de todos os mods vão
    // para o jogo juntos quando o conteúdo fica pronto (na thread do jogo): um
    // {$chave} pode apontar para o texto de outro mod.
    static Load(mod) {
        const entry = LocalizationLoader.#EntryOf(mod);
        if (!entry.paths.size) return;

        LocalizationLoader.#Hook();
        if (LocalizationLoader.#scheduled) return;

        LocalizationLoader.#scheduled = true;
        Ready.Add(() => LocalizationLoader.#ApplyAll(ModLocalization.ActiveCultureName), 'groups');
    }

    static CallerEntry() {
        const mod = bl.mod;
        return mod ? LocalizationLoader.#EntryOf(mod) : undefined;
    }

    static Relative(entry, path) {
        const p = String(path);
        return p.startsWith(entry.prefix) ? p.slice(entry.prefix.length) : p;
    }

    static Lookup(path) {
        const culture = LocalizationLoader.CultureName;
        const own = LocalizationLoader.CallerEntry();
        if (own) {
            const text = LocalizationLoader.Pick(own, LocalizationLoader.Relative(own, path), culture);
            if (text !== undefined) return text;
        }

        const key = String(path);
        let found, count = 0;
        for (const entry of LocalizationLoader.#mods.values()) {
            if (entry === own) continue;
            if (key.startsWith(entry.prefix)) return LocalizationLoader.Pick(entry, key.slice(entry.prefix.length), culture);
            if (!own && entry.paths.has(key)) {
                found = entry;
                count++;
            }
        }
        return count === 1 ? LocalizationLoader.Pick(found, key, culture) : undefined;
    }

    static Texts(path) {
        const entry = LocalizationLoader.CallerEntry();
        if (!entry) return undefined;

        const rel = LocalizationLoader.Relative(entry, path);
        if (!entry.paths.has(rel)) return undefined;

        const out = {};
        for (const [culture, map] of entry.texts) {
            if (!map.has(rel)) continue;
            const text = map.get(rel);
            if (!text.includes('{$')) {
                out[culture] = text;
                continue;
            }
            let texts = LocalizationLoader.#templates.get(culture);
            if (!texts) {
                texts = LocalizationLoader.#Collect(culture).texts;
                LocalizationLoader.#Substitute(texts, true);
                LocalizationLoader.#templates.set(culture, texts);
            }
            out[culture] = texts.get(entry.prefix + rel);
        }
        return out;
    }

    // A cultura pedida; senão inglês; senão a primeira que tiver. Com os
    // {$chave} resolvidos.
    static Pick(entry, rel, culture) {
        return LocalizationLoader.#Resolve(entry.prefix + rel, LocalizationLoader.#Raw(entry, rel, culture), culture);
    }

    static #Raw(entry, rel, culture) {
        if (!entry.paths.has(rel)) return undefined;

        const own = entry.texts.get(culture);
        if (own && own.has(rel)) return own.get(rel);

        const english = entry.texts.get('en-US');
        if (english && english.has(rel)) return english.get(rel);

        for (const map of entry.texts.values()) {
            if (map.has(rel)) return map.get(rel);
        }
        return undefined;
    }

    static Register(key, text) {
        LocalizationLoader.#extra.set(key, text);
        LocalizationLoader.#resolved.clear();
        LocalizationLoader.#templates.clear();
        LocalizationLoader.#Hook();

        const culture = LocalizationLoader.CultureName;
        LocalizationLoader.Put(key, LocalizationLoader.#Resolve(key, Lang.Pick(text, culture), culture));
    }

    // O texto com os {$chave} trocados, da tabela da cultura (montada uma vez
    // por cultura; um mod ou Register novo a descarta).
    static #resolved = new Map();   // cultura -> Map<chave, texto>
    static #Resolve(key, text, culture) {
        if (typeof text !== 'string' || !text.includes('{$')) return text;

        let texts = LocalizationLoader.#resolved.get(culture);
        if (!texts) {
            texts = LocalizationLoader.#Collect(culture).texts;
            LocalizationLoader.#Substitute(texts);
            LocalizationLoader.#resolved.set(culture, texts);
        }
        return texts.get(key) ?? text;
    }

    // Uma chave só, na hora. Chave que já existe: troca o valor (quem guardou
    // o LocalizedText vê o texto novo). Chave nova: um LocalizedText no
    // dicionário do jogo.
    static Put(key, text) {
        const manager = Terraria.Localization.LanguageManager.Instance;
        if (!manager) return;

        const all = manager._localizedTexts;
        if (all.ContainsKey(key)) {
            all.get_Item(key)['void SetValue(string text)'](text);
            return;
        }

        const localized = Terraria.Localization.LocalizedText.new();
        localized['void .ctor(string key, string text)'](key, text);
        all['void set_Item(string key, LocalizedText value)'](key, localized);
    }

    static #EntryOf(mod) {
        let entry = LocalizationLoader.#mods.get(mod.uuid);
        if (entry) return entry;

        const texts = new Map();
        const paths = new Set();
        for (const culture of Lang.CULTURES) {
            const json = Lang.File(culture);
            if (!json || typeof json !== 'object') continue;

            const map = LocalizationLoader.#Flatten(json, '', new Map());
            texts.set(culture, map);
            for (const p of map.keys()) paths.add(p);
        }

        entry = { prefix: 'Mods.' + (mod.id || mod.uuid) + '.', texts, paths };
        LocalizationLoader.#mods.set(mod.uuid, entry);
        LocalizationLoader.#resolved.clear();
        LocalizationLoader.#templates.clear();
        return entry;
    }

    // { A: { B: 'x' } } -> 'A.B' => 'x'
    static #Flatten(obj, prefix, out) {
        for (const [key, value] of Object.entries(obj)) {
            const path = prefix + key;
            if (typeof value === 'string') out.set(path, value);
            else if (value && typeof value === 'object' && !Array.isArray(value)) LocalizationLoader.#Flatten(value, path + '.', out);
        }
        return out;
    }

    // Chave do jogo -> texto na cultura, de todos os mods e dos Register (que
    // ganham numa chave repetida), e as chaves de cada um.
    static #Collect(culture) {
        const texts = new Map();
        const groups = [];
        for (const entry of LocalizationLoader.#mods.values()) {
            const keys = [];
            for (const rel of entry.paths) {
                const key = entry.prefix + rel;
                texts.set(key, LocalizationLoader.#Raw(entry, rel, culture));
                keys.push(key);
            }
            groups.push([entry.prefix, keys]);
        }

        const extra = [];
        for (const [key, text] of LocalizationLoader.#extra) {
            texts.set(key, Lang.Pick(text, culture));
            extra.push(key);
        }
        groups.push(['Register', extra]);
        return { texts, groups };
    }

    // Uma chamada ao jogo por mod: um texto que o jogo recuse leva só o mod dele.
    static #ApplyAll(culture) {
        LocalizationLoader.#culture = culture;
        const { texts, groups } = LocalizationLoader.#Collect(culture);
        Safe.Run('ModLocalization {$}', () => LocalizationLoader.#Substitute(texts));
        LocalizationLoader.#resolved.clear();
        LocalizationLoader.#resolved.set(culture, texts);

        for (const [label, keys] of groups) {
            if (keys.length) Safe.Run('ModLocalization ' + label, () => LocalizationLoader.#Write(keys, texts));
        }
        Terraria.UI.ItemTooltip['void InvalidateTooltips()']();
    }

    // O mesmo formato dos arquivos do jogo, { Categoria: { Chave: texto } },
    // com a categoria sendo tudo antes do último ponto. O jogo junta as duas
    // com '.', troca o valor da chave que já existe (o mesmo LocalizedText),
    // cria a que falta, agrupa por categoria (Language.RandomFromCategory) e
    // trata 'Chave$Variante' como variante.
    static #Write(keys, texts) {
        const manager = Terraria.Localization.LanguageManager.Instance;
        if (!manager) return;

        const file = {};
        let count = 0;
        for (const key of keys) {
            const dot = key.lastIndexOf('.');
            if (dot <= 0 || dot === key.length - 1) {
                LocalizationLoader.Put(key, texts.get(key));
                continue;
            }
            const category = key.slice(0, dot);
            (file[category] ??= {})[key.slice(dot + 1)] = texts.get(key);
            count++;
        }
        if (count) manager['void LoadLanguageFromFileText(string fileText)'](JSON.stringify(file));
    }

    // Os {$chave} dos textos, como o ProcessCopyCommandsInTexts do tModLoader.
    // O do jogo só aceita um ponto ({$Categoria.Chave}). Aceita:
    //   {$Mods.X.Common.Y}    a chave inteira, de mod ou do jogo
    //   {$Common.Y}           relativa: a chave do texto e cada prefixo dela,
    //                         do mais longo ao mais curto (em Mods.X.Items.Z.Tooltip:
    //                         Mods.X.Items.Z.Tooltip.Common.Y, ..., Mods.X.Common.Y)
    //   {$Chave@2}            soma 2 a cada {0} e {^0:...} do texto trazido
    // Chave que não existe fica como o próprio nome, como no jogo. O '-' entra
    // além do que o tModLoader aceita: o id de mod daqui tem ('test-x').
    static #Substitute(texts, deferGame = false) {
        const Language = Terraria.Localization.Language;
        const reference = /{\$([\w./-]+)(?:@(\d+))?}/g;
        const argument = /(?<={\^?)(\d+)(?=(?::[^\r\n]+?)?})/g;
        const done = new Set();
        const pending = new Set();

        const exists = key => texts.has(key) || Language['bool Exists(string key)'](key);
        const inScope = (key, scope) => {
            if (exists(key)) return key;

            const parts = scope.split('.');
            for (let j = parts.length - 1; j >= 0; j--) {
                const candidate = parts.slice(0, j + 1).join('.') + '.' + key;
                if (exists(candidate)) return candidate;
            }
            return key;
        };
        const valueOf = key => {
            if (!texts.has(key)) return Language['string GetTextValue(string key)'](key);
            if (pending.has(key)) return key;

            process(key);
            return texts.get(key);
        };
        const process = key => {
            if (done.has(key)) return;
            done.add(key);

            const text = texts.get(key);
            if (typeof text !== 'string' || !text.includes('{$')) return;
            pending.add(key);

            texts.set(key, text.replace(reference, (match, target, offset) => {
                const scoped = inScope(target, key);
                if (deferGame && !texts.has(scoped)) {
                    return '{$' + scoped + (offset === undefined ? '' : '@' + offset) + '}';
                }
                const value = valueOf(scoped);
                if (offset === undefined) return value;
                const shifted = value.replace(argument, n => String(+n + +offset));
                return deferGame ? shifted.replace(reference, (nested, name, inner) =>
                    '{$' + name + '@' + (+(inner || 0) + +offset) + '}') : shifted;
            }));
            pending.delete(key);
        };

        for (const key of texts.keys()) process(key);
    }

    // A troca de idioma põe cada texto registrado de volta na própria chave e
    // recarrega só os arquivos do jogo: os do mod entram de novo depois.
    static #Hook() {
        Hooks.Once('localization.language', () => {
            const Manager = Terraria.Localization.LanguageManager;

            Manager['void LoadFilesForCulture(GameCulture culture)'].hook((original, self, culture) => {
                original(self, culture);
                Safe.Run('ModLocalization (idioma)', () => LocalizationLoader.#ApplyAll(culture.Name));
            });
        });
    }
}
