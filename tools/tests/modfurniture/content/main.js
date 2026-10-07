// Os móveis do Example Mod de verdade no mundo, numa caverna cavada embaixo
// do spawn (o jogador vai para lá: luz e música só contam na tela):
//   - casa com porta, mesa, cadeira e tocha de mod: o jogo aceita como casa?
//   - a luz da tocha, fogueira, lustre e luminária de mod, quadro a quadro
//     (acesa sempre, sem piscar), comparada com a tocha do jogo;
//   - a caixa de música ligada toca a faixa do Example Mod;
//   - a porta de mod abre sozinha para o jogador que encosta e para o morador
//     que passa, fecha depois, e o fio abre e fecha; o goblin guerreiro
//     arromba (abre) e o peão derruba (sai o item da porta de mod).
// Não salva: o mundo do arquivo fica como estava. Loga "modfurniture ...".
const Main = Terraria.Main;
const W = Terraria.WorldGen;
const { TileID, WallID } = Terraria.ID;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('modfurniture ' + label + ': ok');
        else { fails++; bl.log('modfurniture ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('modfurniture ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const tileAt = (x, y) => Main.tile['Tile get_Item(int x, int y)'](x, y);
const kill = (x, y) => W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](x, y, false, false, true);
const place = (x, y, type, style = 0) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, type, true, true, -1, style);
const placeObject = (x, y, type) => W['bool PlaceObject(int x, int y, int type, bool mute, int style, int alternate, int random, int direction)'](x, y, type, true, 0, 0, -1, -1);
const light = (x, y) => {
    const c = Terraria.Lighting['Color GetColor(int x, int y)'](x, y);
    return c.R + c.G + c.B;
};

let T = {};
let area = null;
const samples = {};   // nome -> [soma de R+G+B por quadro]

function build() {
    const p = Main.player[Main.myPlayer];
    const cx = Main.spawnTileX, floorY = Main.spawnTileY + 70;
    area = { x0: cx - 40, x1: cx + 30, top: floorY - 14, floorY };

    // A caverna: tudo sai (blocos e paredes); chão e teto de pedra.
    for (let x = area.x0; x <= area.x1; x++) {
        for (let y = area.top; y < floorY; y++) {
            kill(x, y);
            W['void KillWall(int i, int j, bool fail)'](x, y, false);
            tileAt(x, y).liquid = 0;
        }
        place(x, floorY, TileID.Stone);
        place(x, area.top - 1, TileID.Stone);
    }

    // A casa: 12 de largura, parede de madeira no fundo, porta de mod na direita.
    const h = { x0: cx - 36, floorY };
    const x1 = h.x0 + 11, top = floorY - 8;
    for (let x = h.x0; x <= x1; x++) place(x, top, TileID.WoodBlock);
    for (let y = top; y < floorY; y++) {
        place(h.x0, y, TileID.WoodBlock);
        if (y < floorY - 3) place(x1, y, TileID.WoodBlock);
    }
    for (let x = h.x0 + 1; x < x1; x++) {
        for (let y = top + 1; y < floorY; y++) W['void PlaceWall(int i, int j, int type, bool mute)'](x, y, WallID.Wood, true);
    }
    h.door = placeObject(x1, floorY - 3, T.door);
    h.table = placeObject(h.x0 + 3, floorY - 1, T.table);
    h.chair = placeObject(h.x0 + 6, floorY - 1, T.chair);
    h.torch = place(h.x0 + 8, floorY - 4, T.torch);
    area.house = h;

    for (let x = area.x0; x <= area.x1; x++) for (let y = area.top; y < floorY; y++) tileAt(x, y).liquid = 0;
    for (const x of [cx - 16, cx - 8]) W['void PlaceWall(int i, int j, int type, bool mute)'](x, floorY - 1, WallID.Stone, true);
    // As luzes, longe umas das outras (e a tocha do jogo para comparar).
    area.lights = {
        vanillaTorch: { x: cx - 16, y: floorY - 1, ok: placeObject(cx - 16, floorY - 1, TileID.Torches) },
        modTorch: { x: cx - 8, y: floorY - 1, ok: placeObject(cx - 8, floorY - 1, T.torch) },
        campfire: { x: cx, y: floorY - 1, ok: placeObject(cx, floorY - 1, T.campfire) },
        lamp: { x: cx + 8, y: floorY - 3, ok: placeObject(cx + 8, floorY - 1, T.lamp) },
        chandelier: { x: cx + 16, y: area.top, ok: placeObject(cx + 16, area.top, T.chandelier) },
    };
    area.musicBox = { x: cx - 4, y: floorY - 1, ok: placeObject(cx - 4, floorY - 1, T.musicBox) };

    // O jogador no meio (luz e música contam na tela).
    p.position = Vector2.new(cx * 16, (floorY - 3) * 16);
    p.velocity = Vector2.new(0, 0);
}

