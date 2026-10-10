// ModTileEntity: um barril 2x2 que conta as atualizações.
// Rodada 1: chão de pedra longe do spawn e
//   A colocado pelo caminho do jogador (CanPlace + PlaceThing_Tiles_PlaceIt,
//     que chama o HookPostPlaceMyPlayer): a entidade nasce no canto de cima à
//     esquerda, acha-se por qualquer célula e atualiza;
//   B colocado e quebrado: a entidade sai com o bloco (KillMultiTile -> Kill);
//   C uma entidade posta à mão onde não há barril.
//   Salva o mundo: o arquivo leva A e C.
// Rodada 2 (reabrindo o jogo): A voltou com os dados (LoadData), C foi
// descartada (IsTileValidForEntity). Depois tudo sai e o mundo é salvo limpo.
// Loga "modtileentity <caso>: ok | FALHOU".
const Main = Terraria.Main;
const W = Terraria.WorldGen;
const { TileObjectData } = Terraria.ObjectData;
const STONE = Terraria.ID.TileID.Stone;
const getData = (type) => TileObjectData['TileObjectData GetTileData(int type, int style, int alternate)'](type, 0, 0);
const FILE = bl.path.join(bl.mod.dataDirectory, 'barrel.json');

let kills = 0;

export class TestBarrel extends ModTileEntity {
    Ticks = 0;
    Loaded = -1;

    IsTileValidForEntity(x, y) {
        return bl.tiles.typeAt(x, y) === ModContent.GetInstance(TestBarrelTile).Type;
    }

    Update() { this.Ticks++; }
    OnKill() { kills++; }
    SaveData(tag) { tag.Ticks = this.Ticks; }
    LoadData(tag) { this.Loaded = tag.Ticks ?? -1; this.Ticks = this.Loaded; }
}

export class TestBarrelTile extends ModTile {
    Texture = 'Textures/TestBarrelTile';

