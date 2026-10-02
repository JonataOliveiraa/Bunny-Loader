import assert from 'node:assert/strict';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';
import vm from 'node:vm';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const content = resolve(root, 'samples/TemploPiramide/content');
// Carrega os mesmos módulos ES do QuickJS sem exigir package.json no mod.
const planModule = await import('data:text/javascript;base64,' + Buffer.from(readFileSync(resolve(content, 'temple-plan.js'))).toString('base64'));
const { Cell, createTemplePlan } = planModule;
const runtimeSource = readFileSync(resolve(content, 'temple-runtime.js'), 'utf8')
    .replace("import { Cell, createTemplePlan } from './temple-plan.js';", '');
function random(seed) {
    let state = seed >>> 0;
    return (a, b) => {
        state = (Math.imul(state, 1664525) + 1013904223) >>> 0;
        return a + Math.floor(state / 4294967296 * (b - a + 1));
    };
}
// Posição é o canto superior esquerdo de um jogador de 2x3 tiles.
function reachable(p, locked = false) {
    const blocked = (x, y) => locked && x === p.door.x && y >= p.door.y - 1 && y <= p.door.y + 1;
    const fits = (x, y) => {
        for (let dy = 0; dy < 3; dy++) for (let dx = 0; dx < 2; dx++) {
            if (p.cell(x + dx, y + dy) !== Cell.AIR || blocked(x + dx, y + dy)) return false;
        }
        return true;
    };
    const seen = new Uint8Array(p.grid.length);
    const queue = new Int32Array(p.grid.length);
    const sx = p.entrance.side > 0 ? p.entrance.x - 1 : p.entrance.x;
    const sy = p.entrance.y - 1;
    assert(fits(sx, sy), 'entrada deve comportar o jogador');
    const start = sy * p.width + sx - p.left;
    let head = 0, tail = 1;
    queue[0] = start; seen[start] = 1;
    while (head < tail) {
        const index = queue[head++], x = index % p.width + p.left, y = Math.floor(index / p.width);
        for (const [nx, ny] of [[x - 1, y], [x + 1, y], [x, y - 1], [x, y + 1]]) {
            const n = ny * p.width + nx - p.left;
            if (n >= 0 && n < seen.length && !seen[n] && fits(nx, ny)) {
                seen[n] = 1; queue[tail++] = n;
            }
        }
    }
    return (x, y) => !!seen[y * p.width + x - p.left];
}

