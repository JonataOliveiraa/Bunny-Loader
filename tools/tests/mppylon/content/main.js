// ModPylon no multijogador (anfitrião que joga + um cliente).
// Anfitrião: faz três pisos no alto, longe do spawn, e põe os pilares A e B
//   pelo caminho do jogador antes de o cliente entrar. Confere que o C do
//   cliente vira TEModdedPylon e entra na lista do servidor, que o cliente foi
//   teletransportado e que o C quebrado sai; no fim quebra A e B.
// Cliente: A e B chegam ao entrar (OnPlayerJoining); parado ao lado de A, pede
//   o teletransporte para B ao servidor (RequestTeleportation); põe o C pelo
//   caminho do jogador e o vê voltar pela lista (PylonWasAdded); o mapa desenha
//   o ícone do mod; quebra o C e o vê sair; A e B saem quando o anfitrião quebra.
// Loga "mppylon <lado> <caso>: ok | FALHOU".
const Main = Terraria.Main;
const W = Terraria.WorldGen;
const { TileObjectData } = Terraria.ObjectData;
const STONE = Terraria.ID.TileID.Stone;
const SEND = 'void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)';
const send = (...args) => Terraria.NetMessage[SEND](...args);

let mapIcons = 0;

export class MpPylonEntity extends TEModdedPylon {}

export class MpPylon extends ModPylon {
    Texture = 'Textures/TestPylon';

    SetStaticDefaults() {
        Main.tileFrameImportant[this.Type] = true;
        Main.tileLighted[this.Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x4);
        TileObjectData.newTile.LavaDeath = false;
        TileObjectData.newTile.DrawYOffset = 2;
        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.newTile.HookPostPlaceMyPlayer = ModContent.GetInstance(MpPylonEntity).PlacementHook(false);
        TileObjectData.addTile(this.Type);
        this.AddMapEntry(Color.White, 'Mp Pylon');
    }

    ValidTeleportCheck_NPCCount(pylonInfo, defaultNecessaryNPCCount) { return true; }
    KillMultiTile(i, j, frameX, frameY) { ModContent.GetInstance(MpPylonEntity).Kill(i, j); }
    DrawMapIcon(context, mouseOverText, pylonInfo, isNearPylon, drawColor, deselectedScale, selectedScale) {
        mapIcons++;
        super.DrawMapIcon(context, mouseOverText, pylonInfo, isNearPylon, drawColor, deselectedScale, selectedScale);
    }
}

export class MpPylonB extends MpPylon {}
export class MpPylonC extends MpPylon {}

let fails = 0;
const side = () => (Main.netMode & 2) ? 'anfitrião' : 'cliente';
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('mppylon ' + side() + ' ' + label + ': ok');
        else { fails++; bl.log('mppylon ' + side() + ' ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('mppylon ' + side() + ' ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}
const finish = () => bl.log('mppylon ' + side() + ' FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));

const A = () => ModContent.GetInstance(MpPylon);
const B = () => ModContent.GetInstance(MpPylonB);
const C = () => ModContent.GetInstance(MpPylonC);
const placeTile = (x, y, type) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, type, true, true, -1, 0);
const killLocal = (x, y) => W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](x, y, false, false, true);
const me = () => Main.player[Main.myPlayer];

function area() {
    const floor = Main.spawnTileY - 40;
    return { floor, ax: Main.spawnTileX - 100, bx: Main.spawnTileX - 130, cx: Main.spawnTileX - 160 };
}

// Como o jogador coloca: o alvo na origem (o meio de baixo do 3x4).
function placeAsPlayer(x, y, type) {
    Terraria.Player.tileTargetX = x;
    Terraria.Player.tileTargetY = y;
    const data = new Ref();
    const can = Terraria.TileObject['bool CanPlace(int x, int y, int type, int style, int dir, out TileObject objectData, bool onlyCheck, Nullable<int> forcedRandom)'](
        x, y, type, 0, 1, data, false, null);
    if (!can) return false;
    me()['TileObject PlaceThing_Tiles_PlaceIt(bool newObjectType, TileObject data, int tileToCreate)'](true, data.value, type);
    return bl.tiles.typeAt(x, y) === type;
}

