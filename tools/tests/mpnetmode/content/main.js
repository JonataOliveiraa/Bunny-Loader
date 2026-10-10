// O lado do servidor quando o host também joga (Main.netMode 3, campo de
// bits): tudo o que antes testava `netMode === 2` e pulava o host do celular.
//   - Expositor: o item de mod que o host põe chega ao cliente com os dados
//     (SendData 86 e #Placed); o que o cliente põe chega ao host (te-place);
//     o SendSection do host manda os dados de novo.
//   - Baú de mod que o cliente põe e quebra (ChestNetworkHooks.Receive).
//   - Comandos: Server/World do cliente rodam no host com o jogador dele;
//     o chat do próprio host vale como Chat e como Server.
//   - HijackSendData recebe o cliente de destino como whoAmI.
// Instale no host e no cliente, com o Example Mod. Loga "mpnm <papel> ...".
const Main = Terraria.Main;
const W = Terraria.WorldGen;
const TE = Terraria.GameContent.Tile_Entities;
const TileEntity = Terraria.DataStructures.TileEntity;
const sendData = Terraria.NetMessage['void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)'];
const sendSquare = (x, y, w, h) => Terraria.NetMessage['void SendTileSquare(int whoAmi, int tileX, int tileY, int xSize, int ySize, TileChangeType changeType)'](-1, x, y, w, h, 0);
const kill = (x, y) => W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](x, y, false, false, true);
const killWall = (x, y) => W['void KillWall(int i, int j, bool fail)'](x, y, false);
const place = (x, y, type) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, type, true, true, -1, 0);
const placeWall = (x, y, type) => W['void PlaceWall(int i, int j, int type, bool mute)'](x, y, type, true);
const placeObject = (x, y, type) => W['bool PlaceObject(int x, int y, int type, bool mute, int style, int alternate, int random, int direction)'](x, y, type, true, 0, 0, -1, 1);
const FRAME = 395;

