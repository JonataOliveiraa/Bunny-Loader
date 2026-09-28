// Etapa B5 do ModBiome (docs/local/PLANO-MODBIOME.md), de dia, na superfície,
// numa floresta:
//   - BackgroundTextureLoader: as texturas de Assets/Textures/Backgrounds com
//     números depois dos 344 do jogo, largura e altura, as três grafias de
//     GetBackgroundSlot, e o erro de uma que não existe;
//   - números dos estilos: superfície depois dos 16, subsolo depois dos 22;
//   - a cena com Priority None não troca nada; com BiomeLow ganha da floresta:
//     Main.bgStyle vira o do mod, a transparência dele sobe, as três camadas
//     pedem textura, e o subsolo (que o jogo desenha também com a tela na
//     superfície) troca as 4 texturas em backTexture/backTextureValues;
//   - desligada, tudo volta ao do jogo.
// Loga "modbg <caso>: ok | FALHOU".
const Main = Terraria.Main;
const TextureAssets = Terraria.GameContent.TextureAssets;
const BG = 'Assets/Textures/Backgrounds/';

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('modbg ' + label + ': ok');
        else { fails++; bl.log('modbg ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('modbg ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const calls = { far: 0, mid: 0, close: 0, fill: 0, fades: 0 };
export class TestSurfaceStyle extends ModSurfaceBackgroundStyle {
    ModifyFarFades(fades, speed) {
        calls.fades++;
        super.ModifyFarFades(fades, speed);
    }
    ChooseFarTexture() { calls.far++; return BackgroundTextureLoader.GetBackgroundSlot(this.Mod, BG + 'ExampleBiomeSurfaceFar'); }
    ChooseMiddleTexture() { calls.mid++; return BackgroundTextureLoader.GetBackgroundSlot(this.Mod, BG + 'ExampleBiomeSurfaceMid0'); }
    ChooseCloseTexture(scale, parallax, a, b) { calls.close++; return BackgroundTextureLoader.GetBackgroundSlot(this.Mod, BG + 'ExampleBiomeSurfaceClose'); }
}

export class TestUndergroundStyle extends ModUndergroundBackgroundStyle {
    FillTextureArray(slots) {
        calls.fill++;
        for (let i = 0; i < 4; i++) slots[i] = BackgroundTextureLoader.GetBackgroundSlot(this.Mod, BG + 'ExampleBiomeUnderground' + i);
    }
}

export class TestScene extends ModSceneEffect {
    static on = false;
    static priority = SceneEffectPriority.None;
    get Priority() { return TestScene.priority; }
    get SurfaceBackgroundStyle() { return ModContent.GetInstance(TestSurfaceStyle); }
    get UndergroundBackgroundStyle() { return ModContent.GetInstance(TestUndergroundStyle); }
    IsSceneEffectActive(player) { return TestScene.on; }
}

const surface = () => ModContent.GetInstance(TestSurfaceStyle);
const underground = () => ModContent.GetInstance(TestUndergroundStyle);
const slotOf = (name) => BackgroundTextureLoader.GetBackgroundSlot(bl.mod, BG + name);
const state = () => JSON.stringify({
    bgStyle: Main.bgStyle, ug: Main.undergroundBackground,
    front: Main.bgAlphaFrontLayer.length > surface().Slot ? Main.bgAlphaFrontLayer[surface().Slot].toFixed(2) : 'curto',
    far: Main.bgAlphaFarBackLayer.length > surface().Slot ? Main.bgAlphaFarBackLayer[surface().Slot].toFixed(2) : 'curto',
    calls,
});

function registration() {
    const far = slotOf('ExampleBiomeSurfaceFar');
    check('texturas depois das do jogo, com largura e altura', () => {
        const vanilla = Main.maxBackgrounds;
        return (far >= vanilla && TextureAssets.Background.length > far && TextureAssets.Background[far].Value &&
            Main.backgroundWidth[far] === 1024 && Main.backgroundHeight[far] === 408) ||
            `${far} de ${TextureAssets.Background.length}, ${Main.backgroundWidth[far]}x${Main.backgroundHeight[far]}`;
    });
    check('GetBackgroundSlot pelo mod, pelo caminho do mod e com o nome do mod na frente', () => {
        const a = BackgroundTextureLoader.GetBackgroundSlot(bl.mod, BG + 'ExampleBiomeSurfaceFar');
        const b = BackgroundTextureLoader.GetBackgroundSlot(BG + 'ExampleBiomeSurfaceFar');
        const c = BackgroundTextureLoader.GetBackgroundSlot('test-modbg/' + BG + 'ExampleBiomeSurfaceFar');
        return (a === far && b === far && c === far) || `${a} ${b} ${c}`;
    });
    check('TryGetBackgroundSlot e o erro de um que não existe', () => {
        const r = new Ref();
        const found = BackgroundTextureLoader.TryGetBackgroundSlot(BG + 'ExampleBiomeSurfaceFar', r);
        const missing = BackgroundTextureLoader.TryGetBackgroundSlot(BG + 'NaoExiste', new Ref());
        let threw = false;
        try { BackgroundTextureLoader.GetBackgroundSlot(BG + 'NaoExiste'); } catch (e) { threw = true; }
        return (found && r.value === far && !missing && threw) || `${found} ${r.value} ${missing} ${threw}`;
    });
    check('números dos estilos: superfície depois dos 16, subsolo depois dos 22', () =>
        (surface().Slot >= 16 && underground().Slot >= 22) || `${surface().Slot} ${underground().Slot}`);
}

let vanillaBg = -1, vanillaUg = -1, onAt = 0, sawStyle = -1, sawUg = -1, offAt = 0;
function ugPatched() {
    const s = LocalUserGameState.Instance;
    const values = s.backTextureValues, numbers = s.backTexture;
    for (let i = 0; i < 4; i++) {
        const slot = slotOf('ExampleBiomeUnderground' + i);
        if (numbers[i] !== slot || values[i] !== TextureAssets.Background[slot].Value) return 'posição ' + i + ': ' + numbers[i];
    }
    return true;
}

let frame = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    frame++;
    if (frame === 10) check('registro', registration);
    if (frame === 20) {
        vanillaBg = Main.bgStyle;
        vanillaUg = Main.undergroundBackground;
        check('começo numa floresta, com a caverna comum embaixo (' + vanillaBg + ', ' + vanillaUg + ')', () =>
            [0, 10, 11, 12].includes(vanillaBg) || 'bgStyle ' + vanillaBg + ' não é floresta');
        TestScene.on = true;
        TestScene.priority = SceneEffectPriority.None;
    }
    if (frame === 110) {
        check('Priority None: nada troca', () => (Main.bgStyle === vanillaBg && Main.undergroundBackground === vanillaUg) || state());
        TestScene.priority = SceneEffectPriority.BiomeLow;
        onAt = frame;
    }
    if (onAt && sawStyle < 0 && Main.bgStyle === surface().Slot) sawStyle = frame - onAt;
    if (onAt && sawUg < 0 && Main.undergroundBackground === underground().Slot) sawUg = frame - onAt;
    if (frame === 260) {
        check('BiomeLow ganha da floresta: bgStyle do mod (' + sawStyle + ' quadros)', () => Main.bgStyle === surface().Slot || state());
        check('a transparência do estilo subiu nas duas camadas', () =>
            (Main.bgAlphaFrontLayer[surface().Slot] > 0.99 && Main.bgAlphaFarBackLayer[surface().Slot] > 0.99) || state());
        check('as três camadas pediram textura e o ModifyFarFades rodou', () =>
            (calls.far > 0 && calls.mid > 0 && calls.close > 0 && calls.fades > 0) || state());
        check('subsolo: o estilo do mod (' + sawUg + ' quadros)', () => Main.undergroundBackground === underground().Slot || state());
        check('subsolo: as 4 texturas trocadas em backTexture/backTextureValues', ugPatched);
        bl.log('modbg: fundo do mod na tela por 5 s');
    }
    if (frame === 560) {
        TestScene.on = false;
        offAt = frame;
    }
    if (frame === 720) {
        check('desligada: bgStyle volta ao do jogo', () => Main.bgStyle === vanillaBg || state());
        check('desligada: subsolo volta ao do jogo', () => Main.undergroundBackground === vanillaUg || state());
        check('desligada: a camada da frente do mod sumiu', () => Main.bgAlphaFrontLayer[surface().Slot] < 0.01 || state());
        done = true;
        bl.log('modbg FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('modbg: carregado');

export default class TestModbg extends Mod {}
