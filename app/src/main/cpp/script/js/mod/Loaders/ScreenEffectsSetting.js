// A linha "Efeitos de Tela" da aba Geral das Configurações (no menu principal
// e no mundo), logo abaixo das linhas do Bunny Loader: cada toque liga ou
// desliga ScreenEffects.Enabled. Desligados, os efeitos de todos os mods
// continuam vivendo, só não desenham (para quem passa mal com clarão e
// distorção, ou quer o fps). A escolha fica em BunnyLoader.screen.json, na
// pasta de saves do jogo. Como a "Tema do Menu", é um GUIOpenUIButtonSetting
// com o id None, posto no _categorySettings depois do SetCategory.
class ScreenEffectsSetting {
    static #entry = null;
    static #OpenButton = null;
    static #restored = false;

    static #SETTING_NONE = 192;
    static #GENERAL = 0;
    static #CAPTION_PAD = 16;

    static Install() {
        Hooks.Once('screen.setting', () => Safe.Run('ScreenEffectsSetting.Install', () => ScreenEffectsSetting.#Hook()));
    }

    // Lê a escolha salva (uma vez: no primeiro efeito ou na tela de opções).
    static Restore() {
        if (ScreenEffectsSetting.#restored) return;
        ScreenEffectsSetting.#restored = true;

        const file = ScreenEffectsSetting.#File();
        const text = Safe.Run('ScreenEffectsSetting: ler', () => bl.file.exists(file) ? bl.file.read(file) : null);
        if (!text) return;
        try {
            const data = JSON.parse(text) || {};
            if (typeof data.Enabled === 'boolean') ScreenEffects.Enabled = data.Enabled;
        } catch (e) {
            bl.error('ScreenEffects: ' + file + ' is broken (' + e + '); screen effects stay on');
        }
    }

    // Liga ou desliga e grava.
    static Set(enabled) {
        ScreenEffectsSetting.#restored = true;
        ScreenEffects.Enabled = enabled === true;
        const data = { Enabled: ScreenEffects.Enabled };
        Safe.Run('ScreenEffectsSetting: gravar', () => bl.file.write(ScreenEffectsSetting.#File(), JSON.stringify(data)));
    }

    static #File() {
        return bl.path.join(Terraria.Main.SavePath, 'BunnyLoader.screen.json');
    }

    static #Hook() {
        const Overlay = bl.classOf('', 'GUISettingsOverlay');
        const OpenButton = ScreenEffectsSetting.#OpenButton = bl.classOf('', 'GUIOpenUIButtonSetting');

        Overlay['void SetCategory(SettingsOverlayOptionCategories_Layout.Category category)'].hook((original, self, category) => {
            original(self, category);
            Safe.Run('ScreenEffectsSetting.SetCategory', () => ScreenEffectsSetting.#AddEntry(self));
        });

        const ours = (self) => self === ScreenEffectsSetting.#entry;
        OpenButton['string GetOption()'].hook((original, self) =>
            ours(self) ? ScreenEffectsSetting.#Label() : original(self));
        OpenButton['string GetButtonCaption()'].hook((original, self) =>
            ours(self) ? ScreenEffectsSetting.#Caption(self) : original(self));
        OpenButton['void OpenUI()'].hook((original, self) => {
            if (!ours(self)) return original(self);
            Safe.Run('ScreenEffectsSetting.Toggle', () => ScreenEffectsSetting.Set(!ScreenEffects.Enabled));
            return undefined;
        });
    }

    // Depois da "Config. dos Mods" e da "Tema do Menu" (as que estiverem lá).
    static #AddEntry(overlay) {
        if (Number(overlay.Selected) !== ScreenEffectsSetting.#GENERAL) return;

        const list = overlay._categorySettings;
        if (!list) return;
        ScreenEffectsSetting.Restore();

        let entry = ScreenEffectsSetting.#entry;
        if (!entry) {
            entry = ScreenEffectsSetting.#OpenButton.new();
            entry['void .ctor(GUISettingID id)'](ScreenEffectsSetting.#SETTING_NONE);
            entry._category = ScreenEffectsSetting.#GENERAL;
            ScreenEffectsSetting.#entry = entry;
        }
        if (list['bool Contains(GUISettingEntry item)'](entry)) return;

        let at = 0;
        for (const ours of [ModConfigMenu.MenuEntry, MenuThemeSetting.Entry]) {
            if (ours) at = Math.max(at, list['int IndexOf(GUISettingEntry item)'](ours) + 1);
        }
        list['void Insert(int index, GUISettingEntry item)'](at, entry);
    }

    static #Pt() {
        return /^pt/.test(ModLocalization.ActiveCultureName || '');
    }

    static #Label() {
        return ScreenEffectsSetting.#Pt() ? 'Efeitos de Tela:' : 'Screen Effects:';
    }

    static #Caption(self) {
        const pt = ScreenEffectsSetting.#Pt();
        const value = ScreenEffects.Enabled ? (pt ? 'Ligados' : 'On') : (pt ? 'Desligados' : 'Off');
        const layout = self['SettingsOverlayListOption_Layout GetLayout()']();
        const caption = layout ? layout.Caption : null;
        return caption ? ModConfigMenu.Fit(caption, value, caption.overloadedSize.X - ScreenEffectsSetting.#CAPTION_PAD) : value;
    }
}
