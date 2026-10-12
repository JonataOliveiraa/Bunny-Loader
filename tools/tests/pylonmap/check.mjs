// O toque num pilar de mod abre o mapa como o toque num pilar do jogo (o
// "modo pilar" do celular), sem o jogo. Roda com: node tools/tests/pylonmap/check.mjs
//
// A falha (relato de 2026-10-11): de um pilar de mod dava para ir só para ele
// mesmo; dos pilares do jogo, para ele. Progressão:
//   1. o RightClick do ModPylon chamava só o TryOpeningFullscreenMap, que grava
//      Main.MapPylonTile = (-1, -1): o mapa abria no modo comum, centrado no
//      jogador, com zoom 2,5;
//   2. o TeleportPylonsMapLayer.Draw do celular desenha cada pilar do jogo com
//      o DrawClamped(..., borda 10, out onScreen): fora da tela, o ícone fica
//      preso na borda e onScreen = false;
//   3. o toque num ícone com onScreen = false só desliza o mapa até ele
//      (PanTargetMapFullscreen); com true, pede o teletransporte;
//   4. o próprio pilar de mod está perto (na tela) e o ícone dele (o nosso
//      ClickIcon) pede o teletransporte: "só para ele mesmo";
//   5. o toque num pilar do jogo (TileInteractionsUse, tile 597) grava o
//      pilar em MapPylonTile e enquadra todos os pilares revelados: todos na
//      tela, inclusive o de mod, e o teletransporte sai.
// A correção: PylonLoader.OpenMap faz o mesmo que o passo 5.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
const source = path.join(root, 'app/src/main/cpp/script/js/mod');
// Sem os valores padrão dos parâmetros (a ponte escreve só tipo e nome).
const dump = fs.readFileSync(path.join(root, 'refs/dump.cs'), 'utf8').replace(/ = [^,)\n]*(?=[,)])/g, '');
let checks = 0;
function test(label, run) { run(); checks++; console.log(label + ': ok'); }

// ---- as assinaturas que a ponte exige pelo nome exato ----
const loaderSource = fs.readFileSync(path.join(source, 'Loaders/PylonLoader.js'), 'utf8');
const pylonSource = fs.readFileSync(path.join(source, 'ModPylon.js'), 'utf8');
test('toda assinatura da ponte no PylonLoader e no ModPylon existe no jogo', () => {
    const sigs = [...(loaderSource + pylonSource).matchAll(/'([\w.<>\[\]`]+ [.\w]+\((?:[^'()]|\([^)]*\))*\))'/g)].map((m) => m[1]);
    assert.ok(sigs.length >= 15, sigs.length + ' assinaturas');
    assert.deepEqual(sigs.filter((s) => !dump.includes(s + ' { }')), []);
});

// ---- o enquadramento do jogo, passo a passo (TileInteractionsUse, 597) ----
// Rectangle(x, y, 0, 0); Utils.Including por pilar revelado; Inflate(20, 20);
// mapFullscreenPos = Center.ToVector2(); o zoom diminui até caber em
// (_cameraWidth - 40) e (_cameraHeight - 40).
function vanillaFraming(origin, points, width, height, scale) {
    let r = { X: origin.x, Y: origin.y, Width: 0, Height: 0 };
    for (const p of points) {
        const x = Math.min(r.X, p.x), right = Math.max(r.X + r.Width, p.x);
        const y = Math.min(r.Y, p.y), bottom = Math.max(r.Y + r.Height, p.y);
        r = { X: x, Y: y, Width: right - x, Height: bottom - y };
    }
    r = { X: r.X - 20, Y: r.Y - 20, Width: r.Width + 40, Height: r.Height + 40 };
    const center = { X: r.X + Math.trunc(r.Width / 2), Y: r.Y + Math.trunc(r.Height / 2) };
    const fitX = Math.fround((width - 40) / r.Width);
    if (scale > fitX) scale = fitX;
    const fitY = Math.fround((height - 40) / r.Height);
    if (scale > fitY) scale = fitY;
    return { center, scale, r };
}

