// Objeto de mod que volta do .tiles.bl com algo no lugar de uma célula (o
// que acontece com o mod desligado: o lugar fica vazio no mundo e o mato
// cresce ali). Duas rodadas, num chão no alto:
// Rodada 1: põe as caixas A e B (3x2), salva o mundo e guarda o .tiles.bl;
//   tira as duas, põe teia (o jogo a corta como o mato: Main.tileCut) no lugar
//   do pé direito de A e pedra no de
//   B, salva de novo (o .wld fica com o mato e a pedra) e devolve o .tiles.bl
//   guardado (com A e B).
// Rodada 2 (reabrindo o jogo): A voltou inteira (a teia deu lugar); B não
//   voltou nem pela metade (a pedra ficou; B segue guardada no arquivo). Depois
//   tira tudo e apaga B do arquivo.
// Loga "objrestore <caso>: ok | FALHOU".
const Main = Terraria.Main;
const W = Terraria.WorldGen;
const { TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const STATE = bl.path.join(bl.mod.dataDirectory, 'state.json');
const BACKUP = bl.path.join(bl.mod.dataDirectory, 'tiles.bl');

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('objrestore ' + label + ': ok');
        else { fails++; bl.log('objrestore ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('objrestore ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

export class TestCrate extends ModTile {
    Texture = 'TestCrate';
    SetStaticDefaults() {
        Main.tileFrameImportant[this.Type] = true;
        Main.tileNoAttach[this.Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x2);
        TileObjectData.addTile(this.Type);
        this.AddMapEntry(Color.new(150, 150, 150), 'Test Crate');
    }
}

const CRATE = () => ModContent.TileType(TestCrate);
const save = () => Terraria.IO.WorldFile['void SaveWorld(WorldSaveContext saveContext)'](0);
const sideFile = () => String(Main.ActiveWorldFileData.Path) + '.tiles.bl';
const placeTile = (x, y, type) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, type, true, true, -1, 0);
const killTile = (x, y) => W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](x, y, false, false, true);
const placeObject = (x, y, type) => W['bool PlaceObject(int x, int y, int type, bool mute, int style, int alternate, int random, int direction)'](x, y, type, true, 0, 0, -1, -1);

// A esquerda de A e de B; o chão é a linha de baixo + 1.
function layout(state) {
    const x = state ? state.x : Main.spawnTileX - 60, floor = state ? state.floor : Main.spawnTileY - 30;
    return { x, floor, a: x + 1, b: x + 6 };
}

function crateCells(left, floor) {
    let n = 0;
    for (let i = left; i < left + 3; i++) for (let j = floor - 1; j <= floor; j++) if (bl.tiles.typeAt(i, j) === CRATE()) n++;
    return n;
}

function clearArea(x, floor) {
    for (let i = x - 1; i <= x + 11; i++) for (let j = floor - 4; j <= floor + 1; j++) if (bl.tiles.typeAt(i, j) >= 0) killTile(i, j);
}

function removeDrops(x, floor) {
    for (let k = 0; k < 400; k++) {
        const w = Main.item[k];
        if (w.active && Math.abs(w.position.X / 16 - (x + 5)) < 12 && Math.abs(w.position.Y / 16 - floor) < 10) w['void TurnToAir()']();
    }
}

function round1() {
    const { x, floor, a, b } = layout(null);
    clearArea(x, floor);
    for (let i = x - 1; i <= x + 11; i++) placeTile(i, floor + 1, TileID.Stone);
    placeObject(a + 1, floor, CRATE());
    placeObject(b + 1, floor, CRATE());
    if (crateCells(a, floor) !== 6 || crateCells(b, floor) !== 6) {
        const can = Terraria.TileObject['bool CanPlace(int x, int y, int type, int style, int dir, out TileObject objectData, bool onlyCheck, Nullable<int> forcedRandom)'](
            a + 1, floor, CRATE(), 0, -1, new Ref(), true, null);
        clearArea(x, floor);
        throw new Error(`as caixas não foram colocadas (A ${crateCells(a, floor)}, B ${crateCells(b, floor)}, CanPlace ${can}, chão ${bl.tiles.typeAt(a + 1, floor + 1)})`);
    }
    check('rodada 1: A e B colocadas', () => true);
    save();
    const text = bl.file.read(sideFile());
    check('rodada 1: A e B no .tiles.bl', () => {
        const key = bl.mod.uuid + '/TestCrate';
        const lines = (text || '').split('\n').filter((l) => l.endsWith('\t' + key)).length;
        return lines === 12 || lines + ' linha(s)';
    });
    bl.file.write(BACKUP, text);

    killTile(a, floor);
    killTile(b, floor);
    removeDrops(x, floor);
    placeTile(a + 2, floor, TileID.Cobweb);
    placeTile(b + 2, floor, TileID.Stone);
    check('rodada 1: teia no pé de A, pedra no de B, sem as caixas', () =>
        (bl.tiles.typeAt(a + 2, floor) === TileID.Cobweb && bl.tiles.typeAt(b + 2, floor) === TileID.Stone && crateCells(a, floor) === 0 && crateCells(b, floor) === 0)
        || `pé de A ${bl.tiles.typeAt(a + 2, floor)}, pé de B ${bl.tiles.typeAt(b + 2, floor)}, A ${crateCells(a, floor)}, B ${crateCells(b, floor)}`);
    save();
    bl.file.write(sideFile(), text);
    bl.file.write(STATE, JSON.stringify({ x, floor }));
    bl.log('objrestore rodada 1: pronto (reabra o jogo)');
}

function round2(state) {
    const { x, floor, a, b } = layout(state);
    check('rodada 2: A voltou inteira, a teia deu lugar', () => crateCells(a, floor) === 6 || `${crateCells(a, floor)}/6, pé de A ${bl.tiles.typeAt(a + 2, floor)}`);
    check('rodada 2: B não voltou pela metade', () => crateCells(b, floor) === 0 || `${crateCells(b, floor)}/6`);
    check('rodada 2: a pedra no pé de B ficou', () => bl.tiles.typeAt(b + 2, floor) === TileID.Stone || 'pé de B ' + bl.tiles.typeAt(b + 2, floor));

    // Limpa: o mundo e, no .tiles.bl, a B que ficou guardada.
    clearArea(x, floor);
    removeDrops(x, floor);
    save();
    const key = '\t' + bl.mod.uuid + '/TestCrate';
    const text = bl.file.read(sideFile());
    check('rodada 2: B segue guardada no arquivo (sem perder nada)', () => {
        const lines = (text || '').split('\n').filter((l) => l.endsWith(key)).length;
        return lines === 6 || lines + ' linha(s)';
    });
    if (text) {
        const rest = text.split('\n').filter((l) => !l.endsWith(key));
        if (rest.filter((l) => l.trim()).length > 1) bl.file.write(sideFile(), rest.join('\n'));
        else bl.file.delete(sideFile());
    }
    bl.file.delete(STATE);
    bl.file.delete(BACKUP);
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    self.statLife = self.statLifeMax2;
    if (++frames !== 30) return;
    done = true;
    const raw = bl.file.read(STATE);
    if (raw) check('rodada 2', () => round2(JSON.parse(raw)));
    else check('rodada 1', round1);
    bl.log('objrestore FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
});
bl.log('objrestore: carregado');

export default class TestObjRestore extends Mod {}
