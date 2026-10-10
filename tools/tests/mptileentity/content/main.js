// ModTileEntity no multijogador (anfitrião que joga + um cliente).
// Anfitrião: faz um chão longe do spawn e coloca o barril B pelo caminho do
//   jogador antes de o cliente entrar; o Update conta e, a cada 60, manda a
//   entidade (NetMessage.SendData(86, número: ID), como no tModLoader).
// Cliente: recebe B com o pedaço do mundo; coloca A pelo caminho do jogador (o
//   servidor cria, OnNetPlace, e manda de volta); vê o Ticks de A subir; quebra
//   A e B (KillTile + mensagem 17) e confere que somem.
// O anfitrião confere o OnNetPlace e que A e B sumiram dele também; depois tira o chão.
// Loga "mptileentity <lado> <caso>: ok | FALHOU".
const Main = Terraria.Main;
const W = Terraria.WorldGen;
const { TileObjectData } = Terraria.ObjectData;
const STONE = Terraria.ID.TileID.Stone;
const SEND = 'void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)';
const getData = (type) => TileObjectData['TileObjectData GetTileData(int type, int style, int alternate)'](type, 0, 0);

let netPlaced = 0;

export class MpBarrel extends ModTileEntity {
    Ticks = 0;

    IsTileValidForEntity(x, y) {
        return bl.tiles.typeAt(x, y) === ModContent.GetInstance(MpBarrelTile).Type;
    }

    Update() {
        this.Ticks++;
        if (this.Ticks % 60 === 0 && (Main.netMode & 2)) Terraria.NetMessage[SEND](86, -1, -1, null, this.ID, 0, 0, 0, 0, 0, 0);
    }

    OnNetPlace() { netPlaced++; }
    NetSend(writer) { writer.Write(this.Ticks); }
    NetReceive(reader) { this.Ticks = reader.ReadInt32(); }
}

export class MpBarrelTile extends ModTile {
    Texture = 'Textures/MpBarrelTile';