// ---- o sandbox do PylonLoader ----
class List {
    constructor(items = []) { this.items = items; }
    get Count() { return this.items.length; }
    get_Item(k) { return this.items[k]; }
    RemoveAt(k) { this.items.splice(k, 1); }
    Add(x) { this.items.push(x); }
}
const info = (x, y, type) => ({ PositionInTiles: { X: x, Y: y }, TypeOfPylon: type });
const log = [];
const hooks = new Map();
const hookable = (name, impl = () => undefined) => {
    const fn = (...args) => fn.impl(...args);
    fn.impl = impl;
    fn.hook = (callback, filter) => hooks.set(name, { callback, filter });
    return fn;
};
const revealed = new Set();
const key = (x, y) => x + ',' + y;
const camera = { _cameraWidth: 1600, _cameraHeight: 900 };
const Main = {
    myPlayer: 0,
    mapFullscreen: false,
    mapEnabled: true,
    mapFullscreenScale: 1,
    mapFullscreenPos: null,
    MapPylonTile: { X: -1, Y: -1 },
    PanTargetMapFullscreen: true,
    resetMapFull: false,
    spriteBatch: { name: 'Main.spriteBatch' },
    PylonSystem: { _pylons: new List() },
    player: [{
        // O do jogo (desmontado): zoom 2,5, MapPylonTile (-1, -1), mapa cheio e
        // recentrar no jogador (resetMapFull). Com o mapa desligado, nada.
        TryOpeningFullscreenMap() {
            log.push('TryOpeningFullscreenMap');
            if (!Main.mapEnabled) return;
            Main.mapFullscreenScale = 2.5;
            Main.MapPylonTile = { X: -1, Y: -1 };
            Main.mapFullscreen = true;
            Main.resetMapFull = true;
        },
    }],
};
const Layer = {
    'bool IsRevealed(TeleportPylonInfo info)': (i) => revealed.has(key(i.PositionInTiles.X, i.PositionInTiles.Y)),
    'void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color col, float width)': (sb, start, end, col, width) =>
        log.push(`line ${start.X},${start.Y}->${end.X},${end.Y} ${col} ${width} ${sb.name}`),
    'void Draw(ref MapOverlayDrawContext context, ref string text)': hookable('layer.Draw', () => log.push('vanilla Draw')),
};
const System = {
    'void UpdatePylonsListAndBroadcastChanges()': hookable('update'),
    'bool IsPlayerNearAPylon(Player player)': hookable('near', () => true),
    'void HandleTeleportRequest(TeleportPylonInfo info, int playerIndex)': hookable('request'),
    'bool DoesPositionHaveEnoughNPCs(int necessaryNPCCount, Point16 centerPoint)': hookable('npcs', () => false),
};
const errors = [];
const context = vm.createContext({
    Terraria: {
        Main,
        Map: { TeleportPylonsMapLayer: Layer },
        GameContent: {
            TeleportPylonsSystem: System,
            TeleportPylonInfo: { new: () => ({ PositionInTiles: null, TypeOfPylon: 0 }) },
            TextureAssets: { Extra: { 182: { Value: { name: 'pylon icons' } } } },
            NetModules: { NetTeleportPylonModule: { 'NetPacket SerializePylonWasAddedOrRemoved(TeleportPylonInfo info, NetTeleportPylonModule.SubPacketType packetType)': () => ({}) } },
        },
        Net: { NetManager: { Instance: { BroadcastOrLoopback: () => {} } } },
        DataStructures: {
            SpriteFrame: { new: () => ({ 'void .ctor(byte columns, byte rows, byte currentColumn, byte currentRow)'() {}, PaddingY: 2 }) },
            TileReachCheckSettings: { Pylons: {} },
        },
        ObjectData: { TileObjectData: { TopLeft: (i, j) => ({ X: i - (i % 3), Y: j - (j % 4) }) } },
        UI: { Alignment: { Center: 'Center' } },
        TileObject: { 'bool CanPlace(int x, int y, int type, int style, int dir, out TileObject objectData, bool onlyCheck, Nullable<int> forcedRandom)': hookable('canplace') },
    },
    bl: {
        hookMarks: { set() {} },
        classOf: (ns, name) => (ns === '' && name === 'XNAUnityRunner' ? { _instance: { UICameraSettings: camera } } : assert.fail('classOf ' + name)),
    },
    Point16: { new: (X, Y) => ({ X, Y }) },
    Vector2: { new: (X, Y) => ({ X, Y }) },
    Color: { Black: 'Black', White: 'White' },
    Ref: class Ref { constructor(value) { this.value = value; } },
    Hooks: {
        Once(name, run) { if (!this[name]) { this[name] = true; run(); } },
        Overrides: (cls, base, name) => cls.prototype[name] !== base.prototype[name],
    },
    Safe: { Run(label, run) { try { return run(); } catch (e) { errors.push(label + ': ' + e.stack); return undefined; } } },
    TileEntityLoader: { ByID: new Map() },
    TileLoader: { ByType: new Map(), GetItemDropFromTypeAndStyle: () => 0, At: () => null },
    ModTile: class ModTile { static register() { return 900; } },
    TEModdedPylon: class TEModdedPylon {},
    ContentLookup: {},
    ModFiles: {},
});
for (const file of ['Loaders/PylonLoader.js', 'ModPylon.js']) {
    vm.runInContext(fs.readFileSync(path.join(source, file), 'utf8'), context, { filename: file });
}
const PylonLoader = vm.runInContext('PylonLoader', context);
const ModPylon = vm.runInContext('ModPylon', context);
const TEModdedPylon = vm.runInContext('TEModdedPylon', context);

