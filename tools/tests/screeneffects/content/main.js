// A API dos efeitos de tela (script/js/mod/Effects). Fases (quadros depois de
// entrar no mundo):
//   a. ScreenPresets.Twirl seguindo o jogador ("SHOT a");
//   b. Rift (Content/Effects/Rift.js + .frag, autoload) acima do jogador,
//      VoidTwirl (estende Twirl, mesmo shader) à direita e uma Shockwave a cada
//      60 quadros ("SHOT b" no meio de uma onda);
//   c. zoom mínimo e máximo com o redemoinho de 3 blocos ("SHOT c1", "c2");
//   d. camada Screen por cima da barra de itens ("SHOT d");
//   e. shader quebrado: falha com a linha do arquivo (abre o painel de erro).
// Loga "screeneffects ...".
import { Rift } from './Content/Effects/Rift.js';

const Main = Terraria.Main;
const GUIInstance = bl.classOf('', 'GUIInstance');
const Runner = bl.classOf('', 'XNAUnityRunner');
const { Space } = ScreenUtils;
const log = (s) => bl.log('screeneffects ' + s);

class VoidTwirl extends ScreenPresets.Twirl {
    SetDefaults() {
        super.SetDefaults();
        this.Radius = 40;
        this.Spin = -5;
    }
}

class Broken extends ModScreenEffect {
    static Source = 'void main() {\n    fragColor = vec4(oops, 1.0);\n}\n';
}

let fails = 0;
function check(label, ok, detail) {
    if (!ok) fails++;
    log(label + ': ' + (ok ? 'ok' : 'FALHOU') + ' (' + detail + ')');
}

const player = () => Main.player[Main.myPlayer];
const above = (tiles) => ({ X: player().Center.X, Y: player().Center.Y - tiles * 16 });
const zoom = (scale) => GUIInstance.Active.GUIZoom['void ApplyPixelZoom(float pixelScale, bool useBump)'](scale, false);

function measure(name, a, b) {
    const s0 = a.s, s1 = b.s;
    const frames = s1.worldPostRender - s0.worldPostRender;
    const fps = frames / ((b.at - a.at) / 1000);
    const gpuN = s1.gpuSamples - s0.gpuSamples;
    const gpu = gpuN > 0 ? ((s1.gpuNs - s0.gpuNs) / gpuN / 1000).toFixed(0) : '-';
    const draws = s1.draws - s0.draws;
    log('fase ' + name + ': ' + fps.toFixed(1) + ' fps, ' + draws + ' desenhos em ' + (s1.frames - s0.frames)
        + ' quadros com efeito, GPU ' + gpu + ' µs/quadro, marcador ' + (s1.markersIssued - s0.markersIssued) + '/'
        + (s1.markerHits - s0.markerHits) + ', fim do quadro ' + (s1.screenFrames - s0.screenFrames));
}

const snapshot = () => ({ at: Date.now(), s: bl.screen.__stats() });

let frame = 0, done = false, mark = null, twirl = null, defaultZoom = 0, killedWaves = 0;
const waves = [];

