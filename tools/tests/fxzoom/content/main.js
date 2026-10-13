// Efeitos de tela com o zoom do celular. O zoom (a barra e a pinça) passa
// por GUIZoom.ApplyPixelZoom: muda a escala da câmera do mundo e, com ela, a
// tela virtual (Main.screenWidth/Height). Em cada nível o redemoinho vai no
// centro do jogador com raio de 3 blocos do MUNDO convertido pela tela
// virtual; o anel deve envolver sempre a mesma área em volta do jogador.
// Também loga o GameViewMatrix.Zoom (o zoom do PC), para saber se ele entra.
// Loga "fxzoom ...".
const Main = Terraria.Main;
const GUIInstance = bl.classOf('', 'GUIInstance');
const Runner = bl.classOf('', 'XNAUnityRunner');
const log = (s) => bl.log('fxzoom ' + s);

const WARMUP = 120;
const STEP = 150;
const RADIUS_WORLD = 3 * 16;

let frames = 0, done = false, levels = null, defaultScale = 0;

function camera() {
    return Runner._instance.WorldCameraSettings;
}

function applyZoom(scale) {
    GUIInstance.Active.GUIZoom['void ApplyPixelZoom(float pixelScale, bool useBump)'](scale, false);
}

function describe() {
    const z = Main.GameViewMatrix.Zoom;
    const c = camera();
    return 'tela virtual ' + Main.screenWidth + 'x' + Main.screenHeight + ', escala da câmera ' + c.ZoomScale.toFixed(3)
        + ', GameViewMatrix.Zoom ' + z.X.toFixed(3) + 'x' + z.Y.toFixed(3) + ', GameZoomTarget ' + Main.GameZoomTarget.toFixed(3);
}

function aimAtPlayer() {
    const p = Main.player[Main.myPlayer];
    const sp = Main.screenPosition;
    const u = (p.Center.X - sp.X) / Main.screenWidth;
    const v = 1 - (p.Center.Y - sp.Y) / Main.screenHeight;
    bl.screen.__poc(true, u, v, RADIUS_WORLD / Main.screenHeight, 2.0);
    return { u, v };
}

Main['void DoDraw(GameTime gameTime)'].hook((original, self, gameTime) => {
    if (!done && !Main.gameMenu) {
        frames++;
        const p = Main.player[Main.myPlayer];
        p.statLife = p.statLifeMax2;

        if (frames === WARMUP) {
            const c = camera();
            defaultScale = GUIInstance.Active.GUIZoom.GetDefaultWorldZoomScale();
            levels = [defaultScale, c.MinPixelScale, c.MaxPixelScale, (c.MinPixelScale + c.MaxPixelScale) / 2];
            log('início: ' + describe() + ', escalas min ' + c.MinPixelScale.toFixed(3) + ' max ' + c.MaxPixelScale.toFixed(3)
                + ' padrão ' + defaultScale.toFixed(3));
        }
        if (frames >= WARMUP && levels) {
            const i = Math.floor((frames - WARMUP) / STEP);
            const local = (frames - WARMUP) % STEP;
            if (i < levels.length) {
                if (local === 0) applyZoom(levels[i]);
                const at = aimAtPlayer();
                if (local === STEP - 30) {
                    log('nível ' + i + ' (escala pedida ' + levels[i].toFixed(3) + '): ' + describe()
                        + ', centro ' + at.u.toFixed(3) + ',' + at.v.toFixed(3)
                        + ', raio ' + (RADIUS_WORLD / Main.screenHeight).toFixed(3) + ' da altura');
                    log('SHOT ' + i);
                }
            } else {
                applyZoom(defaultScale);
                bl.screen.__poc(false);
                log('FIM: zoom de volta ao padrão (' + describe() + ')');
                done = true;
            }
        }
    }
    original(self, gameTime);
});
log('carregado');

export default class TestFxZoom extends Mod {}
