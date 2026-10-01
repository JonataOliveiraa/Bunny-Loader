// O fundo do bioma sumindo (só o céu azul fica). Uma cena sempre ativa, com a
// prioridade mais alta, pede um fundo de superfície com as texturas do Example Mod; a cada 60
// quadros o log mostra o que o desenho do fundo usa, para comparar antes e
// depois do evento suspeito (app no segundo plano, troca de usuário...).
const Main = Terraria.Main;
const Textures = Terraria.GameContent.TextureAssets;

// As texturas do Example Mod (a chave com o nome do mod vale de fora dele).
const BG = 'examplemod/Assets/Textures/Backgrounds/ExampleBiomeSurface';
export class BgResetStyle extends ModSurfaceBackgroundStyle {
    ChooseFarTexture() { return BackgroundTextureLoader.GetBackgroundSlot(BG + 'Far'); }
    ChooseMiddleTexture() { return BackgroundTextureLoader.GetBackgroundSlot(BG + 'Mid0'); }
    ChooseCloseTexture(scale, parallax, a, b) { return BackgroundTextureLoader.GetBackgroundSlot(BG + 'Close'); }
}

export class BgResetScene extends ModSceneEffect {
    get Priority() { return SceneEffectPriority.BiomeHigh; }
    get SurfaceBackgroundStyle() { return ModContent.GetInstance(BgResetStyle); }
    IsSceneEffectActive(player) { return true; }
}

function state() {
    const style = ModContent.GetInstance(BgResetStyle);
    const far = Main.bgAlphaFarBackLayer, front = Main.bgAlphaFrontLayer;
    const slot = style.Slot;
    const texSlot = style.ChooseFarTexture();
    const lugs = bl.classOf('', 'LocalUserGameState').Instance;
    return {
        bgStyle: Main.bgStyle, slot,
        far: far.length > slot ? +far[slot].toFixed(2) : 'curto ' + far.length,
        front: front.length > slot ? +front[slot].toFixed(2) : 'curto ' + front.length,
        vanillaFar: +far[0].toFixed(2),
        textures: Textures.Background.length, widths: Main.backgroundWidth.length,
        texSlot, texOk: texSlot < Textures.Background.length && !!Textures.Background[texSlot] && !!Textures.Background[texSlot].Value,
        lugs: lugs ? bl.addressOf(lugs).toString(16) : null,
        bgOn: Main.BackgroundEnabled,
    };
}

let frames = 0;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu) return;
    if (++frames % 60 !== 0) return;
    try {
        bl.log('bgreset ' + frames + ': ' + JSON.stringify(state()));
    } catch (e) {
        bl.log('bgreset ' + frames + ': erro ' + e);
    }
    if (frames === 300) bl.log('bgreset FIM: tudo ok');
});
bl.log('bgreset: carregado');

export default class TestBgReset extends Mod {}
