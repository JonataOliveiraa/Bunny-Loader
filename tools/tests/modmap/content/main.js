// Etapa B6 do ModBiome (docs/local/PLANO-MODBIOME.md), o fundo do mapa:
//   - a cena com MapBackground (BiomeHigh) põe o caminho no canal mapBackground;
//   - com o mapa em tela cheia aberto, o fundo desenhado é a textura do mod
//     (37 x 23, vermelha) cobrindo a tela, com a cor do céu na superfície;
//   - MapBackgroundFullbright deixa branco; MapBackgroundColor troca a cor;
//   - desligada, o fundo volta a ser o do jogo.
// Loga "modmap <caso>: ok | FALHOU" e "modmap: tela <nome>" para a captura.
const Main = Terraria.Main;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('modmap ' + label + ': ok');
        else { fails++; bl.log('modmap ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('modmap ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const PATH = 'Map/TestMapBg';
const TINT = [10, 20, 30, 255];

export class TestMapScene extends ModSceneEffect {
    static on = false;
    static fullbright = false;
    static tint = false;
    get Priority() { return SceneEffectPriority.BiomeHigh; }
    get MapBackground() { return PATH; }
    get MapBackgroundFullbright() { return TestMapScene.fullbright; }
    MapBackgroundColor(color) { if (TestMapScene.tint) color.value = Color.new(...TINT); }
    IsSceneEffectActive(player) { return TestMapScene.on; }
}

const rgba = (c) => [c.R, c.G, c.B, c.A].join(',');

// O que o fundo do mapa desenhou (só dentro do DrawMapFullscreenBackground do GUIMap).
let draws = { mod: 0, game: 0, last: null };
let gameRect = null;   // o retângulo do fundo do jogo (a tela do mapa)
function watchDraws() {
    const gate = GUIMap['void DrawMapFullscreenBackground(Vector2 screenPosition, int screenWidth, int screenHeight)'];
    Microsoft.Xna.Framework.Graphics.SpriteBatch['void Draw(Texture2D texture, Rectangle destinationRectangle, Color color)'].hook(
        (original, self, texture, rect, color) => {
            const mod = texture && texture.Width === 37 && texture.Height === 23;
            if (mod) draws.mod++; else draws.game++;
            draws.last = { mod, w: rect.Width, h: rect.Height, color: rgba(color) };
            if (!mod) gameRect = { w: rect.Width, h: rect.Height };
            return original(self, texture, rect, color);
        }, { whileIn: gate });
}

const map = () => GUIInstance.Active.GUIMap;
const reset = () => { draws = { mod: 0, game: 0, last: null }; };

let frame = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    frame++;
    if (frame === 1) { Main.dayTime = true; Main.time = 27000; }
    self.statLife = self.statLifeMax2;
    self.immune = true;
    self.immuneTime = 30;

    if (frame === 10) {
        check('o hook do fundo do mapa aceita o whileIn (o loader instalou o dele)', () => { watchDraws(); });
        check('desligada: a cena sem fundo de mapa', () => self.CurrentSceneEffect.mapBackground.from === null || 'veio ' + self.CurrentSceneEffect.mapBackground.value);
        map().OpenFullscreenMap();
    }
    // O mundo quase não explorado cobre o fundo de preto: o mapa longe, com a
    // borda do mundo na tela, deixa o fundo à vista na captura.
    if (frame > 10) {
        Main.mapFullscreenScale = 0.2;
        Main.mapFullscreenPos = Vector2.new(Main.maxTilesX / 2, Main.maxTilesY / 2);
    }
    if (frame === 60) {
        check('mapa aberto: o fundo do jogo desenhado', () => (draws.game > 0 && draws.mod === 0) || JSON.stringify(draws));
        bl.log('modmap: tela jogo-antes');
    }
    // Cada tela fica uns 5 s: a captura (tools, pelo log) chega 1 a 3 s depois da linha "tela".
    if (frame === 360) TestMapScene.on = true;
    if (frame === 420) {
        const scene = self.CurrentSceneEffect.mapBackground;
        check('ligada: o canal mapBackground tem o caminho', () => (scene.value === PATH && scene.from instanceof TestMapScene) || 'veio ' + scene.value);
        check('o fundo é a textura do mod, na tela toda', () => {
            const l = draws.last;
            return (draws.mod > 0 && l && l.mod && gameRect && l.w === gameRect.w && l.h === gameRect.h) ||
                JSON.stringify(draws) + ' fundo do jogo ' + JSON.stringify(gameRect);
        });
        check('na superfície, a cor do céu', () => {
            const sky = rgba(Main.ColorOfTheSkies);
            return (draws.last && draws.last.color === sky) || (draws.last && draws.last.color) + ', céu ' + sky;
        });
        bl.log('modmap: tela de-mod');
    }
    if (frame === 720) {
        TestMapScene.fullbright = true;
        reset();
    }
    if (frame === 760) {
        check('MapBackgroundFullbright: branco', () => (draws.last && draws.last.mod && draws.last.color === '255,255,255,255') || JSON.stringify(draws.last));
        TestMapScene.tint = true;
        reset();
    }
    if (frame === 800) {
        check('MapBackgroundColor troca a cor', () => (draws.last && draws.last.mod && draws.last.color === TINT.join(',')) || JSON.stringify(draws.last));
        TestMapScene.on = false;
        TestMapScene.fullbright = false;
        TestMapScene.tint = false;
    }
    // A cena é refeita no quadro seguinte: conta só depois.
    if (frame === 810) reset();
    if (frame === 860) {
        check('desligada: o fundo do jogo volta', () => (draws.game > 0 && draws.mod === 0) || JSON.stringify(draws));
        bl.log('modmap: tela jogo-depois');
    }
    if (frame === 1160) {
        map().CloseFullscreenMap();
        done = true;
        bl.log('modmap FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('modmap: carregado');

export default class TestModMap extends Mod {}
