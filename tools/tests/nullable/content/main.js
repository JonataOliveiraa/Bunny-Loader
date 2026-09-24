// Teste de Nullable<T> na ponte: parametro, retorno, campo e assinatura.
//
// Roda uma vez, dentro do mundo, e loga "nullable <caso>: ok | FALHOU". Tambem
// desenha dois retangulos no canto esquerdo (vermelho com sourceRectangle
// null, verde com um Rectangle), para conferir o Rectangle? a olho.
const Main = Terraria.Main;
const { Rectangle, Vector2 } = Microsoft.Xna.Framework;
const { Color, SpriteBatch, SpriteFont } = Microsoft.Xna.Framework.Graphics;

let fails = 0;
/** fn devolve true/undefined = ok; false ou outro valor = falhou (mostra o valor). */
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('nullable ' + label + ': ok');
        else { fails++; bl.log('nullable ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('nullable ' + label + ': FALHOU com ' + e);
    }
}
const near = (a, b) => Math.abs(a - b) < 1e-4;

function rect(x, y, w, h) {
    const r = Rectangle.new();
    r['void .ctor(int x, int y, int width, int height)'](x, y, w, h);
    return r;
}

// --- float? : Main.StartRain, nas tres grafias ---
function testRain() {
    const sigs = {
        tl: 'void StartRain(bool instant, Nullable`1 strengthOverride, bool garenteeCoinRain)',
        cs: 'void StartRain(bool instant, float? strengthOverride, bool garenteeCoinRain)',
        gen: 'void StartRain(bool instant, Nullable<float> strengthOverride, bool garenteeCoinRain)',
    };
    const f = {};
    for (const k in sigs) check('assinatura ' + sigs[k].split('(')[1].split(' ')[2], () => { f[k] = Main[sigs[k]]; });
    const stop = Main['void StopRain(bool instant)'];

    check('StartRain(0.7) via Nullable`1', () => {
        stop(true);
        f.tl(true, 0.7, false);
        return (near(Main.maxRaining, 0.7) && near(Main.cloudAlpha, 0.7)) || 'max=' + Main.maxRaining + ' nuvem=' + Main.cloudAlpha;
    });
    check('StartRain(null) via float?: o jogo sorteia (0,05..0,9)', () => {
        stop(true);
        f.cs(true, null, false);
        return (Main.raining && Main.maxRaining >= 0.05 && Main.maxRaining <= 0.9 && !near(Main.maxRaining, 0.7)) || 'max=' + Main.maxRaining;
    });
    check('StartRain(0.35) via Nullable<float>', () => {
        stop(true);
        f.gen(true, 0.35, false);
        return near(Main.maxRaining, 0.35) || 'max=' + Main.maxRaining;
    });
    check('float? recusa texto', () => {
        try { f.tl(true, 'muito', false); } catch (e) { bl.log('nullable   (erro esperado: ' + e + ')'); return true; }
        return 'aceitou texto';
    });
    check('StopRain', () => {
        stop(true);
        return !Main.raining || 'ainda chove';
    });
}

// --- char? : construtor do SpriteFont, propriedade e campo ---
function testFont(tex) {
    let g, textures, glyphs;
    check('classe aninhada SpriteFont.Glyph', () => {
        g = SpriteFont.Glyph.new();
        g.Character = 65;                   // 'A'
        g.BoundsInTexture = rect(0, 0, 1, 1);
        g.Cropping = rect(0, 0, 1, 1);
        g.Width = 1;
        return g.Character === 65 || g.Character;
    });

    check('arrays por Array.CreateInstance', () => {
        const CreateInstance = System.Array['Array CreateInstance(Type elementType, int length)'];
        textures = CreateInstance(tex['Type GetType()'](), 1);
        glyphs = CreateInstance(g['Type GetType()'](), 1);
        textures[0] = tex;
        glyphs[0] = g;
        return (textures.length === 1 && glyphs[0].Character === 65) || 'length=' + textures.length;
    });

    const sig = 'void .ctor(Texture2D[] textures, Glyph[] glyphs, int lineSpacing, float spacing, Nullable`1 defaultCharacter)';
    const a = SpriteFont.new();
    check('ctor com char? = null', () => {
        a[sig](textures, glyphs, 20, 1.5, null);
        return (a.DefaultCharacter === null && a.LineSpacing === 20 && near(a.Spacing, 1.5)) ||
            'default=' + a.DefaultCharacter + ' linha=' + a.LineSpacing;
    });

    const b = SpriteFont.new();
    check('ctor com char? = 65 (assinatura char?)', () => {
        b['void .ctor(Texture2D[] textures, SpriteFont.Glyph[] glyphs, int lineSpacing, float spacing, char? defaultCharacter)'](textures, glyphs, 20, 1.5, 65);
        return b.DefaultCharacter === 65 || 'default=' + b.DefaultCharacter;
    });
    check('campo Nullable<char> lido direto', () => b._defaultCharacter === 65 || b._defaultCharacter);
    check('propriedade recebe null', () => {
        b.DefaultCharacter = null;
        return b.DefaultCharacter === null || b.DefaultCharacter;
    });
    check('campo escrito com 65', () => {
        b._defaultCharacter = 65;
        return b.DefaultCharacter === 65 || b.DefaultCharacter;
    });
}