// Dois pilares de mod (tipos 11 e 12) e os ganchos instalados pelo Add.
class ExamplePylon extends ModPylon {
    Type = 900;
    ValidTeleportCheck_NPCCount() { return true; }
    DrawMapIcon(ctx, text, i) { log.push(`icon ${i.PositionInTiles.X},${i.PositionInTiles.Y}`); }
}
class OtherPylon extends ModPylon {
    Type = 901;
    DrawMapIcon(ctx, text, i) { log.push(`icon ${i.PositionInTiles.X},${i.PositionInTiles.Y}`); }
}
const example = new ExamplePylon(), other = new OtherPylon();
PylonLoader.Add(example);
PylonLoader.Add(other);

// O mundo do relato: o de exemplo em (2100, 300); do jogo, a floresta a 550
// blocos, o deserto a 900 e a selva ao norte e longe; outro de mod a 150.
const world = {
    example: { x: 2100, y: 300, type: example.PylonType },
    other: { x: 2250, y: 290, type: other.PylonType },
    forest: { x: 1550, y: 310, type: 0 },
    desert: { x: 1200, y: 330, type: 6 },
    jungle: { x: 3600, y: 420, type: 1 },
    hidden: { x: 4100, y: 900, type: 4 },   // nunca visto no mapa
};
for (const [name, p] of Object.entries(world)) {
    if (name !== 'hidden') revealed.add(key(p.x, p.y));
}
const entity = (p, pylon) => Object.assign(new TEModdedPylon(), { Position: { X: p.x, Y: p.y }, TryGetModPylon: () => pylon });
context.TileEntityLoader.ByID.set(1, entity(world.example, example));
context.TileEntityLoader.ByID.set(2, entity(world.other, other));
const list = Main.PylonSystem._pylons;
for (const name of ['forest', 'desert', 'jungle', 'hidden']) list.Add(info(world[name].x, world[name].y, world[name].type));
hooks.get('update').callback(() => {}, Main.PylonSystem);   // os de mod entram na lista
assert.deepEqual(errors, []);

