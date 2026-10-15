// As barras de chefe de mod (ModBossBar) e os GlobalBossBar. O
// BigProgressBarSystem do jogo escolhe o NPC no Update e desenha no Draw
// (Main.DrawInterface_15). Aqui:
//   - o NPC que o jogo acompanha, se tem barra de mod, passa pelo ModifyInfo
//     dela (false: o jogo procura outro);
//   - sem nada acompanhado, um NPC de mod com barra também ganha a dele (o
//     jogo só acompanha quem tem cabeça de chefe), procurado a cada 30 quadros;
//   - o desenho é o das barras do jogo no celular: GUIBossBar.Draw (painéis
//     do GUIPanel, embaixo do minimapa), entre o PreDraw e o PostDraw. O
//     DrawFancyBar do PC não tem quem o chame no celular;
//   - as barras do jogo passam pelos GlobalBossBar.
// O que o BossBarDrawParams muda e o GUIBossBar.Draw não recebe (cor da
// faixa, do ícone, texto) entra nos desenhos de dentro dele. A posição é a
// do layout do celular: o BarCenter diz onde ela está, mas mudar não a move
// (nem o TextOffset), e o ícone é um botão do layout (o IconScale não vale).
class BossBarLoader {
    static #state = null;   // a barra deste quadro: { bar, index, life, lifeMax, shield, shieldMax }
    static #tracked = -1;   // o NPC que só a barra de mod acompanha
    static #scanAge = 0;
    static #SCAN_EVERY = 30;

    static #system = null;      // o BigProgressBarSystem no Draw
    static #drawing = false;    // dentro do GUIBossBar.Draw que o Bunny Loader chamou
    static #look = null;        // o desenho mudado deste GUIBossBar.Draw
    static #panel = null;       // o Panel_Layout da barra (o centro dela)

    static #GUI_DRAW = 'void Draw(float life, float lifeMax, Texture2D bossIconTexture, Rectangle bossIconFrame, float shield, float shieldMax)';
    static #REGION = 'Rectangle Region(Panel_Layout layout)';

    static Add(inst) {
        BossBarLoader.WantGlobals();
    }

