// Etapa B6 do ModBiome (docs/local/PLANO-MODBIOME.md), a água de mod:
//   - números: a água depois dos 15 do jogo, a cachoeira depois dos 28; as
//     tabelas por estilo com o tamanho novo (texturas e liquidAlpha);
//   - um lago na frente do jogador; a cena com WaterStyle (BiomeHigh) troca
//     Main.waterStyle, o fade sobe o alpha dela e zera os do jogo, o
//     CalculateWaterStyle devolve ela, o respingo e a chuva são os dela, e a
//     luz atravessa a água sem perda (LightColorMultiplier 1);
//   - a cachoeira de mod é pedida ao jogo (DrawWaterfall com o número dela),
//     com a cor do ColorMultiplier (só o vermelho), num meio-bloco na parede;
//   - a chuva com a textura da água (GetRainTexture, vermelha, 16 x 44) e a
//     tintura de bioma com a cor dela (BiomeHairColor);
//   - desligada, tudo volta ao do jogo.
// Loga "modwater <caso>: ok | FALHOU" e "modwater: tela <nome>" para a captura.
const Main = Terraria.Main;
const W = Terraria.WorldGen;
const LR = Terraria.GameContent.Liquid.LiquidRenderer;
const TextureAssets = Terraria.GameContent.TextureAssets;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('modwater ' + label + ': ok');
        else { fails++; bl.log('modwater ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('modwater ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const SPLASH = Terraria.ID.DustID.GoldFlame;

let tintCalls = 0;
export class TestWaterfall extends ModWaterfallStyle {
    Texture = 'Water/TestWaterfall';
    ColorMultiplier(r, g, b, a) {
        tintCalls++;
        g.value = 0;
        b.value = 0;
    }
}

export class TestWater extends ModWaterStyle {
    Texture = 'Water/TestWater';
    ChooseWaterfallStyle() { return ModContent.GetInstance(TestWaterfall).Slot; }
    GetSplashDust() { return SPLASH; }
    LightColorMultiplier(r, g, b) { r.value = 1; g.value = 1; b.value = 1; }
    GetRainVariant() { return 1; }
    GetRainTexture() { return 'Water/TestRain'; }
    BiomeHairColor() { return Color.new(255, 0, 0, 255); }
}

const HAIR_RED = '255,0,0,255';
const rgba = (c) => [c.R, c.G, c.B, c.A].join(',');
// A tintura de bioma do jogo (a lambda do item 1983), chamada direto.
function biomeHair(player) {
    const lambdas = Terraria.Initializers.DyeInitializer['<>c'];
    player.hairDyeColor = Color.new(0, 0, 0, 0);
    return lambdas['<>9']['Color <LoadLegacyHairdyes>b__5_6(Player player, Color newColor, ref bool lighting)'](player, Color.White, new Ref(false));
}

// O desenho da chuva (depois do hook do loader, então vê a textura trocada).
const rainDraws = { mod: 0, game: 0, x: -1 };
function watchRain() {
    Microsoft.Xna.Framework.Graphics.SpriteBatch['void Draw(Texture2D texture, ref Vector2 position, ref Rectangle srcRect, ref Color color, float rotation, Vector2 origin, float scale)'].hook(
        (original, self, texture, position, srcRect, color, rotation, origin, scale) => {
            if (texture && texture.Width === 16 && texture.Height === 44) { rainDraws.mod++; rainDraws.x = srcRect.value.X; }
            else rainDraws.game++;
            return original(self, texture, position, srcRect, color, rotation, origin, scale);
        }, { whileIn: Terraria.Main['void DrawRain()'] });
}
function dropRain() {
    const p = Main.player[Main.myPlayer];
    for (let k = 0; k < 6; k++) {
        Terraria.Rain['int NewRainForced(Vector2 Position, Vector2 Velocity)'](Vector2.new(p.Center.X - 60 + k * 20, p.Center.Y - 260), Vector2.new(0, 4));
    }
}

export class TestScene extends ModSceneEffect {
    static on = false;
    get Priority() { return SceneEffectPriority.BiomeHigh; }
    get WaterStyle() { return ModContent.GetInstance(TestWater); }
    IsSceneEffectActive(player) { return TestScene.on; }
}

const water = () => ModContent.GetInstance(TestWater);
const waterfall = () => ModContent.GetInstance(TestWaterfall);

// Quantas vezes o jogo desenhou a cachoeira de cada número.
const drawnWaterfalls = {};
Terraria.WaterfallManager['void DrawWaterfall(SpriteBatch spriteBatch, int Style, float Alpha)'].hook((original, self, sb, style, alpha) => {
    drawnWaterfalls[style] = (drawnWaterfalls[style] || 0) + 1;
    return original(self, sb, style, alpha);
});

// Diagnóstico da luz através da água: em que thread o motor novo atualiza, e
// que estilo de água ele vê ali.
const decayDiag = { calls: 0, threads: {}, styles: {} };
Terraria.Graphics.Light.LightingEngine['void UpdateLightDecay()'].hook((original, self) => {
    original(self);
    decayDiag.calls++;
    let tid = '?';
    try { tid = System.Threading.Thread.CurrentThread.ManagedThreadId; } catch (e) {}
    decayDiag.threads[tid] = (decayDiag.threads[tid] || 0) + 1;
    const s = Main.waterStyle;
    decayDiag.styles[s] = (decayDiag.styles[s] || 0) + 1;
});

function registration() {
    check('números: a água depois dos 15, a cachoeira depois dos 28', () =>
        (water().Slot >= 15 && waterfall().Slot >= 28) || `água ${water().Slot}, cachoeira ${waterfall().Slot}`);
    check('tabelas de textura com o tamanho novo, e a da água carregada', () => {
        const s = water().Slot;
        const sizes = [LR._liquidTextures.length, TextureAssets.Liquid.length, TextureAssets.LiquidSlope.length];
        const ok = sizes.every((n) => n > s) && Terraria.WaterfallManager.waterfallTexture.length > waterfall().Slot;
        const tex = ok && LR._liquidTextures[s].Value && TextureAssets.Liquid[s].Value;
        return (ok && tex && tex.Width > 0) || `${sizes.join('/')} cachoeiras ${Terraria.WaterfallManager.waterfallTexture.length}`;
    });
}

// ---- o lago: 14 x 4 de água à direita do jogador, com chão e paredes de pedra ----
let pool = null;
const tileAt = (x, y) => Main.tile['Tile get_Item(int x, int y)'](x, y);
const place = (x, y, t) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, t, true, true, -1, 0);
const kill = (x, y) => W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](x, y, false, false, true);
function buildPool() {
    const p = Main.player[Main.myPlayer];
    const x0 = Math.floor((p.position.X + p.width) / 16) + 2;
    const floor = Math.floor((p.position.Y + p.height) / 16) + 2;
    pool = { x0, x1: x0 + 15, floor };
    for (let x = x0; x <= pool.x1; x++) {
        for (let y = floor - 8; y < floor; y++) { kill(x, y); tileAt(x, y).liquid = 0; }
        if (bl.tiles.typeAt(x, floor) >= 0) kill(x, floor);
        place(x, floor, Terraria.ID.TileID.Stone);
    }
    for (let y = floor - 4; y < floor; y++) { place(x0, y, Terraria.ID.TileID.Stone); place(pool.x1, y, Terraria.ID.TileID.Stone); }
    // A cachoeira: o topo da parede direita vira meio-bloco, com água cheia
    // de um lado e ar do outro (o FindWaterfalls do jogo).
    for (let x = pool.x1 + 1; x <= pool.x1 + 2; x++) {
        for (let y = floor - 6; y < floor + 2; y++) { kill(x, y); tileAt(x, y).liquid = 0; }
    }
    W['bool PoundTile(int i, int j)'](pool.x1, floor - 4);
    for (let x = x0 + 1; x < pool.x1; x++) {
        for (let y = floor - 4; y < floor; y++) {
            tileAt(x, y).liquid = 255;   // tipo 0 (água), o padrão
        }
    }
    W['void RangeFrame(int startX, int startY, int endX, int endY)'](x0, floor - 8, pool.x1, floor);
    bl.log(`modwater: lago em ${x0}..${pool.x1}, fundo ${floor}`);
}

