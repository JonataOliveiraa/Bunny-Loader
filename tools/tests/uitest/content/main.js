// A UI dos mods (script/js/mod/UI + ModSystem.ModifyInterfaceLayers), com
// toques de verdade: o teste loga "TAP x y" / "SWIPE x1 y1 x2 y2 ms" em
// unidades de UI e o roteiro toca (UISCALE diz a escala). Confere layout,
// clique, duplo clique, arrasto do painel, rolagem da lista por arrasto (sem
// virar clique), a barra, a camada escondida (Active = false) e que o toque no
// painel não vai para o mundo. Loga "uitest ..." e "uitest FIM".
const Main = Terraria.Main;
const log = (s) => bl.log('uitest ' + s);

let fails = 0;
function check(label, ok, detail) {
    if (!ok) fails++;
    log(label + ': ' + (ok ? 'ok' : 'FALHOU') + (detail === undefined ? '' : ' (' + detail + ')'));
}
const modErrors = [];
const blError = bl.error;
bl.error = (text) => {
    modErrors.push(String(text).split('\n')[0]);
    return blError(text);
};

const counts = { button: 0, double: 0, item: 0, layerDraw: 0, invasion: 0, panelDown: 0, update: 0 };
const ui = { state: null, iface: null, hideInvasion: false, uiWidth: 0 };

class TestDraggablePanel extends UIPanel {
    offset = null;
    dragging = false;

    LeftMouseDown(evt) {
        super.LeftMouseDown(evt);
        if (evt.Target === this) {
            counts.panelDown++;
            this.offset = { X: evt.MousePosition.X - this.Left.Pixels, Y: evt.MousePosition.Y - this.Top.Pixels };
            this.dragging = true;
        }
    }

    LeftMouseUp(evt) {
        super.LeftMouseUp(evt);
        if (evt.Target === this) this.dragging = false;
    }

    Update(gameTime) {
        super.Update(gameTime);
        if (this.dragging) {
            this.Left.Set(Main.mouseX - this.offset.X, 0);
            this.Top.Set(Main.mouseY - this.offset.Y, 0);
            this.Recalculate();
        }
    }
}

class TestState extends UIState {
    OnInitialize() {
        const panel = this.panel = new TestDraggablePanel();
        panel.SetPadding(0);
        panel.Left.Set(80, 0);
        panel.Top.Set(60, 0);
        panel.Width.Set(220, 0);
        panel.Height.Set(110, 0);
        panel.BackgroundColor = Color.new(73, 94, 171);

        const button = this.button = new UIImageButton(ModContent.Request('Terraria/Images/UI/ButtonFavoriteActive'));
        button.Left.Set(170, 0);
        button.Top.Set(10, 0);
        button.OnLeftClick.Add(() => counts.button++);
        button.OnLeftDoubleClick = () => counts.double++;
        panel.Append(button);

        const title = this.title = new UIText('Bunny UI', 1, false);
        title.Left.Set(12, 0);
        title.Top.Set(12, 0);
        panel.Append(title);

        const label = this.label = new UITextPanel('0 cliques', 0.8);
        label.Left.Set(12, 0);
        label.Top.Set(50, 0);
        panel.Append(label);
        this.Append(panel);

        const list = this.list = new UIList();
        list.Left.Set(360, 0);
        list.Top.Set(60, 0);
        list.Width.Set(180, 0);
        list.Height.Set(200, 0);
        for (let i = 0; i < 20; i++) {
            const item = new UITextPanel('Item ' + i, 0.8);
            item.Width.Set(0, 1);
            item.OnLeftClick.Add(() => counts.item++);
            list.Add(item);
        }
        this.Append(list);

        const bar = this.bar = new UIScrollbar();
        bar.Left.Set(545, 0);
        bar.Top.Set(60, 0);
        bar.Height.Set(200, 0);
        list.SetScrollbar(bar);
        this.Append(bar);
    }
}

export class TestUISystem extends ModSystem {
    PostSetupContent() {
        ui.iface = new UserInterface();
        ui.state = new TestState();
        ui.state.Activate();
    }

    UpdateUI(gameTime) {
        counts.update++;
        if (ui.iface && ui.iface.CurrentState) ui.iface.Update(gameTime);
    }

