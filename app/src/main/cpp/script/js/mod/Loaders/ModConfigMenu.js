// "Config. dos Mods" no menu de pausa, e a tela que ele abre: cada mod numa
// seção, com as opções dos ModConfig dele (interruptor, faixa e escolha), como
// o Mod Configuration do tModLoader.
//
// O menu de pausa do celular (GUISettingsPauseMenu) empilha os botões no
// SetupOffsets, que ele chama a cada quadro: o painel de fundo centralizado e
// cada botão um degrau (altura + ButtonSpacing) abaixo do anterior. O gancho
// abre um degrau depois de Configurações: o painel cresce um degrau, o que
// está acima sobe meio, o que está abaixo desce meio.
//
// O botão é desenhado de dentro do GUIPanel.Draw do fundo, e não depois do
// Draw: no meio do Draw o jogo soma às larguras a sobra do texto mais largo e,
// no fim, tira de volta. Ele usa um layout próprio (clone do de Configurações,
// com o coelho de ícone) e entra no FIM da lista de navegação: o menu ativa o
// item selecionado pelo índice, e um índice novo no meio faria o toque abrir
// o botão de baixo.
//
// A tela usa as peças da de Conquistas (Achievements_Layout): moldura,
// divisórias, a aba do título e o Voltar com banner. As linhas de opção são
// as da tela de Configurações do jogo (SettingsOverlay_Layout: o modelo do
// interruptor e o da faixa), que se posicionam pelo "item da grade"
// (ControlAnchor._gridItemRegion): cada linha aponta esse retângulo para si
// antes de desenhar. Tudo só roda com o jogo pausado: fora da pausa não custa
// nada.
class ModConfigMenu {
    static #open = false;
    static #button = null;      // o TransactionButton_Layout do nosso botão
    static #parts = null;       // as peças clonadas (cabeçalho, escolhas)
    static #scales = null;      // os `ref float scale` dos botões
    static #rowState = new Map(); // por opção: escala da linha, valor e arraste da faixa
    static #selected = null;    // o uuid do mod da aba escolhida
    static #tabs = new Map();     // por mod e forma: o botão da aba e a escala dele
    static #tabShape = null;    // as peças do começo, meio e fim da fileira
    static #icons = new Map();    // por mod: a textura do icon.png
    static #scroll = 0;         // quanto a lista subiu, em pixels
    static #momentum = 0;       // o embalo depois de soltar o dedo
    static #lastY = null;       // o Y do dedo no quadro anterior
    static #slider = null;      // a opção da faixa que o dedo segura
    static #fresh = false;      // o dedo encostou neste quadro
    static #pressInList = false; // o toque começou dentro da lista
    static #guard = false;      // o toque que abriu a tela ainda não saiu
    static #pressY = null;      // onde o dedo encostou
    static #dragged = false;    // o dedo andou: ao soltar, não é clique

    static #GAP = 4;
    static #DRAG_PX = 8;        // a partir daqui o toque é arraste
    static #FRICTION = 0.92;    // quanto do embalo sobra a cada quadro
    static #SINGLE_ICON_X = 1.25; // na aba única, o centro do ícone (em alturas da aba)
    static #SCREEN = 0;         // ControlAnchor.ControlId.Screen
    static #TOP_LEFT = 9;       // LayoutCalculator.AnchorType.TopLeft
    static #TOP_RIGHT = 12;
    static #ESCAPE = 27;        // KeyCode.Escape (o voltar do Android)
    static #MENU_OPEN = 10;     // o som do botão de Configurações
    static #MENU_CLOSE = 11;
    static #TICK = 12;          // o som de mudar uma opção
    static #ICON_SCALE = 1.1;