let vanilla = -1;
const alphas = () => Main.liquidAlpha;
function onChecks() {
    const s = water().Slot;
    check('a cena troca a água (Main.waterStyle)', () => Main.waterStyle === s || `waterStyle ${Main.waterStyle}, esperado ${s}`);
    check('CalculateWaterStyle devolve a água da cena', () => {
        const got = Main['int CalculateWaterStyle(bool ignoreFountains)'](false);
        return got === s || 'devolveu ' + got;
    });
    check('fade: a água de mod em 1 e as do jogo em 0', () => {
        const a = alphas();
        const others = [];
        for (let i = 0; i < 15; i++) if (a[i] > 0) others.push(i + ':' + a[i].toFixed(2));
        return (a.length > s && a[s] === 1 && others.length === 0) || `mod ${a.length > s ? a[s] : 'curto'}, outras ${others.join(' ')}`;
    });
    check('respingo: Dust.dustWater é o da água', () => {
        const d = Terraria.Dust['int dustWater()']();
        return d === SPLASH || 'dustWater ' + d;
    });
    check('chuva: a variante da água', () => {
        const idx = Terraria.Rain['int NewRainForced(Vector2 Position, Vector2 Velocity)'](Main.player[Main.myPlayer].Center, Vector2.new(0, 5));
        if (idx < 0 || idx >= Main.rain.length) return 'sem vaga de chuva: ' + idx;
        const type = Main.rain[idx].type;
        Main.rain[idx].active = false;
        return (type >= 64 && (type & 7) === 1) || 'tipo ' + type + ' (esperado 64 + 8k + a variante 1)';
    });
    check('luz: LightColorMultiplier 1 vira 0,91 no LightMap', () => {
        const state = LocalUserGameState.Instance;
        const engine = Terraria.Lighting.UsingNewLighting ? state.NewEngine : state.LegacyEngine;
        const v = engine._workingLightMap.LightDecayThroughWater;
        const near = (x) => Math.abs(x - 0.91) < 0.01;
        return (near(v.X) && near(v.Y) && near(v.Z)) || `${v.X.toFixed(3)} ${v.Y.toFixed(3)} ${v.Z.toFixed(3)} (modo ${Terraria.Lighting.Mode})`;
    });
    check('chuva desenhada com a textura da água, na coluna da variante', () =>
        (rainDraws.mod > 0 && rainDraws.x === 4) || JSON.stringify(rainDraws));
    check('cachoeira de mod com o ColorMultiplier', () => tintCalls > 0 || 'ColorMultiplier não foi chamado; cachoeiras ' + JSON.stringify(drawnWaterfalls));
    check('tintura de bioma: a cor da água (BiomeHairColor)', () => {
        const c = rgba(biomeHair(Main.player[Main.myPlayer]));
        return c === HAIR_RED || c;
    });
    bl.log('modwater cachoeiras desenhadas por número: ' + JSON.stringify(drawnWaterfalls));
    let gameTid = '?';
    try { gameTid = System.Threading.Thread.CurrentThread.ManagedThreadId; } catch (e) {}
    bl.log('modwater luz: UpdateLightDecay ' + JSON.stringify(decayDiag) + ', thread do jogo ' + gameTid);
}

