class ModLocalization {
    static get ActiveCultureName() {
        try {
            return Terraria.Localization.Language.ActiveCulture.Name;
        } catch (e) {
            return 'en-US';   // sem idioma ainda
        }
    }

    // 'NPCName.ExampleBoss' -> o texto no idioma do jogo; sem texto, o próprio caminho.
    static Translate(path) {
        const text = LocalizationLoader.Lookup(path);
        return text !== undefined ? text : path;
    }

    static TryTranslate(path) {
        const text = LocalizationLoader.Lookup(path);
        return text !== undefined ? text : '';
    }

    // O texto do mod ou, se ele não tem, o do jogo ('LegacyInterface.28').
    static GetTextValue(key) {
        const text = LocalizationLoader.Lookup(key);
        return text !== undefined ? text : Terraria.Localization.Language['string GetTextValue(string key)'](key);
    }

    static GetText(key) {
        const getText = Terraria.Localization.Language['LocalizedText GetText(string key)'];
        const entry = LocalizationLoader.CallerEntry();
        const rel = entry && LocalizationLoader.Relative(entry, key);
        if (!entry || !entry.paths.has(rel)) return getText(key);

        // Antes do conteúdo pronto a chave ainda não está no jogo.
        const full = entry.prefix + rel;
        LocalizationLoader.Put(full, LocalizationLoader.Pick(entry, rel, LocalizationLoader.CultureName));
        return getText(full);
    }

    // A chave do jogo para o que ele pede por chave (Bestiário, CurrencyTextKey).
    static Key(path) {
        const entry = LocalizationLoader.CallerEntry();
        if (!entry) return path;

        const rel = LocalizationLoader.Relative(entry, path);
        return entry.paths.has(rel) ? entry.prefix + rel : path;
    }

    static Exists(key) {
        return LocalizationLoader.Lookup(key) !== undefined ||
            Terraria.Localization.Language['bool Exists(string key)'](key);
    }

    // Um texto (ou { cultura: texto }) sob uma chave qualquer do jogo.
    static Register(key, text) {
        LocalizationLoader.Register(key, text);
        return key;
    }
}