function step() {
    const f = frame;
    if (f === 100) {
        mark = snapshot();
        twirl = ScreenPresets.Twirl.Spawn(player(), { radius: 48, key: 'test:twirl' });
        const again = ScreenPresets.Twirl.Spawn(player(), { radius: 48, key: 'test:twirl' });
        check('Key renova em vez de empilhar', again === twirl && ScreenEffects.Active.length === 1, ScreenEffects.Active.length + ' ativos');
    }
    if (f === 250) log('SHOT a');
    if (f === 300) {
        const p = player().Center;
        check('segue o jogador', twirl.Active && Math.abs(twirl.Center.X - p.X) < 1 && Math.abs(twirl.Center.Y - p.Y) < 1,
            twirl.Center.X.toFixed(0) + ',' + twirl.Center.Y.toFixed(0) + ' / ' + p.X.toFixed(0) + ',' + p.Y.toFixed(0));
        const now = snapshot();
        measure('a (redemoinho no jogador)', mark, now);
        mark = now;
        check('Kill pela Key', ScreenEffects.Kill('test:twirl'), 'Kill devolveu');

        const rift = Rift.Spawn(above(7), { key: 'test:rift' });
        const right = { X: player().Center.X + 12 * 16, Y: player().Center.Y - 2 * 16 };
        const voidTwirl = VoidTwirl.Spawn(right);
        check('Rift com shader do .frag', rift.Shader && rift.Shader.Name.endsWith('Rift.frag'), rift.Shader && rift.Shader.Name);
        check('subclasse usa o shader do pronto', voidTwirl.Shader.Id === ScreenEffects.Register(ScreenPresets.Twirl).Id,
            voidTwirl.Shader.Id + ' / ' + ScreenEffects.Register(ScreenPresets.Twirl).Id);
    }
    if (f >= 300 && f < 600 && (f - 300) % 60 === 0) {
        const wave = ScreenPresets.Shockwave.Spawn(player().Center, { radius: 200, duration: 40 });
        wave.OnKill = () => { killedWaves++; };
        waves.push(wave);
    }
    if (f === 380) {
        log('SHOT b');
        const rift = ScreenEffects.Find('test:rift');
        check('shader do Rift compilado', rift && rift.Shader.Status === 'ready', rift && rift.Shader.Status + ' ' + rift.Shader.Error);
    }
    if (f === 600) {
        const now = snapshot();
        measure('b (Rift + VoidTwirl + ondas)', mark, now);
        mark = now;
        check('ondas morrem na Duration (OnKill)', killedWaves >= 4 && waves.slice(0, 4).every((w) => !w.Active), killedWaves + ' mortas');
        ScreenEffects.KillAll();
        twirl = ScreenPresets.Twirl.Spawn(player(), { radius: 48 });
        defaultZoom = GUIInstance.Active.GUIZoom.GetDefaultWorldZoomScale();
        zoom(Runner._instance.WorldCameraSettings.MinPixelScale);
    }
    if (f === 700) log('SHOT c1 (tela ' + Main.screenWidth + 'x' + Main.screenHeight + ', raio ' + Space.PixelsToUV(48).toFixed(3) + ')');
    if (f === 720) zoom(Runner._instance.WorldCameraSettings.MaxPixelScale);
    if (f === 820) log('SHOT c2 (tela ' + Main.screenWidth + 'x' + Main.screenHeight + ', raio ' + Space.PixelsToUV(48).toFixed(3) + ')');
    if (f === 840) {
        zoom(defaultZoom);
        twirl.Kill();
        const now = snapshot();
        measure('c (zoom)', mark, now);
        mark = now;
    }
    if (f === 860) {
        ScreenPresets.Twirl.Spawn(Space.UVToWorld({ X: 0.07, Y: 0.75 }), { radius: 120, layer: ScreenEffects.Layer.Screen, key: 'test:ui' });
    }
    if (f === 960) log('SHOT d');
    if (f === 1000) {
        const now = snapshot();
        measure('d (camada Screen)', mark, now);
        check('camada Screen desenha no fim do quadro', now.s.screenFrames - mark.s.screenFrames > 100,
            (now.s.screenFrames - mark.s.screenFrames) + ' quadros');
        mark = now;
        ScreenEffects.KillAll();
        Broken.Spawn(player().Center, { duration: 20 });
    }
    if (f === 1040) {
        const shader = ScreenEffects.Register(Broken);
        check('shader quebrado falha com a linha', shader.Status === 'failed' && /Broken:2/.test(shader.Error),
            shader.Status + ': ' + shader.Error.replace(/\n/g, ' | '));
        const s = bl.screen.__stats();
        log('GL: contexto ' + s.hasContext + ', viewport ' + s.viewport.join(',') + ', blitError ' + s.blitError
            + ', espelho ' + s.shadowChecks + '/' + s.shadowMismatches);
        check('espelho do estado GL confere', s.shadowMismatches === 0, s.shadowMismatches + ' diferentes');
        check('nada vivo no fim', ScreenEffects.Active.length === 0, ScreenEffects.Active.length + ' ativos');
        log('FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
        done = true;
    }
}

Main['void DoDraw(GameTime gameTime)'].hook((original, self, gameTime) => {
    if (!done && !Main.gameMenu) {
        frame++;
        player().statLife = player().statLifeMax2;
        try {
            step();
        } catch (e) {
            fails++;
            log('erro no passo ' + frame + ': ' + e + (e.stack ? ' ' + e.stack : ''));
        }
    }
    original(self, gameTime);
});
log('carregado');

export default class TestScreenEffects extends Mod {}