    SetStaticDefaults() {
        Main.tileFrameImportant[this.Type] = true;
        Main.tileNoAttach[this.Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
        TileObjectData.newTile.CoordinateHeights = [16, 18];
        TileObjectData.newTile.HookPostPlaceMyPlayer = ModContent.GetInstance(TestBarrel).Generic_HookPostPlaceMyPlayer;
        TileObjectData.addTile(this.Type);
        this.AddMapEntry(Color.new(150, 100, 50), 'Test Barrel');
    }

    KillMultiTile(i, j, frameX, frameY) {
        ModContent.GetInstance(TestBarrel).Kill(i, j);
    }
}

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('modtileentity ' + label + ': ok');
        else { fails++; bl.log('modtileentity ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('modtileentity ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const placeTile = (x, y, type) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, type, true, false, -1, 0);
const kill = (x, y, noItem = true) => W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](x, y, false, false, noItem);
const save = () => Terraria.IO.WorldFile['void SaveWorld(WorldSaveContext saveContext)'](0);
const template = () => ModContent.GetInstance(TestBarrel);
const TILE = () => ModContent.GetInstance(TestBarrelTile).Type;

// Longe do spawn e dos outros testes (o multitile usa spawn-40..-24).
function area() {
    const x = Main.spawnTileX - 70, floor = Main.spawnTileY - 22;
    return { x, floor, a: x + 1, b: x + 5, c: x + 10 };
}

// Como o jogador coloca: o alvo do cursor na origem do objeto (canto de baixo à esquerda).
function placeAsPlayer(ox, oy) {
    const player = Main.player[Main.myPlayer];
    Terraria.Player.tileTargetX = ox;
    Terraria.Player.tileTargetY = oy;
    const data = new Ref();
    const can = Terraria.TileObject['bool CanPlace(int x, int y, int type, int style, int dir, out TileObject objectData, bool onlyCheck, Nullable<int> forcedRandom)'](
        ox, oy, TILE(), 0, 1, data, false, null);
    if (!can) return false;
    player['TileObject PlaceThing_Tiles_PlaceIt(bool newObjectType, TileObject data, int tileToCreate)'](true, data.value, TILE());
    return bl.tiles.typeAt(ox, oy) === TILE();
}

// O canto de cima à esquerda de um 2x2 com a origem em (ox, oy).
function topLeft(ox, oy) {
    const data = getData(TILE());
    return { x: ox - data.Origin.X, y: oy - data.Origin.Y };
}

function clearArea() {
    const { x, floor } = area();
    for (let i = x - 1; i < x + 16; i++) {
        for (let j = floor - 4; j <= floor + 1; j++) if (bl.tiles.typeAt(i, j) >= 0) kill(i, j);
    }
}

let A = null;
function firstRound() {
    const { x, floor, a, b, c } = area();
    check('molde e marca do HookPostPlaceMyPlayer', () => {
        const hook = getData(TILE()).HookPostPlaceMyPlayer;
        return (template() && template().Type >= 0 && hook.badReturn >= 0x5E000000 && hook.processedCoordinates === true) ||
            `tipo ${template() && template().Type}, badReturn ${hook.badReturn}, processed ${hook.processedCoordinates}`;
    });
    clearArea();
    for (let i = x - 1; i < x + 16; i++) placeTile(i, floor + 1, STONE);

    check('A: colocado pelo jogador, a entidade nasce no canto de cima à esquerda', () => {
        if (!placeAsPlayer(a, floor)) return 'não colocou';
        const tl = topLeft(a, floor);
        const e = ModTileEntity.TryGet(tl.x, tl.y, TestBarrel);
        if (!e) return 'sem entidade em ' + tl.x + ',' + tl.y + ' (' + ModTileEntity.CountInWorld() + ' no mundo)';
        A = e;
        return (e.Position.X === tl.x && e.Position.Y === tl.y && e.ID >= 0x40000000 && template().Find(tl.x, tl.y) === e.ID) ||
            `Position ${e.Position}, ID ${e.ID}, Find ${template().Find(tl.x, tl.y)}`;
    });
    check('A: TryGet por outra célula e pela classe', () => {
        const tl = topLeft(a, floor);
        const other = ModTileEntity.TryGet(tl.x + 1, tl.y + 1, TestBarrel);
        const wrongClass = ModTileEntity.TryGet(tl.x, tl.y, TestBarrelTile);
        return (other === A && wrongClass === null && ModTileEntity.ByID.get(A.ID) === A) || `outra ${other === A}, classe errada ${wrongClass}`;
    });
    check('B: quebrar uma célula leva a entidade (KillMultiTile -> Kill -> OnKill)', () => {
        if (!placeAsPlayer(b, floor)) return 'não colocou';
        const tl = topLeft(b, floor);
        const e = ModTileEntity.TryGet(tl.x, tl.y, TestBarrel);
        if (!e) return 'sem entidade';
        const before = kills;
        kill(tl.x + 1, tl.y);
        return (ModTileEntity.TryGet(tl.x, tl.y) === null && !ModTileEntity.ByID.has(e.ID) && kills === before + 1 && bl.tiles.typeAt(tl.x, tl.y) < 0) ||
            `TryGet ${ModTileEntity.TryGet(tl.x, tl.y)}, ByID ${ModTileEntity.ByID.has(e.ID)}, OnKill ${kills - before}`;
    });
    check('C: Place à mão onde não há barril', () => {
        const id = template().Place(c, floor - 1);
        return (id >= 0x40000000 && template().Find(c, floor - 1) === id) || 'id ' + id;
    });
}

function afterUpdates() {
    check('A: Update rodou (servidor ou sozinho)', () => (A && A.Ticks >= 30) || 'Ticks ' + (A && A.Ticks));
    const tl = topLeft(area().a, area().floor);
    const ticks = A ? A.Ticks : -1;
    save();
    check('arquivo do mundo leva A e C', () => {
        const all = JSON.parse(bl.file.read(Main.worldPathName + '.bl.json') || '{}');
        const list = (all['#modTileEntities'] || []).filter((e) => String(e.t).endsWith('/TestBarrel'));
        const a = list.find((e) => e.x === tl.x && e.y === tl.y);
        return (list.length === 2 && a && a.d && a.d.Ticks >= 30) || JSON.stringify(list);
    });
    bl.file.write(FILE, JSON.stringify({ x: tl.x, y: tl.y, ticks, c: { x: area().c, y: area().floor - 1 } }));
    bl.log('modtileentity rodada: salvou');
}

function secondRound(state) {
    check('A voltou com os dados salvos (LoadData)', () => {
        const e = ModTileEntity.TryGet(state.x, state.y, TestBarrel);
        return (e && e.Loaded >= 30 && e.Loaded <= state.ticks + 5) || (e ? 'Loaded ' + e.Loaded + ', salvo ' + state.ticks : 'sem entidade');
    });
    check('C foi descartada ao carregar (IsTileValidForEntity)', () => template().Find(state.c.x, state.c.y) === -1 || 'C ainda existe');
    clearArea();
    check('sem barril, sem entidade', () => ModTileEntity.TryGet(state.x, state.y) === null || 'ficou');
    save();
    check('arquivo do mundo limpo', () => {
        const all = JSON.parse(bl.file.read(Main.worldPathName + '.bl.json') || '{}');
        const list = (all['#modTileEntities'] || []).filter((e) => String(e.t).endsWith('/TestBarrel'));
        return list.length === 0 || JSON.stringify(list);
    });
    bl.file.delete(FILE);
    bl.log('modtileentity rodada: conferiu e limpou');
}

let frames = 0, done = false, state = null;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    frames++;
    if (frames === 1) {
        const text = bl.file.read(FILE);
        state = text ? JSON.parse(text) : null;
        check('preparo', () => (state ? secondRound(state) : firstRound()));
        if (state) done = true;
    }
    if (!state && frames === 90) {
        done = true;
        check('fim da rodada 1', afterUpdates);
    }
    if (done) bl.log('modtileentity FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
});
bl.log('modtileentity: carregado');

export default class TestModTileEntity extends Mod {}