function checkHouse() {
    const h = area.house;
    check('casa: porta, mesa, cadeira e tocha de mod colocadas', () =>
        (h.door && h.table && h.chair && h.torch) || `porta ${h.door}, mesa ${h.table}, cadeira ${h.chair}, tocha ${h.torch}`);

    check('casa: o jogo aceita a sala com os móveis de mod (StartRoomCheck + RoomNeeds)', () => {
        const room = W['bool StartRoomCheck(int x, int y, IRoomCheckFeedback feedback)'](h.x0 + 5, h.floorY - 2, null);
        const needs = W.RoomNeeds();
        const seen = ['door', 'table', 'chair', 'torch'].map((k) => `${k}:${W.houseTile[T[k]]}`).join(' ');
        return (room && needs && W.canSpawn) || `sala ${room}, RoomNeeds ${needs}, canSpawn ${W.canSpawn}, vistos ${seen}`;
    });

    check('porta: abre (OpenDoor) e fecha (CloseDoor)', () => {
        const x = h.x0 + 11, y = h.floorY - 2;
        const opened = W['bool OpenDoor(int i, int j, int direction)'](x, y, 1);
        const openType = bl.tiles.typeAt(x, y) >= 0 ? bl.tiles.typeAt(x, y) : bl.tiles.typeAt(x + 1, y);
        const closed = opened && W['bool CloseDoor(int i, int j, bool forced)'](x, y, true);
        const back = bl.tiles.typeAt(x, y);
        return (opened && openType === T.doorOpen && closed && back === T.door) ||
            `abriu ${opened} (tipo ${openType}, aberta ${T.doorOpen}), fechou ${closed} (tipo ${back}, fechada ${T.door})`;
    });
}

function sampleLights() {
    for (const [name, l] of Object.entries(area.lights)) (samples[name] || (samples[name] = [])).push(light(l.x, l.y));
}

function checkLights() {
    const vanilla = samples.vanillaTorch;
    const top = Math.max(...vanilla);
    bl.log(`modfurniture luz (R+G+B min..max): ` + Object.entries(samples)
        .map(([n, s]) => `${n} ${Math.min(...s)}..${Math.max(...s)}`).join(', '));

    for (const name of ['modTorch', 'campfire', 'lamp', 'chandelier']) {
        const l = area.lights[name], s = samples[name];
        check(`luz: ${name} acesa e firme`, () => {
            if (!l.ok) return 'não foi colocado';
            const min = Math.min(...s), max = Math.max(...s);
            return (min > top * 0.4 && max - min < top * 0.35) || `min ${min}, max ${max} (tocha do jogo até ${top})`;
        });
    }
}


let musicSlot = 0;
function checkMusic() {
    const box = area.musicBox;
    check('caixa de música: ligada, toca a faixa do Example Mod', () => {
        if (!box.ok) return 'não foi colocada';
        const on = tileAt(box.x, box.y).frameX >= 36;
        return (on && MusicLoader.IsMusicPlaying(musicSlot)) ||
            `ligada ${on} (frameX ${tileAt(box.x, box.y).frameX}), slot ${musicSlot}, tocando ${MusicLoader.IsMusicPlaying(musicSlot)}, curMusic ${Main.curMusic}`;
    });
}


// Os caminhos de verdade do jogo (e não o método pelo ponteiro): um hook num
// método que o jogo tem copiado dentro de outro não pega.
function checkRealPaths() {
    const p = Main.player[Main.myPlayer];
    const h = area.house;

    check('toque na cadeira (TileInteractionsCheck -> RightClick): senta', () => {
        const x = h.x0 + 6, y = h.floorY - 1;
        p.position = Vector2.new(x * 16 - 4, (h.floorY - 3) * 16);
        Terraria.Player.tileTargetX = x;
        Terraria.Player.tileTargetY = y;
        p.tileInteractAttempted = true;
        p.releaseUseTile = true;
        p.TileInteractionsCheck(x, y);
        const sitting = p.sitting.isSitting;
        if (sitting) p.sitting['void SitUp(Player player, bool multiplayerBroadcast)'](p, false);
        return sitting || 'não sentou';
    });

    check('fio (Wiring.TripWire -> HitWire): a luminária apaga', () => {
        const l = area.lights.lamp;
        const before = tileAt(l.x, l.y).frameX;
        for (let x = l.x - 3; x <= l.x; x++) tileAt(x, area.floorY - 1)['void wire(bool wire)'](true);
        Terraria.Wiring['void TripWire(int left, int top, int width, int height)'](l.x - 3, area.floorY - 1, 1, 1);
        const after = tileAt(l.x, l.y).frameX;
        return after !== before || `frameX ${before} -> ${after}`;
    });
}

