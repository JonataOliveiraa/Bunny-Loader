// Dois relatos de usuario sobre a ponte (bl.log com 'optarg ...'):
//   1. Metodo solto de um objeto lido de campo estatico chama no dono:
//      `const DrawString = Main.spriteBatch['...']; DrawString(...)`, no
//      desenho, como no relato.
//   2. `null` num struct com valor padrao no C# (`Color newColor = default`)
//      e o default(T), zerado, pela chamada direta e pelo original() de um
//      hook; o Nullable<T> segue aceitando null; struct obrigatorio e numero
//      continuam recusando null.
const Main = Terraria.Main;
const Dust = Terraria.Dust;
const { Vector2 } = Microsoft.Xna.Framework;
const { Color } = Microsoft.Xna.Framework.Graphics;

const NEW_DUST = 'int NewDust(Vector2 Position, int Width, int Height, int Type, float SpeedX, float SpeedY, int Alpha, Color newColor, float Scale)';
const NEW_DUST_PERFECT = 'Dust NewDustPerfect(Vector2 Position, int Type, Nullable`1 Velocity, int Alpha, Color newColor, float Scale)';
const DRAW_STRING = 'void DrawString(SpriteFont spriteFont, string text, Vector2 position, Color color)';

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('optarg ' + label + ': ok');
        else { fails++; bl.log('optarg ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('optarg ' + label + ': FALHOU com ' + e);
    }
}

function color(r, g, b) {
    const c = Color.new();
    c['void .ctor(int r, int g, int b)'](r, g, b);
    return c;
}
function vec(x, y) {
    const v = Vector2.new();
    v['void .ctor(float x, float y)'](x, y);
    return v;
}
const rgba = (c) => `${c.R},${c.G},${c.B},${c.A}`;
// Recusou com TypeError (e nao chamou o jogo).
function throws(fn, want) {
    try {
        fn();
        return 'aceitou';
    } catch (e) {
        return String(e).includes(want) || String(e);
    }
}

// --- 1. metodo solto do Main.spriteBatch, no desenho ---
let drawState = 'esperando';   // esperando -> ok | erro
Main['void DrawInterface(GameTime gameTime)'].hook((o, self, gt) => {
    o(self, gt);
    if (Main.gameMenu || drawState !== 'esperando') return;
    const sb = Main.spriteBatch;
    const DrawString = Main.spriteBatch[DRAW_STRING];
    const font = Terraria.GameContent.FontAssets.MouseText.Value;
    sb['void Begin(SpriteSortMode sortMode, bool defferedBatch)'](0, true);
    try {
        DrawString(font, 'optarg: metodo solto', vec(40, 300), color(255, 255, 255));
        drawState = 'ok';
    } catch (e) {
        drawState = String(e);
    } finally {
        sb['void End()']();
    }
});

// --- 2. null em parametro com valor padrao ---
let viaHook = false;   // o hook troca a cor por null no original()
function hookNewDust() {
    Dust[NEW_DUST].hook((original, pos, w, h, type, sx, sy, alpha, newColor, scale) => {
        if (!viaHook) return original(pos, w, h, type, sx, sy, alpha, newColor, scale);
        return original(pos, w, h, type, sx, sy, alpha, null, scale);
    });
}

function run() {
    const p = Main.LocalPlayer;
    const at = () => vec(p.position.X, p.position.Y - 40);
    const NewDust = Dust[NEW_DUST];

    check('controle: cor dada chega', () => {
        const i = NewDust(at(), 8, 8, 6, 0, 0, 0, color(200, 10, 10), 1);
        const c = Main.dust[i].color;
        return (c.R === 200 && c.G === 10 && c.B === 10) || rgba(c);
    });
    check('Color opcional com null -> default (zerado)', () => {
        const i = NewDust(at(), 8, 8, 6, 0, 0, 0, null, 1);
        return rgba(Main.dust[i].color) === '0,0,0,0' || rgba(Main.dust[i].color);
    });
    check('Color opcional com undefined -> default', () => {
        const i = NewDust(at(), 8, 8, 6, 0, 0, 0, undefined, 1);
        return rgba(Main.dust[i].color) === '0,0,0,0' || rgba(Main.dust[i].color);
    });
    check('Nullable<Vector2> e Color opcional com null', () => {
        const dust = Dust[NEW_DUST_PERFECT](at(), 6, null, 0, null, 1);
        return (dust && rgba(dust.color) === '0,0,0,0') || (dust ? rgba(dust.color) : 'null');
    });
    check('Vector2 obrigatorio com null: recusa', () =>
        throws(() => NewDust(null, 8, 8, 6, 0, 0, 0, null, 1), 'nao tem valor padrao'));
    check('int opcional com null: recusa', () =>
        throws(() => NewDust(at(), 8, 8, 6, 0, 0, null, null, 1), 'numero'));

    hookNewDust();
    check('null no original() do hook -> default', () => {
        viaHook = true;
        try {
            const i = NewDust(at(), 8, 8, 6, 0, 0, 0, color(10, 200, 10), 1);
            return rgba(Main.dust[i].color) === '0,0,0,0' || rgba(Main.dust[i].color);
        } finally {
            viaHook = false;
        }
    });
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    ++frames;
    if (frames === 90) check('preparo', run);
    if (frames === 120) {
        done = true;
        check('DrawString solto do Main.spriteBatch', () => drawState === 'ok' || drawState);
        bl.log('optarg FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('optarg: carregado');

// A classe do mod, obrigatória no arquivo de entrada.
export default class TestOptarg extends Mod {}
