// A linha "Tema do Menu" da aba Geral das Configurações do menu principal,
// logo abaixo da "Config. dos Mods": o botão mostra o tema atual e cada toque
// passa para o próximo (MenuLoader.Next). No tModLoader é o texto no rodapé
// do título. Como a da Config. dos Mods, é um GUIOpenUIButtonSetting com o id
// None, posto no _categorySettings depois do SetCategory; o Draw do jogo toca
// o som e chama o OpenUI.
class MenuThemeSetting {
    static #entry = null;
    static #OpenButton = null;

    static #SETTING_NONE = 192;
    static #GENERAL = 0;
    static #CAPTION_PAD = 16;

    static Install() {
        Hooks.Once('menu.theme', () => Safe.Run('MenuThemeSetting.Install', () => MenuThemeSetting.#Hook()));
    }

    static #Hook() {
        const Overlay = bl.classOf('', 'GUISettingsOverlay');
        const OpenButton = MenuThemeSetting.#OpenButton = bl.classOf('', 'GUIOpenUIButtonSetting');

        Overlay['void SetCategory(SettingsOverlayOptionCategories_Layout.Category category)'].hook((original, self, category) => {
            original(self, category);
            Safe.Run('MenuThemeSetting.SetCategory', () => MenuThemeSetting.#AddEntry(self));
        });

        const ours = (self) => self === MenuThemeSetting.#entry;
        OpenButton['string GetOption()'].hook((original, self) =>
            ours(self) ? MenuThemeSetting.#Label() : original(self));
        OpenButton['string GetButtonCaption()'].hook((original, self) =>
            ours(self) ? MenuThemeSetting.#Caption(self) : original(self));
        OpenButton['void OpenUI()'].hook((original, self) => {
            if (!ours(self)) return original(self);
            Safe.Run('MenuThemeSetting.Next', () => MenuLoader.Next());
            return undefined;
        });
    }

    // Logo depois da "Config. dos Mods" (ou no topo, se ela entrar depois:
    // ela vai para o índice 0 e esta fica embaixo).
    static #AddEntry(overlay) {
        if (!Terraria.Main.gameMenu || Number(overlay.Selected) !== MenuThemeSetting.#GENERAL) return;

        const list = overlay._categorySettings;
        if (!list) return;

        let entry = MenuThemeSetting.#entry;
        if (!entry) {
            entry = MenuThemeSetting.#OpenButton.new();
            entry['void .ctor(GUISettingID id)'](MenuThemeSetting.#SETTING_NONE);
            entry._category = MenuThemeSetting.#GENERAL;
            MenuThemeSetting.#entry = entry;
        }
        if (list['bool Contains(GUISettingEntry item)'](entry)) return;

        const config = ModConfigMenu.MenuEntry;
        const at = config ? list['int IndexOf(GUISettingEntry item)'](config) + 1 : 0;
        list['void Insert(int index, GUISettingEntry item)'](at, entry);
    }

    static #Label() {
        return /^pt/.test(ModLocalization.ActiveCultureName || '') ? 'Tema do Menu:' : 'Menu Theme:';
    }

    static #Caption(self) {
        const menu = MenuLoader.CurrentMenu;
        const name = menu ? MenuLoader.Name(menu) : 'Terraria';
        const layout = self['SettingsOverlayListOption_Layout GetLayout()']();
        const caption = layout ? layout.Caption : null;
        return caption ? ModConfigMenu.Fit(caption, name, caption.overloadedSize.X - MenuThemeSetting.#CAPTION_PAD) : name;
    }
}