test('a lista do jogo tem os pilares de mod depois dos do jogo', () => {
    assert.deepEqual(list.items.map((i) => i.TypeOfPylon), [0, 6, 1, 4, example.PylonType, other.PylonType]);
});

test('Framing é o enquadramento do jogo (Including, Inflate 20, Center, zoom só diminui)', () => {
    let seed = 7;
    const rand = (n) => (seed = (seed * 1103515245 + 12345) % 2147483648) % n;
    for (let k = 0; k < 2000; k++) {
        const origin = { x: rand(8400), y: rand(2400) };
        const points = Array.from({ length: rand(9) }, () => ({ x: rand(8400), y: rand(2400) }));
        if (rand(4) === 0) points.push({ ...origin });
        const w = 800 + rand(2000), h = 480 + rand(1200), scale = [2.5, 1, 0.3, 0.05][rand(4)];
        const ours = PylonLoader.Framing(origin, points, w, h, scale), game = vanillaFraming(origin, points, w, h, scale);
        assert.deepEqual([ours.x, ours.y, ours.width, ours.height], [game.r.X, game.r.Y, game.r.Width, game.r.Height]);
        assert.deepEqual([ours.centerX, ours.centerY], [game.center.X, game.center.Y]);
        assert.ok(Math.abs(ours.scale - game.scale) < 1e-6, `${ours.scale} vs ${game.scale}`);
        assert.ok(ours.scale <= scale);
    }
});

test('Framing: só o pilar tocado dá o quadrado de 40 em volta dele, sem mexer no zoom pequeno', () => {
    const f = PylonLoader.Framing({ x: 100, y: 50 }, [{ x: 100, y: 50 }], 1600, 900, 2.5);
    assert.deepEqual([f.x, f.y, f.width, f.height, f.centerX, f.centerY], [80, 30, 40, 40, 100, 50]);
    assert.equal(f.scale, 2.5);   // 1560 / 40 e 860 / 40 são maiores
});

// ---- o toque ----
function tap(i, j) {
    log.length = 0;
    Main.mapFullscreen = false;
    Main.PanTargetMapFullscreen = true;
    Main.MapPylonTile = { X: 7, Y: 7 };
    return new ExamplePylon().RightClick(i, j);
}

test('RightClick do ModPylon abre o mapa no modo pilar (o OpenMap)', () => {
    assert.equal(tap(2101, 302), true);   // uma célula do meio do 3x4
    assert.equal(log[0], 'TryOpeningFullscreenMap');
    assert.deepEqual(Main.MapPylonTile, { X: 2100, Y: 300 });   // o canto, não a célula tocada
    assert.equal(Main.mapFullscreen, true);
    assert.equal(Main.PanTargetMapFullscreen, false);
    assert.equal(Main.resetMapFull, false);   // senão o jogo recentra no jogador
    const revealedPoints = list.items.filter((i) => revealed.has(key(i.PositionInTiles.X, i.PositionInTiles.Y)))
        .map((i) => ({ x: i.PositionInTiles.X, y: i.PositionInTiles.Y }));
    const game = vanillaFraming({ x: 2100, y: 300 }, revealedPoints, 1600, 900, 2.5);
    assert.deepEqual(Main.mapFullscreenPos, game.center);
    assert.ok(Math.abs(Main.mapFullscreenScale - game.scale) < 1e-6);
});

test('o pilar nunca visto no mapa não entra no enquadramento', () => {
    tap(2100, 300);
    const withHidden = PylonLoader.Framing({ x: 2100, y: 300 }, list.items.map((i) => ({ x: i.PositionInTiles.X, y: i.PositionInTiles.Y })), 1600, 900, 2.5);
    assert.notDeepEqual(Main.mapFullscreenPos, { X: withHidden.centerX, Y: withHidden.centerY });
    assert.ok(Main.mapFullscreenScale > withHidden.scale);
});