    ModifyInterfaceLayers(layers) {
        ui.layerNames = layers.map((l) => l.Name);
        if (ui.hideInvasion) {
            const i = layers.FindIndex((l) => l.Name === 'Vanilla: Invasion Progress Bars');
            if (i >= 0) layers[i].Active = false;
        }
        const mouseText = layers.FindIndex((l) => l.Name === 'Vanilla: Mouse Text');
        if (mouseText < 0) return;
        layers.Insert(mouseText, ui.layer || (ui.layer = new LegacyGameInterfaceLayer('UITest: Painel', () => {
            counts.layerDraw++;
            ui.uiWidth = Main.screenWidth;
            if (ui.iface && ui.iface.CurrentState) ui.iface.Draw(Main.spriteBatch, null);
            return true;
        }, InterfaceScaleType.UI)));
    }
}

Main['void DrawInterface_15_InvasionProgressBars()'].hook((original) => {
    counts.invasion++;
    original();
});

const center = (el) => {
    const d = el.GetDimensions();
    return { X: d.X + d.Width / 2, Y: d.Y + d.Height / 2 };
};
const tap = (p) => log('TAP ' + p.X.toFixed(1) + ' ' + p.Y.toFixed(1));
const swipe = (a, b, ms) => log('SWIPE ' + a.X.toFixed(1) + ' ' + a.Y.toFixed(1) + ' ' + b.X.toFixed(1) + ' ' + b.Y.toFixed(1) + ' ' + ms);
const player = () => Main.player[Main.myPlayer];

