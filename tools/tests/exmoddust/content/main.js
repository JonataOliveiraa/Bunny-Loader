// As poeiras portadas do ExampleMod, no jogo: cada uma nasce perto do jogador
// e e conferida; depois um movel do ExampleMod e quebrado (Sparkle) e um
// ioio do ExampleMod e solto (Sparkle no PostAI). Precisa do Example Mod.
const Main = Terraria.Main;
const Dust = Terraria.Dust;
const WorldGen = Terraria.WorldGen;
const XColor = Microsoft.Xna.Framework.Graphics.Color;
const log = (s) => bl.log('exmoddust ' + s);

let fails = 0;
// Erro de mod (Safe.Report -> bl.error, o painel): a checagem final conta.
const modErrors = [];
const blError = bl.error;
bl.error = (text) => {
    modErrors.push(String(text).split('\n')[0]);
    return blError(text);
};
function check(label, ok, detail) {
    if (!ok) fails++;
    log(label + ': ' + (ok ? 'ok' : 'FALHOU') + (detail === undefined ? '' : ' (' + detail + ')'));
}

const player = () => Main.player[Main.myPlayer];
const at = (dx, dy) => Vector2.new(player().Center.X + dx * 16, player().Center.Y + dy * 16);
const type = (name) => ModContent.DustType(name);
const spawn = (name, dx, dy, vx = 0, vy = 0, scale = 1) => Main.dust[Dust.NewDust(at(dx, dy), 0, 0, type(name), vx, vy, 0, XColor.White, scale)];
const countType = (t) => {
    let n = 0;
    for (let i = 0; i < 6000; i++) {
        const d = Main.dust[i];
        if (d.active && d.type === t) n++;
    }
    return n;
};

