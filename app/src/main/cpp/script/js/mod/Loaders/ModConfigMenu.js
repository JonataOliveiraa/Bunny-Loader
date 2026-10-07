// A tela de Config. dos Mods: no mundo, pelo botão do menu de pausa; no menu
// principal, pela linha no topo da aba Geral das Configurações. No mundo só
// entram as configs com VisibleInWorld.
class ModConfigMenu {
    static #open = false;
    // 'pause' ou 'menu': de onde a tela abriu, e quem ela substitui no Draw.
    static #where = null;
    static #menuEntry = null;
    static #button = null;
    static #parts = null;
    static #scales = null;
    static #rowState = new Map();
    static #colorState = new Map();
    static #selected = null;
    static #tabs = new Map();
    static #tabShape = null;
    static #icons = new Map();
    static #scroll = 0;
    static #momentum = 0;
    static #lastY = null;
    static #slider = null;
    static #pressInList = false;
    static #guard = false;
    static #guardIdle = 0;
    static #pressY = null;
    static #dragged = false;
    static #popup = null;
    static #listTop = 0;
    static #listBottom = 0;
    static #tabScroll = 0;
    static #tabMomentum = 0;
    static #tabPressX = null;
    static #tabLastX = null;
    static #tabDragged = false;
    static #tabPressIn = false;

    static #GAP = 4;
    static #HEADER_EXTRA = 8;
    static #SWATCH_INSET = 4;
    static #CYCLE_X = 24;
    static #POPUP_MARGIN = 8;
    static #DRAG_PX = 8;
    static #FRICTION = 0.92;
    static #SCREEN = 0;
    static #TOP_LEFT = 9;
    static #CENTER = 18;
    // Alignment: os 3 bits de baixo são o horizontal (1 esquerda, 2 centro,
    // 4 direita), o 16 é o meio na vertical.
    static #MIDDLE = 16;
    static #ALIGN_X = { left: 1, center: 2, right: 4 };
    static #HEADER_PAD = 12;
    static #ESCAPE = 27;
    static #MENU_OPEN = 10;
    static #MENU_CLOSE = 11;
    static #TICK = 12;
    static #ICON_SCALE = 1.1;
    // A linha do menu principal: um GUIOpenUIButtonSetting com o id None, que
    // nenhuma tabela do jogo indexa (o ResetSetting dele cai no default).
    static #SETTING_NONE = 192;
    static #GENERAL = 0;

    static #DRAW = 'GUITransactionButton.InputState Draw(TransactionButton_Layout layout, Item item, string label, bool disabled, ref float scale, bool forcedPressed, bool hasControllerFocus, bool forceOver, bool disablePressedState)';
    static #BANNER = 'GUITransactionButton.InputState DrawWithBanner(TransactionButton_Layout layout, ControllerActionButton action, Item item, string label, bool disabled, ref float scale, bool forcedPressed, bool hasControllerFocus, bool forceOver, bool disablePressedState, bool drawWhenControllerConnected, bool addTouchBanner)';
    static #PANEL = 'void Draw(Panel_Layout layout, bool cursorOver, Nullable<Color> overloadBacking, Nullable<Color> overloadBorder, Nullable<Color> overloadHighlight)';
    static #TITLE = 'bool DrawButton(StringButton_Layout layout, string value, ref float scale, bool forcedPressed, bool buttonDisabled)';
    static #TEXT = 'void Draw(String_Layout layout, string value, Color overloadedColour, bool multilineAlignmentApplied)';
    static #SLIDER = 'bool Draw(Slider_Layout layout, bool disablePick, ref float value, GUISlider.DragState dragState, GUISlider.DrawBackingHandler backingHandler, bool forceOver, int minValue, int maxValue, bool ignoreStartPoint)';
    static #SATURATION = 'bool DrawSaturation(Slider_Layout layout, bool disablePick, ref float value, GUISlider.DragState dragState, bool forceOver)';
    static #LIGHTNESS = 'bool DrawLightness(Slider_Layout layout, bool disablePick, ref float value, GUISlider.DragState dragState, bool forceOver)';
    static #OVER_TRACK = 'bool IsCursorOver(Vector2 cursorPosition, Slider_Layout layout)';
    static #ANCHORED = 'Vector2 GetAnchoredPosition(ControlAnchor.ControlId anchorControl, LayoutCalculator.AnchorType anchorType, Vector2 position)';
    static #CLIP = 'void EnableClipping(Rectangle inner, Rectangle outer, SpriteBatch batcher, bool vertical)';
    static #REGION = 'bool RegisterInputRegion(Rectangle rect)';
    static #GET_GRID = 'Rectangle get__gridItemRegion()';
    static #SET_GRID = 'void set__gridItemRegion(Rectangle value)';
    static #BEGIN = 'void Begin(SpriteSortMode sortMode, BlendState blendState, SamplerState samplerState, DepthStencilState depthStencilState, RasterizerState rasterizerState, Effect effect, Nullable<Matrix> transformMatrix, bool defferedBatch)';
    static #CLICKED = 0;

    static #T = null;