function checkBuffs() {
    check('fogueira acesa perto: Main.SceneMetrics.HasCampfire', () => Main.SceneMetrics.HasCampfire === true || 'falso');
}


// O toque do jogador no tile (x, y): o caminho de verdade do RightClick.
function tap(x, y) {
    const p = Main.player[Main.myPlayer];
    Terraria.Player.tileTargetX = x;
    Terraria.Player.tileTargetY = y;
    p.tileInteractAttempted = true;
    p.releaseUseTile = true;
    p.TileInteractionsCheck(x, y);
}

let equipped = false;
function equipMusicBox() {
    const box = area.musicBox;
    tap(box.x, box.y);   // desliga a do chão
    const p = Main.player[Main.myPlayer];
    area.savedAccessory = p.armor[3].type;
    p.armor[3]['void SetDefaults(int Type, ItemVariant variant)'](ModContent.ItemType('examplemod/ExampleMusicBox'), null);
    equipped = true;
}

function checkEquipped() {
    const p = Main.player[Main.myPlayer];
    check('caixa de música equipada (acessório): toca a faixa', () =>
        (tileAt(area.musicBox.x, area.musicBox.y).frameX < 36 && MusicLoader.IsMusicPlaying(musicSlot)) ||
        `caixa do chão frameX ${tileAt(area.musicBox.x, area.musicBox.y).frameX}, tocando ${MusicLoader.IsMusicPlaying(musicSlot)}`);
    p.armor[3]['void SetDefaults(int Type, ItemVariant variant)'](area.savedAccessory, null);
}
// A porta da casa: a coluna x1, a célula do meio.
const doorCell = () => ({ x: area.house.x0 + 11, y: area.house.floorY - 2 });
const doorState = () => {
    const d = doorCell();
    return { at: bl.tiles.typeAt(d.x, d.y), left: bl.tiles.typeAt(d.x - 1, d.y), right: bl.tiles.typeAt(d.x + 1, d.y) };
};
const doorOpen = () => { const s = doorState(); return s.at === T.doorOpen || s.left === T.doorOpen || s.right === T.doorOpen; };
const doorClosed = () => doorState().at === T.door;

// Nenhuma porta do jogo (10/11) sobrou do disfarce em volta da casa.
function leakedDoors() {
    const h = area.house, out = [];
    for (let x = h.x0 - 2; x <= h.x0 + 14; x++) {
        for (let y = h.floorY - 9; y < h.floorY; y++) {
            const t = bl.tiles.typeAt(x, y);
            if (t === 10 || t === 11) out.push(`${x},${y}:${t}`);
        }
    }
    return out;
}

// O jogador parado encostado na porta (lado de fora), andando para ela: o
// DoorOpeningHelper do jogo decide pela velocidade.
function pushIntoDoor() {
    const p = Main.player[Main.myPlayer];
    const d = doorCell();
    p.position = Vector2.new((d.x + 1) * 16, area.house.floorY * 16 - p.height);
    p.velocity = Vector2.new(-2, 0);
    p.fallStart = Math.floor(p.position.Y / 16);
    p.doorHelper.AllowOpeningDoorsByVelocityAloneForATime(5);
}

// Logo depois da porta (o jogo fecha quando o jogador sai da frente dela) ou longe.
function standAway(tiles = 10) {
    const p = Main.player[Main.myPlayer];
    p.position = Vector2.new((doorCell().x + tiles) * 16, area.house.floorY * 16 - p.height);
    p.velocity = Vector2.new(0, 0);
    p.fallStart = Math.floor(p.position.Y / 16);
}

// O morador (Guia) dentro da casa, andando para a direita, até passar a porta.
const npcRun = { index: -1, opened: false, closedAfter: false };
function spawnWalker() {
    const d = doorCell();
    const newNpc = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];
    npcRun.index = newNpc(null, (d.x - 2) * 16 + 8, area.house.floorY * 16, 22, 0, 0, 0, 0, 0, 255);
    return npcRun.index >= 0 && npcRun.index < 200 || `NewNPC ${npcRun.index}`;
}