// --- Rectangle? (20 bytes, vai por endereco): SpriteBatch.Draw ---
const drawSig = 'void Draw(Texture2D texture, Rectangle destinationRectangle, Nullable`1 sourceRectangle, Color color, float rotation, Vector2 origin, SpriteEffects effects, float layerDepth)';
let Draw = null, drawn = 0, drawBroken = false;
Main['void DrawInterface(GameTime gameTime)'].hook((o, self, gt) => {
    o(self, gt);
    if (Main.gameMenu || drawBroken) return;
    try {
        if (!Draw) Draw = SpriteBatch[drawSig];
        const sb = Main.spriteBatch;
        const tex = Terraria.GameContent.TextureAssets.MagicPixel.Value;
        const red = Color.new(); red['void .ctor(int r, int g, int b)'](230, 40, 40);
        const green = Color.new(); green['void .ctor(int r, int g, int b)'](40, 200, 60);
        const origin = Vector2.new(); origin['void .ctor(float x, float y)'](0, 0);
        sb['void Begin(SpriteSortMode sortMode, bool defferedBatch)'](0, true);
        Draw(sb, tex, rect(40, 330, 220, 70), null, red, 0, origin, 0, 0);            // textura inteira
        Draw(sb, tex, rect(40, 410, 220, 70), rect(0, 0, 1, 1), green, 0, origin, 0, 0); // so o pedaco
        sb['void End()']();
        if (drawn++ === 0) bl.log('nullable Draw com Rectangle? (null e Rectangle): chamado sem erro');
    } catch (e) {
        drawBroken = true;
        fails++;
        bl.log('nullable Draw: FALHOU com ' + e);
    }
});

// --- hooks: Nullable chegando no callback, trocado no original(), devolvido ---
function testHooks() {
    const sig = 'void StartRain(bool instant, Nullable`1 strengthOverride, bool garenteeCoinRain)';
    const StartRain = Main[sig];
    const stop = Main['void StopRain(bool instant)'];
    let seen = 'nada';
    check('hook em StartRain instalado', () => {
        // Recebe o float? como numero ou null; troca null por 0,25.
        Main[sig].hook((o, instant, strength, coin) => {
            seen = strength;
            o(instant, strength === null ? 0.25 : strength, coin);
        });
    });
    check('hook recebe 0.6 e repassa', () => {
        stop(true);
        StartRain(true, 0.6, false);
        return (near(seen, 0.6) && near(Main.maxRaining, 0.6)) || 'visto=' + seen + ' max=' + Main.maxRaining;
    });
    check('hook recebe null e troca por 0.25 no original()', () => {
        stop(true);
        StartRain(true, null, false);
        return (seen === null && near(Main.maxRaining, 0.25)) || 'visto=' + seen + ' max=' + Main.maxRaining;
    });
    stop(true);

    // Retorno Nullable<char>: o callback decide o que get_DefaultCharacter devolve.
    let mode = 'repassa';
    check('hook em get_DefaultCharacter instalado', () => {
        SpriteFont['Nullable`1 get_DefaultCharacter()'].hook((o, self) => {
            if (mode === 'null') return null;
            if (mode === '66') return 66;
            return o(self);
        });
    });
    const font = SpriteFont.new();   // sem ctor: _defaultCharacter zerado = null
    check('retorno repassado (null do original)', () => font.DefaultCharacter === null || font.DefaultCharacter);
    check('retorno trocado por 66', () => { mode = '66'; return font.DefaultCharacter === 66 || font.DefaultCharacter; });
    check('retorno trocado por null', () => {
        font._defaultCharacter = 70;
        mode = 'null';
        const r = font.DefaultCharacter;
        mode = 'repassa';
        return (r === null && font.DefaultCharacter === 70) || 'r=' + r + ' depois=' + font.DefaultCharacter;
    });
}

let done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (done || i !== Main.myPlayer || Main.gameMenu) return;
    done = true;
    testRain();
    testFont(Terraria.GameContent.TextureAssets.MagicPixel.Value);
    testHooks();
    bl.log('nullable FIM dos testes logicos: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
});
bl.log('nullable: carregado');
