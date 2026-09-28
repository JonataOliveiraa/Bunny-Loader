// Etapa B5.0 do ModBiome (docs/local/PLANO-MODBIOME.md): medir, antes de
// escrever os fundos de mod, como o celular escolhe e desenha os fundos.
//   1. Tamanhos: TextureAssets.Background, Main.backgroundWidth/Height,
//      maxBackgrounds, bgAlphaFrontLayer/FarBackLayer, as máscaras do celular
//      (TextureMaskManager.BackgroundMasks), SurfaceBackgroundID.Sets.
//   2. Ordem e thread (60 quadros) de: GetPreferredBGStyleForPlayer,
//      UpdateBGVisibility_Front/BackLayer, DrawSurfaceBG, Step1/Step2 das
//      montanhas, GetFogPower (logo depois das camadas da frente),
//      DrawBackground, PickUndergroundBackgroundStyle, get_backTextureValues.
//   3. Três faixas desenhadas pelo JS dentro do DrawSurfaceBG, sem Begin/End
//      (o lote já está aberto): vermelha depois do Step1 (longe), verde depois
//      do Step2 (meio) e azul antes do GetFogPower (perto). Na tela: a vermelha
//      atrás das montanhas, a verde entre elas e as árvores, a azul na frente.
// Loga "bgscan <caso>: ok | FALHOU" e "bgscan medida ...".
const Main = Terraria.Main;
const { Rectangle, Vector2 } = Microsoft.Xna.Framework;
const { Color, SpriteBatch } = Microsoft.Xna.Framework.Graphics;
const TextureAssets = Terraria.GameContent.TextureAssets;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('bgscan ' + label + ': ok');
        else { fails++; bl.log('bgscan ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('bgscan ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}
const measure = (label, value) => bl.log('bgscan medida ' + label + ': ' + value);
function tryHook(label, fn) {
    try { fn(); } catch (e) { fails++; bl.log('bgscan hook ' + label + ': FALHOU com ' + e); }
}
const tid = () => { try { return System.Threading.Thread.CurrentThread.ManagedThreadId; } catch (e) { return '?'; } };

// ---- 2. ordem e thread ----
let frame = 0, gameThread = null;
const counts = new Map(), threads = new Map(), order = [];
function event(name, extra) {
    const t = tid();
    if (!threads.has(name)) threads.set(name, new Set());
    threads.get(name).add(t);
    if (frame >= 60 && frame < 120) counts.set(name, (counts.get(name) || 0) + 1);
    if (frame === 100 && order.length < 40) order.push(name + (extra !== undefined ? '=' + extra : ''));
}

tryHook('GetPreferredBGStyleForPlayer', () => Main['int GetPreferredBGStyleForPlayer()'].hook((original) => {
    const r = original();
    event('GetPreferredBGStyleForPlayer', r);
    return r;
}));
tryHook('UpdateBGVisibility_FrontLayer', () => Main['void UpdateBGVisibility_FrontLayer(Nullable`1 targetBiomeOverride, Nullable`1 transitionAmountOverride)'].hook((original, self, a, b) => {
        event('UpdateBGVisibility_FrontLayer', a === null ? 'null' : a);
        original(self, a, b);
    }));
tryHook('UpdateBGVisibility_BackLayer', () => Main['void UpdateBGVisibility_BackLayer(Nullable`1 targetBiomeOverride, Nullable`1 transitionAmountOverride)'].hook((original, self, a, b) => {
        event('UpdateBGVisibility_BackLayer', a === null ? 'null' : a);
        original(self, a, b);
    }));
tryHook('DrawSurfaceBG', () => Main['void DrawSurfaceBG()'].hook((original, self) => {
        event('DrawSurfaceBG');
        original(self);
    }));

// ---- 3. as faixas ----
const DRAW = 'void Draw(Texture2D texture, Rectangle destinationRectangle, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, SpriteEffects effects, float layerDepth)';
let draw = null, drawn = { far: 0, mid: 0, close: 0 }, drawError = null, step1 = null;
function rect(x, y, w, h) {
    const r = Rectangle.new();
    r['void .ctor(int x, int y, int width, int height)'](x, y, w, h);
    return r;
}
function color(r, g, b, a) {
    const c = Color.new();
    c['void .ctor(int r, int g, int b, int a)'](r, g, b, a);
    return c;
}
function band(which, x, c) {
    if (drawError || frame < 60 || frame > 900) return;
    try {
        if (!draw) draw = SpriteBatch[DRAW];
        const origin = Vector2.new();
        draw(Main.spriteBatch, TextureAssets.MagicPixel.Value, rect(Math.floor(Main.screenWidth * x), 0, Math.floor(Main.screenWidth * 0.12), Main.screenHeight), null, c, 0, origin, 0, 0);
        drawn[which]++;
    } catch (e) {
        drawError = which + ': ' + e;
    }
}
let RED = null, GREEN = null, BLUE = null;

tryHook('DrawSurfaceBG_BackMountainsStep1', () => Main['void DrawSurfaceBG_BackMountainsStep1(float backgroundTopMagicNumber, float bgGlobalScaleMultiplier, int pushBGTopHack)'].hook((original, self, top, scale, push) => {
        event('Step1');
        original(self, top, scale, push);
        if (frame === 100) step1 = { top, scale, push, bgTopY: self.bgTopY, bgStartX: self.bgStartX, bgLoops: self.bgLoops, bgParallax: self.bgParallax, bgScale: Main.bgScale, bgWidthScaled: Main.bgWidthScaled };
        band('far', 0.2, RED || (RED = color(230, 30, 30, 255)));
    }));
tryHook('DrawSurfaceBG_BackMountainsStep2', () => Main['void DrawSurfaceBG_BackMountainsStep2(int pushBGTopHack)'].hook((original, self, push) => {
        event('Step2');
        original(self, push);
        band('mid', 0.44, GREEN || (GREEN = color(30, 210, 60, 255)));
    }));
tryHook('DrawSurfaceBG_GetFogPower', () => Main['float DrawSurfaceBG_GetFogPower()'].hook((original) => {
        event('GetFogPower');
        band('close', 0.68, BLUE || (BLUE = color(40, 80, 240, 255)));
        return original();
    }));

// ---- subsolo ----
tryHook('DrawBackground', () => Main['void DrawBackground()'].hook((original, self) => {
        event('DrawBackground');
        original(self);
    }));
tryHook('DrawBackground_PickUndergroundBackgroundStyle', () => Main['int DrawBackground_PickUndergroundBackgroundStyle(double magmaLayer)'].hook((original, magma) => {
        const r = original(magma);
        event('PickUndergroundBackgroundStyle', r);
        return r;
    }));
tryHook('get_backTextureValues', () => Main['Texture2D[] get_backTextureValues()'].hook((original, self) => {
        event('get_backTextureValues');
        return original(self);
    }));

// ---- 1. tamanhos ----
function sizes() {
    const m = (label, fn) => { try { measure(label, fn()); } catch (e) { measure(label, 'erro: ' + e); } };
    m('TextureAssets.Background.length', () => TextureAssets.Background.length);
    m('Main.maxBackgrounds', () => Main.maxBackgrounds);
    m('Main.backgroundWidth.length', () => Main.backgroundWidth.length);
    m('Main.backgroundHeight.length', () => Main.backgroundHeight.length);
    m('Main.bgAlphaFrontLayer.length', () => Main.bgAlphaFrontLayer.length);
    m('Main.bgAlphaFarBackLayer.length', () => Main.bgAlphaFarBackLayer.length);
    m('TextureMaskManager.BackgroundMasks.length', () => bl.classOf('', 'TextureMaskManager').BackgroundMasks.length);
    m('SurfaceBackgroundID.Count', () => Terraria.ID.SurfaceBackgroundID.Count);
    m('SurfaceBackgroundID.Sets.IsForest.length', () => Terraria.ID.SurfaceBackgroundID.Sets.IsForest.length);
    m('SurfaceBackgroundID.Sets.IsDesertVariant.length', () => Terraria.ID.SurfaceBackgroundID.Sets.IsDesertVariant.length);
    m('Main.HorizonShadersEnabled', () => Main.HorizonShadersEnabled);
    m('Main.BackgroundEnabled', () => Main.BackgroundEnabled);
    m('Main.gfxQuality', () => Main.gfxQuality);
    m('Main.bgStyle', () => Main.bgStyle);
    m('Main.undergroundBackground', () => Main.undergroundBackground);
    m('backTexture', () => Array.from(Main.instance.backTexture).join(','));
    m('backTextureValues.length', () => Main.instance.backTextureValues.length);
    m('bgAlphaFrontLayer', () => Array.from(Main.bgAlphaFrontLayer).map((v) => v.toFixed(2)).join(','));
    m('bgAlphaFarBackLayer', () => Array.from(Main.bgAlphaFarBackLayer).map((v) => v.toFixed(2)).join(','));
    // O mesmo array em quem guarda o estado (LocalUserGameState) e no Main?
    m('bgAlphaFrontLayer é o do LocalUserGameState', () => Main.bgAlphaFrontLayer === LocalUserGameState.Instance.bgAlphaFrontLayer);
    m('TextureAssets.Background[0..2] carregados', () => [0, 1, 2].map((i) => { const a = TextureAssets.Background[i]; return a ? String(a.IsLoaded) : 'nulo'; }).join(','));
}

Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu) return;
    frame++;
    if (gameThread === null) gameThread = tid();
    if (frame === 30) check('tamanhos', sizes);
    if (frame === 130) {
        measure('thread do jogo (Player.Update)', gameThread);
        for (const [name, set] of threads) measure('thread ' + name, Array.from(set).join(','));
        for (const [name, n] of counts) measure('chamadas em 60 quadros ' + name, n);
        measure('ordem no quadro 100', order.join(' > '));
        measure('Step1 no quadro 100', JSON.stringify(step1));
    }
    if (frame === 400) {
        check('faixas desenhadas nas três camadas', () =>
            (!drawError && drawn.far > 0 && drawn.mid > 0 && drawn.close > 0) || JSON.stringify({ drawn, drawError }));
        bl.log('bgscan FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)') + ' (as faixas seguem até o quadro 900)');
    }
});
bl.log('bgscan: carregado');

export default class TestBgscan extends Mod {}