const state = {};
const phases = [
    {
        name: 'montar', frames: 40,
        start() {
            ui.iface.SetState(ui.state);
        },
        end() {
            check('a camada do mod desenha', counts.layerDraw > 20, counts.layerDraw);
            check('as camadas do jogo chegam com nome', ui.layerNames && ui.layerNames.includes('Vanilla: Mouse Text') && ui.layerNames.length > 30,
                ui.layerNames ? ui.layerNames.length + ' camadas' : 'nenhuma');
            log('camadas: ' + ui.layerNames.join(' > '));
            log('UISCALE ' + ui.uiWidth);
            const s = ui.state;
            const d = s.panel.GetDimensions();
            check('layout do painel (Left/Top/Width/Height)', d.X === 80 && d.Y === 60 && d.Width === 220 && d.Height === 110, JSON.stringify(d));
            const b = s.button.GetDimensions();
            check('botao com o tamanho da textura do jogo', b.Width > 10 && b.Height > 10 && b.X === 250 && b.Y === 70, JSON.stringify(b));
            check('UIText mede o texto', s.title.MinWidth.Pixels > 30 && s.title.MinHeight.Pixels > 10, s.title.MinWidth.Pixels + 'x' + s.title.MinHeight.Pixels);
            check('UITextPanel do tamanho do texto', s.label.GetDimensions().Width >= s.label.MinWidth.Pixels && s.label.MinWidth.Pixels > 40, s.label.MinWidth.Pixels);
            check('lista: altura total de 20 itens', s.list.GetTotalHeight() > 400, s.list.GetTotalHeight());
            check('UIState ocupa a tela de UI', s.GetDimensions().Width === ui.uiWidth, s.GetDimensions().Width + ' x ' + ui.uiWidth);
            log('SHOT ui');
        },
    },
    {
        name: 'clique', frames: 150,
        start() {
            state.before = counts.button;
            tap(center(ui.state.button));
        },
        end() {
            check('toque no botao dispara OnLeftClick', counts.button === state.before + 1, counts.button - state.before);
            ui.state.label.SetText(counts.button + ' cliques');
        },
    },
    {
        name: 'duplo', frames: 150,
        start() {
            state.before = counts.double;
            const c = center(ui.state.button);
            tap(c);
            tap(c);
        },
        end() {
            check('dois toques rapidos disparam OnLeftDoubleClick', counts.double === state.before + 1, counts.double - state.before);
        },
    },
    {
        name: 'mundo', frames: 120,
        start() {
            state.down = counts.panelDown;
            state.maxAnim = 0;
            state.update0 = counts.update;
            state.leftFrames = 0;
            state.lastMouse = '';
            const d = ui.state.panel.GetDimensions();
            tap({ X: d.X + 30, Y: d.Y + 95 });
        },
        tick() {
            state.maxAnim = Math.max(state.maxAnim, player().itemAnimation);
            if (Main.mouseLeft) {
                state.leftFrames++;
                state.lastMouse = Main.mouseX + ',' + Main.mouseY;
            }
        },
        end() {
            log('diagnostico: UpdateUI ' + (counts.update - state.update0) + ' vezes, mouseLeft em ' + state.leftFrames + ' quadros, em ' + state.lastMouse);
            check('o painel recebeu o toque', counts.panelDown === state.down + 1, counts.panelDown - state.down);
            check('o toque no painel nao usa o item (nao vai para o mundo)', state.maxAnim === 0, 'itemAnimation max ' + state.maxAnim);
        },
    },
    {
        name: 'arrastar', frames: 150,
        start() {
            const d = ui.state.panel.GetDimensions();
            state.left0 = ui.state.panel.Left.Pixels;
            state.top0 = ui.state.panel.Top.Pixels;
            const from = { X: d.X + 30, Y: d.Y + 95 };
            swipe(from, { X: from.X + 120, Y: from.Y + 60 }, 800);
        },
        end() {
            const dx = ui.state.panel.Left.Pixels - state.left0, dy = ui.state.panel.Top.Pixels - state.top0;
            check('arrastar move o painel', Math.abs(dx - 120) < 12 && Math.abs(dy - 60) < 12, 'dx ' + dx.toFixed(1) + ' dy ' + dy.toFixed(1));
        },
    },
    {
        name: 'lista', frames: 150,
        start() {
            state.view0 = ui.state.list.ViewPosition;
            state.items = counts.item;
            const c = center(ui.state.list);
            swipe({ X: c.X, Y: c.Y + 60 }, { X: c.X, Y: c.Y - 60 }, 700);
        },
        end() {
            const moved = ui.state.list.ViewPosition - state.view0;
            check('arrastar a lista rola', moved > 60, 'rolou ' + moved.toFixed(1));
            check('rolar nao clica nos itens', counts.item === state.items, counts.item - state.items);
            check('a barra acompanha a lista', Math.abs(ui.state.bar.ViewPosition - ui.state.list.ViewPosition) < 0.01);
        },
    },
    {
        name: 'item', frames: 150,
        start() {
            state.items = counts.item;
            const d = ui.state.list.GetDimensions();
            tap({ X: d.X + d.Width / 2, Y: d.Y + 20 });
        },
        end() {
            check('toque parado num item da lista clica', counts.item === state.items + 1, counts.item - state.items);
        },
    },
    {
        name: 'barra', frames: 150,
        start() {
            state.view0 = ui.state.list.ViewPosition;
            const d = ui.state.bar.GetDimensions();
            tap({ X: d.X + 10, Y: d.Y + d.Height - 12 });
        },
        end() {
            check('tocar no fim da barra rola para baixo', ui.state.list.ViewPosition > state.view0 + 50,
                state.view0.toFixed(1) + ' -> ' + ui.state.list.ViewPosition.toFixed(1));
        },
    },
    {
        name: 'camada', frames: 40,
        start() {
            ui.hideInvasion = true;
        },
        tick(t) {
            if (t === 5) state.inv0 = counts.invasion;
        },
        end() {
            check('Active = false esconde a camada do jogo', counts.invasion === state.inv0, (counts.invasion - state.inv0) + ' desenhos');
            ui.hideInvasion = false;
        },
    },
    {
        name: 'volta', frames: 40,
        tick(t) {
            if (t === 5) state.inv0 = counts.invasion;
        },
        end() {
            check('a camada volta quando o mod para de esconder', counts.invasion > state.inv0, (counts.invasion - state.inv0) + ' desenhos');
        },
    },
    {
        name: 'fechar', frames: 30,
        start() {
            ui.iface.SetState(null);
            state.draw0 = null;
        },
        tick(t) {
            if (t === 5) state.draw0 = counts.layerDraw;
        },
        end() {
            check('SetState(null): a camada roda mas nao desenha UI', ui.iface.CurrentState === null && !ui.iface.IsVisible);
            const unexpected = modErrors;
            check('nenhum erro de mod', unexpected.length === 0, unexpected.join(' | '));
        },
    },
];

let frame = 0, index = 0, local = 0, done = false;
function step() {
    if (frame < 90) return;
    if (index >= phases.length) {
        log('FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
        done = true;
        return;
    }
    const p = phases[index];
    if (local === 0) {
        log('fase ' + p.name);
        if (p.start) p.start();
    }
    if (p.tick) p.tick(local);
    local++;
    if (local >= p.frames) {
        if (p.end) p.end();
        index++;
        local = 0;
    }
}

Main['void DoDraw(GameTime gameTime)'].hook((original, self, gameTime) => {
    if (!done && !Main.gameMenu) {
        frame++;
        const p = player();
        p.statLife = p.statLifeMax2;
        try {
            step();
        } catch (e) {
            fails++;
            log('erro no quadro ' + frame + ' (fase ' + (phases[index] ? phases[index].name : '?') + '): ' + e + (e.stack ? ' ' + e.stack : ''));
            index++;
            local = 0;
        }
    }
    original(self, gameTime);
});
log('carregado');

export default class TestUI extends Mod {}