// O pilar de um tipo na lista do jogo (Main.PylonSystem.Pylons), ou null.
function listed(pylonType) {
    const list = Main.PylonSystem.Pylons;
    for (let k = 0; k < list.Count; k++) {
        const info = list.get_Item(k);
        if (info.TypeOfPylon === pylonType) return { x: info.PositionInTiles.X, y: info.PositionInTiles.Y, info };
    }
    return null;
}

// Quebra um pedaço do pilar e manda aos outros (mensagem 17, ação 0).
function breakAndSend(x, y) {
    killLocal(x, y);
    send(17, -1, -1, null, 0, x, y, 0, 0, 0, 0);
}

function stand(x) {
    const p = me(), { floor } = area();
    p.position = Vector2.new(x * 16, (floor - 3) * 16);
    p.velocity = Vector2.new(0, 0);
    p.fallStart = floor - 3;
}

// ---- anfitrião ----
const host = { start: 0, sawC: false, teleported: false, cleaned: false, done: false };
function hostStart() {
    const { floor, ax, bx, cx } = area();
    bl.log('mppylon anfitrião netMode ' + Main.netMode + ', tipos ' + A().PylonType + '/' + B().PylonType + '/' + C().PylonType);
    for (const x0 of [ax, bx, cx]) {
        for (let i = x0 - 6; i <= x0 + 6; i++) for (let j = floor - 8; j <= floor; j++) if (bl.tiles.typeAt(i, j) >= 0) killLocal(i, j);
        for (let i = x0 - 5; i <= x0 + 5; i++) placeTile(i, floor, STONE);
    }
    check('A e B colocados pelo anfitrião', () =>
        (placeAsPlayer(ax, floor - 1, A().Type) && placeAsPlayer(bx, floor - 1, B().Type)) || 'não colocou');
}

function remoteClient() {
    for (let k = 0; k < 255; k++) if (k !== Main.myPlayer && Main.player[k].active) return Main.player[k];
    return null;
}

function hostWatch(frames) {
    const { floor, ax, bx, cx } = area();
    const other = remoteClient();
    const b = listed(B().PylonType);
    if (other && b && !host.teleported && Math.hypot(other.position.X / 16 - (b.x + 1), other.position.Y / 16 - (b.y + 1)) < 6) {
        host.teleported = true;
        check('o servidor teletransportou o cliente até B', () => true);
    }
    const c = listed(C().PylonType);
    if (c && !host.sawC) {
        host.sawC = true;
        check('o C do cliente virou TEModdedPylon no servidor', () =>
            ModTileEntity.TryGet(c.x, c.y, MpPylonEntity) !== null || 'sem entidade em ' + c.x + ',' + c.y);
    }
    const timeout = frames > 60 * 240;
    if (!host.cleaned && ((host.sawC && !c) || timeout)) {
        host.cleaned = true;
        host.start = frames;
        if (host.sawC) check('o C quebrado pelo cliente saiu da lista do servidor', () => true);
        else check('o cliente pôs e quebrou o C', () => 'tempo esgotado (C ' + (c ? 'na lista' : 'nunca visto') + ')');
        if (!host.teleported) check('o servidor teletransportou o cliente até B', () => 'o cliente não chegou a B');
        // Quebra A e B (o pedaço de baixo do meio) e manda aos clientes.
        breakAndSend(ax, floor - 1);
        breakAndSend(bx, floor - 1);
        Terraria.NetMessage['void SendTileSquare(int whoAmi, int tileX, int tileY, int xSize, int ySize, TileChangeType changeType)'](-1, ax - 2, floor - 5, 6, 6, 0);
        Terraria.NetMessage['void SendTileSquare(int whoAmi, int tileX, int tileY, int xSize, int ySize, TileChangeType changeType)'](-1, bx - 2, floor - 5, 6, 6, 0);
    }
    if (host.cleaned && !host.done && frames - host.start >= 120) {
        host.done = true;
        check('A e B quebrados saíram da lista do servidor', () =>
            (!listed(A().PylonType) && !listed(B().PylonType)) || 'ainda na lista');
        for (const x0 of [ax, bx, cx]) for (let i = x0 - 6; i <= x0 + 6; i++) if (bl.tiles.typeAt(i, floor) >= 0) breakAndSend(i, floor);
        finish();
    }
}