test('com o mapa desligado, o toque não grava nada além do que o jogo fez', () => {
    Main.mapEnabled = false;
    Main.mapFullscreen = false;   // o toque num pilar só acontece com o mapa fechado
    Main.MapPylonTile = { X: 7, Y: 7 };
    Main.PanTargetMapFullscreen = true;
    try {
        assert.equal(PylonLoader.OpenMap(2100, 300), false);
        assert.deepEqual(Main.MapPylonTile, { X: 7, Y: 7 });
        assert.equal(Main.PanTargetMapFullscreen, true);
    } finally {
        Main.mapEnabled = true;
    }
});

// ---- a progressão da falha, com a regra do DrawClamped ----
// onScreen: o ponto do ícone (posição + (1,5; 2)) cai dentro da tela menos a
// borda de 10 px, com o mapa centrado em mapFullscreenPos no zoom dado.
function onScreen(p, center, scale, width, height) {
    const sx = width / 2 + (p.x + 1.5 - center.X) * scale, sy = height / 2 + (p.y + 2 - center.Y) * scale;
    return sx >= 10 && sx <= width - 10 && sy >= 10 && sy <= height - 10;
}
const tappable = (center, scale, w, h) => Object.fromEntries(['example', 'other', 'forest', 'desert', 'jungle']
    .map((name) => [name, onScreen(world[name], center, scale, w, h)]));

for (const [w, h, label] of [[1600, 900, 'MuMu'], [2400, 1080, 'celular 20:9']]) {
    camera._cameraWidth = w;
    camera._cameraHeight = h;
    test(`progressão (${label}): o mapa antigo, centrado no jogador, só deixa tocar no próprio pilar de mod`, () => {
        const player = { X: world.example.x + 3, Y: world.example.y };   // ao lado do pilar
        const old = tappable(player, 2.5, w, h);
        assert.equal(old.example, true);    // "exceto ele mesmo"
        assert.equal(old.forest, false);
        assert.equal(old.desert, false);
        assert.equal(old.jungle, false);
    });
    test(`progressão (${label}): o modo pilar põe todos os pilares revelados na tela`, () => {
        tap(2100, 300);
        assert.deepEqual(tappable(Main.mapFullscreenPos, Main.mapFullscreenScale, w, h),
            { example: true, other: true, forest: true, desert: true, jungle: true });
    });
}
camera._cameraWidth = 1600;
camera._cameraHeight = 900;

// ---- as linhas do modo pilar ----
function drawMap() {
    log.length = 0;
    const before = list.items.length;
    hooks.get('layer.Draw').callback((...args) => Layer['void Draw(ref MapOverlayDrawContext context, ref string text)'].impl(...args), {},
        new context.Ref({ 'Rectangle GetUnclampedDrawRegion(Texture2D texture, Vector2 position, SpriteFrame frame, float scale, Alignment alignment)': (t, pos) => ({ Center: { X: Math.round(pos.X * 10), Y: Math.round(pos.Y * 10) } }) }),
        new context.Ref(''));
    assert.equal(list.items.length, before);   // os de mod voltaram para a lista
}

test('modo pilar a partir do de mod: linha até o outro de mod, antes do desenho do jogo, e o ícone depois', () => {
    tap(2100, 300);
    drawMap();
    assert.deepEqual(log.slice(0, 3), [
        'line 21015,3020->22515,2920 Black 6 Main.spriteBatch',
        'line 21015,3020->22515,2920 White 2 Main.spriteBatch',
        'vanilla Draw',
    ]);
    assert.deepEqual(log.slice(3).sort(), ['icon 2100,300', 'icon 2250,290']);
});

test('modo pilar a partir de um do jogo: linhas até os dois de mod', () => {
    Main.mapFullscreen = true;
    Main.MapPylonTile = { X: world.forest.x, Y: world.forest.y };
    drawMap();
    assert.deepEqual(log.filter((l) => l.startsWith('line')).length, 4);
    assert.equal(log[4], 'vanilla Draw');
});

