import { role } from './config.js';
const Main = Terraria.Main;
const send =
    Terraria.NetMessage[
        'void SendData(int msgType, int remoteClient, int ignoreClient, NetworkText text, int number, float number2, float number3, float number4, int number5, int number6, int number7)'
    ];
let mod,
    makeItem,
    check,
    complete,
    phase = '',
    peer = -1,
    ticks = 0,
    started = false,
    finished = false,
    chestIndex = -1;
const log = text => bl.log('moditemdata MP ' + role + ' ' + text);

function packet(kind, value = 0, target = -1) {
    const p = mod.GetPacket();
    p.WriteString(kind);
    p.WriteInt32(value);
    p.Send(target);
}

function findWorld(count) {
    for (let i = 0; i < Main.item.length; i++) {
        const world = Main.item[i];
        if (world.active && world.inner.ModItem && world.inner.ModItem.count === count) return world;
    }
    return null;
}

function findOwned(player, count) {
    for (let i = 0; i < player.inventory.length; ++i) {
        const item = player.inventory[i];
        if (item.ModItem && item.ModItem.count === count) return item;
    }
    return null;
}

function drop(player, item, ui = false) {
    player.inventory[0] = item;
    player.selectedItemState.selected = 0;
    Main.mouseItem['void TurnToAir()']();
    if (ui) {
        GUIPageIcons['void DropUIItem(Player player, Item item, int additionalVelocity)'](player, item, 0);
        item['void TurnToAir()']();
    } else player['void DropSelectedItem()']();
}

export function initMultiplayer(owner, create, verify, finish) {
    mod = owner;
    makeItem = create;
    check = verify;
    complete = finish;
}

export function handleMultiplayer(reader, from) {
    const kind = reader.ReadString(),
        value = reader.ReadInt32();
    log('packet ' + kind + ' from=' + from);
    if (role === 'host' && kind === 'hello') {
        peer = from;
        if (phase === 'hello') {
            phase = 'client-drop';
            packet('drop', 0, peer);
        }
    } else if (role === 'client' && kind === 'drop' && phase === 'hello') {
        const player = Main.player[Main.myPlayer];
        drop(player, makeItem(undefined, 'CLIENT_A', 424), true);
        phase = 'host-drop';
    } else if (role === 'client' && kind === 'chest') {
        chestIndex = value;
        if (!Main.chest[value]) Main.chest[value] = Terraria.Chest['Chest CreateOutOfArray(int index, int x, int y, int maxItems)'](value, 0, 0, 40);
        phase = 'chest';
        packet('chest-ready');
    } else if (role === 'host' && kind === 'chest-ready') {
        phase = 'chest-send';
    } else if (role === 'host' && kind === 'picked') {
        phase = 'inventory';
    } else if (role === 'client' && kind === 'finished' && phase === 'wait-finished') {
        packet('finished');
        complete();
        finished = true;
    } else if (role === 'host' && kind === 'finished' && phase === 'wait-finished') {
        complete();
        finished = true;
    }
}

export function tickMultiplayer(player) {
    if (role === 'native' || finished) return;
    ticks++;
    if (ticks > 7200) {
        check('MP timeout phase=' + phase, () => false);
        complete();
        finished = true;
        return;
    }
    if (!started) {
        started = true;
        phase = 'hello';
        log('START myPlayer=' + Main.myPlayer);
        const net = Terraria.Netplay;
        net.ListenPort = 7777;
        net.ServerPassword = '';
        net.UseUPNP = false;
        if (role === 'host') {
            Terraria.Main.gameMenu = true;
            GUIInstance.Active.GUIMultiplayerHost['void HostServer()']();
        } else {
            Main.netMode = 1;
            Main.gameMenu = true;
            Main.menuMode = 14;
            net['bool SetRemoteIP(string remoteAddress)']('10.0.2.2');
            net['void StartTcpClient(bool connectingToLocalServer)'](false);
        }
        return;
    }
    if (ticks % 180 === 0) log('STATE ' + phase + ' mode=' + Main.netMode + ' menu=' + Main.gameMenu + ' player=' + Main.myPlayer + ' running=' + Terraria.Netplay.IsServerRunning);
    if (Main.gameMenu) return;
    if (role === 'client' && phase === 'hello' && ticks % 120 === 0) packet('hello');
    if (role === 'host' && phase === 'client-drop') {
        const world = findWorld(424);
        if (!world) return;
        check('MP client A -> server retains custom owner', () => world.inner.ModItem.owner === 'CLIENT_A');
        player.inventory[0]['void TurnToAir()']();
        player['void PickupItem(WorldItem itemToPickUp)'](world);
        const picked = findOwned(player, 424);
        check('MP native pickup by another player retains owner', () => picked && picked.ModItem.owner === 'CLIENT_A');
        if (!picked) {
            phase = 'failed';
            return;
        }
        picked.ModItem.count = 425;
        drop(player, picked);
        phase = 'wait-picked';
    } else if (role === 'client' && phase === 'host-drop') {
        const world = findWorld(425);
        if (!world) return;
        check('MP server -> client second drop retains custom owner', () => world.inner.ModItem.owner === 'CLIENT_A');
        player.inventory[0]['void TurnToAir()']();
        player['void PickupItem(WorldItem itemToPickUp)'](world);
        const picked = findOwned(player, 425);
        check('MP second native pickup retains custom owner', () => picked && picked.ModItem.owner === 'CLIENT_A');
        if (!picked) {
            phase = 'failed';
            return;
        }
        picked.ModItem.count = 430;
        packet('picked');
        phase = 'wait-chest';
    } else if (role === 'host' && phase === 'inventory') {
        const item = findOwned(Main.player[peer], 430);
        if (!item) return;
        check('MP custom-only inventory change reaches server', () => item.ModItem.owner === 'CLIENT_A');
        chestIndex = Array.from({ length: Main.chest.length }, (_, i) => i).find(i => Main.chest[i]);
        if (chestIndex === undefined) {
            phase = 'failed';
            check('MP existing chest required', () => false);
            return;
        }
        packet('chest', chestIndex, peer);
        phase = 'chest-ready';
    } else if (role === 'host' && phase === 'chest-send') {
        Main.chest[chestIndex].item[39]['void SetToItem(Item item)'](makeItem(undefined, 'CHEST_A', 426));
        send(32, peer, -1, null, chestIndex, 39, 0, 0, 0, 0, 0);
        phase = 'chest-return';
    } else if (role === 'client' && phase === 'chest') {
        const chest = Main.chest[chestIndex];
        if (!chest) return;
        const item = chest.item[39]['Item ExpandItem()']();
        if (!item.ModItem || item.ModItem.count !== 426) return;
        check('MP chest server -> client retains owner', () => item.ModItem.owner === 'CHEST_A');
        item.ModItem.count = 427;
        chest.item[39]['void SetToItem(Item item)'](item);
        send(32, -1, -1, null, chestIndex, 39, 0, 0, 0, 0, 0);
        phase = 'wait-finished';
    } else if (role === 'host' && phase === 'chest-return') {
        const item = Main.chest[chestIndex].item[39]['Item ExpandItem()']();
        if (!item.ModItem || item.ModItem.count !== 427) return;
        check('MP chest client -> server retains owner', () => item.ModItem.owner === 'CHEST_A');
        packet('finished', 0, peer);
        phase = 'wait-finished';
    }
}
