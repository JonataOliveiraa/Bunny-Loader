class HostSettingsMenu {
    static SyncMods = true;
    static AllowTextures = true;

    static #T = null;
    static #rows = null;
    static #scroll = 0;
    static #momentum = 0;
    static #pressY = null;
    static #lastY = null;
    static #dragged = false;
    static #pressIn = false;

    static #CLICKED = 0;
    static #OVER = 1;
    static #DOWN = 2;
    static #DRAG_PX = 8;
    static #FRICTION = 0.92;
    static #TICK = 12;

    static #DRAW = 'GUITransactionButton.InputState Draw(TransactionButton_Layout layout, Item item, string label, bool disabled, ref float scale, bool forcedPressed, bool hasControllerFocus, bool forceOver, bool disablePressedState)';
    static #PANEL = 'void Draw(Panel_Layout layout, bool cursorOver, Nullable<Color> overloadBacking, Nullable<Color> overloadBorder, Nullable<Color> overloadHighlight)';
    static #TEXT = 'void Draw(String_Layout layout, string value, Color overloadedColour, bool multilineAlignmentApplied)';
    static #ANCHORED = 'Vector2 GetAnchoredPosition(ControlAnchor.ControlId anchorControl, LayoutCalculator.AnchorType anchorType, Vector2 position)';
    static #CLIP = 'void EnableClipping(Rectangle inner, Rectangle outer, SpriteBatch batcher, bool vertical)';
    static #REGION = 'bool RegisterInputRegion(Rectangle rect)';
    static #REGION_OF = 'Rectangle Region(Panel_Layout layout)';

    static Install() {
        Hooks.Once('host.settings', () => Safe.Run('HostSettingsMenu.Install', () => HostSettingsMenu.#Hook()));
    }

    static #Hook() {
        const T = HostSettingsMenu.#T = {
            Host: bl.classOf('', 'GUIMultiplayerHost'),
            Layout: bl.classOf('', 'MultiplayerHost_Layout'),
            Button: bl.classOf('', 'GUITransactionButton'),
            String: bl.classOf('', 'GUIString'),
            Panel: bl.classOf('', 'GUIPanel'),
            Instance: bl.classOf('', 'GUIInstance'),
            Input: bl.classOf('', 'XNAUIInputLayer'),
            Regions: bl.classOf('', 'GUIInputRegionManager'),
            Calc: bl.classOf('', 'LayoutCalculator'),
            SpriteBatchItem: Microsoft.Xna.Framework.Graphics.SpriteBatchItem,
        };
        const H = T.Host;

        H['void Draw()'].hook((original, self) => {
            Safe.Run('HostSettingsMenu.Scroll', () => HostSettingsMenu.#Scroll());
            return original(self);
        });

        const rows = [
            ['void DrawMaxPlayers()', (L) => L.MaxPlayers.Title],
            ['void DrawOpenInviteOnly()', (L) => L.InviteOpenToggle.ToggleButton],
            ['void DrawCrossplay()', (L) => L.Crossplay.ToggleButton],
            ['void DrawPassword()', (L) => L.Password],
        ];
        const last = rows[rows.length - 1][0];
        for (const [signature, main] of rows) {
            H[signature].hook((original, self) => {
                const result = HostSettingsMenu.#GameRow(self, original, main(T.Layout.Instance));
                if (signature === last) Safe.Run('HostSettingsMenu.Rows', () => HostSettingsMenu.#DrawRows(self));
                return result;
            });
        }

        for (const signature of ['void Back()', 'void HostServer()']) {
            H[signature].hook((original, self) => {
                HostSettingsMenu.#scroll = 0;
                return original(self);
            });
        }
    }

    static #Text(key) {
        const pt = /^pt/.test(ModLocalization.ActiveCultureName || '');
        switch (key) {
            case 'sync': return pt ? 'Sincronização de mods:' : 'Mod sync:';
            case 'syncDesc': return pt ? 'Quem entrar recebe os mods deste servidor.' : 'Players who join get this server\'s mods.';
            case 'textures': return pt ? 'Permitir texturas:' : 'Allow textures:';
            case 'texturesDesc': return pt ? 'Quem entrar pode usar os próprios pacotes de textura.' : 'Players who join may use their own texture packs.';
        }
        return key;
    }

    static #Rows() {
        if (HostSettingsMenu.#rows) return HostSettingsMenu.#rows;
        const template = HostSettingsMenu.#T.Layout.Instance.Crossplay;
        const row = (key, desc, field) => {
            // A cópia do layout da linha e do botão dela (o botão anda com a
            // rolagem); as peças de dentro seguem o botão e são as do jogo.
            const layout = template['object MemberwiseClone()']();
            layout.ToggleButton = template.ToggleButton['object MemberwiseClone()']();
            // Os rótulos encolhem para caber no botão (ver #Label): cópias,
            // para a escala não passar para as linhas do jogo.
            layout.Option1Label = template.Option1Label['object MemberwiseClone()']();
            layout.Option2Label = template.Option2Label['object MemberwiseClone()']();
            return { key, desc, field, layout, template, scale: new Ref(1) };
        };
        return HostSettingsMenu.#rows = [
            row('sync', 'syncDesc', 'SyncMods'),
            row('textures', 'texturesDesc', 'AllowTextures'),
        ];
    }

    // A distância entre as linhas do jogo (a Senha vem logo depois do Jogo
    // entre plataformas).
    static #Step(L) {
        return L.Password.Location.Y - L.Crossplay.ToggleButton.Location.Y;
    }

    // A área das linhas: da divisória de baixo do mundo até a de cima dos
    // botões, na largura do fundo.
    static #Area() {
        const T = HostSettingsMenu.#T, L = T.Layout.Instance;
        const region = (layout) => T.Panel[HostSettingsMenu.#REGION_OF](layout);
        const back = region(L.Backing);
        const top = region(L.MenuDivider2);
        const bottom = region(L.MenuDivider);
        const y = top.Y + top.Height;
        return HostSettingsMenu.#Rect(back.X, y, back.Width, bottom.Y - y);
    }

    static #Rect(x, y, width, height) {
        const rect = Microsoft.Xna.Framework.Rectangle.new();
        rect['void .ctor(int x, int y, int width, int height)'](Math.round(x), Math.round(y), Math.round(width), Math.round(height));
        return rect;
    }

    static #Max() {
        return HostSettingsMenu.#Rows().length * HostSettingsMenu.#Step(HostSettingsMenu.#T.Layout.Instance);
    }

    // Como a lista da Config. dos Mods: arrastar com o dedo e, ao soltar,
    // seguir no embalo até parar. Arrastando, o cursor fica capturado: soltar
    // em cima de uma linha não vira toque nela.
    static #Scroll() {
        const T = HostSettingsMenu.#T, M = Terraria.Main;
        const area = HostSettingsMenu.#Area();
        T.Regions.Instance[HostSettingsMenu.#REGION](area);
        const max = HostSettingsMenu.#Max();
        const inside = M.mouseX >= area.X && M.mouseX <= area.X + area.Width
            && M.mouseY >= area.Y && M.mouseY <= area.Y + area.Height;
        if (M.mouseLeft) {
            if (HostSettingsMenu.#pressY === null) {
                HostSettingsMenu.#pressY = M.mouseY;
                HostSettingsMenu.#lastY = M.mouseY;
                HostSettingsMenu.#dragged = false;
                HostSettingsMenu.#pressIn = inside;
                HostSettingsMenu.#momentum = 0;
            } else if (HostSettingsMenu.#pressIn && Math.abs(M.mouseY - HostSettingsMenu.#pressY) > HostSettingsMenu.#DRAG_PX) {
                HostSettingsMenu.#dragged = true;
            }
            if (HostSettingsMenu.#dragged) {
                const delta = HostSettingsMenu.#lastY - M.mouseY;
                HostSettingsMenu.#scroll += delta;
                HostSettingsMenu.#momentum = delta;
                T.Input.Instance['void CaptureUICrusorDrag(int dragFromAxis)'](1);
            }
            HostSettingsMenu.#lastY = M.mouseY;
        } else {
            HostSettingsMenu.#pressY = null;
            HostSettingsMenu.#lastY = null;
            HostSettingsMenu.#dragged = false;
            if (Math.abs(HostSettingsMenu.#momentum) > 0.5) {
                HostSettingsMenu.#scroll += HostSettingsMenu.#momentum;
                HostSettingsMenu.#momentum *= HostSettingsMenu.#FRICTION;
            } else {
                HostSettingsMenu.#momentum = 0;
            }
        }
        if (HostSettingsMenu.#scroll < 0 || HostSettingsMenu.#scroll > max) {
            HostSettingsMenu.#scroll = Math.max(0, Math.min(HostSettingsMenu.#scroll, max));
            HostSettingsMenu.#momentum = 0;
        }
    }

    static #Clipped(layout, y, draw) {
        const T = HostSettingsMenu.#T;
        const area = HostSettingsMenu.#Area();
        const x = layout.Location.X, savedY = layout.Location.Y;
        const top = T.Calc[HostSettingsMenu.#ANCHORED](layout.AnchorControl, layout.Anchor, Vector2.new(x, y)).Y;
        const height = layout.overloadedSize.Y;

        if (top + height <= area.Y || top >= area.Y + area.Height) return undefined;
        const cut = top < area.Y || top + height > area.Y + area.Height;
        if (cut && HostSettingsMenu.#pressY !== null && !HostSettingsMenu.#pressIn) return undefined;
        layout.Location = Vector2.new(x, y);
        T.SpriteBatchItem[HostSettingsMenu.#CLIP](area, area, Terraria.Main.spriteBatch, true);

        try {
            return draw();
        } finally {
            T.SpriteBatchItem['void DisabledClipping()']();
            layout.Location = Vector2.new(x, savedY);
        }
    }

    static #GameRow(self, original, layout) {
        if (!HostSettingsMenu.#scroll) return original(self);
        let result;
        const ok = Safe.Run('HostSettingsMenu.GameRow', () => {
            HostSettingsMenu.#Clipped(layout, layout.Location.Y - HostSettingsMenu.#scroll, () => { result = original(self); });
            return true;
        });
        if (!ok) return original(self);
        return result;
    }

    static #DrawRows(self) {
        const T = HostSettingsMenu.#T, L = T.Layout.Instance;
        const step = HostSettingsMenu.#Step(L);
        const baseX = L.Password.Location.X, baseY = L.Password.Location.Y;
        
        HostSettingsMenu.#Rows().forEach((row, i) => {
            const button = row.layout.ToggleButton;
            button.Location = Vector2.new(baseX, baseY);
            HostSettingsMenu.#Clipped(button, baseY + step * (i + 1) - HostSettingsMenu.#scroll,
                () => HostSettingsMenu.#DrawToggle(self, row));
        });
    }

    static #DrawToggle(self, row) {
        const T = HostSettingsMenu.#T;
        const t = row.layout;
        const gi = T.Instance.Active;
        const pad = gi && gi.GUIVirtualInputController;
        const focus = !!(pad && pad.ControllerActive);
        const state = Number(T.Button[HostSettingsMenu.#DRAW](t.ToggleButton, null, HostSettingsMenu.#Text(row.key), false,
            row.scale, false, focus, false, false));

        if (state === HostSettingsMenu.#CLICKED && HostSettingsMenu.#pressIn && !HostSettingsMenu.#dragged) {
            HostSettingsMenu[row.field] = !HostSettingsMenu[row.field];
            SoundEngine.PlaySound(HostSettingsMenu.#TICK);
        }
        if (state === HostSettingsMenu.#CLICKED || state === HostSettingsMenu.#OVER || state === HostSettingsMenu.#DOWN) {
            self.description = HostSettingsMenu.#Text(row.desc);
        }

        const panel = T.Panel[HostSettingsMenu.#PANEL];
        const on = !!HostSettingsMenu[row.field];

        if (on) {
            panel(t.Option2Disabled, false, null, null, null);
            panel(t.Option1Enabled, false, null, null, null);
        } else {
            panel(t.Option1Disabled, false, null, null, null);
            panel(t.Option2Enabled, false, null, null, null);
        }

        const language = (key) => Terraria.Localization.Language['string GetTextValue(string key)'](key);
        HostSettingsMenu.#Label(t.Option1Label, row.template.Option1Label, t.Option1Enabled.Size.X,
            language('Mobile.ToggleSettingOn'), on ? t.Option1Label.Color : t.Option1DisabledLabelColour);
        HostSettingsMenu.#Label(t.Option2Label, row.template.Option2Label, t.Option2Enabled.Size.X,
            language('Mobile.ToggleSettingOff'), on ? t.Option2DisabledLabelColour : t.Option2Label.Color);
    }

    static #Label(label, template, width, text, color) {
        const font = template['SpriteFont GetFont()']();
        const measured = font ? font['Vector2 MeasureString(string text)'](text).X : 0;
        const room = Math.max(8, width - 8);
        label.Scale = measured > 0 ? Math.min(template.Scale, room / measured) : template.Scale;
        HostSettingsMenu.#T.String[HostSettingsMenu.#TEXT](label, text, color, false);
    }
}
