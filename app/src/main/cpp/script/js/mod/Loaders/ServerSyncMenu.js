class ServerSyncMenu {
    static #active = false;
    static #state = null;
    static #poll = 0;
    static #T = null;
    static #parts = null;
    static #scales = null;
    static #rowScales = [];
    static #scroll = 0;
    static #momentum = 0;
    static #pressY = null;
    static #lastY = null;
    static #dragged = false;
    static #pressIn = false;
    static #guard = false;
    static #guardIdle = 0;

    static #GAP = 4;
    static #TEXT_PAD = 16;
    static #TAG_PAD = 18;
    static #FOOTER_GAP = 6;
    static #POLL_FRAMES = 6;
    static #DRAG_PX = 8;
    static #FRICTION = 0.92;
    static #SCREEN = 0;
    static #TOP_LEFT = 9;
    static #MIDDLE = 16;
    static #CENTER_X = 2;
    static #RIGHT = 4;
    static #TOP = 8;
    static #CLICKED = 0;
    static #ESCAPE = 27;
    static #MENU_OPEN = 10;
    static #MENU_CLOSE = 11;
    static #TICK = 12;

    static #DRAW = 'GUITransactionButton.InputState Draw(TransactionButton_Layout layout, Item item, string label, bool disabled, ref float scale, bool forcedPressed, bool hasControllerFocus, bool forceOver, bool disablePressedState)';
    static #PANEL = 'void Draw(Panel_Layout layout, bool cursorOver, Nullable<Color> overloadBacking, Nullable<Color> overloadBorder, Nullable<Color> overloadHighlight)';
    static #TITLE = 'bool DrawButton(StringButton_Layout layout, string value, ref float scale, bool forcedPressed, bool buttonDisabled)';
    static #TEXT = 'void Draw(String_Layout layout, string value, Color overloadedColour, bool multilineAlignmentApplied)';
    static #ANCHORED = 'Vector2 GetAnchoredPosition(ControlAnchor.ControlId anchorControl, LayoutCalculator.AnchorType anchorType, Vector2 position)';
    static #CLIP = 'void EnableClipping(Rectangle inner, Rectangle outer, SpriteBatch batcher, bool vertical)';
    static #REGION = 'bool RegisterInputRegion(Rectangle rect)';
    static #GET_GRID = 'Rectangle get__gridItemRegion()';
    static #SET_GRID = 'void set__gridItemRegion(Rectangle value)';

    static Install() {
        Hooks.Once('server.sync.menu', () => Safe.Run('ServerSyncMenu.Install', () => ServerSyncMenu.#Hook()));
    }

    static Open() {
        if (!ServerSyncMenu.#T) return;
        ServerSyncMenu.#active = true;
        ServerSyncMenu.#state = { phase: 'checking' };
        ServerSyncMenu.#poll = 0;
        ServerSyncMenu.#scroll = 0;
        ServerSyncMenu.#momentum = 0;
        ServerSyncMenu.#guard = true;
        ServerSyncMenu.#guardIdle = 0;
        SoundEngine.PlaySound(ServerSyncMenu.#MENU_OPEN);
    }

    static #Hook() {
        ServerSyncMenu.#T = {
            Status: bl.classOf('', 'GUINetPlayStatusMenu'),
            Settings: bl.classOf('', 'SettingsOverlay_Layout'),
            Mappings: bl.classOf('', 'KeyboardMappings_Layout'),
            Ach: bl.classOf('', 'Achievements_Layout'),
            Button: bl.classOf('', 'GUITransactionButton'),
            StringButton: bl.classOf('', 'GUIStringButton'),
            String: bl.classOf('', 'GUIString'),
            Panel: bl.classOf('', 'GUIPanel'),
            Anchor: bl.classOf('', 'ControlAnchor'),
            Instance: bl.classOf('', 'GUIInstance'),
            Input: bl.classOf('', 'XNAUIInputLayer'),
            Regions: bl.classOf('', 'GUIInputRegionManager'),
            Calc: bl.classOf('', 'LayoutCalculator'),
            Keyboard: bl.classOf('', 'KeyboardInput'),
            SpriteBatchItem: Microsoft.Xna.Framework.Graphics.SpriteBatchItem,
        };
        ServerSyncMenu.#scales = { title: new Ref(1), left: new Ref(1), right: new Ref(1) };
        ServerSyncMenu.#T.Status['void Draw()'].hook((original, self) => {
            if (!ServerSyncMenu.#active) return original(self);
            const ok = Safe.Run('ServerSyncMenu.Draw', () => { ServerSyncMenu.#Draw(); return true; });
            if (!ok) ServerSyncMenu.#Close(false);
            return undefined;
        });
    }

    static #Text(key) {
        const pt = /^pt/.test(ModLocalization.ActiveCultureName || '');
        switch (key) {
            case 'title': return pt ? 'Mods do servidor' : 'Server mods';
            case 'checking': return pt ? 'Conferindo os mods do servidor...' : 'Checking the server mods...';
            case 'intro': return pt ? 'Este servidor usa outros mods. Para entrar, o jogo reinicia assim e volta sozinho:'
                : 'This server uses other mods. To join, the game restarts like this and comes back on its own:';
            case 'reorder': return pt ? 'Os mods são os mesmos, mas em outra ordem. Para entrar, o jogo reinicia na ordem do servidor e volta sozinho.'
                : 'Same mods in another order. To join, the game restarts in the server order and comes back on its own.';
            case 'after': return pt ? 'Seus mods voltam ao normal na próxima vez que abrir o jogo.'
                : 'Your mods are back to normal the next time you open the game.';
            case 'missing': return pt ? 'Estes mods não estão na loja do Bunny Loader (ou não nesta versão). Instale-os para entrar:'
                : 'These mods are not in the Bunny Loader store (or not in this version). Install them to join:';
            case 'downloading': return pt ? 'Baixando os mods do servidor...' : 'Downloading the server mods...';
            case 'restarting': return pt ? 'Reiniciando...' : 'Restarting...';
            case 'tagDownload': return pt ? 'Baixar' : 'Download';
            case 'tagEnable': return pt ? 'Ligar' : 'Turn on';
            case 'tagDisable': return pt ? 'Desligar' : 'Turn off';
            case 'tagMissing': return pt ? 'Falta' : 'Missing';
            case 'tagDone': return pt ? 'Pronto' : 'Done';
            case 'tagWaiting': return pt ? 'Na fila' : 'Queued';
            case 'cancel': return pt ? 'Cancelar' : 'Cancel';
            case 'back': return pt ? 'Voltar' : 'Back';
            case 'sync': return pt ? 'Sincronizar' : 'Sync';
        }
        return key;
    }

    static #TAG_COLORS = {
        download: [255, 210, 90],
        enable: [120, 225, 120],
        disable: [235, 130, 130],
        missing: [255, 95, 95],
        done: [120, 225, 120],
        busy: [255, 255, 255],
        waiting: [165, 165, 180],
    };

    static #Close(sound = true) {
        if (!ServerSyncMenu.#active) return;
        ServerSyncMenu.#active = false;
        ServerSyncMenu.#state = null;
        bl.__serverSyncCancel();
        if (sound) SoundEngine.PlaySound(ServerSyncMenu.#MENU_CLOSE);
    }

    static #Draw() {
        const T = ServerSyncMenu.#T, S = T.Settings.Instance;
        if (--ServerSyncMenu.#poll <= 0) {
            ServerSyncMenu.#poll = ServerSyncMenu.#POLL_FRAMES;
            const text = bl.__serverSyncState();
            if (text) ServerSyncMenu.#state = JSON.parse(text);
        }
        if (ServerSyncMenu.#guard) {
            ServerSyncMenu.#guardIdle = Terraria.Main.mouseLeft ? 0 : ServerSyncMenu.#guardIdle + 1;
            if (ServerSyncMenu.#guardIdle >= 2) ServerSyncMenu.#guard = false;
        }
        const state = ServerSyncMenu.#state || { phase: 'checking' };
        const panel = T.Panel[ServerSyncMenu.#PANEL];
        panel(S.Backing, false, null, null, null);
        panel(S.MenuDivider2, false, null, null, null);
        T.StringButton[ServerSyncMenu.#TITLE](S.Title, ServerSyncMenu.#Text('title'), ServerSyncMenu.#scales.title, false, true);

        const { rows, footer } = ServerSyncMenu.#Rows(state);
        ServerSyncMenu.#DrawList(S, rows, footer);

        const action = ServerSyncMenu.#DrawButtons(T, state);
        if (action && !ServerSyncMenu.#guard && !ServerSyncMenu.#dragged) {
            T.Input.Instance['void CaptureUICrusorDrag(int dragFromAxis)'](-1);
            if (action === 'sync') {
                SoundEngine.PlaySound(ServerSyncMenu.#TICK);
                bl.__serverSyncApply();
                ServerSyncMenu.#poll = 0;
            } else {
                ServerSyncMenu.#Close();
                return;
            }
        }
        if (T.Keyboard['bool GetKeyUp(KeyCode keycode)'](ServerSyncMenu.#ESCAPE) && state.phase !== 'applying') {
            const gi = T.Instance.Active;
            if (gi && gi.GUIKeyboardMappings) gi.GUIKeyboardMappings['void DisableEscapeKeyUsage()']();
            ServerSyncMenu.#Close();
        }
    }

    static #DrawButtons(T, state) {
        const K = T.Mappings.Instance;
        const focus = ServerSyncMenu.#Focus();
        const applying = state.phase === 'applying';
        const canSync = state.phase === 'ready';
        const leftText = ServerSyncMenu.#Text(canSync || state.phase === 'checking' || applying ? 'cancel' : 'back');
        const draw = (layout, text, disabled, scale) =>
            Number(T.Button[ServerSyncMenu.#DRAW](layout, null, text, disabled, scale, false, focus, false, false)) === ServerSyncMenu.#CLICKED && !disabled;
        let clicked = null;
        if (draw(K.ResetSettings, leftText, applying, ServerSyncMenu.#scales.left)) clicked = 'cancel';
        if (state.phase === 'ready' || state.phase === 'checking' || applying) {
            if (draw(K.Close, ServerSyncMenu.#Text('sync'), !canSync, ServerSyncMenu.#scales.right)) clicked = 'sync';
        }
        return clicked;
    }

    static #Focus() {
        const gi = ServerSyncMenu.#T.Instance.Active;
        const pad = gi && gi.GUIVirtualInputController;
        return !!(pad && pad.ControllerActive);
    }

    static #Rows(state) {
        const text = (key) => ({ kind: 'text', text: ServerSyncMenu.#Text(key) });
        const items = (names, tag, color) => (names || []).map((name) => ({ kind: 'item', text: name, tag: ServerSyncMenu.#Text(tag), color }));
        switch (state.phase) {
            case 'ready': {
                const changes = [...items(state.download, 'tagDownload', 'download'), ...items(state.enable, 'tagEnable', 'enable'),
                    ...items(state.disable, 'tagDisable', 'disable')];
                return { rows: [text(changes.length ? 'intro' : 'reorder'), ...changes], footer: ServerSyncMenu.#Text('after') };
            }
            case 'missing':
                return { rows: [text('missing'), ...items(state.missing, 'tagMissing', 'missing')] };
            case 'applying': {
                const names = state.download || [];
                const restarting = !state.steps || state.step > state.steps;
                return { rows: [text(restarting ? 'restarting' : 'downloading'), ...names.map((name, i) => {
                    const done = restarting || i + 1 < state.step, busy = i + 1 === state.step;
                    return {
                        kind: 'item', text: name,
                        tag: done ? ServerSyncMenu.#Text('tagDone') : busy ? Math.round((state.progress || 0) * 100) + '%' : ServerSyncMenu.#Text('tagWaiting'),
                        color: done ? 'done' : busy ? 'busy' : 'waiting',
                    };
                })] };
            }
            case 'error':
                return { rows: [{ kind: 'text', text: state.message || '' }] };
            default:
                return { rows: [text('checking')] };
        }
    }

    static #Parts(S) {
        if (ServerSyncMenu.#parts) return ServerSyncMenu.#parts;
        const copy = (x) => x['object MemberwiseClone()']();
        const toScreen = (x) => {
            x.AnchorControl = ServerSyncMenu.#SCREEN;
            x.Anchor = ServerSyncMenu.#TOP_LEFT;
            return x;
        };
        const text = toScreen(copy(S.ToggleTemplate.Option1Label));
        text.Alignment = ServerSyncMenu.#TOP | ServerSyncMenu.#CENTER_X;
        const tag = toScreen(copy(S.SliderTemplate.Value));
        tag.Alignment = ServerSyncMenu.#MIDDLE | ServerSyncMenu.#RIGHT;
        return ServerSyncMenu.#parts = { text, tag, names: new Map() };
    }

    static #Rect(x, y, width, height) {
        const rect = Microsoft.Xna.Framework.Rectangle.new();
        rect['void .ctor(int x, int y, int width, int height)'](Math.round(x), Math.round(y), Math.round(width), Math.round(height));
        return rect;
    }

    static #Bounds(T, S) {
        const width = T.Ach.Instance.ItemBacking.Size.X;
        const left = T.Calc[ServerSyncMenu.#ANCHORED](S.Backing.AnchorControl, S.Backing.Anchor, Vector2.new(0, 0)).X - width / 2;
        return { left, width };
    }

    static #Wrap(label, text, width) {
        const font = label['SpriteFont GetFont()']();
        if (!font) return [text];
        const measure = (s) => font['Vector2 MeasureString(string text)'](s).X * label.Scale;
        const lines = [];
        let line = '';
        for (const word of String(text).split(/\s+/)) {
            const next = line ? line + ' ' + word : word;
            if (line && measure(next) > width) {
                lines.push(line);
                line = word;
            } else {
                line = next;
            }
        }
        if (line) lines.push(line);
        return lines;
    }

    static #DrawList(S, rows, footer) {
        const T = ServerSyncMenu.#T;
        const P = ServerSyncMenu.#Parts(S);
        const at = (layout) => T.Calc[ServerSyncMenu.#ANCHORED](layout.AnchorControl, layout.Anchor, layout.Location);
        const top = at(S.Title).Y + S.Title.Size.Y / 2 + 14;
        const { left, width } = ServerSyncMenu.#Bounds(T, S);
        const itemHeight = S.ToggleTemplate.ToggleButton.overloadedSize.Y;
        const font = P.text['SpriteFont GetFont()']();
        const lineHeight = (font ? font.LineSpacing : 24) * P.text.Scale;
        const textWidth = width - ServerSyncMenu.#TEXT_PAD * 2;
        const center = left + width / 2;

        let bottom = at(S.MenuDivider2).Y - 8;
        if (footer) {
            const lines = ServerSyncMenu.#Wrap(P.text, footer, textWidth);
            bottom -= lines.length * lineHeight + ServerSyncMenu.#FOOTER_GAP;
            lines.forEach((line, i) => {
                P.text.Location = Vector2.new(center, bottom + ServerSyncMenu.#FOOTER_GAP + i * lineHeight);
                T.String[ServerSyncMenu.#TEXT](P.text, line, Color.new(175, 175, 195), false);
            });
        }

        const laid = [];
        for (const row of rows) {
            if (row.kind === 'text') {
                const lines = ServerSyncMenu.#Wrap(P.text, row.text, textWidth);
                lines.forEach((line, i) => laid.push({ kind: 'text', text: line,
                    height: lineHeight + (i === lines.length - 1 ? ServerSyncMenu.#GAP * 3 : 0) }));
            } else {
                laid.push({ ...row, height: itemHeight + ServerSyncMenu.#GAP });
            }
        }
        const total = laid.reduce((sum, row) => sum + row.height, 0);
        const area = ServerSyncMenu.#Rect(left, top, width, bottom - top);
        ServerSyncMenu.#Scroll(T, area, total, bottom - top);

        const anchor = T.Anchor;
        const saved = anchor[ServerSyncMenu.#GET_GRID]();
        T.SpriteBatchItem[ServerSyncMenu.#CLIP](area, area, Terraria.Main.spriteBatch, true);
        try {
            let y = top - ServerSyncMenu.#scroll;
            laid.forEach((row, i) => {
                if (y + row.height > top && y < bottom) {
                    if (row.kind === 'text') {
                        P.text.Location = Vector2.new(center, y);
                        T.String[ServerSyncMenu.#TEXT](P.text, row.text, P.text.Color, false);
                    } else {
                        anchor[ServerSyncMenu.#SET_GRID](ServerSyncMenu.#Rect(left, y, width, itemHeight));
                        ServerSyncMenu.#DrawItem(T, S, P, row, i, left, y, width, itemHeight);
                    }
                }
                y += row.height;
            });
        } finally {
            T.SpriteBatchItem['void DisabledClipping()']();
            anchor[ServerSyncMenu.#SET_GRID](saved);
        }
    }

    static #DrawItem(T, S, P, row, i, x, y, width, height) {
        const scale = ServerSyncMenu.#rowScales[i] || (ServerSyncMenu.#rowScales[i] = new Ref(1));
        const button = S.ToggleTemplate.ToggleButton;
        const tagFont = P.tag['SpriteFont GetFont()']();
        const tagWidth = tagFont ? tagFont['Vector2 MeasureString(string text)'](row.tag).X * P.tag.Scale : 80;
        const name = ServerSyncMenu.#Name(P, button.Label, row.text, width - tagWidth - ServerSyncMenu.#TAG_PAD * 3);
        T.Button[ServerSyncMenu.#DRAW](button, null, name, false, scale, false, ServerSyncMenu.#Focus(), false, true);
        const [r, g, b] = ServerSyncMenu.#TAG_COLORS[row.color] || ServerSyncMenu.#TAG_COLORS.busy;
        P.tag.Location = Vector2.new(x + width - ServerSyncMenu.#TAG_PAD, y + height / 2);
        T.String[ServerSyncMenu.#TEXT](P.tag, row.tag, Color.new(r, g, b), false);
    }

    static #Name(P, label, text, width) {
        const key = text + '|' + Math.round(width);
        let fit = P.names.get(key);
        if (fit !== undefined) return fit;
        const font = label ? label['SpriteFont GetFont()']() : null;
        fit = text;
        if (font) {
            const measure = (s) => font['Vector2 MeasureString(string text)'](s).X * label.Scale;
            if (measure(text) > width) {
                while (fit.length > 1 && measure(fit + '...') > width) fit = fit.slice(0, -1);
                fit += '...';
            }
        }
        P.names.set(key, fit);
        return fit;
    }

    static #Scroll(T, area, total, visible) {
        const M = Terraria.Main;
        T.Regions.Instance[ServerSyncMenu.#REGION](area);
        const max = Math.max(0, total - visible);
        const inside = M.mouseX >= area.X && M.mouseX <= area.X + area.Width
            && M.mouseY >= area.Y && M.mouseY <= area.Y + area.Height;
        if (M.mouseLeft) {
            if (ServerSyncMenu.#pressY === null) {
                ServerSyncMenu.#pressY = M.mouseY;
                ServerSyncMenu.#lastY = M.mouseY;
                ServerSyncMenu.#dragged = false;
                ServerSyncMenu.#pressIn = inside && !ServerSyncMenu.#guard;
                ServerSyncMenu.#momentum = 0;
            } else if (ServerSyncMenu.#pressIn && Math.abs(M.mouseY - ServerSyncMenu.#pressY) > ServerSyncMenu.#DRAG_PX) {
                ServerSyncMenu.#dragged = true;
            }
            if (ServerSyncMenu.#dragged) {
                const delta = ServerSyncMenu.#lastY - M.mouseY;
                ServerSyncMenu.#scroll += delta;
                ServerSyncMenu.#momentum = delta;
                T.Input.Instance['void CaptureUICrusorDrag(int dragFromAxis)'](1);
            }
            ServerSyncMenu.#lastY = M.mouseY;
        } else {
            ServerSyncMenu.#pressY = null;
            ServerSyncMenu.#lastY = null;
            ServerSyncMenu.#dragged = false;
            if (Math.abs(ServerSyncMenu.#momentum) > 0.5) {
                ServerSyncMenu.#scroll += ServerSyncMenu.#momentum;
                ServerSyncMenu.#momentum *= ServerSyncMenu.#FRICTION;
            } else {
                ServerSyncMenu.#momentum = 0;
            }
        }
        if (ServerSyncMenu.#scroll < 0 || ServerSyncMenu.#scroll > max) {
            ServerSyncMenu.#scroll = Math.max(0, Math.min(ServerSyncMenu.#scroll, max));
            ServerSyncMenu.#momentum = 0;
        }
    }
}