    static Install() {
        Hooks.Once('modconfig.pause', () => Safe.Run('ModConfigMenu.Install', () => ModConfigMenu.#Hook()));
    }

    static #Hook() {
        const T = ModConfigMenu.#T = {
            Pause: bl.classOf('', 'GUISettingsPauseMenu'),
            Layout: bl.classOf('', 'SettingsPauseMenu_Layout'),
            Ach: bl.classOf('', 'Achievements_Layout'),
            Settings: bl.classOf('', 'SettingsOverlay_Layout'),
            Mappings: bl.classOf('', 'KeyboardMappings_Layout'),
            Button: bl.classOf('', 'GUITransactionButton'),
            ButtonLayout: bl.classOf('', 'TransactionButton_Layout'),
            TextureLayout: bl.classOf('', 'Texture_Layout'),
            StringButton: bl.classOf('', 'GUIStringButton'),
            String: bl.classOf('', 'GUIString'),
            Panel: bl.classOf('', 'GUIPanel'),
            Slider: bl.classOf('', 'GUISlider'),
            DragState: bl.classOf('', 'GUISlider').DragState,
            Anchor: bl.classOf('', 'ControlAnchor'),
            Instance: bl.classOf('', 'GUIInstance'),
            Input: bl.classOf('', 'XNAUIInputLayer'),
            Regions: bl.classOf('', 'GUIInputRegionManager'),
            Calc: bl.classOf('', 'LayoutCalculator'),
            Keyboard: bl.classOf('', 'KeyboardInput'),
            Application: bl.classOf('UnityEngine', 'Application'),
            SpriteBatchItem: Microsoft.Xna.Framework.Graphics.SpriteBatchItem,
            Overlay: bl.classOf('', 'GUISettingsOverlay'),
            OpenButton: bl.classOf('', 'GUIOpenUIButtonSetting'),
        };
        ModConfigMenu.#scales = { button: new Ref(1), title: new Ref(1), discard: new Ref(1), defaults: new Ref(1), apply: new Ref(1) };
        const pauseDraw = T.Pause['void Draw()'];

        T.Pause['void SetupOffsets(bool setup)'].hook((original, self, setup) => {
            original(self, setup);
            if (setup) Safe.Run('ModConfigMenu.SetupOffsets', () => ModConfigMenu.#Layout(self));
        });

        pauseDraw.hook((original, self) => {
            if (ModConfigMenu.#where !== 'pause') return original(self);
            const ok = Safe.Run('ModConfigMenu.Draw', () => { ModConfigMenu.#DrawScreen(); return true; });
            if (!ok) ModConfigMenu.#Close(false);
            return undefined;
        });

        T.Panel[ModConfigMenu.#PANEL].hook((original, layout, cursorOver, backing, border, highlight) => {
            original(layout, cursorOver, backing, border, highlight);
            const L = T.Layout.Instance;
            if (L && layout === L.Backing) Safe.Run('ModConfigMenu.Button', () => ModConfigMenu.#DrawButton(L));
        }, { whileIn: pauseDraw });

        Terraria.WorldGen['void clearWorld()'].hook((original) => {
            original();
            ModConfigMenu.#Close(false);
        });

        ModConfigMenu.#HookMainMenu(T);
    }

    // O SetCategory refaz a lista da aba (filtra o _allSettings pela categoria
    // que o layout dá a cada id e ordena): a linha entra depois, no topo.
    static #HookMainMenu(T) {
        T.Overlay['void SetCategory(SettingsOverlayOptionCategories_Layout.Category category)'].hook((original, self, category) => {
            original(self, category);
            Safe.Run('ModConfigMenu.SetCategory', () => ModConfigMenu.#AddMenuEntry(self));
        });

        T.Overlay['void Draw()'].hook((original, self) => {
            if (ModConfigMenu.#where !== 'menu') return original(self);
            const ok = Safe.Run('ModConfigMenu.Draw', () => { ModConfigMenu.#DrawScreen(); return true; });
            if (!ok) ModConfigMenu.#Close(false);
            return undefined;
        });

        T.Overlay['void Close()'].hook((original, self) => {
            if (ModConfigMenu.#where === 'menu') ModConfigMenu.#Close(false);
            return original(self);
        });

        const ours = (self) => self === ModConfigMenu.#menuEntry;
        T.OpenButton['string GetOption()'].hook((original, self) =>
            ours(self) ? ModConfigMenu.#Text('row') : original(self));
        T.OpenButton['string GetButtonCaption()'].hook((original, self) =>
            ours(self) ? ModConfigMenu.#Text('open') : original(self));
        T.OpenButton['void OpenUI()'].hook((original, self) => {
            if (!ours(self)) return original(self);
            T.Input.Instance['void CaptureUICrusorDrag(int dragFromAxis)'](-1);
            ModConfigMenu.#Open('menu');
            return undefined;
        });
    }

    static #AddMenuEntry(overlay) {
        if (!Terraria.Main.gameMenu || Number(overlay.Selected) !== ModConfigMenu.#GENERAL) return;
        const list = overlay._categorySettings;
        if (!list) return;
        let entry = ModConfigMenu.#menuEntry;
        if (!entry) {
            entry = ModConfigMenu.#T.OpenButton.new();
            entry['void .ctor(GUISettingID id)'](ModConfigMenu.#SETTING_NONE);
            entry._category = ModConfigMenu.#GENERAL;
            ModConfigMenu.#menuEntry = entry;
        }
        if (!list['bool Contains(GUISettingEntry item)'](entry)) list['void Insert(int index, GUISettingEntry item)'](0, entry);
    }

    static #Open(where) {
        SoundEngine.PlaySound(ModConfigMenu.#MENU_OPEN);
        ModConfigMenu.#open = true;
        ModConfigMenu.#where = where;
        ConfigLoader.Begin();
        ModConfigMenu.#ResetScroll();
        ModConfigMenu.#Guard();
    }

    // No mundo, só o que a config deixa (VisibleInWorld); no menu, tudo.
    static #Entries(mod) {
        const inWorld = ModConfigMenu.#where !== 'menu';
        return ConfigLoader.Of(mod).filter((e) => !inWorld || e.visibleInWorld);
    }

    static #Text(key) {
        const pt = /^pt/.test(ModLocalization.ActiveCultureName || '');
        switch (key) {
            case 'button': return pt ? 'Config. dos Mods' : 'Mod Configuration';
            case 'row': return pt ? 'Config. dos Mods:' : 'Mod Configuration:';
            case 'open': return pt ? 'Abrir' : 'Open';
            case 'empty': return pt ? 'Nenhum mod tem opções.' : 'No mod has options.';
        }
        return key;
    }

    static #Language(key) {
        return Terraria.Localization.Language['string GetTextValue(string key)'](key);
    }

    static #Step(L) {
        return L.Close.overloadedSize.Y + L.ButtonSpacing;
    }

    static #ButtonLayout(L) {
        if (ModConfigMenu.#button) return ModConfigMenu.#button;
        const ours = L.Settings['object MemberwiseClone()']();
        Safe.Run('ModConfigMenu: ícone', () => {
            const T = ModConfigMenu.#T;
            const icon = T.TextureLayout.new();
            icon['void .ctor()']();
            icon.TextureId = 'bunny/coelho_cabeca';
            icon._lastTextureId = icon.TextureId;
            icon._texture = bl.builtinTexture('bunnyHead').Value;
            ours.IconTexture = icon;
            ours.ForceIconScale = true;
            ours.ForcedIconScale = ModConfigMenu.#ICON_SCALE;
        });
        return ModConfigMenu.#button = ours;
    }

    static #Layout(self) {
        const L = ModConfigMenu.#T.Layout.Instance;
        const step = ModConfigMenu.#Step(L);
        const move = (b, dy) => { const p = b.Location; b.Location = Vector2.new(p.X, p.Y + dy); };

        const backing = L.Backing;
        backing.Size = Vector2.new(backing.Size.X, backing.Size.Y + step);
        const above = ['Close', 'Controls', 'Settings'];
        if (self.includeMPInviteActive) above.push('Invite');
        const below = ['Achievements', 'Bestiary', 'Home'];
        if (self.creativeModeActive) below.push('JourneySettings');
        for (const k of above) move(L[k], -step / 2);
        for (const k of below) move(L[k], step / 2);

        const ours = ModConfigMenu.#ButtonLayout(L);
        const settings = L.Settings.Location;
        ours.Location = Vector2.new(settings.X, settings.Y + step);

        const list = self._controllerList;
        if (!list) return;
        const items = list._items;
        const all = [];
        for (let i = 0; i < items.length; i++) all.push(items[i]);
        if (all.includes(ours)) return;
        all.push(ours);
        list._items = ModConfigMenu.#T.ButtonLayout.newArray(all);
        list['void Activate()']();
    }

    static #DrawButton(L) {
        if (ModConfigMenu.#open) return;
        const T = ModConfigMenu.#T;
        const ours = ModConfigMenu.#ButtonLayout(L);
        ours.overloadedSize = L.Settings.overloadedSize;
        ours.Location = Vector2.new(L.Settings.Location.X, ours.Location.Y);
        const state = Number(T.Button[ModConfigMenu.#DRAW](ours, null, ModConfigMenu.#Text('button'), false,
            ModConfigMenu.#scales.button, false, ModConfigMenu.#Focus(), false, false));
        if (state !== ModConfigMenu.#CLICKED) return;

        T.Input.Instance['void CaptureUICrusorDrag(int dragFromAxis)'](-1);
        ModConfigMenu.#Open('pause');
    }

    static #Focus() {
        const gi = ModConfigMenu.#T.Instance.Active;
        const pad = gi && gi.GUIVirtualInputController;
        return !!(pad && pad.ControllerActive);
    }

    static #Guard() {
        ModConfigMenu.#guard = true;
        ModConfigMenu.#guardIdle = 0;
    }

    // Fechar sem Aplicar (Descartar, o voltar do Android, sair do mundo)
    // joga o rascunho fora.
    static #Close(sound = true, apply = false) {
        if (!ModConfigMenu.#open) return;
        ModConfigMenu.#open = false;
        ModConfigMenu.#where = null;
        ModConfigMenu.#popup = null;
        ModConfigMenu.#ReleaseSliders();
        if (apply) ConfigLoader.Apply();
        else ConfigLoader.Discard();
        if (sound) SoundEngine.PlaySound(ModConfigMenu.#MENU_CLOSE);
    }

    static #DrawScreen() {
        const T = ModConfigMenu.#T, S = T.Settings.Instance;
        if (ModConfigMenu.#guard) {
            ModConfigMenu.#guardIdle = Terraria.Main.mouseLeft ? 0 : ModConfigMenu.#guardIdle + 1;
            if (ModConfigMenu.#guardIdle >= 2) ModConfigMenu.#guard = false;
        }
        const panel = T.Panel[ModConfigMenu.#PANEL];
        panel(S.Backing, false, null, null, null);
        panel(S.MenuDivider, false, null, null, null);
        panel(S.MenuDivider2, false, null, null, null);

        const mods = ModLoader.Mods.filter((m) => ModConfigMenu.#Entries(m).some((e) => e.options.length));
        let mod = mods.find((m) => m.uuid === ModConfigMenu.#selected) || mods[0] || null;
        if (mod) mod = ModConfigMenu.#DrawTabs(T, S, mods, mod);
        ModConfigMenu.#selected = mod ? mod.uuid : null;
        const title = mod ? mod.name || mod.id : ModConfigMenu.#Text('button');
        T.StringButton[ModConfigMenu.#TITLE](S.Title, title, ModConfigMenu.#scales.title, false, true);
        ModConfigMenu.#DrawList(S, mod);

        const action = ModConfigMenu.#DrawActions(T, mod);
        Safe.Run('ModConfigMenu.Popup', () => ModConfigMenu.#DrawPopup(T, S));
        if (action && !ModConfigMenu.#Blocked()) {
            T.Input.Instance['void CaptureUICrusorDrag(int dragFromAxis)'](-1);
            if (action === 'defaults') {
                ConfigLoader.Defaults(ModConfigMenu.#Entries(mod));
                SoundEngine.PlaySound(ModConfigMenu.#TICK);
            } else {
                ModConfigMenu.#Close(true, action === 'apply');
                return;
            }
        }
        if (T.Keyboard['bool GetKeyUp(KeyCode keycode)'](ModConfigMenu.#ESCAPE)) {
            const gi = T.Instance.Active;
            if (gi && gi.GUIKeyboardMappings) gi.GUIKeyboardMappings['void DisableEscapeKeyUsage()']();
            if (ModConfigMenu.#popup) ModConfigMenu.#popup = null;
            else ModConfigMenu.#Close();
        }
    }

    // Descartar, Padrão e Aplicar, com os botões e os textos da tela de
    // atribuições do teclado do jogo. Padrão volta o mod da aba ao padrão no
    // rascunho; só o Aplicar grava.
    static #DrawActions(T, mod) {
        const K = T.Mappings.Instance;
        const entries = mod ? ModConfigMenu.#Entries(mod) : [];
        const buttons = [
            ['discard', K.ResetSettings, 'Mobile.Discard', false],
            ['defaults', K.Defaults, 'Mobile.Default', !entries.length || ConfigLoader.AtDefaults(entries)],
            ['apply', K.Close, 'Mobile.ApplyChanges', !ConfigLoader.Pending()],
        ];
        let clicked = null;
        for (const [id, layout, text, disabled] of buttons) {
            const state = Number(T.Button[ModConfigMenu.#DRAW](layout, null, ModConfigMenu.#Language(text), disabled,
                ModConfigMenu.#scales[id], false, ModConfigMenu.#Focus(), false, false));
            if (state === ModConfigMenu.#CLICKED && !disabled) clicked = id;
        }
        return clicked;
    }

    static #DrawTabs(T, S, mods, selected) {
        const C = S.Categories;
        T.Panel[ModConfigMenu.#PANEL](C.Backing, false, null, null, null);
        const shape = ModConfigMenu.#tabShape || (ModConfigMenu.#tabShape = ModConfigMenu.#TabShape(C));
        const n = mods.length;
        if (n === 1) {
            const tab = ModConfigMenu.#Tab(T, shape.middle, mods[0], 'middle', shape.iconPx);
            tab.layout.Location = Vector2.new(shape.start.Location.X, shape.start.Location.Y);
            T.Button[ModConfigMenu.#DRAW](tab.layout, null, mods[0].name || mods[0].id, false, tab.scale,
                true, ModConfigMenu.#Focus(), false, false);
            return mods[0];
        }
        // A fileira rola na horizontal quando as abas passam da largura das
        // abas do jogo: arrastar com o dedo, com embalo ao soltar, como a lista.
        const xs = [];
        let x = shape.start.Location.X;
        for (let i = 0; i < n; i++) {
            const kind = i === 0 ? 'start' : i === n - 1 ? 'end' : 'middle';
            if (i > 0) x += kind === 'end' ? shape.toEnd : i === 1 ? shape.toMiddle : shape.middleStep;
            xs.push({ kind, x });
        }
        const at = (layout, lx) => T.Calc[ModConfigMenu.#ANCHORED](layout.AnchorControl, layout.Anchor, Vector2.new(lx, layout.Location.Y));
        const start = shape.start;
        // A posição de um botão é o canto de cima à esquerda dele.
        const origin = at(start, start.Location.X);
        const perUnit = (at(start, start.Location.X + 100).X - origin.X) / 100 || 1;
        // A área visível vai do começo da primeira aba até a borda direita da
        // lista de opções, que é a do fundo da fileira (o C.Backing tira o
        // tamanho de outra âncora: o Size dele é 0).
        const bounds = ModConfigMenu.#ListBounds(T, S);
        const left = origin.X;
        const right = bounds.left + bounds.width;
        const last = xs[n - 1];
        const contentRight = origin.X + (last.x - start.Location.X) * perUnit + shape[last.kind].overloadedSize.X;
        const height = start.overloadedSize.Y;
        const area = ModConfigMenu.#Rect(left, origin.Y, right - left, height);
        const max = Math.max(0, contentRight - right);
        ModConfigMenu.#TabDrag(T, area, max);

        let chosen = selected;
        const clip = max > 0;
        // O corte é só nas laterais: a aba escolhida e o realce passam da
        // altura da fileira.
        const band = ModConfigMenu.#Rect(left, origin.Y - height, right - left, height * 3);
        if (clip) T.SpriteBatchItem[ModConfigMenu.#CLIP](band, band, Terraria.Main.spriteBatch, false);
        try {
            mods.forEach((mod, i) => {
                const { kind, x: tx } = xs[i];
                const tab = ModConfigMenu.#Tab(T, shape[kind], mod, kind, shape.iconPx);
                tab.layout.Location = Vector2.new(tx - ModConfigMenu.#tabScroll / perUnit, start.Location.Y);
                const on = mod === selected;
                const state = Number(T.Button[ModConfigMenu.#DRAW](tab.layout, null, mod.name || mod.id, false, tab.scale,
                    on, ModConfigMenu.#Focus(), false, false));
                if (state === ModConfigMenu.#CLICKED && ModConfigMenu.#tabPressIn && !ModConfigMenu.#tabDragged
                    && !ModConfigMenu.#Blocked() && !on) {
                    chosen = mod;
                    ModConfigMenu.#ResetScroll();
                    SoundEngine.PlaySound(ModConfigMenu.#TICK);
                }
            });
        } finally {
            if (clip) T.SpriteBatchItem['void DisabledClipping()']();
        }
        // Só depois dos botões: o toque que termina um arraste não vira clique
        // na aba onde o dedo saiu.
        if (!Terraria.Main.mouseLeft) {
            ModConfigMenu.#tabDragged = false;
            ModConfigMenu.#tabPressIn = false;
        }
        return chosen;
    }

    static #TabDrag(T, area, max) {
        const M = Terraria.Main;
        T.Regions.Instance[ModConfigMenu.#REGION](area);
        const inside = M.mouseX >= area.X && M.mouseX <= area.X + area.Width
            && M.mouseY >= area.Y && M.mouseY <= area.Y + area.Height;
        if (M.mouseLeft) {
            if (ModConfigMenu.#tabPressX === null) {
                ModConfigMenu.#tabPressX = M.mouseX;
                ModConfigMenu.#tabLastX = M.mouseX;
                ModConfigMenu.#tabDragged = false;
                ModConfigMenu.#tabPressIn = inside && !ModConfigMenu.#guard && !ModConfigMenu.#Modal();
                ModConfigMenu.#tabMomentum = 0;
            } else if (max > 0 && ModConfigMenu.#tabPressIn
                && Math.abs(M.mouseX - ModConfigMenu.#tabPressX) > ModConfigMenu.#DRAG_PX) {
                ModConfigMenu.#tabDragged = true;
            }
            if (ModConfigMenu.#tabDragged) {
                const delta = ModConfigMenu.#tabLastX - M.mouseX;
                ModConfigMenu.#tabScroll += delta;
                ModConfigMenu.#tabMomentum = delta;
            }
            ModConfigMenu.#tabLastX = M.mouseX;
        } else {
            ModConfigMenu.#tabPressX = null;
            ModConfigMenu.#tabLastX = null;
            if (Math.abs(ModConfigMenu.#tabMomentum) > 0.5) {
                ModConfigMenu.#tabScroll += ModConfigMenu.#tabMomentum;
                ModConfigMenu.#tabMomentum *= ModConfigMenu.#FRICTION;
            } else {
                ModConfigMenu.#tabMomentum = 0;
            }
        }
        if (ModConfigMenu.#tabScroll < 0 || ModConfigMenu.#tabScroll > max) {
            ModConfigMenu.#tabScroll = Math.max(0, Math.min(ModConfigMenu.#tabScroll, max));
            ModConfigMenu.#tabMomentum = 0;
        }
    }

    static #TabShape(C) {
        const general = C.General;
        const row = [];
        for (const k of ['General', 'Cursor', 'Video', 'Language', 'Interface', 'Info', 'Gameplay', 'Sound',
            'Multiplayer', 'GraphicalQuality', 'KeyboardMouse', 'GameplayControls']) {
            const layout = C[k];
            if (!layout) continue;
            const p = layout.Location;
            if (Math.abs(p.Y - general.Location.Y) >= 1 || p.X < general.Location.X - 1) continue;
            if (row.some((r) => Math.abs(r.Location.X - p.X) < 1)) continue;
            row.push(layout);
        }
        row.sort((a, b) => a.Location.X - b.Location.X);
        const x = (i) => row[i].Location.X;
        const step = row.length > 1 ? x(1) - x(0) : general.overloadedSize.X + 4;
        let last = 0;
        while (last + 1 < row.length && Math.abs(x(last + 1) - x(last) - step) < 4) last++;
        const group = row.slice(0, last + 1);
        const gx = (i) => group[i].Location.X;
        const k = group.length;

        const theirs = general.IconTexture ? general.IconTexture.Texture : null;
        const iconPx = (theirs ? theirs.Height : 32) * (general.ForceIconScale ? general.ForcedIconScale : 1);

        return {
            iconPx,
            start: group[0] || general,
            middle: k > 2 ? group[1] : group[0] || general,
            end: group[k - 1] || general,
            toMiddle: k > 1 ? gx(1) - gx(0) : step,
            middleStep: k > 2 ? gx(2) - gx(1) : step,
            toEnd: k > 1 ? gx(k - 1) - gx(k - 2) : step,
        };
    }

    static #Tab(T, template, mod, kind, iconPx) {
        const key = mod.uuid + ':' + kind;
        let tab = ModConfigMenu.#tabs.get(key);
        if (tab) return tab;
        const layout = template['object MemberwiseClone()']();
        Safe.Run('ModConfigMenu: ícone de ' + (mod.name || mod.id), () => {
            const texture = ModConfigMenu.#ModIcon(mod);
            const icon = T.TextureLayout.new();
            icon['void .ctor()']();
            icon.TextureId = 'bunny/mod/' + mod.uuid;
            icon._lastTextureId = icon.TextureId;
            icon._texture = texture;
            layout.IconTexture = icon;
            layout.ForceIconScale = true;
            layout.ForcedIconScale = iconPx / Math.max(1, texture.Height);
        });
        tab = { layout, scale: new Ref(1) };
        ModConfigMenu.#tabs.set(key, tab);
        return tab;
    }

    static #ModIcon(mod) {
        let texture = ModConfigMenu.#icons.get(mod.uuid);
        if (texture) return texture;
        const file = mod.root ? bl.path.join(mod.root, 'icon.png') : null;
        texture = file && bl.file.exists(file) ? bl.loadTexture(file) : bl.builtinTexture('bunnyHead').Value;
        ModConfigMenu.#icons.set(mod.uuid, texture);
        return texture;
    }

    static #Parts(S) {
        if (ModConfigMenu.#parts) return ModConfigMenu.#parts;
        const copy = (x) => x['object MemberwiseClone()']();
        const toScreen = (x) => {
            x.AnchorControl = ModConfigMenu.#SCREEN;
            x.Anchor = ModConfigMenu.#TOP_LEFT;
            return x;
        };
        const t = S.ToggleTemplate;
        const p = S.PulldownTemplate;
        const empty = toScreen(copy(t.Option1Label));
        empty.Alignment = ModConfigMenu.#TOP_LEFT;
        const header = toScreen(copy(p.PulldownTitle));
        header.Alignment = ModConfigMenu.#CENTER;
        const option = copy(p.Option1);
        option.AnchorControl = p.PulldownButton.AnchorControl;
        option.Anchor = p.PulldownButton.Anchor;
        return ModConfigMenu.#parts = {
            empty,
            header,
            divider: toScreen(copy(S.MenuDivider)),
            choiceOn: copy(t.Option1Enabled),
            choiceOff: copy(t.Option1Disabled),
            swatch: copy(t.Option1Enabled),
            cycle: copy(S.SliderTemplate.Value),
            label: copy(t.Option1Label),
            greyout: toScreen(copy(p.PulldownGreyout)),
            popBacking: toScreen(copy(p.PulldownBacking)),
            option,
        };
    }

    static #Rows(S, mod) {
        const rows = [];
        if (!mod) return rows;
        const height = S.ToggleTemplate.ToggleButton.overloadedSize.Y + ModConfigMenu.#GAP;
        for (const entry of ModConfigMenu.#Entries(mod)) {
            for (const o of entry.options) {
                const key = entry.name + '.' + o.key;
                if (o.type === 'color') {
                    for (const part of ['color', 'hue', 'saturation', 'lightness']) rows.push({ kind: part, entry, o, key: key + ':' + part, height });
                } else {
                    rows.push({ kind: o.type, entry, o, key, height: o.type === 'header' ? height + ModConfigMenu.#HEADER_EXTRA : height });
                }
            }
        }
        return rows;
    }

    static #DrawList(S, mod) {
        const T = ModConfigMenu.#T;
        const P = ModConfigMenu.#Parts(S);
        const at = (layout) => T.Calc[ModConfigMenu.#ANCHORED](layout.AnchorControl, layout.Anchor, layout.Location);
        const top = at(S.MenuDivider).Y + 8;
        const bottom = at(S.MenuDivider2).Y - 6;
        const { left, width } = ModConfigMenu.#ListBounds(T, S);
        ModConfigMenu.#listTop = top;
        ModConfigMenu.#listBottom = bottom;
        const rows = ModConfigMenu.#Rows(S, mod);

        if (!rows.length) {
            P.empty.Location = Vector2.new(left + 12, top + 10);
            T.String[ModConfigMenu.#TEXT](P.empty, ModConfigMenu.#Text('empty'), P.empty.Color, false);
            return;
        }

        const total = rows.reduce((sum, row) => sum + row.height, 0) - ModConfigMenu.#GAP;
        const area = ModConfigMenu.#Rect(left, top, width, bottom - top);
        ModConfigMenu.#Scroll(T, area, total, left, top, width, bottom);

        const anchor = T.Anchor;
        const saved = anchor[ModConfigMenu.#GET_GRID]();
        T.SpriteBatchItem[ModConfigMenu.#CLIP](area, area, Terraria.Main.spriteBatch, true);
        try {
            let y = top - ModConfigMenu.#scroll;
            for (const row of rows) {
                const h = row.height - ModConfigMenu.#GAP;
                if (y + h > top && y < bottom) {
                    anchor[ModConfigMenu.#SET_GRID](ModConfigMenu.#Rect(left, y, width, h));
                    Safe.Run('ModConfig ' + row.key, () => ModConfigMenu.#DrawOption(T, S, P, row));
                }
                y += row.height;
            }
        } finally {
            T.SpriteBatchItem['void DisabledClipping()']();
            anchor[ModConfigMenu.#SET_GRID](saved);
        }
    }

    static #ListBounds(T, S) {
        const width = T.Ach.Instance.ItemBacking.Size.X;
        const left = T.Calc[ModConfigMenu.#ANCHORED](S.Backing.AnchorControl, S.Backing.Anchor, Vector2.new(0, 0)).X - width / 2;
        return { left, width };
    }

    static #Rect(x, y, width, height) {
        const rect = Microsoft.Xna.Framework.Rectangle.new();
        rect['void .ctor(int x, int y, int width, int height)'](Math.round(x), Math.round(y), Math.round(width), Math.round(height));
        return rect;
    }

    static #Modal() {
        return !!ModConfigMenu.#popup;
    }

    static #Blocked() {
        return ModConfigMenu.#guard || ModConfigMenu.#dragged || ModConfigMenu.#Modal();
    }

    static #RowBlocked() {
        return ModConfigMenu.#Blocked() || !ModConfigMenu.#pressInList;
    }

    static #ResetScroll() {
        ModConfigMenu.#scroll = 0;
        ModConfigMenu.#momentum = 0;
    }

    static #ReleaseSliders() {
        ModConfigMenu.#slider = null;
        for (const st of ModConfigMenu.#rowState.values()) {
            if (st.drag) st.drag.wasDragging = false;
        }
    }

    static #Scroll(T, area, total, left, top, width, bottom) {
        const M = Terraria.Main;
        T.Regions.Instance[ModConfigMenu.#REGION](area);
        const max = Math.max(0, total - (bottom - top));
        const inside = M.mouseX >= left && M.mouseX <= left + width && M.mouseY >= top && M.mouseY <= bottom;

        const fresh = M.mouseLeft && ModConfigMenu.#pressY === null;
        if (M.mouseLeft) {
            if (fresh) {
                ModConfigMenu.#pressY = M.mouseY;
                ModConfigMenu.#lastY = M.mouseY;
                ModConfigMenu.#dragged = false;
                ModConfigMenu.#pressInList = inside && !ModConfigMenu.#guard && !ModConfigMenu.#Modal();
                ModConfigMenu.#momentum = 0;
            } else if (!ModConfigMenu.#slider && ModConfigMenu.#pressInList
                && Math.abs(M.mouseY - ModConfigMenu.#pressY) > ModConfigMenu.#DRAG_PX) {
                ModConfigMenu.#dragged = true;
            }
            if (ModConfigMenu.#dragged) {
                const delta = ModConfigMenu.#lastY - M.mouseY;
                ModConfigMenu.#scroll += delta;
                ModConfigMenu.#momentum = delta;
            }
            ModConfigMenu.#lastY = M.mouseY;
        } else {
            ModConfigMenu.#pressY = null;
            ModConfigMenu.#lastY = null;
            ModConfigMenu.#slider = null;
            ConfigLoader.Flush();
            if (Math.abs(ModConfigMenu.#momentum) > 0.5) {
                ModConfigMenu.#scroll += ModConfigMenu.#momentum;
                ModConfigMenu.#momentum *= ModConfigMenu.#FRICTION;
            } else {
                ModConfigMenu.#momentum = 0;
            }
        }
        if (ModConfigMenu.#scroll < 0 || ModConfigMenu.#scroll > max) {
            ModConfigMenu.#scroll = Math.max(0, Math.min(ModConfigMenu.#scroll, max));
            ModConfigMenu.#momentum = 0;
        }
    }

    static #Label(T, P, template, x, width, text, color, centered = false) {
        const label = P.label;
        label.AnchorControl = template.AnchorControl;
        label.Anchor = template.Anchor;
        label.Alignment = centered ? (template.Alignment & ~7) | 2 : template.Alignment;
        const font = template['SpriteFont GetFont()']();
        const measured = font ? font['Vector2 MeasureString(string text)'](text).X : 0;
        const room = Math.max(8, width - 8);
        label.Scale = measured > 0 ? Math.min(template.Scale, room / measured) : template.Scale;
        label.Location = Vector2.new(x, template.Location.Y);
        T.String[ModConfigMenu.#TEXT](label, text, color, false);
    }

    static #Fit(layout, text, width) {
        const label = layout.Label;
        const font = label ? label['SpriteFont GetFont()']() : null;
        if (!font) return text;
        const measure = (s) => font['Vector2 MeasureString(string text)'](s).X * label.Scale;
        if (measure(text) <= width) return text;
        let cut = text;
        while (cut.length > 1 && measure(cut + '...') > width) cut = cut.slice(0, -1);
        return cut + '...';
    }

    static #State(row) {
        let st = ModConfigMenu.#rowState.get(row.key);
        if (!st) ModConfigMenu.#rowState.set(row.key, st = { scale: new Ref(1), scale2: new Ref(1), value: new Ref(0), drag: null });
        return st;
    }

    static #Segments(T, t, n) {
        const a = t.Option1Enabled, b = t.Option2Enabled;
        const y = a.Location.Y;
        const anchorPos = (x) => T.Calc[ModConfigMenu.#ANCHORED](a.AnchorControl, a.Anchor, Vector2.new(x, y));
        const dir = Math.sign((anchorPos(a.Location.X).X - anchorPos(b.Location.X).X) / (a.Location.X - b.Location.X)) || 1;
        const ua = a.Location.X * dir, ub = b.Location.X * dir;
        const right = Math.max(ua + a.Size.X / 2, ub + b.Size.X / 2);
        const span = right - Math.min(ua - a.Size.X / 2, ub - b.Size.X / 2);
        const left = right - span * Math.max(1, n / 2);
        const each = (right - left) / n;
        return { y, each, height: a.Size.Y, anchorPos, xOf: (i) => (left + each * (i + 0.5)) * dir };
    }

    static #DrawOption(T, S, P, row) {
        const enabled = ConfigLoader.IsEnabled(row.entry, row.o);
        switch (row.kind) {
            case 'header': return ModConfigMenu.#DrawHeader(T, P, row);
            case 'toggle': return ModConfigMenu.#DrawToggle(T, S, P, row, enabled);
            case 'range': return ModConfigMenu.#DrawRange(T, S, row, enabled);
            case 'radio': return ModConfigMenu.#DrawRadio(T, S, P, row, enabled);
            case 'dropdown': return ModConfigMenu.#DrawDropdown(T, S, row, enabled);
            case 'cycle': return ModConfigMenu.#DrawCycle(T, S, P, row, enabled);
            case 'color': return ModConfigMenu.#DrawColor(T, S, P, row, enabled);
            case 'hue':
            case 'saturation':
            case 'lightness': return ModConfigMenu.#DrawColorPart(T, S, row, enabled);
            case 'button':
            case 'link': return ModConfigMenu.#DrawAction(T, S, row, enabled);
        }
    }

    static #DrawHeader(T, P, row) {
        const r = T.Anchor[ModConfigMenu.#GET_GRID]();
        const panel = T.Panel[ModConfigMenu.#PANEL];
        const divider = P.divider;
        divider.Size = Vector2.new(r.Width, divider.Size.Y);
        divider.Location = Vector2.new(r.X + r.Width / 2, r.Y + r.Height - divider.Size.Y / 2);
        panel(divider, false, null, null, null);
        const { align = 'center', color } = row.o;
        const x = align === 'left' ? r.X + ModConfigMenu.#HEADER_PAD
            : align === 'right' ? r.X + r.Width - ModConfigMenu.#HEADER_PAD
            : r.X + r.Width / 2;
        P.header.Alignment = ModConfigMenu.#MIDDLE | ModConfigMenu.#ALIGN_X[align];
        P.header.Location = Vector2.new(x, r.Y + r.Height / 2 - 2);
        T.String[ModConfigMenu.#TEXT](P.header, row.o.label, ModConfigMenu.#HeaderColor(color), false);
    }

    static #HeaderColor(hex) {
        if (!hex) return Color.new(255, 255, 255);
        const n = parseInt(hex.slice(1), 16);
        return Color.new((n >> 16) & 255, (n >> 8) & 255, n & 255);
    }

    static #DrawToggle(T, S, P, row, enabled) {
        const { entry, o } = row;
        const st = ModConfigMenu.#State(row);
        const t = enabled ? S.ToggleTemplate : S.DisabledToggleTemplate;
        const panel = T.Panel[ModConfigMenu.#PANEL];
        const state = Number(T.Button[ModConfigMenu.#DRAW](t.ToggleButton, null, o.label, !enabled, st.scale, false, ModConfigMenu.#Focus(), false, false));
        if (enabled && state === ModConfigMenu.#CLICKED && !ModConfigMenu.#RowBlocked()) {
            ConfigLoader.Set(entry, o, !ConfigLoader.View(entry)[o.key]);
            SoundEngine.PlaySound(ModConfigMenu.#TICK);
        }
        const on = !!ConfigLoader.View(entry)[o.key];
        if (on) {
            panel(t.Option2Disabled, false, null, null, null);
            panel(t.Option1Enabled, false, null, null, null);
        } else {
            panel(t.Option1Disabled, false, null, null, null);
            panel(t.Option2Enabled, false, null, null, null);
        }
        const onColor = on && enabled ? t.Option1Label.Color : t.Option1DisabledLabelColour;
        const offColor = !on && enabled ? t.Option2Label.Color : t.Option2DisabledLabelColour;
        ModConfigMenu.#Label(T, P, t.Option1Label, t.Option1Label.Location.X, t.Option1Enabled.Size.X,
            ModConfigMenu.#Language('Mobile.ToggleSettingOn'), onColor);
        ModConfigMenu.#Label(T, P, t.Option2Label, t.Option2Label.Location.X, t.Option2Enabled.Size.X,
            ModConfigMenu.#Language('Mobile.ToggleSettingOff'), offColor);
    }

    static #DrawSlider(T, S, row, title, enabled, layout, value01, draw) {
        const t = S.SliderTemplate;
        const st = ModConfigMenu.#State(row);
        T.Button[ModConfigMenu.#DRAW](t.Title, null, title, !enabled, st.scale, false, ModConfigMenu.#Focus(), false, false);
        if (!st.drag) {
            st.drag = T.DragState.new();
            st.drag['void .ctor()']();
        }
        st.value.value = value01;
        const M = Terraria.Main;
        const r = T.Anchor[ModConfigMenu.#GET_GRID]();
        const onTrack = (x) => T.Slider[ModConfigMenu.#OVER_TRACK](Vector2.new(x, r.Y + r.Height / 2), layout);
        const pad = r.Height / 2;
        if (enabled && !ModConfigMenu.#slider && M.mouseLeft && ModConfigMenu.#pressInList
            && !ModConfigMenu.#dragged && !ModConfigMenu.#guard && !ModConfigMenu.#Modal()
            && M.mouseY >= r.Y && M.mouseY <= r.Y + r.Height
            && (onTrack(M.mouseX) || onTrack(M.mouseX - pad) || onTrack(M.mouseX + pad))) ModConfigMenu.#slider = row.key;
        const mine = ModConfigMenu.#slider === row.key;
        const dragging = !!draw(layout, !mine, st.value, st.drag, mine && M.mouseLeft);
        st.drag.wasDragging = dragging && mine;
        return mine ? st.value.value : null;
    }

    static #DrawValue(T, S, text, enabled) {
        const t = S.SliderTemplate;
        T.String[ModConfigMenu.#TEXT](t.Value, text, enabled ? t.Value.Color : t.DisabledValueColor, false);
    }

    static #DrawRange(T, S, row, enabled) {
        const { entry, o } = row;
        const draw = (layout, disable, ref, drag, force) => T.Slider[ModConfigMenu.#SLIDER](layout, disable, ref, drag, null, force, -1, -1, false);
        const v = ModConfigMenu.#DrawSlider(T, S, row, o.label, enabled, S.SliderTemplate.Option,
            (ConfigLoader.View(entry)[o.key] - o.min) / (o.max - o.min), draw);
        if (v !== null) ConfigLoader.Set(entry, o, o.min + v * (o.max - o.min));
        const digits = String(o.step).includes('.') ? String(o.step).split('.')[1].length : 0;
        ModConfigMenu.#DrawValue(T, S, ConfigLoader.View(entry)[o.key].toFixed(digits) + (o.suffix || ''), enabled);
    }

    static #DrawRadio(T, S, P, row, enabled) {
        const { entry, o } = row;
        const st = ModConfigMenu.#State(row);
        const t = S.ToggleTemplate;
        const panel = T.Panel[ModConfigMenu.#PANEL];
        const state = Number(T.Button[ModConfigMenu.#DRAW](t.ToggleButton, null, o.label, !enabled, st.scale, false, ModConfigMenu.#Focus(), false, false));
        const n = o.choices.length;
        const seg = ModConfigMenu.#Segments(T, t, n);
        if (enabled && state === ModConfigMenu.#CLICKED && !ModConfigMenu.#RowBlocked()) {
            const M = Terraria.Main;
            let picked = -1;
            for (let i = 0; i < n; i++) {
                const c = seg.anchorPos(seg.xOf(i));
                if (Math.abs(M.mouseX - c.X) <= seg.each / 2 && Math.abs(M.mouseY - c.Y) <= seg.height / 2 + 4) picked = i;
            }
            if (picked < 0) picked = (o.choices.indexOf(ConfigLoader.View(entry)[o.key]) + 1) % n;
            ConfigLoader.Set(entry, o, o.choices[picked]);
            SoundEngine.PlaySound(ModConfigMenu.#TICK);
        }
        const selected = o.choices.indexOf(ConfigLoader.View(entry)[o.key]);
        const order = [...Array(n).keys()].filter((i) => i !== selected);
        if (selected >= 0) order.push(selected);
        for (const i of order) {
            const piece = i === selected && enabled ? P.choiceOn : P.choiceOff;
            piece.Location = Vector2.new(seg.xOf(i), seg.y);
            piece.Size = Vector2.new(seg.each - 2, seg.height);
            panel(piece, false, null, null, null);
        }
        for (let i = 0; i < n; i++) {
            ModConfigMenu.#Label(T, P, t.Option1Label, seg.xOf(i), seg.each, o.choiceLabels[i],
                i === selected && enabled ? t.Option1Label.Color : t.Option1DisabledLabelColour, true);
        }
    }

    static #DrawTitle(T, S, text, enabled) {
        const p = S.PulldownTemplate;
        T.String[ModConfigMenu.#TEXT](p.PulldownTitle, text, enabled ? p.PulldownTitle.Color : p.DisabledPulldownLabelColour, false);
    }

    static #DrawDropdown(T, S, row, enabled) {
        const { entry, o } = row;
        const st = ModConfigMenu.#State(row);
        const p = S.PulldownTemplate;
        const open = ModConfigMenu.#popup && ModConfigMenu.#popup.row.key === row.key;
        const value = o.choiceLabels[o.choices.indexOf(ConfigLoader.View(entry)[o.key])] || '';
        const state = Number(T.Button[ModConfigMenu.#DRAW](p.PulldownButton, null, value, !enabled, st.scale, open, ModConfigMenu.#Focus(), false, false));
        ModConfigMenu.#DrawTitle(T, S, o.label, enabled);
        if (enabled && state === ModConfigMenu.#CLICKED && !ModConfigMenu.#RowBlocked()) {
            ModConfigMenu.#popup = {
                row,
                rect: T.Anchor[ModConfigMenu.#GET_GRID](),
                down: false,
                downInside: false,
                armed: false,
                scales: o.choices.map(() => new Ref(1)),
            };
            SoundEngine.PlaySound(ModConfigMenu.#TICK);
        }
    }

    static #DrawPopup(T, S) {
        const pop = ModConfigMenu.#popup;
        if (!pop) return;
        const { entry, o } = pop.row;
        const P = ModConfigMenu.#Parts(S);
        const p = S.PulldownTemplate;
        const M = Terraria.Main;
        const panel = T.Panel[ModConfigMenu.#PANEL];
        const batch = M.spriteBatch;
        batch['void End()']();
        batch[ModConfigMenu.#BEGIN](0, null, null, null, null, null, null, true);
        T.Regions.Instance[ModConfigMenu.#REGION](ModConfigMenu.#Rect(-4000, -4000, 12000, 12000));
        P.greyout.Location = Vector2.new(0, 0);
        P.greyout.Size = Vector2.new(12000, 12000);
        panel(P.greyout, false, null, null, null);

        const h = p.Option1.overloadedSize.Y;
        const step = p.Option2.Location.Y - p.Option1.Location.Y;
        const label = p.Option1.Label;
        const font = label ? label['SpriteFont GetFont()']() : null;
        const widest = font ? Math.max(...o.choiceLabels.map((c) => font['Vector2 MeasureString(string text)'](c).X * label.Scale)) : 0;
        const width = Math.min(pop.rect.Width, Math.max(p.Option1.overloadedSize.X, widest + 40));
        const n = o.choices.length;
        const blockH = (n - 1) * step + h;
        const x0 = pop.rect.X + pop.rect.Width - width - 6;
        const frame = T.Panel['Rectangle Region(Panel_Layout layout)'](S.Backing);
        const lowest = frame.Y + frame.Height - blockH - ModConfigMenu.#POPUP_MARGIN;
        let y0 = pop.rect.Y + pop.rect.Height + 4;
        if (y0 > lowest) y0 = pop.rect.Y - 4 - blockH;
        y0 = Math.max(frame.Y + ModConfigMenu.#POPUP_MARGIN, Math.min(y0, lowest));

        const tl = p.PulldownBackingTLOffset, br = p.PulldownBackingBROffset;
        const bx = x0 + tl.X, by = y0 + tl.Y, bw = width - tl.X + br.X, bh = blockH - tl.Y + br.Y;
        P.popBacking.Location = Vector2.new(bx + bw / 2, by + bh / 2);
        P.popBacking.Size = Vector2.new(bw, bh);
        panel(P.popBacking, false, null, null, null);

        const anchor = T.Anchor;
        const saved = anchor[ModConfigMenu.#GET_GRID]();
        P.option.overloadedSize = Vector2.new(width, h);
        P.option.Location = Vector2.new(-width / 2, -h / 2);
        const selected = o.choices.indexOf(ConfigLoader.View(entry)[o.key]);
        let close = false;
        try {
            for (let i = 0; i < n; i++) {
                anchor[ModConfigMenu.#SET_GRID](ModConfigMenu.#Rect(x0, y0 + i * step, width, h));
                const state = Number(T.Button[ModConfigMenu.#DRAW](P.option, null, o.choiceLabels[i], false, pop.scales[i],
                    i === selected, ModConfigMenu.#Focus(), false, false));
                if (state === ModConfigMenu.#CLICKED && pop.armed && !ModConfigMenu.#guard) {
                    ConfigLoader.Set(entry, o, o.choices[i]);
                    SoundEngine.PlaySound(ModConfigMenu.#TICK);
                    close = true;
                }
            }
        } finally {
            anchor[ModConfigMenu.#SET_GRID](saved);
        }

        const inside = M.mouseX >= bx && M.mouseX <= bx + bw && M.mouseY >= by && M.mouseY <= by + bh;
        if (!M.mouseLeft && !pop.down) pop.armed = true;
        if (M.mouseLeft && pop.armed) {
            if (!pop.down) {
                pop.down = true;
                pop.downInside = inside;
            }
        } else if (pop.down) {
            pop.down = false;
            if (!pop.downInside) close = true;
        }
        if (close) {
            ModConfigMenu.#popup = null;
            ModConfigMenu.#Guard();
        }
    }

    static #DrawCycle(T, S, P, row, enabled) {
        const { entry, o } = row;
        const st = ModConfigMenu.#State(row);
        const index = o.choices.indexOf(ConfigLoader.View(entry)[o.key]);
        const state = Number(T.Button[ModConfigMenu.#DRAW](S.ToggleTemplate.ToggleButton, null, o.label, !enabled, st.scale, false, ModConfigMenu.#Focus(), false, false));
        const value = S.SliderTemplate.Value;
        P.cycle.Location = Vector2.new(ModConfigMenu.#CYCLE_X, value.Location.Y);
        T.String[ModConfigMenu.#TEXT](P.cycle, '<  ' + (o.choiceLabels[index] || '') + '  >', enabled ? value.Color : S.SliderTemplate.DisabledValueColor, false);
        if (enabled && state === ModConfigMenu.#CLICKED && !ModConfigMenu.#RowBlocked()) {
            ConfigLoader.Set(entry, o, o.choices[(index + 1) % o.choices.length]);
            SoundEngine.PlaySound(ModConfigMenu.#TICK);
        }
    }

    static #HexToHsl(hex) {
        const r = parseInt(hex.slice(1, 3), 16) / 255;
        const g = parseInt(hex.slice(3, 5), 16) / 255;
        const b = parseInt(hex.slice(5, 7), 16) / 255;
        const max = Math.max(r, g, b), min = Math.min(r, g, b);
        const l = (max + min) / 2;
        if (max === min) return [0, 0, l];
        const d = max - min;
        const s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        let h;
        if (max === r) h = (g - b) / d + (g < b ? 6 : 0);
        else if (max === g) h = (b - r) / d + 2;
        else h = (r - g) / d + 4;
        return [h / 6, s, l];
    }

    static #HslToHex(h, s, l) {
        const channel = (p, q, t) => {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1 / 6) return p + (q - p) * 6 * t;
            if (t < 1 / 2) return q;
            if (t < 2 / 3) return p + (q - p) * (2 / 3 - t) * 6;
            return p;
        };
        let r = l, g = l, b = l;
        if (s > 0) {
            const q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            const p = 2 * l - q;
            r = channel(p, q, h + 1 / 3);
            g = channel(p, q, h);
            b = channel(p, q, h - 1 / 3);
        }
        const hex = (v) => Math.round(Math.min(1, Math.max(0, v)) * 255).toString(16).padStart(2, '0');
        return ('#' + hex(r) + hex(g) + hex(b)).toUpperCase();
    }

    static #ColorState(row) {
        const key = row.entry.name + '.' + row.o.key;
        let cs = ModConfigMenu.#colorState.get(key);
        if (!cs) ModConfigMenu.#colorState.set(key, cs = { hex: null, hsl: [0, 0, 1] });
        const hex = ConfigLoader.View(row.entry)[row.o.key];
        if (cs.hex !== hex) {
            cs.hex = hex;
            cs.hsl = ModConfigMenu.#HexToHsl(hex);
        }
        return cs;
    }

    static #DrawColor(T, S, P, row, enabled) {
        const { entry, o } = row;
        const st = ModConfigMenu.#State(row);
        const t = S.ToggleTemplate;
        T.Button[ModConfigMenu.#DRAW](t.ToggleButton, null, o.label, !enabled, st.scale, false, ModConfigMenu.#Focus(), false, false);
        const hex = ConfigLoader.View(entry)[o.key];
        const seg = ModConfigMenu.#Segments(T, t, 1);
        P.swatch.Location = Vector2.new(seg.xOf(0), seg.y);
        P.swatch.Size = Vector2.new(seg.each - 2, seg.height);
        const r = parseInt(hex.slice(1, 3), 16), g = parseInt(hex.slice(3, 5), 16), b = parseInt(hex.slice(5, 7), 16);
        T.Panel[ModConfigMenu.#PANEL](P.swatch, false, null, null, null);
        const box = T.Panel['Rectangle Region(Panel_Layout layout)'](P.swatch);
        const inset = ModConfigMenu.#SWATCH_INSET;
        Terraria.Main.spriteBatch['void Draw(Texture2D texture, Rectangle destinationRectangle, Color color)'](
            Terraria.GameContent.TextureAssets.MagicPixel.Value,
            ModConfigMenu.#Rect(box.X + inset, box.Y + inset, box.Width - inset * 2, box.Height - inset * 2),
            Color.new(r, g, b, 255));
        const value = S.SliderTemplate.Value;
        T.String[ModConfigMenu.#TEXT](value, hex, enabled ? value.Color : S.SliderTemplate.DisabledValueColor, false);
    }

    static #DrawColorPart(T, S, row, enabled) {
        const { entry, o } = row;
        const t = S.SliderTemplate;
        const M = Terraria.Main;
        const cs = ModConfigMenu.#ColorState(row);
        const index = { hue: 0, saturation: 1, lightness: 2 }[row.kind];
        const title = '   ' + ModConfigMenu.#Language({ hue: 'Mobile.Hue', saturation: 'Mobile.Saturation', lightness: 'Mobile.Lightness' }[row.kind]);
        const layout = { hue: t.HueOption, saturation: t.SaturationOption, lightness: t.LightOption }[row.kind];
        const signature = { hue: ModConfigMenu.#SLIDER, saturation: ModConfigMenu.#SATURATION, lightness: ModConfigMenu.#LIGHTNESS }[row.kind];
        const draw = row.kind === 'hue'
            ? (l, disable, ref, drag, force) => T.Slider[signature](l, disable, ref, drag, null, force, -1, -1, false)
            : (l, disable, ref, drag, force) => T.Slider[signature](l, disable, ref, drag, force);
        const saved = [M.hBar, M.sBar, M.lBar];
        M.hBar = cs.hsl[0];
        M.sBar = cs.hsl[1];
        M.lBar = cs.hsl[2];
        let v;
        try {
            v = ModConfigMenu.#DrawSlider(T, S, row, title, enabled, layout, cs.hsl[index], draw);
        } finally {
            M.hBar = saved[0];
            M.sBar = saved[1];
            M.lBar = saved[2];
        }
        if (v !== null && v !== cs.hsl[index]) {
            cs.hsl[index] = v;
            cs.hex = ModConfigMenu.#HslToHex(cs.hsl[0], cs.hsl[1], cs.hsl[2]);
            ConfigLoader.Set(entry, o, cs.hex);
        }
        ModConfigMenu.#DrawValue(T, S, Math.round(cs.hsl[index] * 100) + '%', enabled);
    }

    static #DrawAction(T, S, row, enabled) {
        const { entry, o } = row;
        const st = ModConfigMenu.#State(row);
        const link = o.type === 'link';
        const t = link ? S.LinkTemplate : S.UIButtonOptionTemplate;
        const main = link ? t.LinkButton : t.Option;
        const caption = ModConfigMenu.#Fit(t.Caption, o.text || '', t.Caption.overloadedSize.X - 16);
        const focus = ModConfigMenu.#Focus();
        const s1 = Number(T.Button[ModConfigMenu.#DRAW](main, null, o.label, !enabled, st.scale, false, focus, false, false));
        const s2 = Number(T.Button[ModConfigMenu.#DRAW](t.Caption, null, caption, !enabled, st.scale2, false, focus, false, false));
        if (!enabled || (s1 !== ModConfigMenu.#CLICKED && s2 !== ModConfigMenu.#CLICKED) || ModConfigMenu.#RowBlocked()) return;
        SoundEngine.PlaySound(ModConfigMenu.#TICK);
        if (link) T.Application['void OpenURL(string url)'](o.url);
        else ConfigLoader.Run(entry, o);
    }
}