let role = '', fails = 0, done = false;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('mpnm ' + role + ' ' + label + ': ok');
        else { fails++; bl.log('mpnm ' + role + ' ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('mpnm ' + role + ' ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}
function finish() {
    done = true;
    bl.log('mpnm ' + role + ' FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
}

export class NMProbe extends ModItem {
    Texture = 'Box';
    HideFromModMenu = true;
    owner = '';

    SetDefaults(item) {
        item.width = item.height = 16;
        item.maxStack = 1;
        item.damage = 10;
        item.useStyle = 1;
        this.owner = '';
    }

    SaveData(tag) { tag.Set('owner', this.owner); }
    LoadData(tag) { this.owner = tag.GetString('owner'); }
}

const commands = [];
function record(name, caller, args) {
    commands.push({ name, type: caller.CommandType, who: caller.Player ? caller.Player.whoAmI : -1, arg: args[0] || '' });
    bl.log(`mpnm ${role} comando ${name} ${args[0] || ''}: tipo ${caller.CommandType}, jogador ${caller.Player ? caller.Player.whoAmI : -1}`);
    caller.Reply('mpnm ' + name + ' ok');
}
class NMChatCommand extends ModCommand {
    get Command() { return 'nmchat'; }
    get Type() { return CommandType.Chat; }
    Action(caller, input, args) { record('chat', caller, args); }
}
class NMServerCommand extends ModCommand {
    get Command() { return 'nmserver'; }
    get Type() { return CommandType.Server; }
    Action(caller, input, args) { record('server', caller, args); }
}
class NMWorldCommand extends ModCommand {
    get Command() { return 'nmworld'; }
    get Type() { return CommandType.World; }
    Action(caller, input, args) { record('world', caller, args); }
}
ModCommand.register(NMChatCommand);
ModCommand.register(NMServerCommand);
ModCommand.register(NMWorldCommand);

const hijacked = [];
class NMHijack extends ModSystem {
    HijackSendData(whoAmI, msgType, remoteClient, ignoreClient, text, number, number2, number3, number4, number5, number6, number7) {
        if (msgType === 7 && remoteClient >= 0) hijacked.push({ whoAmI, remoteClient });
        return false;
    }
}
ModSystem.register(NMHijack);

let me = null, clientDone = false, resendFrom = -1;
export default class TestMPNetMode extends Mod {
    Load() { me = this; }

    HandlePacket(reader, whoAmI) {
        const kind = reader.ReadString();
        if (kind === 'done') clientDone = true;
        else if (kind === 'resend') resendFrom = whoAmI;
    }
}
function packet(kind) {
    const p = me.GetPacket();
    p.Write(kind);
    p.Send();
}

// Os dois calculam o mesmo lugar (o mundo é o mesmo): longe do spawn, no ar.
function area() {
    const x0 = Main.spawnTileX + 30, top = Main.spawnTileY - 34;
    return { x0, x1: x0 + 16, top, floorY: top + 9 };
}
function frameSpots() {
    const a = area();
    return [{ x: a.x0 + 2, y: a.floorY - 6 }, { x: a.x0 + 6, y: a.floorY - 6 }];
}
const chestSpot = () => { const a = area(); return { x: a.x0 + 11, y: a.floorY - 1 }; };
const chestType = () => ModContent.TileType('ExampleChest');

function teAt(x, y) {
    const key = (x & 0xFFFF) | (y << 16);
    return TileEntity.ByPosition.ContainsKey(key) ? TileEntity.ByPosition.get_Item(key) : null;
}
function ownerOf(te) {
    const item = te && te.item;
    if (!item || item.type !== ModContent.ItemType(NMProbe)) return null;
    return item.ModItem.owner;
}
function probe(owner) {
    const i = Terraria.Item.new();
    i['void .ctor()']();
    i['void SetDefaults(int Type, ItemVariant variant)'](ModContent.ItemType(NMProbe), null);
    i.ModItem.owner = owner;
    return i;
}
// Põe o item de mod pela função do jogo, com ele na mão (como o toque).
function placeInFrame(player, te, owner) {
    const sel = player.selectedItem;
    const saved = player.inventory[sel];
    player.inventory[sel] = probe(owner);
    player.itemTime = 0;
    player.itemAnimation = 0;
    try {
        TE.TEItemFrame['void PlaceItemInFrame(Player player, int x, int y)'](player, te.Position.X, te.Position.Y);
    } finally {
        player.inventory[sel] = saved;
    }
}
function chatMessage(text) {
    return Terraria.UI.Chat.ChatManager.Commands['ChatMessage CreateOutgoingMessage(string text)'](text);
}
function remoteIndex() {
    for (let i = 0; i < 255; i++) if (i !== Main.myPlayer && Main.player[i].active) return i;
    return -1;
}

// -------------------------------- host --------------------------------
// O chat do host como a caixa de chat do celular manda: o texto vai ao
// ChatCommandProcessor com o índice do próprio host.
function hostChat(text) {
    Terraria.UI.Chat.ChatManager.Commands['void ProcessIncomingMessage(ChatMessage message, int clientId)'](chatMessage(text), Main.myPlayer);
}

function build() {
    const a = area();
    for (let x = a.x0 - 1; x <= a.x1 + 1; x++) {
        for (let y = a.top - 1; y <= a.floorY + 1; y++) {
            kill(x, y);
            killWall(x, y);
            if (y < a.floorY) placeWall(x, y, 1);
        }
        place(x, a.floorY, 1);
    }
    const data = Terraria.ObjectData.TileObjectData['TileObjectData GetTileData(int type, int style, int alternate)'](FRAME, 0, 0);
    const ids = [];
    for (const s of frameSpots()) {
        const px = s.x + data.Origin.X, py = s.y + data.Origin.Y;
        const ok = placeObject(px, py, FRAME);
        ids.push(ok ? TE.TEItemFrame['int Hook_AfterPlacement(int x, int y, int type, int style, int direction, int alternate)'](px, py, FRAME, 0, 1, 0) : -1);
    }
    sendSquare(a.x0 - 1, a.top - 1, a.x1 - a.x0 + 3, a.floorY - a.top + 3);
    for (const id of ids) if (id >= 0) sendData(86, -1, -1, null, id, 0, 0, 0, 0, 0, 0);
    return ids;
}

function cleanup() {
    const a = area();
    for (const s of frameSpots()) {
        const te = teAtFrame(s);
        if (te && te.item) te.item['void TurnToAir()']();
    }
    for (let x = a.x0 - 1; x <= a.x1 + 1; x++) {
        for (let y = a.top - 1; y <= a.floorY + 1; y++) {
            kill(x, y);
            killWall(x, y);
        }
    }
    sendSquare(a.x0 - 1, a.top - 1, a.x1 - a.x0 + 3, a.floorY - a.top + 3);
    for (let i = 0; i < 400; i++) {
        const it = Main.item[i];
        if (it && it.active && it.inner.type === ModContent.ItemType(NMProbe)) it['void TurnToAir()']();
    }
}

// O canto do expositor é o do TE (o Hook_AfterPlacement o cria no canto de cima).
function teAtFrame(s) {
    for (let dx = 0; dx < 2; dx++) for (let dy = 0; dy < 2; dy++) {
        const te = teAt(s.x + dx, s.y + dy);
        if (te) return te;
    }
    return null;
}

let started = -1, ids = [], sawChest = false, resent = false;
function hostTick(frames) {
    const r = remoteIndex();
    if (r < 0) return;
    if (started < 0) {
        started = frames;
        bl.log('mpnm host: cliente entrou (' + Main.player[r].name + ', ' + r + ')');
        return;
    }
    const t = frames - started;
    if (t === 120) {
        ids = build();
        check('montou os dois expositores', () => ids.every((id) => id >= 0) || JSON.stringify(ids));
    }
    if (t === 180) {
        const te = teAtFrame(frameSpots()[0]);
        if (te) placeInFrame(Main.player[Main.myPlayer], te, 'host');
        check('o host pôs o item no expositor A', () => ownerOf(teAtFrame(frameSpots()[0])) === 'host' || 'dono ' + ownerOf(teAtFrame(frameSpots()[0])));
        sendData(7, r, -1, null, 0, 0, 0, 0, 0, 0, 0);
        hostChat('/nmchat host');
        hostChat('/nmserver host');
        hostChat('/nmworld host');
    }
    if (!sawChest) {
        const c = chestSpot();
        sawChest = bl.tiles.typeAt(c.x, c.y) === chestType() && Terraria.Chest.FindChest(c.x, c.y - 1) >= 0;
    }
    if (resendFrom >= 0 && !resent) {
        resent = true;
        const s = frameSpots()[0];
        Terraria.NetMessage['void SendSection(int whoAmi, int sectionX, int sectionY)'](resendFrom, Math.floor(s.x / 200), Math.floor(s.y / 150));
    }
    if ((clientDone && t > 240) || t > 3000) {
        const me = Main.myPlayer;
        const by = (name, arg) => commands.filter((c) => c.name === name && c.arg === arg);
        check('o cliente mandou o fim', () => clientDone || 'sem o "done" do cliente');
        check('te-place: o item que o cliente pôs no expositor B chega com os dados', () =>
            ownerOf(teAtFrame(frameSpots()[1])) === 'client' || 'dono ' + ownerOf(teAtFrame(frameSpots()[1])));
        check('baú de mod do cliente: o host o criou', () => sawChest || 'tile ' + bl.tiles.typeAt(chestSpot().x, chestSpot().y));
        check('baú de mod do cliente: quebrado no host também', () => bl.tiles.typeAt(chestSpot().x, chestSpot().y) !== chestType() || 'o baú ficou');
        check('/nmserver do cliente roda no host como Server, com o jogador dele', () => {
            const c = by('server', 'client');
            return (c.length === 1 && c[0].type === CommandType.Server && c[0].who === r) || JSON.stringify(c);
        });
        check('/nmworld do cliente roda no host como Server, com o jogador dele', () => {
            const c = by('world', 'client');
            return (c.length === 1 && c[0].type === CommandType.Server && c[0].who === r) || JSON.stringify(c);
        });
        check('/nmchat do cliente não chega ao host (roda no cliente)', () => by('chat', 'client').length === 0 || JSON.stringify(commands));
        check('chat do host: /nmchat vale como Chat, uma vez', () => {
            const c = by('chat', 'host');
            return (c.length === 1 && c[0].type === CommandType.Chat && c[0].who === me) || JSON.stringify(c);
        });
        check('chat do host: /nmserver vale como Server, com o jogador do host', () => {
            const c = by('server', 'host');
            return (c.length === 1 && c[0].type === CommandType.Server && c[0].who === me) || JSON.stringify(c);
        });
        check('chat do host: /nmworld vale como Server, com o jogador do host', () => {
            const c = by('world', 'host');
            return (c.length === 1 && c[0].type === CommandType.Server && c[0].who === me) || JSON.stringify(c);
        });
        check('HijackSendData: whoAmI é o cliente de destino', () =>
            (hijacked.length > 0 && hijacked.every((h) => h.whoAmI === h.remoteClient)) || JSON.stringify(hijacked));
        check('o cliente pediu o SendSection', () => resent || 'sem pedido');
        cleanup();
        finish();
    }
}

// ------------------------------- cliente -------------------------------
let cStage = 0, cSince = 0;
function clientTick(frames) {
    const [A, B] = frameSpots();
    const c = chestSpot();
    const player = Main.player[Main.myPlayer];
    if (cStage === 0) {
        const te = teAtFrame(A);
        if (ownerOf(te) === 'host') {
            check('o item de mod que o host pôs chega com os dados', () => true);
            cStage = 10;
            cSince = frames;
        } else if (frames > 2400) {
            check('o item de mod que o host pôs chega com os dados', () => 'dono ' + ownerOf(te) + (te ? '' : ' (sem o expositor)'));
            cStage = 1;
            cSince = frames;
        }
        return;
    }
    // Espera o que ainda vinha do host assentar, apaga o dado local e só
    // então pede o SendSection: o que voltar é dele.
    if (cStage === 10) {
        if (frames - cSince < 120) return;
        teAtFrame(A).item.ModItem.owner = 'apagado';
        cStage = 11;
        cSince = frames;
        return;
    }
    if (cStage === 11) {
        if (frames - cSince < 60) return;
        const still = ownerOf(teAtFrame(A));
        check('nada mais chega ao expositor antes do pedido', () => still === 'apagado' || 'dono ' + still);
        packet('resend');
        cStage = 1;
        cSince = frames;
        return;
    }
    if (cStage === 1) {
        const back = ownerOf(teAtFrame(A)) === 'host';
        if (!back && frames - cSince < 300) return;
        check('SendSection do host manda os dados do expositor de novo', () => back || 'dono ' + ownerOf(teAtFrame(A)));
        const te = teAtFrame(B);
        if (te) placeInFrame(player, te, 'client');
        else check('expositor B no cliente', () => 'sem o expositor');
        placeObject(c.x, c.y, chestType());
        Terraria.Chest['int AfterPlacement_Hook(int x, int y, int type, int style, int direction, int alternate)'](c.x, c.y, chestType(), 0, 1, 0);
        const send = (text) => Terraria.Chat.ChatHelper['void SendChatMessageFromClient(ChatMessage message)'](chatMessage(text));
        send('/nmchat client');
        send('/nmserver client');
        send('/nmworld client');
        cStage = 2;
        cSince = frames;
        return;
    }
    if (cStage === 2) {
        const placed = Terraria.Chest.FindChest(c.x, c.y - 1) >= 0;
        const own = ownerOf(teAtFrame(B)) === 'client';
        if ((!placed || !own) && frames - cSince < 600) return;
        check('baú de mod: o host confirma (placed) e o cliente cria o Main.chest', () => placed || 'sem o baú em ' + c.x + ',' + (c.y - 1));
        check('o item que o cliente pôs volta do host com os dados', () => own || 'dono ' + ownerOf(teAtFrame(B)));
        check('/nmchat roda no cliente como Chat', () => {
            const list = commands.filter((x) => x.name === 'chat');
            return (list.length === 1 && list[0].type === CommandType.Chat && list[0].who === Main.myPlayer) || JSON.stringify(commands);
        });
        check('Server/World não rodam no cliente', () => commands.every((x) => x.name === 'chat') || JSON.stringify(commands));
        for (let k = 0; k < 4 && bl.tiles.typeAt(c.x, c.y) === chestType(); k++) {
            player['void PickTile(int x, int y, int pickPower, int dealDamageAsIfBaseNumberIs)'](c.x, c.y, 10000, -1);
        }
        bl.log(`mpnm cliente: depois do PickTile, tile ${bl.tiles.typeAt(c.x, c.y)}, baú ${Terraria.Chest.FindChest(c.x, c.y - 1)}, ` +
               `CanKillTile ${W['bool CanKillTile(int i, int j)'](c.x, c.y)}`);
        cStage = 3;
        cSince = frames;
        return;
    }
    if (cStage === 3 && frames - cSince > 120) {
        check('baú de mod quebrado no cliente', () => bl.tiles.typeAt(c.x, c.y) !== chestType() || 'o baú ficou');
        packet('done');
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
        bl.log(`mpnm: netMode ${mode}, papel ${role || 'nenhum'}, jogador ${Main.myPlayer}`);
        if (!role) { done = true; return; }
    }
    if (!self.dead) self.statLife = self.statLifeMax2;
    self.fallStart = Math.floor(self.position.Y / 16);
    if (role === 'host') hostTick(frames);
    else clientTick(frames);
});
bl.log('mpnm: carregado');
