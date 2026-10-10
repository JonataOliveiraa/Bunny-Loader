// As barras de chefe de mod (ModBossBar). O BigProgressBarSystem do jogo
// escolhe o NPC no Update e desenha no Draw (Main.DrawInterface_15). Aqui:
//   - o NPC que o jogo acompanha, se tem barra de mod, passa pelo ModifyInfo
//     dela (false: o jogo procura outro);
//   - sem nada acompanhado, um NPC de mod com barra também ganha a dele (o
//     jogo só acompanha quem tem cabeça de chefe), procurado a cada 30 quadros;
//   - o Draw dessas usa o DrawFancyBar do jogo, entre o PreDraw e o PostDraw.
class BossBarLoader {
    static #state = null;   // a barra deste quadro: { bar, index, life, lifeMax, shield, shieldMax }
    static #tracked = -1;   // o NPC que só a barra de mod acompanha
    static #scanAge = 0;
    static #SCAN_EVERY = 30;

    static Add(inst) {
        Hooks.Once('bossbar', () => Safe.Run('ModBossBar: ganchos', () => BossBarLoader.#Hook()));
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

    static #Draw(state, spriteBatch) {
        const { bar } = state;
        const name = bar.constructor.name;
        const npc = Terraria.Main.npc[state.index];
        const frame = new Ref(null);
        const icon = Safe.Run(name + '.GetIconTexture', () => bar.GetIconTexture(frame)) ||
            Terraria.GameContent.TextureAssets.NpcHead[0];
        const texture = 'Value' in icon ? icon.Value : icon;
        const params = {
            IconTexture: texture,
            IconFrame: frame.value || Rectangle.new(0, 0, texture.Width, texture.Height),
            Life: state.life, LifeMax: state.lifeMax, Shield: state.shield, ShieldMax: state.shieldMax,
        };
        if (Safe.Run(name + '.PreDraw', () => bar.PreDraw(spriteBatch, npc, params)) !== false) {
            Terraria.GameContent.UI.BigProgressBar.BigProgressBarHelper['void DrawFancyBar(SpriteBatch spriteBatch, float lifeAmount, float lifeMax, Texture2D barIconTexture, Rectangle barIconFrame, float shieldCurrent, float shieldMax)'](
                spriteBatch, params.Life, params.LifeMax, params.IconTexture, params.IconFrame, params.Shield, params.ShieldMax);
        }
        Safe.Run(name + '.PostDraw', () => bar.PostDraw(spriteBatch, npc, params));
    }

    static #Hook() {
        const System = Terraria.GameContent.UI.BigProgressBar.BigProgressBarSystem;
        System['void Update()'].hook((original, self) => {
            original(self);
            Safe.Run('ModBossBar: Update', () => BossBarLoader.#Update(self));
        });
        System['void Draw(SpriteBatch spriteBatch)'].hook((original, self, spriteBatch) => {
            const state = BossBarLoader.#state;
            if (!state) return original(self, spriteBatch);
            Safe.Run('ModBossBar: Draw', () => BossBarLoader.#Draw(state, spriteBatch));
            return undefined;
        });
    }
}
