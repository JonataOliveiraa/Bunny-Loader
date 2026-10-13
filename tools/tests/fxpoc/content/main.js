// Prova de conceito dos efeitos de tela (graphics/RenderHook.cpp, GlHooks.cpp).
// Esta libunity não tem GL.IssuePluginEvent. Quando a câmera do mundo termina,
// um Graphics.Blit para uma RenderTexture 17x3 vira, na thread de render, um
// glViewport(0,0,17,3) na posição certa da fila: ali o efeito desenha, depois
// do mundo e antes da interface.
//
// Fases de 300 quadros, cada uma medida (fps, CPU do marcador na thread do
// jogo, CPU e GPU do efeito na thread de render):
//   desligado → redemoinho pequeno acima do jogador ("SHOT") → redemoinho
//   grande em cima da barra de itens ("SHOT2": a interface NÃO pode estar
//   distorcida) → desligado de novo.
// Não mexe no horário do mundo: roda também no celular de quem testa.
// Loga "fxpoc ...".
const Main = Terraria.Main;
const log = (s) => bl.log('fxpoc ' + s);

const WARMUP = 120;
const PHASE = 300;
const phases = [
    { name: 'desligado', poc: null },
    { name: 'pequeno', poc: 'player', shot: 'SHOT' },
    { name: 'grande', poc: 'hotbar', shot: 'SHOT2' },
    { name: 'desligado de novo', poc: null },
];

let fails = 0;
function check(label, ok, detail) {
    if (!ok) fails++;
    log(label + ': ' + (ok ? 'ok' : 'FALHOU') + ' (' + detail + ')');
}

function centerAbovePlayer() {
    const p = Main.player[Main.myPlayer];
    const sp = Main.screenPosition;
    const y = p.Center.Y - 8 * 16;
    return { u: (p.Center.X - sp.X) / Main.screenWidth, v: 1 - (y - sp.Y) / Main.screenHeight };
}

function applyPoc(kind) {
    if (kind === 'player') {
        const c = centerAbovePlayer();
        bl.screen.__poc(true, c.u, c.v, 0.18, 3.0);
    } else if (kind === 'hotbar') {
        bl.screen.__poc(true, 0.07, 0.75, 0.45, 3.0);
    } else {
        bl.screen.__poc(false);
    }
}

function snapshot() {
    return { at: Date.now(), s: bl.screen.__stats() };
}

const us = (ns, n) => n > 0 ? (ns / n / 1000).toFixed(1) : '-';

function report(phase, a, b) {
    const frames = b.s.worldPostRender - a.s.worldPostRender;
    const fps = frames / ((b.at - a.at) / 1000);
    const draws = b.s.draws - a.s.draws;
    const markers = b.s.markersIssued - a.s.markersIssued;
    const gpuN = b.s.gpuSamples - a.s.gpuSamples;
    const frameN = b.s.gpuFrameSamples - a.s.gpuFrameSamples;
    const r = {
        fps,
        draws,
        markers,
        hits: b.s.markerHits - a.s.markerHits,
        swap: b.s.swapDraws - a.s.swapDraws,
        markerUs: us(b.s.markerCpuNs - a.s.markerCpuNs, markers),
        renderUs: us(b.s.renderCpuNs - a.s.renderCpuNs, draws),
        gpuUs: us(b.s.gpuNs - a.s.gpuNs, gpuN),
        gpuN,
        frameUs: us(b.s.gpuFrameNs - a.s.gpuFrameNs, frameN),
        frameN,
        boxKpx: draws > 0 ? ((b.s.boxPixels - a.s.boxPixels) / draws / 1000).toFixed(0) : '-',
    };
    log('fase ' + phase.name + ': ' + fps.toFixed(1) + ' fps, ' + frames + ' quadros, ' + draws + ' desenhos, marcador '
        + markers + '/' + r.hits + ' (' + r.markerUs + ' µs CPU no jogo), efeito ' + r.renderUs + ' µs CPU na render, '
        + r.gpuUs + ' µs GPU (' + gpuN + ' medidas), caixa ' + r.boxKpx + ' mil px, quadro inteiro ' + r.frameUs
        + ' µs GPU (' + frameN + ' medidas), swap ' + r.swap);
    return r;
}

let frames = 0, done = false, start = null;
const results = [];

Main['void DoDraw(GameTime gameTime)'].hook((original, self, gameTime) => {
    if (!done && !Main.gameMenu) {
        frames++;
        const p = Main.player[Main.myPlayer];
        p.statLife = p.statLifeMax2;

        if (frames === WARMUP) {
            const s = bl.screen.__stats();
            log('início: tela ' + Main.screenWidth + 'x' + Main.screenHeight + ', hook ' + s.hookInstalled
                + ', mundo ' + s.worldPostRender + ', interface ' + s.uiPostRender);
            check('hook da câmera', s.hookInstalled && s.worldPostRender > 0 && s.uiPostRender > 0,
                s.worldPostRender + '/' + s.uiPostRender);
            bl.screen.__trace();
            start = snapshot();
        }
        if (frames >= WARMUP) {
            const i = Math.floor((frames - WARMUP) / PHASE);
            const local = (frames - WARMUP) % PHASE;
            if (i < phases.length) {
                applyPoc(phases[i].poc);
                if (local === PHASE / 2 && phases[i].shot) log(phases[i].shot);
                if (local === PHASE / 2 && phases[i].poc === 'player') bl.screen.__trace();
            }
            if (local === 0 && i > 0) {
                const now = snapshot();
                results.push(report(phases[i - 1], start, now));
                start = now;
            }
            if (i === phases.length) finish();
        }
    }
    original(self, gameTime);
});

function finish() {
    done = true;
    bl.screen.__poc(false);
    const [off, small, big, off2] = results;
    const s = bl.screen.__stats();
    log('GL: contexto ' + s.hasContext + ', fbo ' + s.drawFbo + ', viewport ' + s.viewport.join(',') + ', samples '
        + s.samples + ', blitError ' + s.blitError + ', cópias ' + s.copyFallbacks + ', timer de GPU ' + s.gpuTimer);
    check('marcador montado', s.markerReady, String(s.markerReady));
    for (const r of [small, big]) {
        check('marcador chega (' + r.markers + ' emitidos)', r.hits >= r.markers * 0.9 && r.markers > 0, r.hits + ' vistos');
        check('desenha no marcador, não no swap', r.draws > 0 && r.swap === 0, r.draws + ' desenhos, ' + r.swap + ' no swap');
    }
    check('desligado não desenha', off.draws === 0 && off2.draws === 0, off.draws + ' e ' + off2.draws);
    check('espelho do estado GL confere', s.shadowChecks > 0 && s.shadowMismatches === 0,
        s.shadowChecks + ' conferidos, ' + s.shadowMismatches + ' diferentes');
    const base = (off.fps + off2.fps) / 2;
    log('custo: fps ' + base.toFixed(1) + ' desligado, ' + small.fps.toFixed(1) + ' pequeno, ' + big.fps.toFixed(1)
        + ' grande; GPU do efeito ' + small.gpuUs + ' / ' + big.gpuUs + ' µs (caixa ' + small.boxKpx + ' / ' + big.boxKpx
        + ' mil px); quadro inteiro ' + off.frameUs + ' desligado, ' + small.frameUs + ' pequeno, ' + big.frameUs + ' grande, '
        + off2.frameUs + ' desligado µs; CPU render ' + small.renderUs + ' / ' + big.renderUs
        + ' µs; marcador ' + small.markerUs + ' / ' + big.markerUs + ' µs');
    log('FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
}

log('carregado');

export default class TestFxPoc extends Mod {}