    static WantGlobals() {
        Hooks.Once('bossbar', () => Safe.Run('ModBossBar: ganchos', () => BossBarLoader.#Hook()));
        Hooks.Once('bossbar.mobile', () => Safe.Run('ModBossBar: ganchos do desenho', () => BossBarLoader.#HookMobile()));
    }

    // A barra de mod do NPC (o BossBar do ModNPC dele), ou null.
    static Of(npc) {
        if (!npc || !npc.active || npc.type < FIRST_NPC) return null;
        const m = NPCLoader.Of(npc);
        return m && m.BossBar instanceof ModBossBar ? m.BossBar : null;
    }

    // O ValidateAndCollectNecessaryInfo do tModLoader.
    static #Collect(bar, index) {
        const Main = Terraria.Main;
        if (index < 0 || index >= 200) return null;
        const info = { npcIndexToAimAt: index };
        const life = new Ref(0), lifeMax = new Ref(0), shield = new Ref(0), shieldMax = new Ref(0);
        const decided = Safe.Run(bar.constructor.name + '.ModifyInfo', () => bar.ModifyInfo(info, life, lifeMax, shield, shieldMax));
        const at = Number.isInteger(info.npcIndexToAimAt) ? info.npcIndexToAimAt : index;
        if (decided === false || at < 0 || at >= 200) return null;
        if (decided !== true) {
            const npc = Main.npc[at];
            if (!npc.active) return null;
            lifeMax.value = npc.lifeMax;
            life.value = Math.max(0, Math.min(npc.life, npc.lifeMax));
        }
        bar.Collected(life.value, lifeMax.value, shield.value, shieldMax.value);
        return { bar, index: at, life: life.value, lifeMax: lifeMax.value, shield: shield.value, shieldMax: shieldMax.value };
    }

    // O NPC de mod com barra mais perto do jogador, na tela aumentada em 5000 (o do jogo).
    static #Find() {
        const Main = Terraria.Main;
        const me = Main.player[Main.myPlayer];
        const cx = me.position.X + me.width / 2, cy = me.position.Y + me.height / 2;
        const sx = Main.screenPosition.X, sy = Main.screenPosition.Y;
        let best = -1, bestDistance = Infinity;
        for (let i = 0; i < 200; i++) {
            const npc = Main.npc[i];
            if (!npc.active || npc.type < FIRST_NPC) continue;
            const x = npc.position.X, y = npc.position.Y;
            if (x + npc.width < sx - 5000 || x > sx + Main.screenWidth + 5000 ||
                y + npc.height < sy - 5000 || y > sy + Main.screenHeight + 5000) continue;
            if (!BossBarLoader.Of(npc)) continue;
            const d = Math.hypot(x + npc.width / 2 - cx, y + npc.height / 2 - cy);
            if (d < bestDistance) {
                best = i;
                bestDistance = d;
            }
        }
        return best;
    }

    static #Update(system) {
        const self = BossBarLoader;
        const Main = Terraria.Main;
        self.#state = null;
        if (system._currentBar) {
            self.#tracked = -1;
            const index = system._info.npcIndexToAimAt;
            const bar = index >= 0 && index < 200 ? self.Of(Main.npc[index]) : null;
            if (!bar) return;
            self.#state = self.#Collect(bar, index);
            if (!self.#state) system._currentBar = null;
            return;
        }
        if (self.#tracked >= 0) {
            const bar = self.Of(Main.npc[self.#tracked]);
            self.#state = bar ? self.#Collect(bar, self.#tracked) : null;
            if (self.#state) return;
            self.#tracked = -1;
        }
        if (++self.#scanAge < self.#SCAN_EVERY) return;
        self.#scanAge = 0;
        const found = self.#Find();
        if (found < 0) return;
        self.#state = self.#Collect(self.Of(Main.npc[found]), found);
        if (self.#state) self.#tracked = found;
    }

    // O BossBarDrawParams com os valores do jogo. Sem a UI_BossBar do PC: o
    // BarTexture é null.
    static #Params(icon, frame, life, lifeMax, shield, shieldMax) {
        const size = Terraria.Main.ScreenSize;
        return {
            BarTexture: null,
            BarCenter: BossBarLoader.#Center() || Vector2.new(size.X * 0.5, size.Y - 50),
            BarColor: Color.White,
            IconTexture: icon,
            IconFrame: frame,
            IconColor: Color.White,
            Life: life, LifeMax: lifeMax, Shield: shield, ShieldMax: shieldMax,
            IconScale: 1,
            ShowText: true,
            TextOffset: Vector2.new(0, 0),
        };
    }

    // O centro do painel da barra, visto no último desenho.
    static #Center() {
        const panel = BossBarLoader.#panel;
        if (!panel) return null;

        const r = bl.classOf('', 'GUIPanel')[BossBarLoader.#REGION](panel);
        return Vector2.new(r.X + r.Width / 2, r.Y + r.Height / 2);
    }

    static #DrawModBar(state, spriteBatch) {
        const { bar } = state;
        const gui = bl.classOf('', 'GUIInstance').Active?.GUIBossBar;
        if (!gui) return;

        const npc = Terraria.Main.npc[state.index];
        const frame = new Ref(null);
        const icon = Safe.Run(bar.constructor.name + '.GetIconTexture', () => bar.GetIconTexture(frame)) ||
            Terraria.GameContent.TextureAssets.NpcHead[0];
        const texture = 'Value' in icon ? icon.Value : icon;
        const params = BossBarLoader.#Params(texture, frame.value || Rectangle.new(0, 0, texture.Width, texture.Height),
            state.life, state.lifeMax, state.shield, state.shieldMax);

        // Sem escudo, o -1 e 1 que as barras do jogo passam.
        BossBarLoader.#Run(spriteBatch, npc, params, bar, (p) => {
            const shielded = p.ShieldMax > 0;
            gui[BossBarLoader.#GUI_DRAW](p.Life, p.LifeMax, p.IconTexture, p.IconFrame,
                shielded ? p.Shield : -1, shielded ? p.ShieldMax : 1);
        });
    }

    // A ordem do tModLoader: PreDraw dos Global, depois o da barra de mod;
    // PostDraw da barra de mod, depois os dos Global.
    static #Run(spriteBatch, npc, params, bar, draw) {
        const before = BossBarLoader.#Snapshot(params);

        let ok = globalBossBars.All('PreDraw', (g) => g.PreDraw(spriteBatch, npc, params));
        if (ok && bar) ok = Safe.Run(bar.constructor.name + '.PreDraw', () => bar.PreDraw(spriteBatch, npc, params)) !== false;
        if (ok) BossBarLoader.#Draw(params, before, draw);

        if (bar) Safe.Run(bar.constructor.name + '.PostDraw', () => bar.PostDraw(spriteBatch, npc, params));
        globalBossBars.Each('PostDraw', (g) => g.PostDraw(spriteBatch, npc, params));
    }

    static #Draw(params, before, draw) {
        const self = BossBarLoader;
        const after = self.#Snapshot(params);
        const look = self.#Changed(before, after) ? self.#Look(params, after) : null;

        self.#drawing = true;
        self.#look = look;
        if (look || !self.#panel) bl.hookFlags.set('bossbar.mobile', true);
        try {
            draw(params);
        } finally {
            self.#drawing = false;
            self.#look = null;
            bl.hookFlags.set('bossbar.mobile', false);
        }
    }

    // Em números: o struct que o mod mexe (BarColor.R = ...) é o mesmo.
    static #Snapshot(p) {
        const packed = (c) => ((c.R << 24) | (c.G << 16) | (c.B << 8) | c.A) >>> 0;
        return { bar: packed(p.BarColor), icon: packed(p.IconColor), text: p.ShowText !== false };
    }