let plans = 0;
let preview;
for (const [worldWidth, worldHeight, maxY] of [[4200, 1200, 965], [6400, 1800, 1565], [8400, 2400, 2165]]) {
    for (let seed = 1; seed <= 40; seed++) {
        const options = { worldWidth, worldHeight, minY: 350, maxY, originX: seed % 2 ? 0 : worldWidth,
            originY: seed % 3 ? maxY : 0, rand: random(seed) };
        const p = createTemplePlan(options);
        assert(p.dx + p.left >= 50);
        assert(p.dx + p.left + p.width - 1 < worldWidth - 50);
        assert(p.dy >= options.minY && p.dy + p.height - 1 <= maxY);
        const open = reachable(p), closed = reachable(p, true);
        for (const row of p.rows) for (const room of row.rooms) {
            const x = Math.floor((room.x0 + room.x1) / 2), y = room.floor - 7;
            assert(open(x, y), `câmara inacessível: mundo ${worldWidth}, seed ${seed}, ${x},${y}`);
            assert(!closed(x, y), 'porta trancada deve bloquear todas as câmaras');
        }
        for (let y = 0; y < 2; y++) for (let x = 0; x < 3; x++) assert.equal(p.cell(p.altar.x + x, p.altar.y + y), Cell.AIR);
        for (let x = 0; x < 3; x++) assert.equal(p.cell(p.altar.x + x, p.altar.y + 2), Cell.BRICK);
        for (let i = 1; i < p.rows.length - 1; i++) {
            assert(p.rows[i].floor - p.rows[i - 1].floor >= 64);
            assert(p.rows[i].rooms[0].top - 5 - p.rows[i - 1].floor >= 20);
        }
        assert.equal(p.stairs.length, p.rows.length - 1);
        const rooms = p.rows.flatMap(r => r.rooms).filter(r => r !== p.arena);
        assert.equal(new Set(rooms.map(r => r.kind)).size, 8, 'oito tipos de câmaras');
        assert(new Set(rooms.map(r => r.x1 - r.x0)).size >= 5, 'larguras variadas');
        assert(new Set(rooms.map(r => r.floor - r.top)).size >= 5, 'alturas variadas');
        assert(p.grid.includes(Cell.SPIKES), 'fossos com espinhos');
        assert.equal(p.chests.length, Math.ceil(p.roomCount * 0.35));
        assert.equal(new Set(p.chests.map(c => c.level)).size, p.levels, 'baús em todos os andares');
        for (const c of p.chests) for (const x of [c.x - 1, c.x]) {
            assert.equal(p.cell(x, c.floor), Cell.BRICK);
            assert.equal(p.cell(x, c.floor - 1), Cell.AIR);
            assert.equal(p.cell(x, c.floor - 2), Cell.AIR);
        }
        assert(p.traps.length >= p.roomCount, 'armadilhas como parte principal');
        assert.equal(new Set(p.traps.map(t => t.level)).size, p.levels);
        assert(new Set(p.traps.map(t => t.emitter.style)).size >= 3);
        for (const t of p.traps) {
            assert.equal(p.cell(t.plate.x, t.plate.y), Cell.AIR);
            assert.equal(p.cell(t.plate.x, t.plate.y + 1), Cell.BRICK);
            assert.equal(p.cell(t.emitter.x, t.emitter.y), Cell.BRICK);
            assert(t.emitter.style >= 1 && t.emitter.style <= 4);
            for (const c of p.chests) assert(!(Math.abs(t.plate.x - c.x) < 5 && Math.abs(t.plate.y + 1 - c.floor) < 5));
        }
        assert.equal(p.exteriorMarginScale, 0.62);
        assert(p.bounds.right - p.bounds.left + 1 < 2 * (10 + Math.floor((p.height - 1) * 0.91)) + 1);
        for (let y = 1; y < p.height; y++) {
            const minimum = 10 + p.requiredSlope * y;
            const original = 10 + Math.floor(y * 0.91);
            assert.equal(p.outerHalf[y], Math.ceil(minimum + 0.62 * (original - minimum)));
        }
        // O piso escavado não pode exigir saltos maiores que dois tiles.
        for (const s of p.stairs) {
            const floors = [];
            for (let x = Math.min(s.x0, s.x1); x <= Math.max(s.x0, s.x1); x++) {
                let bottom = s.y0 - 1;
                for (let y = s.y0; y < s.y1; y++) if (p.cell(x, y) === Cell.AIR) bottom = y;
                floors.push(bottom);
            }
            for (let i = 1; i < floors.length; i++) assert(Math.abs(floors[i] - floors[i - 1]) <= 2, 'degrau alto demais');
        }
        if (seed === 1) {
            const copy = createTemplePlan({ ...options, rand: random(seed) });
            assert.deepEqual(p.grid, copy.grid, 'mesma sequência aleatória produz a mesma planta');
            if (worldWidth === 6400) preview = p;
        }
        plans++;
    }
}
for (const choose of [(a, b) => a, (a, b) => b]) {
    for (const w of [4200, 6400, 8400]) {
        const p = createTemplePlan({ worldWidth: w, worldHeight: 2400, minY: 300, maxY: 2000, originX: 2000, originY: 700, rand: choose });
        assert(reachable(p)(0, p.arena.floor - 8));
    }
}
const shallow = createTemplePlan({ worldWidth: 4200, worldHeight: 1200, minY: 700, maxY: 965, originX: 3000, originY: 900, rand: random(1) });
assert(shallow.levels < 5);
assert.throws(() => createTemplePlan({ worldWidth: 4200, worldHeight: 1200, minY: 900, maxY: 965, originX: 3000, originY: 900, rand: random(1) }), /profundidade/);
assert.throws(() => createTemplePlan({ worldWidth: NaN, worldHeight: 1200, minY: 300, maxY: 965, originX: 3000, originY: 900, rand: random(1) }), /inválidas/);

