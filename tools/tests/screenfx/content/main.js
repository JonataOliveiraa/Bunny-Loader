// Os prontos do item 3 (script/js/mod/Effects/Presets). Fases de 200 quadros,
// medidas (passos por quadro, GPU), com um print no quadro 120 de cada:
//   ripple (pulso de anéis), puddle (anéis sem fim), field (ForceField no
//   jogador, golpeado da direita), chroma (radial, tela inteira), chromadir
//   (direcional, num círculo), pixel (círculo no jogador), pixelfull (tela
//   inteira, a câmera andando), flash, glitch, zoom (ZoomBlur), lens.
// No fim, confere se todos os shaders dos prontos compilaram.
// Loga "screenfx ...".
const Main = Terraria.Main;
const log = (s) => bl.log('screenfx ' + s);

let fails = 0;
function check(label, ok, detail) {
    if (!ok) fails++;
    log(label + ': ' + (ok ? 'ok' : 'FALHOU') + ' (' + detail + ')');
}

const player = () => Main.player[Main.myPlayer];
const offset = (dx, dy) => ({ X: player().Center.X + dx * 16, Y: player().Center.Y + dy * 16 });
const snapshot = () => ({ at: Date.now(), s: bl.screen.__stats() });

let field = null;
const PHASE = 200;
const phases = [
    { name: 'ripple', tick: (t) => { if (t % 60 === 40) ScreenPresets.Ripple.Spawn(offset(0, -4), { radius: 260, strength: 8 }); } },
    { name: 'puddle', start: () => ScreenPresets.Ripple.Spawn(player(), { radius: 160, duration: 0, strength: 4 }) },
    {
        name: 'field',
        start: () => { field = ScreenPresets.ForceField.Spawn(player(), { radius: 70, key: 'fx:field' }); },
        tick: (t) => { if (t % 30 === 0) field.Hit(offset(10, 0)); },
    },
    { name: 'chroma', start: () => ScreenPresets.ChromaticAberration.Spawn(player(), { amount: 6 }) },
    {
        name: 'chromadir',
        start: () => ScreenPresets.ChromaticAberration.Spawn(player(), { fullscreen: false, radius: 160, amount: 8, direction: { X: 1, Y: 1 } }),
    },
    { name: 'pixel', start: () => ScreenPresets.Pixelate.Spawn(player(), { radius: 120, size: 8 }) },
    {
        name: 'pixelfull',
        start: () => ScreenPresets.Pixelate.Spawn(null, { size: 5 }),
        tick: (t) => { player().velocity.X = t < 100 ? 4 : -4; },
    },
    { name: 'flash', tick: (t) => { if (t % 40 === 35) ScreenPresets.Flash.Spawn(null, { color: '#fff4d0', duration: 40, fadeOut: 30 }); } },
    { name: 'glitch', start: () => ScreenPresets.Glitch.Spawn(null, { strength: 0.8 }) },
    { name: 'zoom', start: () => ScreenPresets.ZoomBlur.Spawn(player(), { strength: 0.15 }) },
    { name: 'lens', start: () => ScreenPresets.Lens.Spawn(offset(0, -5), { radius: 110, strength: 24 }) },
];

function shaderChecks() {
    for (const name of ['Ripple', 'ForceField', 'ChromaticAberration', 'Pixelate', 'Flash', 'Glitch', 'ZoomBlur']) {
        const shader = ScreenEffects.Register(ScreenPresets[name]);
        check('shader ' + name, shader && shader.Status === 'ready', shader ? shader.Status + (shader.Error ? ' ' + shader.Error : '') : 'sem shader');
    }
    check('Lens só distorce', ScreenEffects.Register(ScreenPresets.Lens) === null, 'sem shader próprio');
}

let frame = 0, done = false, mark = null;

function measure(phase) {
    const now = snapshot();
    const a = mark.s, b = now.s;
    const frames = b.frames - a.frames;
    const gpuN = b.gpuSamples - a.gpuSamples;
    const gpu = gpuN > 0 ? ((b.gpuNs - a.gpuNs) / gpuN / 1000).toFixed(0) : '-';
    const fps = (b.worldPostRender - a.worldPostRender) / ((now.at - mark.at) / 1000);
    log('fase ' + phase.name + ': ' + fps.toFixed(1) + ' fps, ' + ((b.draws - a.draws) / Math.max(frames, 1)).toFixed(2)
        + ' passos por quadro em ' + frames + ' quadros, GPU ' + gpu + ' µs/quadro');
    check('desenhou (' + phase.name + ')', b.draws > a.draws, (b.draws - a.draws) + ' passos');
    mark = now;
}

function step() {
    if (frame < 60) return;

    const i = Math.floor((frame - 60) / PHASE);
    const local = (frame - 60) % PHASE;
    if (local === 0 && i > 0) {
        measure(phases[i - 1]);
        ScreenEffects.KillAll();
        player().velocity.X = 0;
    }
    if (i >= phases.length) {
        shaderChecks();
        const s = bl.screen.__stats();
        check('espelho do estado GL confere', s.shadowMismatches === 0, s.shadowChecks + ' conferidos');
        log('FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
        done = true;
        return;
    }
    const phase = phases[i];
    if (local === 0) {
        if (i === 0) mark = snapshot();
        if (phase.start) phase.start();
    }
    if (phase.tick) phase.tick(local);
    if (local === 120) log('SHOT ' + phase.name);
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

export default class TestScreenFx extends Mod {}
