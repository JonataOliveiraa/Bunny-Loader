// Os efeitos de tela do Example Mod, no jogo (precisa dele ligado):
//   - Último Prisma: o prisma na mão, "segurado" (channel) pelo teste; os 10
//     feixes nascem, crescem até a carga cheia e o Ripple nasce onde batem;
//     soltando, tudo some e o Ripple morre junto;
//   - Cajado da Fenda: o projétil leva o ExampleRiftEffect (shader do .frag
//     compilado); ao morrer, Shockwave + Flash + ChromaticAberration;
//   - pouca vida: o Tint com key ExampleMod:lowLife aparece abaixo de 25% e
//     some (Tween) ao se curar.
// Loga "exmodfx ...".
const Main = Terraria.Main;
const NewProjectile = Terraria.Projectile['int NewProjectile(IEntitySource spawnSource, Vector2 position, Vector2 velocity, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)'];
const log = (s) => bl.log('exmodfx ' + s);

let fails = 0;
function check(label, ok, detail) {
    if (!ok) fails++;
    log(label + ': ' + (ok ? 'ok' : 'FALHOU') + ' (' + detail + ')');
}

const player = () => Main.player[Main.myPlayer];
const typeOf = (base, name) => ModContent.Find(base, 'examplemod/' + name).Type;
const count = (type) => {
    let n = 0;
    for (let i = 0; i < 1000; i++) {
        const p = Main.projectile[i];
        if (p.active && p.type === type) n++;
    }
    return n;
};
const first = (type) => {
    for (let i = 0; i < 1000; i++) {
        const p = Main.projectile[i];
        if (p.active && p.type === type) return p;
    }
    return null;
};
const effect = (name) => ScreenEffects.Active.find((fx) => fx.constructor.name === name) ?? null;

let T = null, saved = null, holding = false, lowLife = false, slot = 0;

Terraria.Projectile['void AI()'].hook((original, self) => {
    if (holding && T && self.type === T.holdout) {
        const p = Main.player[self.owner];
        p.channel = true;
        p.statMana = p.statManaMax2;
    }
    original(self);
});

const steps = {
    60: () => {
        T = {
            prism: typeOf(ModItem, 'ExampleLastPrism'),
            holdout: typeOf(ModProjectile, 'ExampleLastPrismHoldout'),
            beam: typeOf(ModProjectile, 'ExampleLastPrismBeam'),
            rift: typeOf(ModProjectile, 'ExampleRiftProjectile'),
        };
        log('tipos ' + JSON.stringify(T));
        const p = player();
        slot = p.selectedItem;
        saved = p.inventory[slot].Clone();
        p.inventory[slot]['void SetDefaults(int Type, ItemVariant variant)'](T.prism, null);
        holding = true;
        NewProjectile(p.GetProjectileSource_Item(p.HeldItem), p.Center, Vector2.new(30, -8), T.holdout, p.HeldItem.damage, 0, p.whoAmI, 0, 0, 0, null);
    },
    90: () => {
        check('prisma vivo', count(T.holdout) === 1, count(T.holdout) + ' prisma(s)');
        check('10 feixes', count(T.beam) === 10, count(T.beam) + ' feixes');
    },
    260: () => {
        const beam = first(T.beam);
        check('carga cheia: feixe no tamanho máximo', beam && Math.abs(beam.scale - 1.8) < 0.01, beam && beam.scale.toFixed(2));
        check('feixe com comprimento', beam && beam.localAI.val1 > 16, beam && beam.localAI.val1.toFixed(0));
        const ripple = effect('Ripple');
        check('Ripple onde os feixes batem', ripple && ripple.Intensity > 0.99, ripple ? 'intensidade ' + ripple.Intensity.toFixed(2) : 'sem Ripple');
        log('SHOT prism');
    },
    300: () => {
        holding = false;
    },
    330: () => {
        check('soltou: prisma e feixes somem', count(T.holdout) === 0 && count(T.beam) === 0, count(T.holdout) + '/' + count(T.beam));
        check('Ripple morre junto', !effect('Ripple'), effect('Ripple') ? 'vivo' : 'morto');
        const p = player();
        NewProjectile(p.GetProjectileSource_Item(p.HeldItem), Vector2.new(p.Center.X - 160, p.Center.Y - 90), Vector2.new(3, 0), T.rift, 30, 0, p.whoAmI, 0, 0, 0, null);
    },
    380: () => {
        const rift = effect('ExampleRiftEffect');
        const proj = first(T.rift);
        check('fenda segue o projétil', rift && proj && Math.abs(rift.Center.X - proj.Center.X) < 1, rift ? 'ok' : 'sem efeito');
        check('shader do .frag compilado', rift && rift.Shader && rift.Shader.Name.endsWith('ExampleRiftEffect.frag') && rift.Shader.Status === 'ready',
            rift && rift.Shader ? rift.Shader.Name + ' ' + rift.Shader.Status + ' ' + (rift.Shader.Error || '') : '-');
        check('fenda abriu (Tween do Open)', rift && rift.Open > 0.9, rift && rift.Open.toFixed(2));
        log('SHOT rift');
    },
    455: () => {
        const p = first(T.rift);
        if (p) p.Kill();
    },
    457: () => {
        check('explosão: Shockwave, Flash e ChromaticAberration', !!(effect('Shockwave') && effect('Flash') && effect('ChromaticAberration')),
            ScreenEffects.Active.map((fx) => fx.constructor.name).join(','));
        log('SHOT boom');
    },
    500: () => {
        check('fenda fechou e morreu', !effect('ExampleRiftEffect'), effect('ExampleRiftEffect') ? 'viva' : 'morta');
        lowLife = true;
    },
    // Neste MuMu algum mod enche a vida dentro do Player.Update (o PostUpdate
    // lê 100/100 mesmo com a vida baixa aqui): o PostUpdate do exemplo é
    // chamado direto, com a vida já baixa; o do jogo, vendo a vida cheia, já
    // começa a apagar o efeito (30 ticks), que é a volta conferida no fim.
    530: () => {
        const p = player();
        p.GetModPlayer('ExampleLowLifePlayer').PostUpdate(p);
    },
    534: () => {
        const tint = ScreenEffects.Find('ExampleMod:lowLife');
        check('pouca vida: bordas vermelhas', tint && tint.constructor.name === 'Tint', tint ? tint.constructor.name : 'nada');
        log('SHOT lowlife');
        lowLife = false;
    },
    640: () => {
        check('curado: o efeito sumiu', !ScreenEffects.Find('ExampleMod:lowLife'), ScreenEffects.Find('ExampleMod:lowLife') ? 'vivo' : 'morto');
        player().inventory[slot] = saved;
        const s = bl.screen.__stats();
        check('espelho do estado GL confere', s.shadowMismatches === 0, s.shadowChecks + ' conferidos');
        log('FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    },
};

let frame = 0;
Main['void DoDraw(GameTime gameTime)'].hook((original, self, gameTime) => {
    if (frame <= 640 && !Main.gameMenu) {
        frame++;
        const p = player();
        p.statLife = lowLife ? Math.floor(p.statLifeMax2 * 0.1) : p.statLifeMax2;
        const step = steps[frame];
        if (step) {
            try {
                step();
            } catch (e) {
                fails++;
                log('erro no quadro ' + frame + ': ' + e + (e.stack ? ' ' + e.stack : ''));
            }
        }
    }
    original(self, gameTime);
});
log('carregado');

export default class TestExModFx extends Mod {}
