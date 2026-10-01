// O gancho do Example Mod desenha a própria corrente no PreDrawExtras e
// devolve false: a corrente do jogo não pode sair por baixo. O teste põe o
// gancho no espaço de gancho e lança de tempos em tempos (para o print).
// A partir do quadro 400: PreDrawExtras true sem desenhar nada e PreDraw
// false. Como no tModLoader, fica a corrente do jogo, a cabeça do gancho some
// e o PostDraw roda.
const Main = Terraria.Main;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('hookchain ' + label + ': ok');
        else { fails++; bl.log('hookchain ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('hookchain ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

let type = 0, calls = 0, posts = 0, noSprite = false;

function setup() {
    type = ModContent.ProjectileType('examplemod/ExampleHookProjectile');
    const m = ModContent.GetModProjectile(type);
    const proto = Object.getPrototypeOf(m);
    const own = proto.PreDrawExtras;
    proto.PreDrawExtras = function (proj) { ++calls; return noSprite ? true : own.call(this, proj); };
    proto.PreDraw = function () { return !noSprite; };
    proto.PostDraw = function () { if (noSprite) ++posts; };

    const p = Main.LocalPlayer;
    p.miscEquips[4]['void SetDefaults(int Type, ItemVariant variant)'](ModContent.ItemType('examplemod/ExampleHookItem'), null);
    return type > 0 || 'tipo ' + type;
}

function fire() {
    const p = Main.LocalPlayer;
    for (let i = 0; i < 1000; i++) {
        const proj = Main.projectile[i];
        if (proj.active && proj.type === type && proj.owner === p.whoAmI) proj.Kill();
    }
    p.QuickGrapple();
    // Para cima, no céu (sem bloco para agarrar): a corrente longa na tela.
    let n = 0;
    for (let i = 0; i < 1000; i++) {
        const proj = Main.projectile[i];
        if (!proj.active || proj.type !== type || proj.owner !== p.whoAmI) continue;
        proj.velocity = Vector2.new(4 + 4 * n++, -12 + 2 * (shots % 3));
    }
    ++shots;
}
let shots = 0;

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    ++frames;
    if (frames === 60) check('setup', setup);
    if (frames > 60 && frames % (noSprite ? 40 : 90) === 0) fire();
    if (frames === 400) {
        check('PreDrawExtras chamado', () => calls > 0 || 'chamadas ' + calls);
        noSprite = true;
    }
    if (frames === 700) {
        check('PostDraw com o PreDraw false', () => posts > 0 || 'chamadas ' + posts);
        bl.log('hookchain FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
    if (frames === 2400) done = true;
});
bl.log('hookchain: carregado');

export default class TestHookchain extends Mod {}
