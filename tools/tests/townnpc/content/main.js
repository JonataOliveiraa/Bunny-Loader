// O morador de mod (a Pessoa do Example Mod), em rodadas, reabrindo o jogo:
//   1 com o mod: casa de madeira no ceu, jogador com o Exemplo de Item; a
//     checagem de mudanca do jogo marca a Pessoa e o WorldGen.SpawnTownNPC do
//     jogo a muda para a casa: nome da lista, casa, fala, cabeca; a sala
//     guardada no TownManager (como na morte). Salva;
//   2 com o mod: voltou do arquivo ao lado com o mesmo nome e a mesma casa;
//   3 sem o Example Mod: o mundo abriu, sem ela; salva (o arquivo ao lado fica);
//   4 com o mod: voltou de novo.
// O estado entre as rodadas fica em dataDirectory. Loga "townnpc ...".
const Main = Terraria.Main;
const W = Terraria.WorldGen;
const { NPCID, TileID, WallID } = Terraria.ID;
const STATE = bl.path.join(bl.mod.dataDirectory, 'state.json');
const NAMES = ['Someone', 'Somebody', 'Blocky', 'Colorless'];

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('townnpc ' + label + ': ok');
        else { fails++; bl.log('townnpc ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('townnpc ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const place = (x, y, type) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, type, true, true, -1, 0);
const save = () => Terraria.IO.WorldFile['void SaveWorld(WorldSaveContext saveContext)'](0);

function personType() {
    for (let t = bl.npcs.vanillaCount; bl.npcs.isModNpc(t); t++) {
        const m = ModNPC.getModNPC(t);
        if (m && m.constructor.name === 'ExamplePerson') return t;
    }
    return -1;
}

function findNpc(type) {
    const npcs = Main.npc;
    for (let i = 0; i < npcs.length - 1; i++) {
        if (npcs[i].active && npcs[i].type === type) return npcs[i];
    }
    return null;
}

function buildHouse(x0, y0) {
    const x1 = x0 + 11, top = y0 - 7;
    for (let x = x0; x <= x1; x++) {
        for (let y = top; y <= y0; y++) {
            W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](x, y, false, false, true);
            W['void KillWall(int i, int j, bool fail)'](x, y, false);
        }
    }
    for (let x = x0; x <= x1; x++) {
        place(x, top, x === x0 + 5 ? TileID.Platforms : TileID.WoodBlock);
        place(x, y0, TileID.WoodBlock);
    }
    for (let y = top; y <= y0; y++) {
        place(x0, y, TileID.WoodBlock);
        place(x1, y, TileID.WoodBlock);
    }
    for (let x = x0 + 1; x < x1; x++) {
        for (let y = top + 1; y < y0; y++) W['void PlaceWall(int i, int j, int type, bool mute)'](x, y, WallID.Wood, true);
    }
    place(x0 + 3, y0 - 1, TileID.Tables);
    place(x0 + 6, y0 - 1, TileID.Chairs);
    place(x0 + 8, y0 - 4, TileID.Torches);
    return { x: x0 + 5, y: y0 - 2 };
}

function readState() {
    const txt = bl.file.read(STATE);
    return txt ? JSON.parse(txt) : { round: 0 };
}