const state = {};
const phases = [
    {
        name: 'tipos', frames: 2,
        start() {
            const names = ['Sparkle', 'ExampleBubble', 'ExampleSolutionDust', 'ExampleCustomDrawDust', 'ExampleAdvancedDust'];
            const types = names.map(type);
            check('as cinco poeiras do Example Mod tem tipo', types.every((t) => t >= Terraria.ID.DustID.Count),
                names.map((n, i) => n + '=' + types[i]).join(' '));
            check('UpdateType da ExampleSolutionDust', DustLoader.GetDust(types[2]).UpdateType === Terraria.ID.DustID.PureSpray);
            const sheet = Terraria.GameContent.TextureAssets.Dust.Value;
            check('ExampleCustomDrawDust usa a folha do jogo', DustLoader.GetDust(types[3]).Texture2D.Value === sheet);
            check('Sparkle tem textura propria', DustLoader.GetDust(types[0]).Texture2D.Value !== sheet);
        },
    },
    {
        name: 'sparkle', frames: 40,
        start() {
            const d = spawn('Sparkle', 0, -4, 2, 0, 1);
            Object.assign(state, { d, x0: d.position.X, s0: d.scale });
            check('Sparkle: OnSpawn (sem gravidade, 1,5x)', d.noGravity === true && d.noLight === true && d.scale > 1.1,
                'escala ' + d.scale.toFixed(2));
        },
        end() {
            const d = state.d;
            check('Sparkle: se move pelo Update', !d.active || d.position.X > state.x0, d.position.X.toFixed(1) + ' > ' + state.x0.toFixed(1));
            check('Sparkle: encolhe', !d.active || d.scale < state.s0, d.scale.toFixed(3));
        },
    },
    {
        name: 'bolha', frames: 10,
        start() {
            const d = spawn('ExampleBubble', 1, -4, 0, -1, 1);
            d.velocity.X = 0;
            d.velocity.Y = -1;   // o NewDust sorteia a velocidade
            check('ExampleBubble: moldura 30x30', d.frame.Width === 30 && d.frame.Height === 30, d.frame.Width + 'x' + d.frame.Height);
            Object.assign(state, { d, y0: d.position.Y });
        },
        end() {
            check('ExampleBubble: sobe', state.d.position.Y < state.y0, (state.d.position.Y - state.y0).toFixed(1));
        },
    },
    {
        name: 'solucao', frames: 10,
        start() {
            state.d = spawn('ExampleSolutionDust', 2, -4, 1, 0, 1);
        },
        end() {
            check('ExampleSolutionDust: segue com o tipo de mod', !state.d.active || state.d.type === type('ExampleSolutionDust'), state.d.type);
        },
    },
    {
        name: 'rastro', frames: 10,
        start() {
            const d = spawn('ExampleCustomDrawDust', 3, -4, 3, 0, 1);
            check('ExampleCustomDrawDust: moldura da eletrica', d.frame.X === (Terraria.ID.DustID.Electric * 10) % 1000, d.frame.X);
            const c = d['Color GetAlpha(Color newColor)'](XColor.Black);
            check('ExampleCustomDrawDust: GetAlpha (alfa 25)', c.A === 25 && c.R > 150, c.R + ',' + c.A);
        },
    },
    {
        name: 'orbita', frames: 6,
        start() {
            const d = spawn('ExampleAdvancedDust', 0, 0, 0, 0, 2);
            d.rotation = 1;
            d.customData = player();
            state.d = d;
        },
        end() {
            const d = state.d;
            const c = player().Center;
            const r = Math.hypot(d.position.X - c.X, d.position.Y - c.Y);
            // No escuro o desenho a apaga (cor preta), como no tModLoader: vale a posicao.
            check('ExampleAdvancedDust: gira em volta do jogador (customData)', Math.abs(r - d.scale * 50) < 2,
                'raio ' + r.toFixed(1) + ' x ' + (d.scale * 50).toFixed(1) + ', ativa ' + d.active + ', escala ' + d.scale.toFixed(3) + ', tipo ' + d.type);
        },
    },
    {
        name: 'parede', frames: 30,
        start() {
            // Uma parede do ExampleMod no ar (moveis precisam de chao): o DustType dela e a Sparkle.
            const x = Math.floor(player().Center.X / 16) + 4;
            const y = Math.floor(player().Center.Y / 16) - 6;
            state.tile = [x, y];
            WorldGen['void PlaceWall(int i, int j, int type, bool mute)'](x, y, ModContent.WallType('ExampleWall'), true);
            state.before = countType(type('Sparkle'));
        },
        tick(t) {
            if (t === 2) WorldGen['void KillWall(int i, int j, bool fail)'](state.tile[0], state.tile[1], false);
            if (t === 3) state.after = countType(type('Sparkle'));
        },
        end() {
            check('quebrar a parede do Example Mod solta Sparkle', state.after > state.before, state.before + ' -> ' + state.after);
        },
    },
    {
        name: 'ioio', frames: 90,
        start() {
            const p = player();
            state.proj = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'](
                null, p.Center.X, p.Center.Y, 4, -2, ModContent.ProjectileType('ExampleYoyoProjectile'), 1, 0, Main.myPlayer, 0, 0, 0, null);
            state.seen = 0;
        },
        tick(t) {
            state.seen = Math.max(state.seen, countType(type('Sparkle')));
            if (t === 45) log('SHOT exmoddust');
        },
        end() {
            check('o ioio do Example Mod solta Sparkle', state.seen > 0, state.seen + ' no maximo');
            const pr = Main.projectile[state.proj];
            if (pr.active) pr.Kill();
            const s = bl.dusts.__pool();
            check('pilha confere', s.active === s.activeCount && s.active + s.freeCount === 6000,
                'ativas ' + s.active + ', contador ' + s.activeCount + ', livres ' + s.freeCount + ', consertos ' + s.repairs);
        },
    },
];

let frame = 0, index = 0, local = 0, done = false;

function step() {
    if (frame < 90) return;
    if (index >= phases.length) {
        const unexpected = modErrors.filter((e) => !(false));
        check('nenhum erro de mod', unexpected.length === 0, unexpected.join(' | '));
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

export default class TestExModDust extends Mod {}