// ---- cliente ----
const client = { step: 0, wait: 0, done: false };
function next() { client.step++; client.wait = 0; }
function clientStep() {
    const { floor, ax, cx } = area();
    client.wait++;
    switch (client.step) {
        case 0:
            if ((!listed(A().PylonType) || !listed(B().PylonType)) && client.wait < 600) return;
            check('A e B chegaram ao entrar (OnPlayerJoining)', () =>
                (!!listed(A().PylonType) && !!listed(B().PylonType)) || `A ${!!listed(A().PylonType)}, B ${!!listed(B().PylonType)}`);
            return next();
        case 1:
            // Parado ao lado de A, mandando a posição ao servidor.
            stand(ax + 3);
            if (client.wait % 10 === 0) send(13, -1, -1, null, Main.myPlayer, 0, 0, 0, 0, 0, 0);
            if (client.wait < 150) return;
            check('ao lado de A, perto de um pilar', () =>
                Terraria.GameContent.TeleportPylonsSystem['bool IsPlayerNearAPylon(Player player)'](me()) === true || 'não');
            Main.PylonSystem['void RequestTeleportation(TeleportPylonInfo info, Player player)'](listed(B().PylonType).info, me());
            return next();
        case 2: {
            const b = listed(B().PylonType), p = me();
            p.fallStart = Math.floor(p.position.Y / 16);
            const near = Math.hypot(p.position.X / 16 - (b.x + 1), p.position.Y / 16 - (b.y + 1)) < 6;
            if (!near && client.wait < 300) return;
            check('o teletransporte pedido ao servidor leva até B', () =>
                near || `jogador em ${Math.round(p.position.X / 16)},${Math.round(p.position.Y / 16)}, B em ${b.x},${b.y}`);
            return next();
        }
        case 3:
            check('C colocado pelo cliente', () => placeAsPlayer(cx, floor - 1, C().Type) || 'não colocou');
            return next();
        case 4: {
            const c = listed(C().PylonType);
            if (!c && client.wait < 600) return;
            check('C entrou na lista pelo servidor (PylonWasAdded)', () => !!c || 'não chegou em 600 quadros');
            check('a TEModdedPylon do C voltou do servidor', () => !c || ModTileEntity.TryGet(c.x, c.y, MpPylonEntity) !== null || 'sem entidade');
            return next();
        }
        case 5:
            if (client.wait === 1) {
                mapIcons = 0;
                Main.mapFullscreen = true;
                Main.mapFullscreenScale = 4;
                Main.mapFullscreenPos = Vector2.new(cx + 1, floor - 2);
            }
            if (client.wait < 120) return;
            Main.mapFullscreen = false;
            check('o mapa do cliente desenhou o ícone do mod', () => mapIcons > 0 || 'DrawMapIcon não foi chamado');
            breakAndSend(cx, floor - 1);
            return next();
        case 6: {
            const c = listed(C().PylonType);
            if (c && client.wait < 600) return;
            check('quebrado pelo cliente, C saiu da lista', () => !c || 'ainda na lista');
            return next();
        }
        case 7: {
            const left = listed(A().PylonType) || listed(B().PylonType);
            if (left && client.wait < 1800) return;
            check('A e B saíram quando o anfitrião os quebrou (PylonWasRemoved)', () => !left || 'ainda na lista');
            client.done = true;
            finish();
            return;
        }
    }
}

let frames = 0;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || Main.netMode === 0) return;
    frames++;
    self.statLife = self.statLifeMax2;
    if (Main.netMode & 2) {
        if (frames === 60) check('preparo', hostStart);
        if (frames === 120) check('A e B na lista do servidor', () =>
            (!!listed(A().PylonType) && !!listed(B().PylonType)) || `A ${!!listed(A().PylonType)}, B ${!!listed(B().PylonType)}`);
        else if (frames > 120 && !host.done) hostWatch(frames);
    } else if (frames >= 300 && !client.done) {
        clientStep();
    }
});
bl.log('mppylon: carregado');

export default class TestMpPylon extends Mod {}