function driveWalker() {
    const n = Main.npc[npcRun.index];
    if (!n.active) return;
    n.homeless = true;
    n.ai[0] = 1;
    n.ai[1] = 300;
    n.direction = 1;
    if (doorOpen()) npcRun.opened = true;
    if (npcRun.opened && doorClosed() && n.position.X / 16 > doorCell().x + 2) npcRun.closedAfter = true;
    npcRun.frames = (npcRun.frames || 0) + 1;
    if (npcRun.frames % 60 === 1) {
        bl.log(`modfurniture morador q${npcRun.frames}: x ${(n.position.X / 16).toFixed(1)} vx ${n.velocity.X.toFixed(2)} ` +
               `ai ${n.ai[0]},${n.ai[1].toFixed(0)},${n.ai[2]},${n.ai[3]} dir ${n.direction} porta ${doorCell().x} ` +
               `noite ${!Main.dayTime} parado ${n.velocity.X === 0}`);
    }
}

function checkWire() {
    // O fio vem de 3 tiles do lado: o TripWire pula o tile onde o sinal nasce.
    const d = doorCell(), y = area.house.floorY - 1;
    const trip = () => Terraria.Wiring['void TripWire(int left, int top, int width, int height)'](d.x + 3, y, 1, 1);
    for (let x = d.x; x <= d.x + 3; x++) tileAt(x, y)['void wire(bool wire)'](true);
    trip();
    const opened = doorOpen();
    trip();
    const closed = doorClosed();
    return (opened && closed) || `abriu ${opened}, fechou ${closed} (${JSON.stringify(doorState())})`;
}

// Goblins dentro da casa, o jogador do lado de fora: o guerreiro bate até
// abrir, o peão derruba (WorldGen.KillTile com a porta disfarçada).
const raid = { index: -1, opened: false, broken: false };
function spawnGoblin(type) {
    const d = doorCell();
    if (raid.index >= 0) Main.npc[raid.index].active = false;
    const newNpc = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];
    raid.index = newNpc(null, (d.x - 3) * 16 + 8, area.house.floorY * 16, type, 0, 0, 0, 0, 0, 255);
    return raid.index >= 0 && raid.index < 200 || `NewNPC ${raid.index}`;
}

function watchRaid() {
    const p = Main.player[Main.myPlayer];
    standAway(4);
    p.statLife = p.statLifeMax2;
    p.immune = true;
    p.immuneTime = 30;
    if (doorOpen()) raid.opened = true;
    const s = doorState();
    if (![s.at, s.left, s.right].some((t) => t === T.door || t === T.doorOpen)) raid.broken = true;
}

// Os itens caídos perto da porta, por tipo.
function dropsNearDoor() {
    const d = doorCell(), out = {};
    for (let i = 0; i < Main.item.length; i++) {
        const it = Main.item[i];
        if (!it || !it.active || Math.abs(it.position.X / 16 - d.x) > 6 || Math.abs(it.position.Y / 16 - d.y) > 6) continue;
        out[it.type] = (out[it.type] || 0) + it.stack;
    }
    return out;
}

function cleanup() {
    if (npcRun.index >= 0) Main.npc[npcRun.index].active = false;
    if (raid.index >= 0) Main.npc[raid.index].active = false;
    for (let x = area.x0; x <= area.x1; x++) {
        for (let y = area.top - 1; y <= area.floorY; y++) kill(x, y);
    }
    for (let i = 0; i < Main.item.length; i++) {
        const it = Main.item[i];
        if (it && it.active && Math.abs(it.position.X / 16 - Main.spawnTileX) < 60 && it.position.Y / 16 > Main.spawnTileY + 40) it['void TurnToAir()']();
    }
    const p = Main.player[Main.myPlayer];
    p.position = Vector2.new(Main.spawnTileX * 16, (Main.spawnTileY - 3) * 16);
}

