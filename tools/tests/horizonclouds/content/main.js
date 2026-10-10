// Nuvem de mod no desenho do horizonte (docs/historico/FUNDO-SUMINDO-NUVEM-DE-MOD.md).
// O NextHorizonRenderer.DrawCloud do celular lê TextureMaskManager.CloudMasks[tipo]
// sem conferir o limite; sem máscara para a nuvem de mod, a exceção sai com o
// lote das nuvens aberto e o DrawSurfaceBG para ali em todo quadro seguinte.
// O MuMu não liga esse desenho sozinho: aqui ele é ligado pelo
// Main.SetUseHorizonRender(true).
//
// Só conta o que NÃO lança (CloudsEnd, DrawSurfaceLayer): um hook num método
// que lança engoliria a exceção (o callOriginal a segura) e esconderia o
// defeito. Com o fundo inteiro, o CloudsEnd roda 3 vezes por quadro (as três
// passadas de nuvens).
//   1. base: horizonte ligado, nuvens do jogo;
//   2. com a correção: toda nuvem ativa vira a de mod;
//   3. prova: a máscara da nuvem de mod vira null (o estado de antes da
//      correção). O fundo QUEBRA até fechar o jogo: só no emulador.
// Loga "horizonclouds ...".
const Main = Terraria.Main;
const Next = Terraria.GameContent.Drawing.NextHorizonRenderer;
const Masks = bl.classOf('', 'TextureMaskManager');
const PHASE = 240;

export class HorizonTestCloud extends ModCloud {
    Texture = 'Textures/HorizonTestCloud';
    SpawnChance() { return 0; }
}

let cloudsEnd = 0, layers = 0;
Next['void CloudsEnd()'].hook((original, self) => { cloudsEnd++; original(self); }, { flag: 'horizonclouds.count' });
Next['void DrawSurfaceLayer(int layerIndex)'].hook((original, self, i) => { layers++; original(self, i); }, { flag: 'horizonclouds.count' });

let fails = 0;
const log = (s) => bl.log('horizonclouds ' + s);
function check(label, ok, detail) {
    if (!ok) fails++;
    log(label + ': ' + (ok ? 'ok' : 'FALHOU') + ' (' + detail + ')');
}

function modType() { return Terraria.ID.CloudID.Count; }

function forceModClouds() {
    const type = modType();
    const texture = Terraria.GameContent.TextureAssets.Cloud[type].Value;
    let n = 0;
    for (let i = 0; i < 200; i++) {
        const cloud = Main.cloud[i];
        if (!cloud.active) continue;
        if (cloud.type !== type) {
            cloud.type = type;
            cloud.width = Math.trunc(texture.Width * cloud.scale);
            cloud.height = Math.trunc(texture.Height * cloud.scale);
        }
        n++;
    }
    return n;
}

const phases = [];
let frames = 0, phase = -1, start = { cloudsEnd: 0, layers: 0 }, done = false;

function closePhase(name) {
    phases.push({ name, end: (cloudsEnd - start.cloudsEnd) / PHASE, layers: (layers - start.layers) / PHASE });
    start = { cloudsEnd, layers };
}

Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (done || i !== Main.myPlayer || Main.gameMenu) return;
    frames++;
    // Meio-dia, céu limpo: as nuvens e o fundo de superfície na tela.
    Main.dayTime = true;
    Main.time = 27000;
    if (frames === 120) {
        const before = String(Main.instance.HorizonRenderer);
        Main['void SetUseHorizonRender(bool horizonEnabled)'](true);
        const after = String(Main.instance.HorizonRenderer);
        log('renderizador: ' + before + ' -> ' + after + ', HorizonShadersEnabled ' + Main.HorizonShadersEnabled);
        check('horizonte ligado', after.includes('NextHorizonRenderer'), after);
        const masks = Masks.CloudMasks;
        check('máscara da nuvem de mod', modType() < masks.length && masks[modType()] !== null, 'tipo ' + modType() + ' de ' + masks.length);
        bl.hookFlags.set('horizonclouds.count', true);
        start = { cloudsEnd, layers };
        phase = 0;
    } else if (phase === 0 && frames === 120 + PHASE) {
        closePhase('base');
        phase = 1;
    } else if (phase === 1) {
        const n = forceModClouds();
        if (frames === 120 + 2 * PHASE) {
            closePhase('nuvens de mod (' + n + ' ativas)');
            Masks.CloudMasks[modType()] = null;
            phase = 2;
        }
    } else if (phase === 2) {
        forceModClouds();
        if (frames === 120 + 3 * PHASE) {
            closePhase('sem a máscara (o defeito antigo)');
            bl.hookFlags.set('horizonclouds.count', false);
            done = true;
            for (const p of phases) log('fase ' + p.name + ': CloudsEnd ' + p.end.toFixed(2) + '/quadro, DrawSurfaceLayer ' + p.layers.toFixed(2) + '/quadro');
            const [base, fixed, broken] = phases;
            check('base desenha as nuvens até o fim', base.end >= 2.9, base.end.toFixed(2));
            check('com nuvens de mod o fundo continua', fixed.end >= 2.9 && fixed.layers >= base.layers * 0.9, fixed.end.toFixed(2) + ' / camadas ' + fixed.layers.toFixed(2));
            check('sem a máscara o defeito volta (o teste enxerga o defeito)', broken.end < 0.5, broken.end.toFixed(2));
            log('FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)') + ' — o fundo fica quebrado até fechar o jogo');
        }
    }
});
log('carregado');

export default class TestHorizonClouds extends Mod {}
