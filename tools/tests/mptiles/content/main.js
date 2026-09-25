// Tiles de mod no multijogador. O host poe uma fileira (5 ExampleTile, 1
// ExampleOre) acima do spawn antes de o cliente entrar: ela chega ao cliente
// com o mundo. Depois o cliente poe um bloco acima da fileira e o quebra; o
// host tem de ver os dois. Loga "mpt <papel> <caso>: ok | FALHOU".
const Main = Terraria.Main;
const W = Terraria.WorldGen;
const TILE = 753, ORE = 754;

let role = '', fails = 0, done = false;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('mpt ' + role + ' ' + label + ': ok');
        else { fails++; bl.log('mpt ' + role + ' ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('mpt ' + role + ' ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}
function finish() {
    done = true;
    bl.log('mpt ' + role + ' FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
}

const place = (x, y, type) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, type, true, false, -1, 0);
const kill = (x, y) => W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](x, y, false, false, true);
const sendSquare = (x, y, w, h) => Terraria.NetMessage['void SendTileSquare(int whoAmi, int tileX, int tileY, int xSize, int ySize, TileChangeType changeType)'](-1, x, y, w, h, 0);
const expected = (k) => (k < 5 ? TILE : ORE);
const spot = () => ({ x: Main.spawnTileX - 3, y: Main.spawnTileY - 10 });
const row = () => { const s = spot(); return Array.from({ length: 6 }, (_, k) => bl.tiles.typeAt(s.x + k, s.y)); };
const clientBlock = () => { const s = spot(); return { x: s.x + 2, y: s.y - 2 }; };

function remoteIndex() {
    for (let i = 0; i < 255; i++) if (i !== Main.myPlayer && Main.player[i].active) return i;
    return -1;
}

// -------------------------------- host --------------------------------
let hostStage = 0, hostSince = 0;
function hostTick(frames) {
    if (hostStage === 0) {
        const s = spot();
        for (let k = 0; k < 6; k++) { kill(s.x + k, s.y); place(s.x + k, s.y, expected(k)); }
        const b = clientBlock();
        kill(b.x, b.y);
        check('fileira posta', () => row().every((t, k) => t === expected(k)) || row().join(' '));
        hostStage = 1;
        return;
    }
    if (remoteIndex() < 0) return;
    const b = clientBlock();
    const t = bl.tiles.typeAt(b.x, b.y);
    if (hostStage === 1) {
        if (!hostSince) { hostSince = frames; bl.log('mpt host: cliente entrou'); }
        if (t === TILE) {
            check('o bloco que o cliente pos chegou', () => true);
            hostStage = 2;
            hostSince = frames;
        } else if (frames - hostSince > 2400) {
            check('o bloco que o cliente pos chegou', () => 'nao chegou (tipo ' + t + ')');
            finish();
        }
    } else if (hostStage === 2) {
        if (t === -1) {
            check('o cliente quebrou o bloco e o host viu', () => true);
            finish();
        } else if (frames - hostSince > 1200) {
            check('o cliente quebrou o bloco e o host viu', () => 'ainda tipo ' + t);
            finish();
        }
    }
}

// ------------------------------- cliente -------------------------------
function clientTick(frames) {
    const b = clientBlock();
    if (frames === 200) {
        check('a fileira do host chegou com o mundo', () => row().every((t, k) => t === expected(k)) || row().join(' '));
        place(b.x, b.y, TILE);
        sendSquare(b.x, b.y, 1, 1);
        check('pos o bloco', () => bl.tiles.typeAt(b.x, b.y) === TILE || 'tipo ' + bl.tiles.typeAt(b.x, b.y));
    }
    if (frames === 400) {
        kill(b.x, b.y);
        sendSquare(b.x, b.y, 1, 1);
        check('quebrou o bloco', () => bl.tiles.typeAt(b.x, b.y) === -1 || 'tipo ' + bl.tiles.typeAt(b.x, b.y));
        finish();
    }
}

let frames = 0;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    ++frames;
    if (frames === 1) {
        const mode = Main.netMode;
        role = (mode & 2) ? 'host' : mode === 1 ? 'cliente' : '';
        bl.log(`mpt: netMode ${mode}, papel ${role || 'nenhum'}`);
        if (!role) { done = true; return; }
    }
    if (!self.dead) { self.statLife = self.statLifeMax2; self.immune = true; self.immuneTime = 10; }
    if (role === 'host') hostTick(frames);
    else clientTick(frames);
});
bl.log('mpt: carregado');