    static #DRAW = 'GUITransactionButton.InputState Draw(TransactionButton_Layout layout, Item item, string label, bool disabled, ref float scale, bool forcedPressed, bool hasControllerFocus, bool forceOver, bool disablePressedState)';
    static #BANNER = 'GUITransactionButton.InputState DrawWithBanner(TransactionButton_Layout layout, ControllerActionButton action, Item item, string label, bool disabled, ref float scale, bool forcedPressed, bool hasControllerFocus, bool forceOver, bool disablePressedState, bool drawWhenControllerConnected, bool addTouchBanner)';
    static #PANEL = 'void Draw(Panel_Layout layout, bool cursorOver, Nullable<Color> overloadBacking, Nullable<Color> overloadBorder, Nullable<Color> overloadHighlight)';
    static #TITLE = 'bool DrawButton(StringButton_Layout layout, string value, ref float scale, bool forcedPressed, bool buttonDisabled)';
    static #TEXT = 'void Draw(String_Layout layout, string value, Color overloadedColour, bool multilineAlignmentApplied)';
    static #SLIDER = 'bool Draw(Slider_Layout layout, bool disablePick, ref float value, GUISlider.DragState dragState, GUISlider.DrawBackingHandler backingHandler, bool forceOver, int minValue, int maxValue, bool ignoreStartPoint)';
    static #ANCHORED = 'Vector2 GetAnchoredPosition(ControlAnchor.ControlId anchorControl, LayoutCalculator.AnchorType anchorType, Vector2 position)';
    static #CLIP = 'void EnableClipping(Rectangle inner, Rectangle outer, SpriteBatch batcher, bool vertical)';
    static #CLICKED = 0;        // GUITransactionButton.InputState.Clicked

    static #T = null;           // as classes do jogo, resolvidas no Install

    static Install() {
        Hooks.Once('modconfig.pause', () => Safe.Run('ModConfigMenu.Install', () => ModConfigMenu.#Hook()));
    }

    static #Hook() {
        const T = ModConfigMenu.#T = {
            Pause: bl.classOf('', 'GUISettingsPauseMenu'),
            Layout: bl.classOf('', 'SettingsPauseMenu_Layout'),
            Ach: bl.classOf('', 'Achievements_Layout'),
            Settings: bl.classOf('', 'SettingsOverlay_Layout'),
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
            SpriteBatchItem: Microsoft.Xna.Framework.Graphics.SpriteBatchItem,
        };
        ModConfigMenu.#scales = { button: new Ref(1), title: new Ref(1), back: new Ref(1) };
        const pauseDraw = T.Pause['void Draw()'];

        T.Pause['void SetupOffsets(bool setup)'].hook((original, self, setup) => {
            original(self, setup);
            if (setup) Safe.Run('ModConfigMenu.SetupOffsets', () => ModConfigMenu.#Layout(self));
        });

        pauseDraw.hook((original, self) => {
            if (!ModConfigMenu.#open) return original(self);
            // Um erro na tela não pode prender o jogador nela.
            const ok = Safe.Run('ModConfigMenu.Draw', () => { ModConfigMenu.#DrawScreen(); return true; });
            if (!ok) ModConfigMenu.#Close(false);
            return undefined;
        });

        T.Panel[ModConfigMenu.#PANEL].hook((original, layout, cursorOver, backing, border, highlight) => {
            original(layout, cursorOver, backing, border, highlight);
            const L = T.Layout.Instance;
            if (L && layout === L.Backing) Safe.Run('ModConfigMenu.Button', () => ModConfigMenu.#DrawButton(L));
        }, { whileIn: pauseDraw });

        // Sair do mundo com a tela aberta não pode deixá-la aberta no próximo.
        Terraria.WorldGen['void clearWorld()'].hook((original) => {
            original();
            ModConfigMenu.#Close(false);
        });
    }

    static #Text(key) {
        const pt = /^pt/.test(ModLocalization.ActiveCultureName || '');
        switch (key) {
            case 'button': return pt ? 'Config. dos Mods' : 'Mod Configuration';
            case 'empty': return pt ? 'Nenhum mod tem opções.' : 'No mod has options.';
        }
        return key;
    }

    static #Language(key) {
        return Terraria.Localization.Language['string GetTextValue(string key)'](key);
    }

    // ------------------------------------------------------------ o botão

    static #Step(L) {
        return L.Close.overloadedSize.Y + L.ButtonSpacing;
    }

    // O clone do botão de Configurações, com o coelho no lugar da engrenagem.
    // O Texture_Layout só recarrega quando o TextureId muda: com o id, o
    // último id e a textura preenchidos, o get_Texture devolve a nossa.
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
            // O coelho tem 32 px; os ícones do jogo são maiores.
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
        // A lista mede as regiões de toque ao ser ativada, e foi ativada sem o
        // nosso: o toque nele caía no vizinho (Conquistas) até a próxima vez.
        list['void Activate()']();
    }