function start() {
    const type = (name) => ModContent.TileType('examplemod/' + name);
    T = {
        door: type('ExampleDoorClosed'), doorOpen: type('ExampleDoorOpen'), table: type('ExampleTable'),
        chair: type('ExampleChair'), torch: type('ExampleTorch'), campfire: type('ExampleCampfire'),
        lamp: type('ExampleLamp'), chandelier: type('ExampleChandelier'), musicBox: type('ExampleMusicBoxTile'),
    };
    check('tipos', () => Object.values(T).every((t) => t >= bl.tiles.vanillaCount) || JSON.stringify(T));
    musicSlot = MusicLoader.GetMusicSlot(ModLoader.GetMod('examplemod'), 'Music/Ropocalypse2');

    build();
    check('luzes colocadas', () => Object.values(area.lights).every((l) => l.ok) ||
        Object.entries(area.lights).map(([n, l]) => `${n} ${l.ok}`).join(', '));
    checkHouse();

    // Ligar a caixa de música com um toque.
    tap(area.musicBox.x, area.musicBox.y);
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    frames++;
    if (frames === 1) check('preparo', start);
    if (frames > 1 && frames < 329) {
        const p = Main.player[Main.myPlayer];
        p.position = Vector2.new(Main.spawnTileX * 16, (area.floorY - 3) * 16);
        p.velocity = Vector2.new(0, 0);
        p.fallStart = Math.floor(p.position.Y / 16);
    }
    if (frames >= 120 && frames < 200) sampleLights();
    if (frames === 150) {
        checkBuffs();
    }
    if (frames === 200) {
        check('luz', checkLights);
        check('música', checkMusic);
        check('caminhos do jogo', checkRealPaths);
    }
    // Um toque novo por quadro (TileUseLoader): o da cadeira foi no 200.
    if (frames === 210) check('equipar a caixa', equipMusicBox);
    if (frames === 330) check('caixa equipada', checkEquipped);

    // A porta: o jogador encosta (abre), se afasta (fecha); o morador passa; o fio.
    // Noite fixa: de dia, na superfície, o goblin (AI_003) não mira o jogador e
    // anda a esmo, e o morador (AI_007) quase não anda; o teste dependia da hora.
    if (frames > 330 && frames <= 1650) {
        Main.dayTime = false;
        if (Main.time > 30000) Main.time = 0;
    }
    if (frames > 330 && frames <= 390) pushIntoDoor();
    if (frames === 390) check('porta: abre sozinha com o jogador encostando (DoorOpeningHelper)', () =>
        doorOpen() || JSON.stringify(doorState()));
    if (frames > 390 && frames <= 450) standAway(3);
    if (frames === 450) {
        check('porta: fecha sozinha depois que o jogador passa', () => doorClosed() || JSON.stringify(doorState()));
        check('porta: nenhuma porta do jogo sobrou do disfarce', () => leakedDoors().length === 0 || leakedDoors().join(' '));
        check('morador criado', spawnWalker);
    }
    if (frames > 450 && frames <= 850) {
        standAway();
        if (npcRun.index >= 0) driveWalker();
    }
    if (frames === 850) {
        check('porta: o morador abre ao passar (AI_007_TownEntities)', () =>
            npcRun.opened || `${JSON.stringify(doorState())}, npc em ${Math.floor(Main.npc[npcRun.index].position.X / 16)}`);
        check('porta: o morador fecha depois de passar', () =>
            npcRun.closedAfter || `${JSON.stringify(doorState())}, npc em ${Math.floor(Main.npc[npcRun.index].position.X / 16)}`);
        check('porta: nenhuma porta do jogo sobrou (morador)', () => leakedDoors().length === 0 || leakedDoors().join(' '));
        check('porta: o fio abre e fecha (Wiring.HitWireSingle)', checkWire);
        Main.npc[npcRun.index].active = false;
        check('goblin guerreiro criado', () => spawnGoblin(28));
    }
    if (frames > 850 && frames <= 1250) watchRaid();
    if (frames === 1250) {
        check('porta: o goblin guerreiro arromba e abre (AI_003_Fighters + KillTile)', () =>
            raid.opened || `${JSON.stringify(doorState())}, goblin em ${Math.floor(Main.npc[raid.index].position.X / 16)}`);
        check('porta: nenhuma porta do jogo sobrou (guerreiro)', () => leakedDoors().length === 0 || leakedDoors().join(' '));
        const d = doorCell();
        if (doorOpen()) W['bool CloseDoor(int i, int j, bool forced)'](d.x, d.y, true);
        raid.broken = false;
        check('goblin peão criado', () => spawnGoblin(26));
    }
    if (frames > 1250 && frames <= 1650) watchRaid();
    if (frames === 1650) {
        const doorItem = ModContent.ItemType('examplemod/ExampleDoor');
        check('porta: o goblin peão derruba e sai o item da porta de mod', () => {
            const drops = dropsNearDoor();
            return (raid.broken && drops[doorItem] > 0 && !drops[25]) ||
                `derrubou ${raid.broken} (${JSON.stringify(doorState())}), itens ${JSON.stringify(drops)} (porta de mod ${doorItem}, de madeira 25)`;
        });
        check('porta: nenhuma porta do jogo sobrou (peão)', () => leakedDoors().length === 0 || leakedDoors().join(' '));
        done = true;
        check('limpeza', cleanup);
        bl.log('modfurniture FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('modfurniture: carregado');

export default class TestModFurniture extends Mod {}
