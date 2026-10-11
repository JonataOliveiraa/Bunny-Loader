// Excecao C# lancada por um metodo do jogo chamado do JS. Antes derrubava o
// processo (SIGSEGV na personalidade da libbunny, que lia o contexto do
// desenrolador do libil2cpp); agora vira InternalError no JS, com a mensagem
// da excecao, e o jogo segue.
const Main = Terraria.Main;
const WorldGen = Terraria.WorldGen;
const IdDictionary = ReLogic.Reflection.IdDictionary;

const placeObject = WorldGen['bool PlaceObject(int x, int y, int type, bool mute, int style, int alternate, int random, int direction)'];
const inWorld = WorldGen['bool InWorld(int x, int y, int fluff)'];

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('gameexceptions ' + label + ': ok');
        else { fails++; bl.log('gameexceptions ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('gameexceptions ' + label + ': FALHOU com ' + e);
    }
}

// O erro de `fn`, ou null se nao lancou.
function errorOf(fn) {
    try { fn(); } catch (e) { return e; }
    return null;
}

// O original do GetName lanca (KeyNotFound) para id que nao existe. Quem chama
// o hook e o JS; a excecao nasce DENTRO do original(), que o hook chama: ela
// para ali, vai ao log, e o hook devolve null.
IdDictionary['string GetName(int id)'].hook((original, self, id) => original(self, id));

function run() {
    const me = Main.player[Main.myPlayer];
    const x = Math.floor(me.position.X / 16), y = Math.floor(me.position.Y / 16);

    check('PlaceObject com tipo -1 vira InternalError com a mensagem', () => {
        const e = errorOf(() => placeObject(x, y - 20, -1, true, 0, 0, -1, -1));
        if (e === null) return 'nao lancou';
        bl.log('gameexceptions   (erro: ' + e + ')');
        if (!(e instanceof InternalError)) return 'tipo ' + (e && e.name);
        const msg = String(e.message);
        return (msg.includes("'PlaceObject' lancou excecao no jogo") && msg.includes('Exception')) || msg;
    });
    check('o jogo segue: chamada normal logo depois', () => inWorld(x, y, 0) === true || 'InWorld falso');
    check('GetId de nome que nao existe (KeyNotFound)', () => {
        const e = errorOf(() => Terraria.ID.NPCID.Search['int GetId(string name)']('NaoExisteNenhum'));
        if (e === null) return 'nao lancou';
        bl.log('gameexceptions   (erro: ' + e + ')');
        return String(e.message).includes('KeyNotFound') || String(e.message);
    });
    check('200 excecoes seguidas', () => {
        const t0 = Date.now();
        let caught = 0;
        for (let k = 0; k < 200; k++) {
            if (errorOf(() => placeObject(x, y - 20, -1, true, 0, 0, -1, -1)) !== null) caught++;
        }
        bl.log('gameexceptions   (200 excecoes em ' + (Date.now() - t0) + ' ms)');
        return caught === 200 || 'pegou ' + caught;
    });
    check('hook: original que lanca nao derruba e devolve null', () => {
        const r = Terraria.ID.NPCID.Search.GetName(999999);
        return r === null || 'devolveu ' + r;
    });
    check('hook: o mesmo metodo sem excecao', () => {
        const r = Terraria.ID.NPCID.Search.GetName(1);
        return r === 'BlueSlime' || 'devolveu ' + r;
    });
    check('chamada normal nao ficou mais lenta (10k InWorld)', () => {
        const t0 = Date.now();
        for (let k = 0; k < 10000; k++) inWorld(x, y, 0);
        bl.log('gameexceptions   (10k InWorld em ' + (Date.now() - t0) + ' ms)');
    });
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    if (++frames < 60) return;
    done = true;
    check('preparo', run);
    bl.log('gameexceptions FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
});
bl.log('gameexceptions: carregado');

// A classe do mod, obrigatória no arquivo de entrada.
export default class TestGameExceptions extends Mod {}
