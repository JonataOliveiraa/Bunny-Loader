class Lang {
    static CULTURES = ['en-US', 'pt-BR', 'de-DE', 'it-IT', 'fr-FR', 'es-ES', 'ru-RU',
                       'zh-Hans', 'zh-Hant', 'pl-PL', 'ja-JP', 'ko-KR'];

    static File(culture) {
        return bl.readJson('Localization/' + culture + '.json');
    }

    // { 'pt-BR': 'Espada', ... } de <secao>.<chave> em cada cultura.
    static Localized(section, key) {
        return LocalizationLoader.Texts(section + '.' + key);
    }

    // O texto da cultura (a do jogo, sem ela); senão inglês; senão o primeiro.
    static Pick(map, culture) {
        if (typeof map === 'string') return map;

        const now = culture || ModLocalization.ActiveCultureName;
        return map[now] ?? map['en-US'] ?? map[''] ?? Object.values(map)[0] ?? '';
    }

    // Cada texto por cultura passado pelo ModifyDisplayName/ModifyDescription do mod.
    static ModifyPerCulture(inst, field, texts, modify) {
        const run = () => Safe.Run(inst.constructor.name + '.' + modify, () => inst[modify]());

        if (!texts || typeof texts !== 'object') {
            inst[field] = texts || '';
            run();
            return inst[field];
        }

        const out = {};
        for (const culture of Object.keys(texts)) {
            inst[field] = texts[culture];
            run();
            out[culture] = inst[field];
        }
        return out;
    }
}