// Ponte simulada estrita: testa assinatura, escrita, porta, altar e GenVars.
globalThis.__templeCell = Cell;
globalThis.__templePlan = createTemplePlan;
// A integração é importada após disponibilizar suas dependências.
const { buildTemple } = await import('data:text/javascript;base64,' + Buffer.from(
    `const Cell = globalThis.__templeCell; const createTemplePlan = globalThis.__templePlan;\n${runtimeSource}\n// integration`
).toString('base64'));
function gameMock(boundMethods = true) {
    const data = new Map();
    const stats = { writes: 0, fallbacks: 0, hooks: [], logs: [], chests: [], circuits: [] };
    const get = (x, y) => {
        assert(x >= 0 && x < 4200 && y >= 0 && y < 1200, 'acesso fora do mundo');
        const key = `${x},${y}`;
        if (!data.has(key)) data.set(key, { type: 1, wall: 0, sTileHeader: 0x20, liquid: 255, frameX: 0, frameY: 0,
            ClearEverything() { stats.writes++; this.type = 0; this.wall = 0; this.sTileHeader = 0; this.liquid = 0; this.frameX = 0; this.frameY = 0; }
        });
        return data.get(key);
    };
    // Como gm_call com MethodRef.owner: a instância já está vinculada.
    // Um objeto passado novamente vira o argumento x e deve ser recusado.
    const tileData = { 'Tile get_Item(int x, int y)': (x, y) => {
        assert.equal(typeof x, 'number', 'System.Int32 espera um número para x');
        assert.equal(typeof y, 'number', 'System.Int32 espera um número para y');
        return get(x, y);
    } };
    if (!boundMethods) {
        tileData['Tile get_Item(int x, int y)'] = function (x, y) {
            assert.equal(this, tileData, 'método de instância exige o receptor tiles');
            assert.equal(typeof x, 'number'); assert.equal(typeof y, 'number');
            return get(x, y);
        };
    }
    const rng = random(42);
    const world = {
        genRand: { 'int Next(int minValue, int maxValue)': (a, b) => rng(a, b - 1) },
        'void AddLihzahrdAltar(int x, int y)': (x, y) => {
            for (let i = 0; i < 3; i++) {
                assert.equal(get(x + i, y + 2).type, 226);
                for (let j = 0; j < 2; j++) Object.assign(get(x + i, y + j), { type: 237, sTileHeader: 0x20, frameX: i * 18, frameY: j * 18 });
            }
        },
        'bool AddBuriedChest(int i, int j, int mainItemInChest, bool notNearOtherChests, int chestStyle, bool trySlope, ushort chestTileType)': (x, y, item, near, style, slope, type) => {
            assert.equal(item, 1293); assert.equal(style, 16); assert.equal(near, true);
            assert.equal(slope, false); assert.equal(type, 0);
            while (!(get(x, y).sTileHeader & 0x20)) y++;
            for (const cx of [x - 1, x]) {
                assert.equal(get(cx, y).type, 226);
                assert.equal(get(cx, y - 1).sTileHeader & 0x20, 0);
                assert.equal(get(cx, y - 2).sTileHeader & 0x20, 0);
            }
            // Chest.NearOtherChests: varredura de 50x16 tiles.
            if (stats.chests.some(c => Math.abs(c.x - (x - 1)) < 25 && Math.abs(c.y - (y - 2)) < 8)) return false;
            for (let i = 0; i < 2; i++) for (let j = 0; j < 2; j++) {
                Object.assign(get(x - 1 + i, y - 2 + j), { type: 21, sTileHeader: 0x20, frameX: style * 36 + i * 18, frameY: j * 18 });
            }
            stats.chests.push({ x: x - 1, y: y - 2, item: { type: item, stack: 1 } });
            return true;
        },
        'bool AddWireFromPointToPoint(int x, int y, int x2, int y2, int wireColor, bool debugPaint)': (x, y, x2, y2, color, debug) => {
            const plate = get(x, y), emitter = get(x2, y2);
            assert.equal(plate.type, 135); assert.equal(plate.frameY, 108);
            assert.equal(emitter.type, 137);
            assert(emitter.frameY >= 18 && emitter.frameY <= 72);
            assert(color >= 0 && color <= 2); assert.equal(debug, false);
            const circuit = { x, y, x2, y2, color, style: emitter.frameY / 18 };
            const wire = (a, b) => { get(a, b).wires = (get(a, b).wires || 0) | (1 << color); };
            wire(x2, y2);
            while (x !== x2 || y !== y2) {
                x2 += Math.sign(x - x2); wire(x2, y2);
                y2 += Math.sign(y - y2); wire(x2, y2);
            }
            stats.circuits.push(circuit);
            return true;
        },
        'void makeTemple(int x, int y, GenerationProgress progress)': { hook(cb) { stats.hooks.push(cb); } }
    };
    Object.defineProperty(world, 'bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)', {
        get() { throw new Error('PlaceTile indisponível: a porta deve ser montada diretamente'); }
    });
    const Terraria = { Main: { maxTilesX: 4200, maxTilesY: 1200, worldSurface: 250, UnderworldLayer: 1000, tile: tileData },
        WorldGen: world, WorldBuilding: { GenVars: {} } };
    return { Terraria, bl: { log: s => stats.logs.push(s) }, stats, data, get };
}
const mock = gameMock();
const boundGetTile = mock.Terraria.Main.tile['Tile get_Item(int x, int y)'];
assert.throws(() => boundGetTile(mock.Terraria.Main.tile, 0, 0), /System.Int32/);
globalThis.Terraria = mock.Terraria; globalThis.bl = mock.bl;
assert.equal(buildTemple(3500, 850, () => mock.stats.fallbacks++), true);
assert.equal(mock.stats.fallbacks, 0);
assert(mock.stats.writes > 10000);
const gv = mock.Terraria.WorldBuilding.GenVars;
assert.equal(mock.get(gv.lAltarX, gv.lAltarY).type, 237);
assert.equal(mock.get(gv.lAltarX, gv.lAltarY).wall, 87);
assert(gv.tLeft >= 50 && gv.tBottom <= 965 && gv.tRooms > 20);
assert.equal(mock.stats.chests.length, Math.ceil(gv.tRooms * 0.35));
assert(mock.stats.circuits.length >= gv.tRooms);
assert.equal(new Set(mock.stats.circuits.map(c => c.style)).size, 4);
for (const c of mock.stats.chests) {
    assert.equal(c.item.type, 1293); assert(c.item.stack > 0);
    assert.equal(mock.get(c.x, c.y).type, 21, 'circuitos não sobrescrevem baús');
}
for (const c of mock.stats.circuits) {
    assert(mock.get(c.x, c.y).wires & (1 << c.color));
    assert(mock.get(c.x2, c.y2).wires & (1 << c.color));
}
const doors = [...mock.data.values()].filter(t => t.type === 10);
assert.equal(doors.length, 3);
assert.deepEqual(doors.map(t => t.frameY).sort((a, b) => a - b), [594, 612, 630]);
for (const tile of mock.data.values()) assert.equal(tile.liquid, 0);
const explicitReceiver = gameMock(false);
globalThis.Terraria = explicitReceiver.Terraria; globalThis.bl = explicitReceiver.bl;
buildTemple(3500, 850, () => explicitReceiver.stats.fallbacks++);
assert.equal(explicitReceiver.stats.fallbacks, 0);
assert(explicitReceiver.stats.writes > 10000);
const empty = gameMock();
empty.Terraria.Main.UnderworldLayer = 420;
globalThis.Terraria = empty.Terraria; globalThis.bl = empty.bl;
assert.equal(buildTemple(3500, 850, () => empty.stats.fallbacks++), false);
assert.equal(empty.stats.fallbacks, 0); assert.equal(empty.stats.writes, 0);

