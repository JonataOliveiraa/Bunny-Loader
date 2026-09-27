// Os textos de Localization/<cultura>.json, como no tModLoader: cada chave do
// mod vira 'Mods.<id>.<Secao>.<Chave>' no LanguageManager do jogo, e ganha de
// novo o texto do idioma ativo a cada troca de idioma.
class LocalizationLoader {
    static #mods = new Map();    // uuid -> { prefix, texts: Map<cultura, Map<caminho, texto>>, paths }
    static #extra = new Map();   // chave do jogo -> texto ou { cultura: texto } (Register)

    // Na carga do mod: lê os JSONs uma vez e registra tudo no jogo quando o
    // conteúdo fica pronto (na thread do jogo).
    static Load(mod) {
        const entry = LocalizationLoader.#EntryOf(mod);
        if (!entry.paths.size) return;

        LocalizationLoader.#Hook();
        Ready.Add(() => Safe.Run('ModLocalization ' + entry.prefix,
            () => LocalizationLoader.#Apply(entry, ModLocalization.ActiveCultureName)), 'groups');
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
        const entry = LocalizationLoader.CallerEntry();
        if (!entry) return undefined;

        return LocalizationLoader.Pick(entry, LocalizationLoader.Relative(entry, path), ModLocalization.ActiveCultureName);
    }

    // { 'pt-BR': 'Espada', ... } do caminho em cada cultura, do mod de quem chama.
    static Texts(path) {
        const entry = LocalizationLoader.CallerEntry();
        if (!entry) return undefined;

        const rel = LocalizationLoader.Relative(entry, path);
        if (!entry.paths.has(rel)) return undefined;

        const out = {};
        for (const [culture, map] of entry.texts) {
            if (map.has(rel)) out[culture] = map.get(rel);
        }
        return out;
    }

    // A cultura pedida; senão inglês; senão a primeira que tiver.
    static Pick(entry, rel, culture) {
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
        LocalizationLoader.#Hook();
        LocalizationLoader.Put(key, Lang.Pick(text));
    }

    // Chave que já existe: troca o valor (quem guardou o LocalizedText vê o
    // texto novo). Chave nova: um LocalizedText no dicionário do jogo.
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

    static #Apply(entry, culture) {
        for (const rel of entry.paths) {
            LocalizationLoader.Put(entry.prefix + rel, LocalizationLoader.Pick(entry, rel, culture));
        }
    }

    static #ApplyAll(culture) {
        for (const entry of LocalizationLoader.#mods.values()) LocalizationLoader.#Apply(entry, culture);
        for (const [key, text] of LocalizationLoader.#extra) LocalizationLoader.Put(key, Lang.Pick(text, culture));
    }

    // A troca de idioma põe cada texto registrado de volta na própria chave e
    // recarrega só os arquivos do jogo: os do mod entram de novo depois.
    static #Hook() {
        Hooks.Once('localization.language', () => {
            const Manager = Terraria.Localization.LanguageManager;

            Manager['void LoadLanguage(GameCulture culture, bool processCopyCommands)'].hook((original, self, culture, copy) => {
                original(self, culture, copy);
                Safe.Run('ModLocalization (idioma)', () => LocalizationLoader.#ApplyAll(culture.Name));
            });

            Manager['void SetLanguage(GameCulture culture)'].hook((original, self, culture) => {
                original(self, culture);
                Safe.Run('ModLocalization (idioma)', () => LocalizationLoader.#ApplyAll(ModLocalization.ActiveCultureName));
            });
        });
    }
}