    static #DrawButton(L) {
        if (ModConfigMenu.#open) return;
        const T = ModConfigMenu.#T;
        const ours = ModConfigMenu.#ButtonLayout(L);
        // A largura deste quadro, já com a sobra do texto (ver o topo).
        ours.overloadedSize = L.Settings.overloadedSize;
        ours.Location = Vector2.new(L.Settings.Location.X, ours.Location.Y);
        const state = Number(T.Button[ModConfigMenu.#DRAW](ours, null, ModConfigMenu.#Text('button'), false,
            ModConfigMenu.#scales.button, false, ModConfigMenu.#Focus(), false, false));
        if (state !== ModConfigMenu.#CLICKED) return;

        // O que o botão de Configurações faz: o toque fica por consumido.
        T.Input.Instance['void CaptureUICrusorDrag(int dragFromAxis)'](-1);
        SoundEngine.PlaySound(ModConfigMenu.#MENU_OPEN);
        ModConfigMenu.#open = true;
        ModConfigMenu.#ResetScroll();
        ModConfigMenu.#guard = true;
    }

    static #Focus() {
        const gi = ModConfigMenu.#T.Instance.Active;
        const pad = gi && gi.GUIVirtualInputController;
        return !!(pad && pad.ControllerActive);
    }

    // ------------------------------------------------------------ a tela

    static #Close(sound = true) {
        if (!ModConfigMenu.#open) return;
        ModConfigMenu.#open = false;
        ModConfigMenu.#ReleaseSliders();
        ConfigLoader.Flush();
        if (sound) SoundEngine.PlaySound(ModConfigMenu.#MENU_CLOSE);
    }

    static #DrawScreen() {
        const T = ModConfigMenu.#T, S = T.Settings.Instance;
        // O toque que abriu a tela solta no quadro seguinte, já aqui dentro:
        // sem a trava, ele clicava na linha que estivesse sob o dedo.
        if (ModConfigMenu.#guard && !Terraria.Main.mouseLeft && !Terraria.Main.mouseLeftRelease) ModConfigMenu.#guard = false;
        const panel = T.Panel[ModConfigMenu.#PANEL];
        panel(S.Backing, false, null, null, null);
        panel(S.MenuDivider, false, null, null, null);
        panel(S.MenuDivider2, false, null, null, null);

        const mods = ModLoader.Mods.filter((m) => ConfigLoader.Of(m).some((e) => e.options.length));
        let mod = mods.find((m) => m.uuid === ModConfigMenu.#selected) || mods[0] || null;
        if (mod) mod = ModConfigMenu.#DrawTabs(T, S, mods, mod);
        ModConfigMenu.#selected = mod ? mod.uuid : null;
        const title = mod ? mod.name || mod.id : ModConfigMenu.#Text('button');
        T.StringButton[ModConfigMenu.#TITLE](S.Title, title, ModConfigMenu.#scales.title, false, true);
        ModConfigMenu.#DrawList(S, mod);

        const back = ModConfigMenu.#Language('UI.Back');
        const state = Number(T.Button[ModConfigMenu.#BANNER](S.Close, null, null, back, false,
            ModConfigMenu.#scales.back, false, true, false, false, false, true));
        if (state === ModConfigMenu.#CLICKED) {
            T.Input.Instance['void CaptureUICrusorDrag(int dragFromAxis)'](-1);
            ModConfigMenu.#Close();
            return;
        }
        // O voltar do Android, como na tela de Conquistas.
        if (T.Keyboard['bool GetKeyUp(KeyCode keycode)'](ModConfigMenu.#ESCAPE)) {
            const gi = T.Instance.Active;
            if (gi && gi.GUIKeyboardMappings) gi.GUIKeyboardMappings['void DisableEscapeKeyUsage()']();
            ModConfigMenu.#Close();
        }
    }

    // As abas, como as categorias da tela de Configurações: uma por mod com
    // opções, com o ícone dele (icon.png; sem ele, o coelho). Cada aba é um
    // clone de uma categoria do primeiro grupo do jogo, pela posição: a
    // primeira (borda arredondada à esquerda), uma do meio (reta) e a
    // última (arredondada à direita). A distância de uma aba para a próxima
    // é a das categorias do jogo na mesma junção: elas não são espaçadas por
    // igual, e um passo único deixava um vão antes da última. Com um mod só,
    // uma aba larga (ver #DrawSingleTab). A aba do mod escolhido fica
    // pressionada. Devolve o mod escolhido.
    static #DrawTabs(T, S, mods, selected) {
        const C = S.Categories;
        T.Panel[ModConfigMenu.#PANEL](C.Backing, false, null, null, null);
        const shape = ModConfigMenu.#tabShape || (ModConfigMenu.#tabShape = ModConfigMenu.#TabShape(C));
        const n = mods.length;
        if (n === 1) {
            ModConfigMenu.#DrawSingleTab(T, shape, mods[0]);
            return mods[0];
        }
        let chosen = selected;
        let x = shape.start.Location.X;
        mods.forEach((mod, i) => {
            const kind = i === 0 ? 'start' : i === n - 1 ? 'end' : 'middle';
            if (i > 0) x += kind === 'end' ? shape.toEnd : i === 1 ? shape.toMiddle : shape.middleStep;
            const tab = ModConfigMenu.#Tab(T, shape[kind], mod, kind, shape.iconPx);
            tab.layout.Location = Vector2.new(x, shape.start.Location.Y);
            const on = mod === selected;
            const state = Number(T.Button[ModConfigMenu.#DRAW](tab.layout, null, mod.name || mod.id, false, tab.scale,
                on, ModConfigMenu.#Focus(), false, false));
            if (state === ModConfigMenu.#CLICKED && !ModConfigMenu.#Blocked() && !on) {
                chosen = mod;
                ModConfigMenu.#ResetScroll();
                SoundEngine.PlaySound(ModConfigMenu.#TICK);
            }
        });
        return chosen;
    }

    // Um mod só: uma aba da largura da fileira de categorias inteira, com as
    // duas pontas arredondadas (o botão do menu de pausa), o ícone e o nome.
    // O jogo não tem peça de categoria arredondada dos dois lados.
    static #DrawSingleTab(T, shape, mod) {
        const key = mod.uuid + ':single';
        let tab = ModConfigMenu.#tabs.get(key);
        if (!tab) {
            const layout = T.Layout.Instance.Settings['object MemberwiseClone()']();
            Safe.Run('ModConfigMenu: ícone de ' + (mod.name || mod.id), () => {
                const texture = ModConfigMenu.#ModIcon(mod);
                const icon = T.TextureLayout.new();
                icon['void .ctor()']();
                icon.TextureId = 'bunny/mod/' + mod.uuid;
                icon._lastTextureId = icon.TextureId;
                icon._texture = texture;
                layout.IconTexture = icon;
                layout.ForceIconScale = true;
                layout.ForcedIconScale = shape.iconPx / Math.max(1, texture.Height);
            });
            tab = { layout, scale: new Ref(1) };
            ModConfigMenu.#tabs.set(key, tab);
        }
        // Este botão se posiciona pelo canto de cima à esquerda, a partir do
        // centro da tela; a fileira vem em pixels de tela.
        const r = shape.rowRect;
        const center = T.Calc[ModConfigMenu.#ANCHORED](tab.layout.AnchorControl, tab.layout.Anchor, Vector2.new(0, 0));
        tab.layout.Location = Vector2.new(r.X - center.X, r.Y - center.Y);
        tab.layout.overloadedSize = Vector2.new(r.Width, r.Height);
        // O IconOffset é o centro do ícone a partir do canto de cima à
        // esquerda do botão. O do clone vem do botão de pausa (44 de altura):
        // na aba, de outra altura, o ícone descia e colava na ponta esquerda.
        tab.layout.IconOffset = Vector2.new(r.Height * ModConfigMenu.#SINGLE_ICON_X, r.Height / 2);
        T.Button[ModConfigMenu.#DRAW](tab.layout, null, mod.name || mod.id, false, tab.scale,
            true, ModConfigMenu.#Focus(), false, false);
    }

    // As categorias do jogo na fileira da "Geral", da esquerda para a
    // direita. O primeiro grupo é o das que estão coladas uma na outra (o vão
    // maior separa o segundo). Devolve as peças do começo, do meio e do fim,
    // a distância em cada junção, a altura dos ícones e o retângulo (em
    // pixels de tela) da fileira inteira, para a aba única.
    static #TabShape(C) {
        const T = ModConfigMenu.#T;
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

        // A altura do ícone da primeira categoria vale para todas as abas: os
        // ícones das categorias do jogo não têm todos o mesmo tamanho.
        const theirs = general.IconTexture ? general.IconTexture.Texture : null;
        const iconPx = (theirs ? theirs.Height : 32) * (general.ForceIconScale ? general.ForcedIconScale : 1);

        const rect = (l) => T.Calc['Rectangle GetLayoutRect(ControlAnchor.ControlId anchorControl, LayoutCalculator.AnchorType anchorType, Vector2 position, Vector2 size, Texture2D texture)'](
            l.AnchorControl, l.Anchor, l.Location, l.overloadedSize, null);
        const a = rect(row[0]), b = rect(row[row.length - 1]);
        const rowRect = { X: a.X, Y: a.Y, Width: b.X + b.Width - a.X, Height: a.Height };

        return {
            iconPx,
            rowRect,
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
            // A altura dos ícones das abas do jogo, para um ícone de 256 px e
            // o coelho de 32 saírem do mesmo tamanho.
            const target = iconPx;
            const icon = T.TextureLayout.new();
            icon['void .ctor()']();
            icon.TextureId = 'bunny/mod/' + mod.uuid;
            icon._lastTextureId = icon.TextureId;
            icon._texture = texture;
            layout.IconTexture = icon;
            layout.ForceIconScale = true;
            layout.ForcedIconScale = target / Math.max(1, texture.Height);
        });
        tab = { layout, scale: new Ref(1) };
        ModConfigMenu.#tabs.set(key, tab);
        return tab;
    }

    // A textura do icon.png do mod, lida uma vez; sem ele, o coelho.
    static #ModIcon(mod) {
        let texture = ModConfigMenu.#icons.get(mod.uuid);
        if (texture) return texture;
        const file = mod.root ? bl.path.join(mod.root, 'icon.png') : null;
        texture = file && bl.file.exists(file) ? bl.loadTexture(file) : bl.builtinTexture('bunnyHead').Value;
        ModConfigMenu.#icons.set(mod.uuid, texture);
        return texture;
    }

    // O aviso de lista vazia, preso no canto de cima à esquerda da tela: a
    // posição vira pixel do jogo. As escolhas de um Radio são os painéis do
    // interruptor, um por escolha.
    static #Parts(S) {
        if (ModConfigMenu.#parts) return ModConfigMenu.#parts;
        const copy = (x) => x['object MemberwiseClone()']();
        const t = S.ToggleTemplate;
        const empty = copy(t.Option1Label);
        empty.AnchorControl = ModConfigMenu.#SCREEN;
        empty.Anchor = ModConfigMenu.#TOP_LEFT;
        empty.Alignment = ModConfigMenu.#TOP_LEFT;
        return ModConfigMenu.#parts = {
            empty,
            choiceOn: copy(t.Option1Enabled),
            choiceOff: copy(t.Option1Disabled),
            label: copy(t.Option1Label),
        };
    }

    // Uma linha por opção do mod escolhido.
    static #Rows(S, mod) {
        const rows = [];
        if (!mod) return rows;
        for (const entry of ConfigLoader.Of(mod)) {
            for (const o of entry.options) {
                const template = o.type === 'range' ? S.SliderTemplate.Title : S.ToggleTemplate.ToggleButton;
                rows.push({ kind: o.type, entry, o, height: template.overloadedSize.Y + ModConfigMenu.#GAP });
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
        const width = T.Ach.Instance.ItemBacking.Size.X;
        const left = T.Calc[ModConfigMenu.#ANCHORED](S.Backing.AnchorControl, S.Backing.Anchor, Vector2.new(0, 0)).X - width / 2;
        const rows = ModConfigMenu.#Rows(S, mod);

        if (!rows.length) {
            P.empty.Location = Vector2.new(left + 12, top + 10);
            T.String[ModConfigMenu.#TEXT](P.empty, ModConfigMenu.#Text('empty'), P.empty.Color, false);
            return;
        }

        const total = rows.reduce((sum, row) => sum + row.height, 0) - ModConfigMenu.#GAP;
        const area = ModConfigMenu.#Rect(left, top, width, bottom - top);
        ModConfigMenu.#Scroll(T, area, total, left, top, width, bottom);

        // As linhas das opções se posicionam pelo "item da grade": aponta-o
        // para cada uma e devolve o do jogo no fim. A linha cortada pela
        // borda é recortada como na tela de Configurações (o GUIDraggableItemGrid
        // liga o mesmo recorte do SpriteBatch em volta de cada item).
        const anchor = T.Anchor;
        const saved = anchor['Rectangle get__gridItemRegion()']();
        T.SpriteBatchItem[ModConfigMenu.#CLIP](area, area, Terraria.Main.spriteBatch, true);
        try {
            let y = top - ModConfigMenu.#scroll;
            for (const row of rows) {
                const h = row.height - ModConfigMenu.#GAP;
                if (y + h > top && y < bottom) {
                    anchor['void set__gridItemRegion(Rectangle value)'](ModConfigMenu.#Rect(left, y, width, h));
                    Safe.Run('ModConfig ' + row.entry.name + '.' + row.o.key, () => ModConfigMenu.#DrawOption(T, S, P, row));
                }
                y += row.height;
            }
        } finally {
            T.SpriteBatchItem['void DisabledClipping()']();
            anchor['void set__gridItemRegion(Rectangle value)'](saved);
        }
    }

    static #Rect(x, y, width, height) {
        const rect = Microsoft.Xna.Framework.Rectangle.new();
        rect['void .ctor(int x, int y, int width, int height)'](Math.round(x), Math.round(y), Math.round(width), Math.round(height));
        return rect;
    }

    // Clique que não vale: o do toque que abriu a tela, e o de soltar o dedo
    // depois de arrastar a lista.
    static #Blocked() {
        return ModConfigMenu.#guard || ModConfigMenu.#dragged;
    }

    // Clique numa linha: também precisa ter começado dentro da lista. A parte
    // recortada de uma linha na borda fica por cima das abas e do Voltar.
    static #RowBlocked() {
        return ModConfigMenu.#Blocked() || !ModConfigMenu.#pressInList;
    }

    static #ResetScroll() {
        ModConfigMenu.#scroll = 0;
        ModConfigMenu.#momentum = 0;
    }

    // O jogo zera o "arrastando" de cada faixa com o retorno do Draw dela;
    // uma que ficasse marcada seguia o dedo em qualquer lugar e prendia o
    // toque (ver #DrawOption).
    static #ReleaseSliders() {
        ModConfigMenu.#slider = null;
        for (const st of ModConfigMenu.#rowState.values()) {
            if (st.drag) st.drag.wasDragging = false;
        }
    }

    // Rola por pixel, como a tela de Configurações: arrastar move a lista
    // junto com o dedo e, ao soltar, ela segue no embalo até parar. A região
    // registrada é o que faz o toque virar Main.mouseX/Y aqui. Um toque que
    // começa numa faixa é da faixa: a lista não rola.
    static #Scroll(T, area, total, left, top, width, bottom) {
        const M = Terraria.Main;
        T.Regions.Instance['bool RegisterInputRegion(Rectangle rect)'](area);
        const max = Math.max(0, total - (bottom - top));
        const inside = M.mouseX >= left && M.mouseX <= left + width && M.mouseY >= top && M.mouseY <= bottom;

        // O "arrastou" vale do toque até o quadro em que o dedo solta: é nele
        // que o botão da linha vê o clique.
        ModConfigMenu.#fresh = M.mouseLeft && ModConfigMenu.#pressY === null;
        if (M.mouseLeft) {
            if (ModConfigMenu.#fresh) {
                ModConfigMenu.#pressY = M.mouseY;
                ModConfigMenu.#lastY = M.mouseY;
                ModConfigMenu.#dragged = false;
                ModConfigMenu.#pressInList = inside && !ModConfigMenu.#guard;
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

    // Um rótulo de escolha centrado em `x`, encolhido se não couber em
    // `width` (o ScaleLabelToFit do jogo). Usa uma cópia: o modelo é do jogo.
    // `centered`: o alinhamento vira centro na horizontal (AnchorType: 1
    // esquerda, 2 centro, 4 direita; 8, 16 e 32 a vertical).
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

    static #State(row) {
        let st = ModConfigMenu.#rowState.get(row.o);
        if (!st) ModConfigMenu.#rowState.set(row.o, st = { scale: new Ref(1), value: new Ref(0), drag: null });
        return st;
    }

    static #DrawOption(T, S, P, row) {
        const { entry, o } = row;
        const st = ModConfigMenu.#State(row);
        const value = entry.inst[o.key];
        const button = T.Button[ModConfigMenu.#DRAW];
        const text = T.String[ModConfigMenu.#TEXT];
        const panel = T.Panel[ModConfigMenu.#PANEL];

        if (o.type === 'range') {
            const t = S.SliderTemplate;
            button(t.Title, null, o.label, false, st.scale, false, ModConfigMenu.#Focus(), false, false);
            if (!st.drag) {
                st.drag = T.DragState.new();
                st.drag['void .ctor()']();
            }
            // A faixa do jogo trabalha de 0 a 1; o valor do mod sai do degrau.
            st.value.value = (value - o.min) / (o.max - o.min);
            // Como o GUISliderSetting do jogo: a faixa só pega o toque que
            // começou nela (sem isso, um toque em qualquer lugar a movia), o
            // forceOver é o "arrastando" dela e o retorno do Draw diz se
            // continua arrastando. O GUISlider.Draw nunca zera o wasDragging
            // (quem zera é quem chama): sem isso, a faixa seguia o dedo para
            // sempre e prendia o toque.
            const M = Terraria.Main;
            const region = T.Slider['Rectangle GetRegion(Slider_Layout layout)'](t.Option);
            if (ModConfigMenu.#fresh && ModConfigMenu.#pressInList
                && M.mouseX >= region.X && M.mouseX <= region.X + region.Width
                && M.mouseY >= region.Y && M.mouseY <= region.Y + region.Height) ModConfigMenu.#slider = o;
            const mine = ModConfigMenu.#slider === o;
            const dragging = !!T.Slider[ModConfigMenu.#SLIDER](t.Option, !mine, st.value, st.drag, null,
                !!st.drag.wasDragging, -1, -1, false);
            st.drag.wasDragging = dragging && mine;
            if (mine) ConfigLoader.Set(entry, o, o.min + st.value.value * (o.max - o.min));
            const now = entry.inst[o.key];
            const digits = String(o.step).includes('.') ? String(o.step).split('.')[1].length : 0;
            text(t.Value, now.toFixed(digits) + (o.suffix || ''), t.Value.Color, false);
            return;
        }

        const t = S.ToggleTemplate;
        const state = Number(button(t.ToggleButton, null, o.label, false, st.scale, false, ModConfigMenu.#Focus(), false, false));

        // No modelo do jogo, a Option1 fica à DIREITA (ligado, verde) e a
        // Option2 à esquerda (desligado, vermelho).
        if (o.type === 'toggle') {
            if (state === ModConfigMenu.#CLICKED && !ModConfigMenu.#RowBlocked()) {
                ConfigLoader.Set(entry, o, !value);
                SoundEngine.PlaySound(ModConfigMenu.#TICK);
            }
            const on = !!entry.inst[o.key];
            // O apagado primeiro e o aceso por cima, como o jogo: os dois se
            // sobrepõem no meio, e na ordem inversa o cinza cobria o verde.
            if (on) {
                panel(t.Option2Disabled, false, null, null, null);
                panel(t.Option1Enabled, false, null, null, null);
            } else {
                panel(t.Option1Disabled, false, null, null, null);
                panel(t.Option2Enabled, false, null, null, null);
            }
            ModConfigMenu.#Label(T, P, t.Option1Label, t.Option1Label.Location.X, t.Option1Enabled.Size.X,
                ModConfigMenu.#Language('Mobile.ToggleSettingOn'), on ? t.Option1Label.Color : t.Option1DisabledLabelColour);
            ModConfigMenu.#Label(T, P, t.Option2Label, t.Option2Label.Location.X, t.Option2Enabled.Size.X,
                ModConfigMenu.#Language('Mobile.ToggleSettingOff'), on ? t.Option2DisabledLabelColour : t.Option2Label.Color);
            return;
        }

        // Radio: os painéis do interruptor viram um por escolha, lado a lado,
        // da esquerda para a direita. Com mais de duas escolhas o espaço cresce
        // para a esquerda. O toque na linha escolhe a que está sob o dedo;
        // fora delas, passa para a próxima.
        // Estas peças ficam presas à direita, e o X delas pode crescer para a
        // esquerda: `dir` converte para "da esquerda para a direita na tela".
        const a = t.Option1Enabled, b = t.Option2Enabled;
        const y = a.Location.Y;
        const anchorPos = (x) => T.Calc[ModConfigMenu.#ANCHORED](a.AnchorControl, a.Anchor, Vector2.new(x, y));
        const dir = Math.sign((anchorPos(a.Location.X).X - anchorPos(b.Location.X).X) / (a.Location.X - b.Location.X)) || 1;
        const ua = a.Location.X * dir, ub = b.Location.X * dir;
        const right = Math.max(ua + a.Size.X / 2, ub + b.Size.X / 2);
        const span = right - Math.min(ua - a.Size.X / 2, ub - b.Size.X / 2);
        const n = o.choices.length;
        const left = right - span * Math.max(1, n / 2);
        const each = (right - left) / n;
        const xOf = (i) => (left + each * (i + 0.5)) * dir;
        if (state === ModConfigMenu.#CLICKED && !ModConfigMenu.#RowBlocked()) {
            const M = Terraria.Main;
            let picked = -1;
            for (let i = 0; i < n; i++) {
                const c = anchorPos(xOf(i));
                if (Math.abs(M.mouseX - c.X) <= each / 2 && Math.abs(M.mouseY - c.Y) <= a.Size.Y / 2 + 4) picked = i;
            }
            if (picked < 0) picked = (o.choices.indexOf(value) + 1) % n;
            ConfigLoader.Set(entry, o, o.choices[picked]);
            SoundEngine.PlaySound(ModConfigMenu.#TICK);
        }
        // As apagadas primeiro e a escolhida por cima (ver o interruptor);
        // os rótulos depois de todos os painéis.
        const selected = o.choices.indexOf(entry.inst[o.key]);
        const order = [...Array(n).keys()].filter((i) => i !== selected);
        if (selected >= 0) order.push(selected);
        for (const i of order) {
            const seg = i === selected ? P.choiceOn : P.choiceOff;
            seg.Location = Vector2.new(xOf(i), y);
            seg.Size = Vector2.new(each - 2, a.Size.Y);
            panel(seg, false, null, null, null);
        }
        for (let i = 0; i < n; i++) {
            ModConfigMenu.#Label(T, P, t.Option1Label, xOf(i), each, o.choiceLabels[i],
                i === selected ? t.Option1Label.Color : t.Option1DisabledLabelColour, true);
        }
    }
}