// Mesmo comportamento de JsHook.cpp: exceção escapando chamaria o original.
function dispatch(cb, stats, ...args) {
    try { cb(() => stats.fallbacks++, ...args); } catch (_) { stats.fallbacks++; }
}

const editor = readFileSync(resolve(root, 'tools/editor/templo.js'), 'utf8');
const session = gameMock();
const ctx = vm.createContext({ Terraria: session.Terraria, bl: session.bl });
vm.runInContext(editor, ctx); vm.runInContext(editor, ctx);
assert.equal(session.stats.hooks.length, 1, 'reexecutar Editor não duplica hook');
dispatch(session.stats.hooks[0], session.stats, 3500, 850, null);
assert.equal(session.stats.fallbacks, 0);
assert(session.stats.writes > 10000);
session.Terraria.WorldGen['void AddLihzahrdAltar(int x, int y)'] = () => { throw new Error('falha nativa após escrever'); };
dispatch(session.stats.hooks[0], session.stats, 3500, 850, null);
assert.equal(session.stats.fallbacks, 0, 'erro após a construção não aciona original automático');
session.Terraria.Main.UnderworldLayer = 420;
dispatch(session.stats.hooks[0], session.stats, 3500, 850, null);
assert.equal(session.stats.fallbacks, 0, 'erro antes da construção também não aciona original');
session.bl.log = () => { throw new Error('falha no logger'); };
dispatch(session.stats.hooks[0], session.stats, 3500, 850, null);
assert.equal(session.stats.fallbacks, 0);
// Sessão que já contém o hook do script antigo recebe o novo gerador.
const oldSession = gameMock();
const oldCtx = vm.createContext({ Terraria: oldSession.Terraria, bl: oldSession.bl, templeHooked: true, buildTemple: () => {} });
vm.runInContext(editor, oldCtx);
assert.equal(oldSession.stats.hooks.length, 0);
oldCtx.buildTemple(3500, 850, () => oldSession.stats.fallbacks++);
assert(oldSession.stats.writes > 10000);
// O callback antigo tinha catch + original(). buildTemple contém o erro.
oldSession.Terraria.Main.UnderworldLayer = 420;
const legacyCallback = original => {
    try { oldCtx.buildTemple(3500, 850); } catch (_) { original(); }
};
dispatch(legacyCallback, oldSession.stats);
assert.equal(oldSession.stats.fallbacks, 0);

const out = resolve(root, 'out'); mkdirSync(out, { recursive: true });
writeFileSync(resolve(out, 'temple-plan.json'), JSON.stringify({ ...preview, grid: Array.from(preview.grid) }));
console.log(`${plans + 6} plantas verificadas: limites, acesso 2x3, porta, altar, degraus e determinismo.`);
console.log('Integração simulada: porta direta, baús com células, quatro tipos de circuito e zero original, inclusive nas falhas.');
console.log(`Prévia: ${preview.levels} andares, ${preview.roomCount} câmaras, ${preview.chests.length} baús, ${preview.traps.length} circuitos, contorno ${preview.bounds.right - preview.bounds.left + 1}x${preview.height}.`);