    static #Changed(a, b) {
        for (const key of Object.keys(a)) {
            if (a[key] !== b[key]) return true;
        }
        return false;
    }

    static #Look(params, after) {
        return {
            barColor: after.bar !== 0xFFFFFFFF ? params.BarColor : null,
            iconColor: after.icon !== 0xFFFFFFFF ? params.IconColor : null,
            iconAddress: bl.addressOf(params.IconTexture),
            showText: after.text,
        };
    }

    static #Hook() {
        const System = Terraria.GameContent.UI.BigProgressBar.BigProgressBarSystem;
        System['void Update()'].hook((original, self) => {
            original(self);
            Safe.Run('ModBossBar: Update', () => BossBarLoader.#Update(self));
        });
        System['void Draw(SpriteBatch spriteBatch)'].hook((original, self, spriteBatch) => {
            BossBarLoader.#system = self;
            const state = BossBarLoader.#state;
            if (!state) return original(self, spriteBatch);
            Safe.Run('ModBossBar: Draw', () => BossBarLoader.#DrawModBar(state, spriteBatch));
            return undefined;
        });
    }

    // O GUIBossBar.Draw: as barras do jogo passam pelos GlobalBossBar; o
    // desenho de dentro dele (de todas) segue o BossBarDrawParams.
    static #HookMobile() {
        const GUIBossBar = bl.classOf('', 'GUIBossBar');
        const GUIPanel = bl.classOf('', 'GUIPanel');
        const draw = GUIBossBar[BossBarLoader.#GUI_DRAW];

        draw.hook((original, self, life, lifeMax, icon, frame, shield, shieldMax) => {
            const run = () => original(self, life, lifeMax, icon, frame, shield, shieldMax);
            if (BossBarLoader.#drawing || !globalBossBars.list.length) return run();

            const system = BossBarLoader.#system;
            const index = system ? system._info.npcIndexToAimAt : -1;
            const npc = index >= 0 && index < 200 ? Terraria.Main.npc[index] : null;
            if (!npc) return run();

            const params = BossBarLoader.#Params(icon, frame, life, lifeMax, shield, shieldMax);
            BossBarLoader.#Run(Terraria.Main.spriteBatch, npc, params, null,
                (p) => original(self, p.Life, p.LifeMax, p.IconTexture, p.IconFrame, p.Shield, p.ShieldMax));
            return undefined;
        });

        const gate = { whileIn: draw, flag: 'bossbar.mobile' };
        GUIPanel[BossBarLoader.#REGION].hook((original, layout) => {
            const region = original(layout);
            if (!BossBarLoader.#panel) BossBarLoader.#panel = layout;
            return region;
        }, gate);

        GUIPanel['void DrawBacking(Vector2 position, Vector2 size, Texture2D texture, Color color, Nullable`1 sourceFrame)'].hook(
            (original, position, size, texture, color, source) => {
                const look = BossBarLoader.#look;
                if (!look || !look.barColor) return original();
                return original(position, size, texture, BossBarLoader.#Tint(color, look.barColor), source);
            }, gate);

        // O ícone é um botão do GUITransactionButton, que aceita a cor do item.
        bl.classOf('', 'GUITransactionButton')['GUITransactionButton.InputState Draw(TransactionButton_Layout layout, Texture2D itemTexture, Rectangle srcRect, string label, bool disabled, ref float scale, Nullable`1 overloadedItemColour, bool forcedPressed, bool hasControllerFocus, bool forceOver, bool disablePressedState)'].hook(
            (original, layout, texture, source, label, disabled, scale, colour, pressed, focus, over, noPressed) => {
                const look = BossBarLoader.#look;
                if (!look || !look.iconColor || bl.addressOf(texture) !== look.iconAddress) return original();
                return original(layout, texture, source, label, disabled, scale, look.iconColor, pressed, focus, over, noPressed);
            }, gate);

        const GUIString = bl.classOf('', 'GUIString');
        for (const signature of ['void Draw(String_Layout layout, StringBuilder value)', 'void Draw(String_Layout layout, StringBuilder value, Color overloadedColour)']) {
            GUIString[signature].hook((original) => {
                const look = BossBarLoader.#look;
                return look && !look.showText ? undefined : original();
            }, gate);
        }
    }

    // As duas cores multiplicadas, como o Color * Color do XNA.
    static #Tint(color, by) {
        return Color.new(color.R * by.R / 255, color.G * by.G / 255, color.B * by.B / 255, color.A * by.A / 255);
    }
}
