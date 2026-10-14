// As barras de chefe de mod (ModBossBar) e os GlobalBossBar. O
// BigProgressBarSystem do jogo escolhe o NPC no Update e desenha no Draw
// (Main.DrawInterface_15). Aqui:
//   - o NPC que o jogo acompanha, se tem barra de mod, passa pelo ModifyInfo
//     dela (false: o jogo procura outro);
//   - sem nada acompanhado, um NPC de mod com barra também ganha a dele (o
//     jogo só acompanha quem tem cabeça de chefe), procurado a cada 30 quadros;
//   - o Draw dessas usa o DrawFancyBar do jogo, entre o PreDraw e o PostDraw;
//   - as barras do jogo passam pelos GlobalBossBar. No celular elas não usam o
//     DrawFancyBar: desenham pelo GUIBossBar.Draw (painéis do GUIPanel).
// O que o BossBarDrawParams muda e o desenho não recebe (cores, centro,
// escala do ícone, texto) entra nos desenhos de dentro dele (#Restyle e #MobileRestyle).
class BossBarLoader {
    static #state = null;   // a barra deste quadro: { bar, index, life, lifeMax, shield, shieldMax }
    static #tracked = -1;   // o NPC que só a barra de mod acompanha
    static #scanAge = 0;
    static #SCAN_EVERY = 30;

    static #system = null;      // o BigProgressBarSystem no Draw
    static #drawing = false;    // dentro do DrawFancyBar que o Bunny Loader chamou
    static #look = null;        // o desenho mudado deste DrawFancyBar
    static #barTexture = undefined;   // a UI_BossBar, vista no primeiro desenho do jogo
    static #mobileLook = null;   // o desenho mudado deste GUIBossBar.Draw
    static #panel = null;        // o Panel_Layout da barra do celular (o centro dela)

    static #FANCY = 'void DrawFancyBar(SpriteBatch spriteBatch, float lifeAmount, float lifeMax, Texture2D barIconTexture, Rectangle barIconFrame)';
    static #FANCY_SHIELD = 'void DrawFancyBar(SpriteBatch spriteBatch, float lifeAmount, float lifeMax, Texture2D barIconTexture, Rectangle barIconFrame, float shieldCurrent, float shieldMax)';