test('sem linhas no mapa comum (MapPylonTile -1) e fora do mapa cheio', () => {
    Main.mapFullscreen = true;
    Main.MapPylonTile = { X: -1, Y: -1 };
    drawMap();
    assert.equal(log.filter((l) => l.startsWith('line')).length, 0);
    Main.mapFullscreen = false;
    Main.MapPylonTile = { X: 2100, Y: 300 };
    drawMap();
    assert.equal(log.filter((l) => l.startsWith('line')).length, 0);
});

test('o pilar de mod nunca visto não ganha linha', () => {
    revealed.delete(key(world.other.x, world.other.y));
    try {
        Main.mapFullscreen = true;
        Main.MapPylonTile = { X: world.forest.x, Y: world.forest.y };
        drawMap();
        assert.equal(log.filter((l) => l.startsWith('line')).length, 2);
    } finally {
        revealed.add(key(world.other.x, world.other.y));
    }
});

// ---- o servidor, para o pilar de onde se sai ----
test('no pedido, a contagem de NPCs no pilar de mod de onde se sai vem do ValidTeleportCheck_NPCCount dele', () => {
    const npcs = hooks.get('npcs');
    assert.equal(npcs.filter.whileIn, System['void HandleTeleportRequest(TeleportPylonInfo info, int playerIndex)']);
    const original = (count, center) => `jogo ${count} ${center.X},${center.Y}`;
    assert.equal(npcs.callback(original, 2, { X: world.example.x, Y: world.example.y }), true);   // o de exemplo não pede NPCs
    assert.equal(npcs.callback(original, 2, { X: world.forest.x, Y: world.forest.y }), 'jogo 2 1550,310');
});

// ---- o toque num pilar do jogo, dentro do desenho do mapa ----
// Relato de 2026-10-11 no celular (sonda tools/tests/pylonprobe): perto só do
// de exemplo, o toque no ícone da floresta dava "não está perto o bastante de
// um pilar"; o do de exemplo (tratado depois do desenho do jogo) ia. O
// IsPlayerNearAPylon do celular percorre o _pylons, e o desenho tirava os de mod.
test('o pedido feito no meio do desenho do jogo vê os pilares de mod na lista', () => {
    const typesIn = () => list.items.map((i) => i.TypeOfPylon).sort((a, b) => a - b);
    const all = typesIn();
    const request = hooks.get('request');
    let seenInRequest = null, seenAfter = null;
    const draw = Layer['void Draw(ref MapOverlayDrawContext context, ref string text)'];
    const vanillaDraw = draw.impl;
    draw.impl = () => {
        // o toque no ícone da floresta: RequestTeleportation -> HandleTeleportRequest
        request.callback(() => { seenInRequest = typesIn(); }, Main.PylonSystem, info(world.forest.x, world.forest.y, 0), 0);
        seenAfter = typesIn();
    };
    try {
        Main.mapFullscreen = true;
        Main.MapPylonTile = { X: world.example.x, Y: world.example.y };
        drawMap();
    } finally {
        draw.impl = vanillaDraw;
    }
    assert.deepEqual(seenInRequest, all);
    assert.deepEqual(seenAfter, [0, 1, 4, 6]);   // fora de novo para o resto do desenho
    assert.deepEqual(typesIn(), all);
});

test('o pedido fora do desenho (o servidor, o ícone de mod) não mexe na lista', () => {
    const before = list.items.map((i) => i.TypeOfPylon);
    let seen = null;
    hooks.get('request').callback(() => { seen = list.items.map((i) => i.TypeOfPylon); }, Main.PylonSystem, info(world.forest.x, world.forest.y, 0), 0);
    assert.deepEqual(seen, before);
    assert.deepEqual(list.items.map((i) => i.TypeOfPylon), before);
});

assert.deepEqual(errors, []);
console.log(checks + ' checagens do mapa de pilares passaram.');
