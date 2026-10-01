// Observa o fundo de superfície sem mexer em nada, para achar quando ele some
// (só o céu azul fica). A cada 60 quadros compara o estado com o anterior e
// loga se algo mudou (e, de todo jeito, a cada 10 s). As camadas de longe e da
// frente são desenhadas com a transparência de cada estilo: soma zero nas duas
// é a tela só com o céu.
const Main = Terraria.Main;
const Textures = Terraria.GameContent.TextureAssets;
const LUGS = bl.classOf('', 'LocalUserGameState');

const sum = (a) => {
    let s = 0;
    for (let i = 0; i < a.length; i++) s += a[i];
    return +s.toFixed(2);
};

function state() {
    const far = Main.bgAlphaFarBackLayer, front = Main.bgAlphaFrontLayer;
    const style = Main.bgStyle;
    const lugs = LUGS.Instance;
    let scene = null;
    try { scene = Main.LocalPlayer.CurrentSceneEffect; } catch (e) { /* sem cena de mod: o campo não existe */ }
    return {
        style,
        farSum: sum(far), frontSum: sum(front),
        farAt: style < far.length ? +far[style].toFixed(2) : 'fora ' + far.length,
        frontAt: style < front.length ? +front[style].toFixed(2) : 'fora ' + front.length,
        lens: far.length + '/' + front.length + '/' + Textures.Background.length,
        bgOn: Main.BackgroundEnabled,
        lugs: lugs ? bl.addressOf(lugs).toString(16) : null,
        sceneBg: scene && scene.surfaceBackground ? scene.surfaceBackground.value : null,
        menu: Main.gameMenu,
    };
}

let frames = 0, last = '', lastAt = 0;
Terraria.Main['void DoUpdate(GameTime gameTime)'].hook((original, main, time) => {
    original(main, time);
    if (++frames % 60 !== 0) return;
    let text;
    try {
        text = JSON.stringify(state());
    } catch (e) {
        text = 'erro ' + e;
    }
    if (text !== last || frames - lastAt >= 600) {
        bl.log('bgwatch ' + frames + (text !== last ? ' MUDOU' : '') + ': ' + text);
        last = text;
        lastAt = frames;
    }
}, { ifBusy: 'original' });
bl.log('bgwatch: carregado');

export default class DiagBgWatch extends Mod {}