    static Add(inst) {
        Hooks.Once('bossbar', () => Safe.Run('ModBossBar: ganchos', () => BossBarLoader.#Hook()));
    }

    static WantGlobals() {
        Hooks.Once('bossbar', () => Safe.Run('ModBossBar: ganchos', () => BossBarLoader.#Hook()));
        Hooks.Once('bossbar.mobile', () => Safe.Run('GlobalBossBar: ganchos', () => BossBarLoader.#HookMobile()));
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

    // O BossBarDrawParams com os valores do jogo.
    static #Params(icon, frame, life, lifeMax, shield, shieldMax) {
        const size = Terraria.Main.ScreenSize;
        return {
            BarTexture: BossBarLoader.#barTexture,
            BarCenter: Vector2.new(size.X * 0.5, size.Y - 50),
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

    static #DrawModBar(state, spriteBatch) {
        const { bar } = state;
        const npc = Terraria.Main.npc[state.index];
        const frame = new Ref(null);
        const icon = Safe.Run(bar.constructor.name + '.GetIconTexture', () => bar.GetIconTexture(frame)) ||
            Terraria.GameContent.TextureAssets.NpcHead[0];
        const texture = 'Value' in icon ? icon.Value : icon;
        const params = BossBarLoader.#Params(texture, frame.value || Rectangle.new(0, 0, texture.Width, texture.Height),
            state.life, state.lifeMax, state.shield, state.shieldMax);
        BossBarLoader.#Run(spriteBatch, npc, params, bar);
    }

    // Uma barra do jogo, de dentro do DrawFancyBar dela.
    static #DrawVanillaBar(spriteBatch, params, original) {
        const system = BossBarLoader.#system;
        const index = system ? system._info.npcIndexToAimAt : -1;
        const npc = index >= 0 && index < 200 ? Terraria.Main.npc[index] : null;
        if (!npc) return original();

        BossBarLoader.#Run(spriteBatch, npc, params, null);
        return undefined;
    }

    // A ordem do tModLoader: PreDraw dos Global, depois o da barra de mod;
    // PostDraw da barra de mod, depois os dos Global.
    static #Run(spriteBatch, npc, params, bar) {
        const before = BossBarLoader.#Snapshot(params);

        let draw = globalBossBars.All('PreDraw', (g) => g.PreDraw(spriteBatch, npc, params));
        if (draw && bar) draw = Safe.Run(bar.constructor.name + '.PreDraw', () => bar.PreDraw(spriteBatch, npc, params)) !== false;
        if (draw) BossBarLoader.#Fancy(spriteBatch, params, before);

        if (bar) Safe.Run(bar.constructor.name + '.PostDraw', () => bar.PostDraw(spriteBatch, npc, params));
        globalBossBars.Each('PostDraw', (g) => g.PostDraw(spriteBatch, npc, params));
    }

    static #Fancy(spriteBatch, params, before) {
        const Helper = Terraria.GameContent.UI.BigProgressBar.BigProgressBarHelper;
        const after = BossBarLoader.#Snapshot(params);
        const look = BossBarLoader.#Changed(before, after) ? BossBarLoader.#Look(params, before, after) : null;

        BossBarLoader.#drawing = true;
        BossBarLoader.#look = look;
        if (look) bl.hookFlags.set('bossbar.look', true);
        try {
            if (params.Shield > 0 && params.ShieldMax > 0) {
                Helper[BossBarLoader.#FANCY_SHIELD](spriteBatch, params.Life, params.LifeMax, params.IconTexture, params.IconFrame,
                    params.Shield, params.ShieldMax);
            } else {
                Helper[BossBarLoader.#FANCY](spriteBatch, params.Life, params.LifeMax, params.IconTexture, params.IconFrame);
            }
        } finally {
            BossBarLoader.#drawing = false;
            BossBarLoader.#look = null;
            if (look) bl.hookFlags.set('bossbar.look', BossBarLoader.#barTexture === undefined);
        }
    }

    // Em números: o struct que o mod mexe (BarCenter.Y += 10) é o mesmo.
    static #Snapshot(p) {
        const packed = (c) => ((c.R << 24) | (c.G << 16) | (c.B << 8) | c.A) >>> 0;
        return {
            cx: p.BarCenter.X, cy: p.BarCenter.Y,
            bar: packed(p.BarColor), icon: packed(p.IconColor),
            scale: p.IconScale, text: p.ShowText !== false,
            tx: p.TextOffset.X, ty: p.TextOffset.Y,
            texture: p.BarTexture ? bl.addressOf(p.BarTexture) : 0,
        };
    }

    static #Changed(a, b) {
        for (const key of Object.keys(a)) {
            if (a[key] !== b[key]) return true;
        }
        return false;
    }

    static #Look(params, before, after) {
        return {
            dx: after.cx - before.cx, dy: after.cy - before.cy,
            barColor: after.bar !== 0xFFFFFFFF ? params.BarColor : null,
            iconColor: after.icon !== 0xFFFFFFFF ? params.IconColor : null,
            iconScale: after.scale,
            iconAddress: bl.addressOf(params.IconTexture),
            barTexture: after.texture && after.texture !== before.texture ? params.BarTexture : null,
            showText: after.text,
            tx: after.tx, ty: after.ty,
        };
    }

    // Os argumentos de um SpriteBatch.Draw do DrawFancyBar, com o desenho
    // mudado; null deixa como está. O primeiro desenho é sempre o da UI_BossBar.
    static #Restyle(texture, position, color, scale) {
        const icon = BossBarLoader.#look ? bl.addressOf(texture) === BossBarLoader.#look.iconAddress : false;
        if (BossBarLoader.#barTexture === undefined && !icon) {
            BossBarLoader.#barTexture = texture;
            if (!BossBarLoader.#look) bl.hookFlags.set('bossbar.look', false);
        }

        const look = BossBarLoader.#look;
        if (!look) return null;

        const moved = look.dx || look.dy ? Vector2.new(position.X + look.dx, position.Y + look.dy) : position;
        if (icon) {
            const s = look.iconScale;
            return {
                texture,
                position: moved,
                color: look.iconColor || color,
                scale: s === 1 ? scale : typeof scale === 'number' ? scale * s : Vector2.new(scale.X * s, scale.Y * s),
            };
        }

        return {
            texture: look.barTexture || texture,
            position: moved,
            color: look.barColor ? Color.Multiply(look.barColor, color.A / 255) : color,
            scale,
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

        BossBarLoader.#HookFancy();
        BossBarLoader.#HookLook();
    }

    // As barras do jogo: o DrawFancyBar delas passa pelos GlobalBossBar.
    static #HookFancy() {
        const Helper = Terraria.GameContent.UI.BigProgressBar.BigProgressBarHelper;
        const skip = () => BossBarLoader.#drawing || !globalBossBars.list.length;

        Helper[BossBarLoader.#FANCY].hook((original, spriteBatch, life, lifeMax, icon, frame) => {
            if (skip()) return original(spriteBatch, life, lifeMax, icon, frame);

            const params = BossBarLoader.#Params(icon, frame, life, lifeMax, 0, 0);
            return BossBarLoader.#DrawVanillaBar(spriteBatch, params, () => original(spriteBatch, life, lifeMax, icon, frame));
        });

        Helper[BossBarLoader.#FANCY_SHIELD].hook((original, spriteBatch, life, lifeMax, icon, frame, shield, shieldMax) => {
            if (skip()) return original(spriteBatch, life, lifeMax, icon, frame, shield, shieldMax);

            const params = BossBarLoader.#Params(icon, frame, life, lifeMax, shield, shieldMax);
            return BossBarLoader.#DrawVanillaBar(spriteBatch, params,
                () => original(spriteBatch, life, lifeMax, icon, frame, shield, shieldMax));
        });
    }

    // Só entram no JS com a chave ligada: num desenho mudado, ou até achar a UI_BossBar.
    static #HookLook() {
        const Helper = Terraria.GameContent.UI.BigProgressBar.BigProgressBarHelper;
        const SpriteBatch = Microsoft.Xna.Framework.Graphics.SpriteBatch;
        const draws = [
            'void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)',
            'void Draw(Texture2D texture, Vector2 position, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth)',
        ];

        for (const fancy of [BossBarLoader.#FANCY, BossBarLoader.#FANCY_SHIELD]) {
            for (const draw of draws) {
                SpriteBatch[draw].hook((original, self, texture, position, source, color, rotation, origin, scale, effects, depth) => {
                    const args = BossBarLoader.#Restyle(texture, position, color, scale);
                    if (!args) return original();
                    return original(self, args.texture, args.position, source, args.color, rotation, origin, args.scale, effects, depth);
                }, { whileIn: Helper[fancy], flag: 'bossbar.look' });
            }
        }

        Helper['void DrawHealthText(SpriteBatch spriteBatch, Rectangle area, float current, float max)'].hook(
            (original, spriteBatch, area, current, max) => {
                const look = BossBarLoader.#look;
                if (!look) return original();
                if (!look.showText) return undefined;
                return original(spriteBatch, Rectangle.new(area.X + look.dx + look.tx, area.Y + look.dy + look.ty, area.Width, area.Height),
                    current, max);
            }, { flag: 'bossbar.look' });

        bl.hookFlags.set('bossbar.look', true);
    }

    static #GUI_DRAW = 'void Draw(float life, float lifeMax, Texture2D bossIconTexture, Rectangle bossIconFrame, float shield, float shieldMax)';

    // As barras do jogo no celular. A posição é a do layout do celular: o
    // BarCenter diz onde ela está, mas mudar não a move (nem o TextOffset), e
    // o ícone é um botão do layout (o IconScale não vale).
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
            params.BarTexture = null;
            const center = BossBarLoader.#MobileCenter(GUIPanel);
            if (center) params.BarCenter = center;
            BossBarLoader.#RunMobile(self, npc, params, original);
            return undefined;
        });

        const gate = { whileIn: draw, flag: 'bossbar.mobile' };
        GUIPanel['Rectangle Region(Panel_Layout layout)'].hook((original, layout) => {
            const region = original(layout);
            if (!BossBarLoader.#panel) BossBarLoader.#panel = layout;
            return region;
        }, gate);

        GUIPanel['void DrawBacking(Vector2 position, Vector2 size, Texture2D texture, Color color, Nullable`1 sourceFrame)'].hook(
            (original, position, size, texture, color, source) => {
                const look = BossBarLoader.#mobileLook;
                if (!look || !look.barColor) return original();
                return original(position, size, texture, BossBarLoader.#Tint(color, look.barColor), source);
            }, gate);

        // O ícone é um botão do GUITransactionButton, que aceita a cor do item.
        bl.classOf('', 'GUITransactionButton')['GUITransactionButton.InputState Draw(TransactionButton_Layout layout, Texture2D itemTexture, Rectangle srcRect, string label, bool disabled, ref float scale, Nullable`1 overloadedItemColour, bool forcedPressed, bool hasControllerFocus, bool forceOver, bool disablePressedState)'].hook(
            (original, layout, texture, source, label, disabled, scale, colour, pressed, focus, over, noPressed) => {
                const look = BossBarLoader.#mobileLook;
                if (!look || !look.iconColor || bl.addressOf(texture) !== look.iconAddress) return original();
                return original(layout, texture, source, label, disabled, scale, look.iconColor, pressed, focus, over, noPressed);
            }, gate);

        const GUIString = bl.classOf('', 'GUIString');
        for (const signature of ['void Draw(String_Layout layout, StringBuilder value)', 'void Draw(String_Layout layout, StringBuilder value, Color overloadedColour)']) {
            GUIString[signature].hook((original) => {
                const look = BossBarLoader.#mobileLook;
                return look && !look.showText ? undefined : original();
            }, gate);
        }
    }

    static #MobileCenter(GUIPanel) {
        const panel = BossBarLoader.#panel;
        if (!panel) return null;

        const r = GUIPanel['Rectangle Region(Panel_Layout layout)'](panel);
        return Vector2.new(r.X + r.Width / 2, r.Y + r.Height / 2);
    }

    static #RunMobile(self, npc, params, original) {
        const spriteBatch = Terraria.Main.spriteBatch;
        const before = BossBarLoader.#Snapshot(params);

        if (globalBossBars.All('PreDraw', (g) => g.PreDraw(spriteBatch, npc, params))) {
            const after = BossBarLoader.#Snapshot(params);
            const look = BossBarLoader.#Changed(before, after) ? BossBarLoader.#Look(params, before, after) : null;
            BossBarLoader.#mobileLook = look;
            if (look || !BossBarLoader.#panel) bl.hookFlags.set('bossbar.mobile', true);
            try {
                original(self, params.Life, params.LifeMax, params.IconTexture, params.IconFrame, params.Shield, params.ShieldMax);
            } finally {
                BossBarLoader.#mobileLook = null;
                bl.hookFlags.set('bossbar.mobile', false);
            }
        }

        globalBossBars.Each('PostDraw', (g) => g.PostDraw(spriteBatch, npc, params));
    }

    // As duas cores multiplicadas, como o Color * Color do XNA.
    static #Tint(color, by) {
        return Color.new(color.R * by.R / 255, color.G * by.G / 255, color.B * by.B / 255, color.A * by.A / 255);
    }
}
