// Cor, máscaras, texturas e tweens dos efeitos (script/js/mod/Effects).
// Antes das fases, confere Curve, ColorMath, a LUT neutra gravada e um Tween.
// Fases de 200 quadros, medidas (passos por quadro, GPU):
//   grade (LUT que inverte), neutral (LUT neutra gravada agora), rage (Tint
//   vermelho nas bordas, pulsando), gray (cinza fora de um círculo no
//   jogador), warm (calor por um degradê de baixo), vignette, texmask (Tint
//   azul pela máscara de um PNG).
// Loga "screencolor ...".
const Main = Terraria.Main;
const log = (s) => bl.log('screencolor ' + s);
const { Mask, Tween, Curve, ColorMath, Easing } = ScreenUtils;
const { BlendMode, Texture } = ScreenGraphics;

let fails = 0;
function check(label, ok, detail) {
    if (!ok) fails++;
    log(label + ': ' + (ok ? 'ok' : 'FALHOU') + ' (' + detail + ')');
}

const near = (a, b, eps = 0.02) => Math.abs(a - b) <= eps;
const player = () => Main.player[Main.myPlayer];
const NEUTRAL = 'Content/Effects/Neutral.png';

function unitChecks() {
    const curve = new Curve([[0, 0], [0.5, 1], [1, 0]]);
    check('Curve', near(curve.At(0.25), 0.5) && near(curve.At(0.5), 1) && near(curve.At(2), 0), [0.25, 0.5, 2].map((t) => curve.At(t).toFixed(2)).join(' '));

    const red = ColorMath.Hsv(0, 1, 1), hex = ColorMath.FromHex('#ff8000'), white = ColorMath.Temperature(6600);
    check('ColorMath.Hsv', near(red[0], 1) && near(red[1], 0) && near(red[2], 0), red.join(','));
    check('ColorMath.FromHex', near(hex[0], 1) && near(hex[1], 0.502) && near(hex[2], 0), hex.map((v) => v.toFixed(3)).join(','));
    check('ColorMath.Temperature 6600K ~ branco', white.every((v) => v > 0.9), white.map((v) => v.toFixed(2)).join(','));

    Texture.SaveNeutralLut(NEUTRAL, 16);
    const neutral = Texture.Load(NEUTRAL);
    check('LUT neutra gravada e lida', neutral.Width === 256 && neutral.Height === 16, neutral.Width + 'x' + neutral.Height);
    const invert = Texture.Load('Content/Effects/Invert');
    check('PNG do mod carrega', invert.Width === 256 && invert.Height === 16 && invert.Texture <= -1000, invert.Width + 'x' + invert.Height + ' #' + invert.Texture);
}

let tweenTarget = null;

const PHASE = 200;
const phases = [
    { name: 'grade', start: () => ScreenPresets.ColorGrade.Spawn(null, { lut: 'Content/Effects/Invert.png' }) },
    { name: 'neutral', start: () => ScreenPresets.ColorGrade.Spawn(null, { lut: NEUTRAL }) },
    {
        name: 'rage',
        start: () => ScreenPresets.Tint.Spawn(null, {
            color: Color.new(255, 40, 30), blend: BlendMode.SoftLight, amount: 0.9, saturation: 0.7,
            mask: Mask.Vignette({ start: 0.3, softness: 0.6 }), pulse: { period: 40, amount: 0.5 }, key: 't:rage',
        }),
    },
    {
        name: 'gray',
        start: () => ScreenPresets.Tint.Spawn(null, { saturation: 0, contrast: 1.15, mask: Mask.Circle(player().Center, 120, { feather: 60 }).Inverted() }),
    },
    {
        name: 'warm',
        start: () => ScreenPresets.Tint.Spawn(null, {
            color: ColorMath.Temperature(1800), blend: BlendMode.Overlay, amount: 0.8, temperature: 0.6,
            mask: Mask.Gradient('Up', { start: 0, end: 0.6 }),
        }),
    },
    { name: 'vignette', start: () => ScreenPresets.Vignette.Spawn(null, { strength: 0.85, color: '#200010' }) },
    {
        name: 'texmask',
        start: () => ScreenPresets.Tint.Spawn(null, { color: '#2060ff', blend: BlendMode.Normal, amount: 0.8, mask: Mask.Texture(Texture.Load('Content/Effects/Gradient')) }),
    },
];

let frame = 0, done = false, mark = null;
const snapshot = () => ({ at: Date.now(), s: bl.screen.__stats() });

function measure(phase) {
    const now = snapshot();
    const a = mark.s, b = now.s;
    const frames = b.frames - a.frames;
    const gpuN = b.gpuSamples - a.gpuSamples;
    const gpu = gpuN > 0 ? ((b.gpuNs - a.gpuNs) / gpuN / 1000).toFixed(0) : '-';
    const fps = (b.worldPostRender - a.worldPostRender) / ((now.at - mark.at) / 1000);
    log('fase ' + phase.name + ': ' + fps.toFixed(1) + ' fps, ' + ((b.draws - a.draws) / Math.max(frames, 1)).toFixed(2)
        + ' passos por quadro em ' + frames + ' quadros, GPU ' + gpu + ' µs/quadro');
    check('desenhou (' + phase.name + ')', frames > PHASE * 0.8, frames + ' quadros');
    mark = now;
}

function step() {
    if (frame === 60) {
        unitChecks();
        tweenTarget = ScreenPresets.Twirl.Spawn(player(), { radius: 10, key: 't:tween' });
        tweenTarget.Tween('Radius', 100, 30, Easing.OutQuad);
    }
    if (frame === 100) {
        check('Tween chegou ao fim', near(tweenTarget.Radius, 100, 0.01) && Tween.Count === 0, tweenTarget.Radius.toFixed(2) + ', ' + Tween.Count + ' ativos');
        tweenTarget.Kill();
    }
    if (frame < 120) return;

    const i = Math.floor((frame - 120) / PHASE);
    const local = (frame - 120) % PHASE;
    if (local === 0 && i > 0) {
        measure(phases[i - 1]);
        ScreenEffects.KillAll();
    }
    if (i >= phases.length) {
        const s = bl.screen.__stats();
        check('espelho do estado GL confere', s.shadowMismatches === 0, s.shadowChecks + ' conferidos');
        log('FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
        done = true;
        return;
    }
    if (local === 0) {
        if (i === 0) mark = snapshot();
        phases[i].start();
    }
    if (local === 120) log('SHOT ' + phases[i].name);
}

Main['void DoDraw(GameTime gameTime)'].hook((original, self, gameTime) => {
    if (!done && !Main.gameMenu) {
        frame++;
        player().statLife = player().statLifeMax2;
        try {
            step();
        } catch (e) {
            fails++;
            log('erro no quadro ' + frame + ': ' + e + (e.stack ? ' ' + e.stack : ''));
        }
    }
    original(self, gameTime);
});
log('carregado');

export default class TestScreenColor extends Mod {}