function run() {
    const type = personType();
    const state = readState();
    bl.log(`townnpc: rodada ${state.round + 1}, Example Mod ${type > 0 ? 'ligado' : 'desligado'}`);

    if (state.round === 0) {
        const p = Main.player[Main.myPlayer];
        // Uma Pessoa que ja more neste mundo (de outro teste) impediria a mudanca.
        for (let i = 0; i < Main.npc.length - 1; i++) {
            if (Main.npc[i].active && Main.npc[i].type === type) Main.npc[i].active = false;
        }
        check('cabeca: TypeToDefaultHeadIndex da o indice da cabeca de mod', () => {
            const slot = bl.npcs.headSlot(type);
            const got = Terraria.NPC['int TypeToDefaultHeadIndex(int type)'](type);
            return (slot >= 81 && got === slot) || `slot ${slot}, jogo ${got}`;
        });
        check('cabeca: na lista do menu de casas', () => {
            const order = Terraria.ID.NPCHeadID.Sets.HeadListOrder;
            for (let i = 0; i < order.length; i++) if (order[i] === bl.npcs.headSlot(type)) return true;
            return 'fora da HeadListOrder';
        });

        const x0 = Main.spawnTileX + 20, y0 = Main.spawnTileY - 25;
        const room = buildHouse(x0, y0);
        let exampleItem = -1;
        for (let t = bl.items.vanillaCount; bl.items.isModItem(t); t++) {
            const m = ModItem.getModItem(t);
            if (m && m.constructor.name === 'ExampleItem') exampleItem = t;
        }
        p.inventory[49]['void SetDefaults(int Type, ItemVariant variant)'](exampleItem, null);
        check('mudanca: a checagem do jogo marca a Pessoa (CanTownNPCSpawn)', () => {
            // Sem o forceUpdate (1.4.5.8): o contador no limite faz a checagem rodar agora.
            Main.checkForSpawns = 1000000;
            Main['void UpdateTime_SpawnTownNPCs()']();
            return Main.townNPCCanSpawn[type] === true || 'townNPCCanSpawn falso';
        });

        // O Guia sem casa pegaria a sala primeiro: so durante a chamada, ele
        // conta como tendo casa. Mas sem casa ele guarda a casa antiga: se ela
        // era esta sala (de uma rodada antiga), ele a ocuparia; a casa antiga
        // sai durante a chamada. E um morador que ja more na sala a bloquearia.
        const nearRoom = (n) => Math.abs(n.homeTileX - room.x) < 12 && Math.abs(n.homeTileY - room.y) < 12;
        const townBefore = [];
        for (let i = 0; i < Main.npc.length - 1; i++) {
            const n = Main.npc[i];
            if (n.active && n.townNPC) townBefore.push(`${n.type}${n.homeless ? ' sem casa' : ''}@${n.homeTileX},${n.homeTileY}`);
        }
        const homeless = [];
        const tenants = [];
        for (let i = 0; i < Main.npc.length - 1; i++) {
            const n = Main.npc[i];
            if (!n.active || !n.townNPC) continue;
            if (n.homeless) {
                homeless.push([n, n.homeTileX, n.homeTileY]);
                n.homeless = false;
                if (nearRoom(n)) {
                    n.homeTileX = -1;
                    n.homeTileY = -1;
                }
            } else if (nearRoom(n)) {
                tenants.push([n, n.homeTileX, n.homeTileY]);
                n.homeTileX = -1;
                n.homeTileY = -1;
            }
        }
        W.prioritizedTownNPCType = type;
        let result = -1;
        try {
            const r = W['TownNPCSpawnResult SpawnTownNPC(int x, int y, bool canSpawnNewTownNPC)'](room.x, room.y, true);
            result = typeof r === 'number' ? r : r.value__;
        } finally {
            for (const [n, x, y] of homeless) {
                n.homeless = true;
                n.homeTileX = x;
                n.homeTileY = y;
            }
            // Sem casa: a sala agora e da Pessoa, e o jogo acha outra para ele.
            for (const [n] of tenants) n.homeless = true;
        }
        // Por que o jogo recusou (Blocked): o que a checagem da sala deixou nos campos dele.
        const roomState = `canSpawn ${W.canSpawn}, tiles ${W.numRoomTiles}, hiScore ${W.hiScore}, ` +
            `porta ${W.roomDoor} mesa ${W.roomTable} cadeira ${W.roomChair} luz ${W.roomTorch} ` +
            `ocupada ${W.roomOccupied} maligna ${W.roomEvil} percevejo ${W.roomHasStinkbug}; ` +
            `tiles: mesa ${bl.tiles.typeAt(x0 + 3, y0 - 1)} cadeira ${bl.tiles.typeAt(x0 + 6, y0 - 1)} ` +
            `tocha ${bl.tiles.typeAt(x0 + 8, y0 - 4)} plataforma ${bl.tiles.typeAt(x0 + 5, y0 - 7)}; moradores antes: ${townBefore.join(' ') || 'nenhum'}`;
        const npc = findNpc(type);
        const neighbors = () => {
            const out = [];
            for (let i = 0; i < Main.npc.length - 1; i++) {
                const n = Main.npc[i];
                if (n.active && n.townNPC && !n.homeless && Math.abs(n.homeTileX - room.x) < 12 &&
                    Math.abs(n.homeTileY - room.y) < 12) out.push(n.type + '@' + n.homeTileX + ',' + n.homeTileY);
            }
            return out.join(' ') || 'ninguem';
        };
        check('mudanca: o SpawnTownNPC do jogo muda a Pessoa para a casa', () =>
            (result === 1 && npc && !npc.homeless) ||
            `resultado ${result}, npc ${!!npc}, sem casa ${npc && npc.homeless}, casa ${npc && npc.homeTileX},${npc && npc.homeTileY}, sala ${room.x},${room.y}, moradores perto: ${neighbors()}; ${roomState}`);
        if (!npc) return;
        check('nome proprio da lista do mod', () => NAMES.includes(npc.GivenName) || 'nome ' + npc.GivenName);
        check('fala do mod (GetChat)', () => {
            const chat = npc.GetChat();
            return ['Olá, como vai?', 'Olá!', 'Hello, how are you?', 'Hello!'].includes(chat) || 'fala ' + chat;
        });
        // O jogo guarda a sala de um morador que MORREU (para ele renascer ali):
    // registrada como na morte, ela tem de sobreviver ao save sem ir ao .wld.
    W.TownManager['void SetRoom(int npcID, int x, int y)'](type, npc.homeTileX, npc.homeTileY);
    check('a sala guardada (TownManager, como na morte)', () => W.TownManager.HasRoomQuick(type) || 'sem sala');
        save();
        bl.file.write(STATE, JSON.stringify({ round: 1, name: npc.GivenName, homeX: npc.homeTileX, homeY: npc.homeTileY }));
        bl.log('townnpc rodada: salvou');
        return;
    }

    const npc = type > 0 ? findNpc(type) : null;
    if (state.round === 1 || state.round === 3) {
        check(`rodada ${state.round + 1}: a Pessoa voltou do arquivo ao lado, mesmo nome e casa`, () =>
            (npc && npc.GivenName === state.name && npc.homeTileX === state.homeX && npc.homeTileY === state.homeY &&
             !npc.homeless) || (npc ? `${npc.GivenName} ${npc.homeTileX},${npc.homeTileY} sem casa ${npc.homeless}` : 'nao voltou'));
        check(`rodada ${state.round + 1}: a sala dela voltou (TownManager)`, () => W.TownManager.HasRoomQuick(type) || 'sem sala');
        if (state.round === 3) {
            bl.file.delete(STATE);
            bl.log('townnpc rodada: fim');
            return;
        }
        save();
    } else if (state.round === 2) {
        check('rodada 3: sem o mod o mundo abriu e ninguem de tipo desconhecido', () => {
            for (let i = 0; i < Main.npc.length - 1; i++) {
                const n = Main.npc[i];
                if (n.active && n.type >= bl.npcs.vanillaCount) return 'NPC de tipo ' + n.type;
            }
            return true;
        });
        save();
    }
    state.round++;
    bl.file.write(STATE, JSON.stringify(state));
    bl.log('townnpc rodada: salvou');
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    if (++frames === 90) {
        done = true;
        check('preparo', run);
        bl.log('townnpc FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('townnpc: carregado');

// A classe do mod, obrigatória no arquivo de entrada.
export default class TestTownnpc extends Mod {}