    SetStaticDefaults() {
        Main.tileFrameImportant[this.Type] = true;
        Main.tileNoAttach[this.Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
        TileObjectData.newTile.CoordinateHeights = [16, 18];
        TileObjectData.newTile.HookPostPlaceMyPlayer = ModContent.GetInstance(MpBarrel).Generic_HookPostPlaceMyPlayer;
        TileObjectData.addTile(this.Type);
        this.AddMapEntry(Color.new(150, 100, 50), 'Mp Barrel');
    }

    KillMultiTile(i, j, frameX, frameY) {
        ModContent.GetInstance(MpBarrel).Kill(i, j);
    }
}

let fails = 0;
const side = () => (Main.netMode & 2) ? 'anfitrião' : 'cliente';
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('mptileentity ' + side() + ' ' + label + ': ok');
        else { fails++; bl.log('mptileentity ' + side() + ' ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('mptileentity ' + side() + ' ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const TILE = () => ModContent.GetInstance(MpBarrelTile).Type;
const placeTile = (x, y, type) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, type, true, false, -1, 0);
const killLocal = (x, y) => W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](x, y, false, false, true);

function area() {
    const x = Main.spawnTileX - 70, floor = Main.spawnTileY - 22;
    return { x, floor, a: x + 1, b: x + 5 };
}

function topLeft(ox, oy) {
    const data = getData(TILE());
    return { x: ox - data.Origin.X, y: oy - data.Origin.Y };
}

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

// O cliente quebra como a picareta: aqui e no servidor (mensagem 17, ação 0).
function killAsClient(x, y) {
    killLocal(x, y);
    Terraria.NetMessage[SEND](17, -1, -1, null, 0, x, y, 0, 0, 0, 0);
}

// ---- anfitrião ----
const host = { A: null, sawA: false, done: false };
function hostStart() {
    const { x, floor, b } = area();
    bl.log('mptileentity anfitrião netMode ' + Main.netMode);
    for (let i = x - 1; i < x + 12; i++) {
        for (let j = floor - 4; j <= floor + 1; j++) if (bl.tiles.typeAt(i, j) >= 0) killLocal(i, j);
        placeTile(i, floor + 1, STONE);
    }
    check('B colocado pelo anfitrião', () => {
        if (!placeAsPlayer(b, floor)) return 'não colocou';
        const tl = topLeft(b, floor);
        return ModTileEntity.TryGet(tl.x, tl.y, MpBarrel) !== null || 'sem entidade';
    });
}

function hostWatch(frames) {
    const { floor, a, b } = area();
    const ta = topLeft(a, floor), tb = topLeft(b, floor);
    const eA = ModTileEntity.TryGet(ta.x, ta.y, MpBarrel);
    if (eA && !host.sawA) {
        host.sawA = true;
        check('A nasceu no servidor a pedido do cliente (OnNetPlace)', () => netPlaced === 1 || 'OnNetPlace ' + netPlaced);
    }
    const goneA = host.sawA && !eA;
    const goneB = ModTileEntity.TryGet(tb.x, tb.y, MpBarrel) === null;
    if ((goneA && goneB) || frames > 60 * 150) {
        host.done = true;
        check('A e B sumiram no servidor depois da quebra no cliente', () => (goneA && goneB) || `viu A ${host.sawA}, A ${!!eA}, B ${!goneB}`);
        const { x } = area();
        for (let i = x - 1; i < x + 12; i++) for (let j = floor - 4; j <= floor + 1; j++) if (bl.tiles.typeAt(i, j) >= 0) killLocal(i, j);
        bl.log('mptileentity anfitrião FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
}

// ---- cliente ----
const client = { step: 0, wait: 0, A: null };
function clientStep() {
    const { floor, a, b } = area();
    const ta = topLeft(a, floor), tb = topLeft(b, floor);
    client.wait++;
    switch (client.step) {
        case 0:
            check('B chegou com o pedaço do mundo', () => {
                const e = ModTileEntity.TryGet(tb.x, tb.y, MpBarrel);
                return (e && e.ID >= 0x40000000) || 'sem entidade (' + ModTileEntity.CountInWorld() + ' no mundo)';
            });
            check('A colocado pelo cliente', () => placeAsPlayer(a, floor) || 'não colocou');
            client.step = 1; client.wait = 0;
            return;
        case 1: {
            const e = ModTileEntity.TryGet(ta.x, ta.y, MpBarrel);
            if (!e && client.wait < 300) return;
            check('A voltou do servidor', () => (e && e.ID >= 0x40000000) || 'não chegou em 300 quadros');
            client.A = e;
            client.step = 2; client.wait = 0;
            return;
        }
        case 2:
            if (client.wait < 150) return;
            check('o Update do servidor chega (SendData 86 -> NetReceive)', () => {
                const e = ModTileEntity.TryGet(tb.x, tb.y, MpBarrel);
                return (client.A && client.A.Ticks >= 60 && e && e.Ticks >= 60) || `A ${client.A && client.A.Ticks}, B ${e && e.Ticks}`;
            });
            killAsClient(ta.x, ta.y);
            killAsClient(tb.x, tb.y);
            client.step = 3; client.wait = 0;
            return;
        case 3: {
            const left = ModTileEntity.TryGet(ta.x, ta.y) || ModTileEntity.TryGet(tb.x, tb.y);
            if (left && client.wait < 300) return;
            check('quebrar no cliente tira A e B', () => !left || 'ficou ' + left.ID);
            client.step = 4;
            bl.log('mptileentity cliente FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
            return;
        }
    }
}

let frames = 0;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || Main.netMode === 0) return;
    frames++;
    if (Main.netMode & 2) {
        if (frames === 60) check('preparo', hostStart);
        else if (frames > 60 && !host.done) hostWatch(frames);
    } else if (frames >= 300 && client.step < 4) {
        clientStep();
    }
});
bl.log('mptileentity: carregado');

export default class TestMpTileEntity extends Mod {}
