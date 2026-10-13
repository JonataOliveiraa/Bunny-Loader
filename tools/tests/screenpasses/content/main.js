// Distorção, passadas e quadro anterior (script/js/mod/Effects). Fases de 240
// quadros, cada uma medida (passos por quadro, GPU):
//   hole:   BlackHole acima do jogador + HeatHaze à direita (2 passos: a
//           distorção dos dois numa passada só + o núcleo do buraco)
//   blur:   Blur na tela inteira (3 passos: BlurH, BlurV, composição)
//   bloom:  Bloom na tela inteira (4 passos)
//   echo:   Echo no jogador, escorrendo para a direita (lê o quadro anterior)
//   shapes: dois efeitos só de distorção (Ring e Push): 1 passo
// Loga "screenpasses ...".
const Main = Terraria.Main;
const log = (s) => bl.log('screenpasses ' + s);

class RingOnly extends ModScreenEffect {
    DrawDistortion(d) {
        d.Ring(this.Center, 40 + (this.Age % 60) * 2, 24, 10);
    }
}

class PushOnly extends ModScreenEffect {
    DrawDistortion(d) {
        d.Push(this.Center, this.Radius, { X: 1, Y: 0 }, 12);
    }
}

let fails = 0;
function check(label, ok, detail) {
    if (!ok) fails++;
    log(label + ': ' + (ok ? 'ok' : 'FALHOU') + ' (' + detail + ')');
}

const player = () => Main.player[Main.myPlayer];
const offset = (dx, dy) => ({ X: player().Center.X + dx * 16, Y: player().Center.Y + dy * 16 });
const snapshot = () => ({ at: Date.now(), s: bl.screen.__stats() });

const PHASE = 240;
const phases = [
    {
        name: 'hole', perFrame: 2,
        start: () => {
            ScreenPresets.BlackHole.Spawn(offset(0, -7), { key: 'p:hole' });
            ScreenPresets.HeatHaze.Spawn(offset(10, -2), { radius: 90, strength: 4, key: 'p:haze' });
        },
    },
    { name: 'blur', perFrame: 3, start: () => ScreenPresets.Blur.Spawn(null, { fullscreen: true, strength: 6, key: 'p:blur' }) },
    { name: 'bloom', perFrame: 4, start: () => ScreenPresets.Bloom.Spawn(null, { threshold: 0.5, intensity: 1.5, key: 'p:bloom' }) },
    { name: 'echo', perFrame: 1, start: () => ScreenPresets.Echo.Spawn(player(), { radius: 140, persistence: 0.85, drift: [-0.004, 0], key: 'p:echo' }) },
    {
        name: 'shapes', perFrame: 1,
        start: () => {
            RingOnly.Spawn(offset(-8, -3), { radius: 200 });
            PushOnly.Spawn(offset(8, -3), { radius: 80 });
        },
    },
];

let frame = 0, done = false, mark = null;

function measure(phase) {
    const now = snapshot();
    const a = mark.s, b = now.s;
    const frames = b.frames - a.frames;
    const perFrame = frames > 0 ? (b.draws - a.draws) / frames : 0;
    const gpuN = b.gpuSamples - a.gpuSamples;
    const gpu = gpuN > 0 ? ((b.gpuNs - a.gpuNs) / gpuN / 1000).toFixed(0) : '-';
    const fps = (b.worldPostRender - a.worldPostRender) / ((now.at - mark.at) / 1000);
    log('fase ' + phase.name + ': ' + fps.toFixed(1) + ' fps, ' + perFrame.toFixed(2) + ' passos por quadro em ' + frames
        + ' quadros, GPU ' + gpu + ' µs/quadro, marcador ' + (b.markersIssued - a.markersIssued) + '/' + (b.markerHits - a.markerHits));
    check('passos por quadro (' + phase.name + ')', Math.abs(perFrame - phase.perFrame) < 0.1, perFrame.toFixed(2) + ' de ' + phase.perFrame);
    mark = now;
}

function step() {
    if (frame < 100) return;
    const i = Math.floor((frame - 100) / PHASE);
    const local = (frame - 100) % PHASE;
    if (local === 0 && i > 0) {
        measure(phases[i - 1]);
        ScreenEffects.KillAll();
    }
    if (i >= phases.length) {
        const s = bl.screen.__stats();
        check('espelho do estado GL confere', s.shadowMismatches === 0, s.shadowChecks + ' conferidos');
        check('copia sem erro', s.blitError === 0, 'blitError ' + s.blitError);
        log('FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
        done = true;
        return;
    }
    if (local === 0) {
        if (i === 0) mark = snapshot();
        phases[i].start();
    }
    if (local === 150) log('SHOT ' + phases[i].name);
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

export default class TestScreenPasses extends Mod {}
