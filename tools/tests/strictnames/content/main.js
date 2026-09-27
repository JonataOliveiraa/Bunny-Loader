// Nomes exatos na ponte: ler ou escrever um membro que a classe do jogo nao
// tem e erro (com o nome parecido), `in` responde pelo jogo, as consultas do
// motor (simbolo, toJSON, then) seguem sem erro, e a assinatura so casa com o
// nome de cada parametro igual ao do jogo, letra por letra.
const Main = Terraria.Main;
const Renderer = Terraria.Graphics.Renderers.LegacyPlayerRenderer;
const normalLayers = 'void DrawPlayer_UseNormalLayers(PlayerDrawSet drawinfo)';

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('strictnames ' + label + ': ok');
        else { fails++; bl.log('strictnames ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('strictnames ' + label + ': FALHOU com ' + e);
    }
}

// A mensagem do erro de `fn`, ou null se nao lancou.
function errorOf(fn) {
    try { fn(); } catch (e) { return String(e && e.message !== undefined ? e.message : e); }
    return null;
}

function expectError(fn, ...parts) {
    const msg = errorOf(fn);
    if (msg === null) return 'nao lancou';
    const missing = parts.filter((p) => !msg.includes(p));
    return missing.length === 0 || 'mensagem: ' + msg;
}

function run() {
    const me = Main.player[Main.myPlayer];

    check('ler campo que falta: erro com o nome parecido', () =>
        expectError(() => me.whoami, "Member with name 'whoami' is not found in Terraria.Player",
                    "Did you mean 'whoAmI'?"));
    check('o nome certo continua lendo', () => me.whoAmI === Main.myPlayer || 'whoAmI ' + me.whoAmI);
    check('erro de digitacao sugere o campo', () =>
        expectError(() => me.statLifee, "'statLifee'", "Did you mean 'statLife'?"));
    check('escrever campo que falta: erro', () =>
        expectError(() => { me.statLifee = 1; }, "Member with name 'statLifee' is not found"));
    check('in responde pelo jogo', () =>
        ('whoAmI' in me && 'statLife' in me && 'Heal' in me && !('whoami' in me)) || 'in errado');

    check('estatico que falta: erro', () =>
        expectError(() => Main.dayTme, "is not found in Terraria.Main", "Did you mean 'dayTime'?"));
    check('escrever estatico que falta: erro', () =>
        expectError(() => { Main.dayTme = true; }, "'dayTme'"));
    check('in no estatico', () => ('dayTime' in Main && !('dayTme' in Main)) || 'in errado');
    check('extra do tModLoader continua (DustID.PinkFairy)', () =>
        (Terraria.ID.DustID.PinkFairy === 73 && 'PinkFairy' in Terraria.ID.DustID) || 'PinkFairy');
    check('nome com overloads pede a assinatura', () =>
        expectError(() => Terraria.Item.NewItem, 'overloads', 'use a assinatura'));

    check('struct: campo que falta', () =>
        expectError(() => me.position.Z, "Member with name 'Z' is not found in Microsoft.Xna.Framework.Vector2"));
    check('struct: in', () => ('X' in me.position && !('Z' in me.position)) || 'in errado');

    check('array: nome que falta', () =>
        expectError(() => Main.player.foo, "Member with name 'foo' is not found", 'cloneResized'));
    check('array: length e somente leitura', () =>
        expectError(() => { Main.player.length = 1; }, 'somente leitura'));
    check('array: in e toString', () =>
        (0 in Main.player && 'length' in Main.player && String(Main.player).startsWith('Player[')) ||
        String(Main.player));

    check('propriedade so de leitura', () =>
        expectError(() => { me.HeldItem = null; }, "'HeldItem'", 'somente leitura'));
    check('metodo nao se atribui', () =>
        expectError(() => { me.ResetEffects = 1; }, "'ResetEffects'", 'metodo'));

    check('consultas do motor sem erro (JSON, texto, then)', () => {
        JSON.stringify(me.position);
        const s = `${me.position}`;
        Promise.resolve(me);
        return typeof s === 'string' || 'texto';
    });
    check('campo extra vazio le undefined', () => {
        const item = me.inventory[58];
        return (item.ModItem === undefined && 'ModItem' in item && Main.npc[0].ModNPC === undefined) ||
            'ModItem ' + item.ModItem;
    });

    check('propriedade de struct encaixotado le os dados, nao a caixa (Color.PackedValue)', () => {
        const c = Color.new(10, 20, 30, 255);
        const packed = c.PackedValue;
        return packed === ((255 * 16777216) + (30 << 16) + (20 << 8) + 10) || 'PackedValue ' + packed;
    });
    check('assinatura com os nomes exatos casa', () =>
        (Renderer[normalLayers] !== undefined && normalLayers in Renderer) || 'nao achou');
    check('nome de parametro com outra caixa: erro com o certo', () =>
        expectError(() => Renderer['void DrawPlayer_UseNormalLayers(PlayerDrawSet drawInfo)'],
                    "se chama 'drawinfo', nao 'drawInfo'",
                    'No jogo: void DrawPlayer_UseNormalLayers(PlayerDrawSet drawinfo)'));
    check('in com o nome errado e false', () =>
        !('void DrawPlayer_UseNormalLayers(PlayerDrawSet drawInfo)' in Renderer) || 'true');
    check('assinatura so com os tipos: erro', () =>
        expectError(() => Renderer['void DrawPlayer_UseNormalLayers(PlayerDrawSet)'], 'sem nome', "'drawinfo'"));
    check('tipo errado: lista a assinatura certa, com nomes', () =>
        expectError(() => Renderer['void DrawPlayer_UseNormalLayers(int drawinfo)'],
                    'metodo nao encontrado', 'Existem: void DrawPlayer_UseNormalLayers(PlayerDrawSet drawinfo)'));
    check('ref no nome exato', () =>
        Terraria.Collision['void StepUp(ref Vector2 position, ref Vector2 velocity, int width, int height, ' +
            'ref float stepSpeed, ref float gfxOffY, int gravDir, bool holdsMatching, int specialChecksMode)'] !==
            undefined || 'StepUp');
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    if (++frames < 60) return;
    done = true;
    check('preparo', run);
    bl.log('strictnames FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
});
bl.log('strictnames: carregado');