function offChecks() {
    const s = water().Slot;
    check('desligada: a água do jogo volta', () => Main.waterStyle === vanilla || `waterStyle ${Main.waterStyle}, antes ${vanilla}`);
    check('desligada: o alpha da água de mod zera', () => alphas()[s] === 0 || 'alpha ' + alphas()[s]);
    check('desligada: o respingo volta ao do jogo', () => Terraria.Dust['int dustWater()']() !== SPLASH || 'ainda o da água de mod');
    check('desligada: a tintura de bioma volta à do jogo', () => {
        const c = rgba(biomeHair(Main.player[Main.myPlayer]));
        return c !== HAIR_RED || c;
    });
}

let frame = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    frame++;
    // Para as capturas: meio-dia, o jogador vivo e sem inimigos por perto.
    if (frame === 1) { Main.dayTime = true; Main.time = 27000; }
    self.statLife = self.statLifeMax2;
    self.immune = true;
    self.immuneTime = 30;
    for (let k = 0; k < 200; k++) {
        const n = Main.npc[k];
        if (n.active && !n.friendly && !n.townNPC) n.active = false;
    }
    if (frame === 10) { registration(); buildPool(); watchRain(); }
    // Cada fase dura uns 5 s: a captura (tools, pelo log) chega 1 a 3 s depois da linha "tela".
    if (frame === 70) { vanilla = Main.waterStyle; bl.log('modwater: tela do-jogo (estilo ' + vanilla + ')'); }
    if (frame === 400) TestScene.on = true;
    if (frame === 500) dropRain();
    // O celular desenha a água em menos quadros que o jogo roda: o fade (0,2
    // por desenho) leva mais que os 5 quadros do PC.
    if (frame === 520) { onChecks(); bl.log('modwater: tela de-mod'); }
    if (frame === 820) TestScene.on = false;
    if (frame === 940) {
        offChecks();
        bl.log('modwater: tela de-volta');
        done = true;
        bl.log('modwater FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('modwater: carregado');

export default class TestModWater extends Mod {}
