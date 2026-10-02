import { Cell, createTemplePlan } from './temple-plan.js';

// JsHook.cpp executa o original quando o callback lança. Contenha aqui todo
// erro, inclusive os anteriores à primeira escrita, para suprimir makeTemple.
export function buildTemple(ox, oy) {
    try {
        applyTemple(ox, oy);
        return true;
    } catch (error) {
        try {
            bl.log('Templo Pirâmide: geração interrompida (' + error + '); original suprimido.');
        } catch (_) {}
        return false;
    }
}

function applyTemple(ox, oy) {
    const M = Terraria.Main, W = Terraria.WorldGen, GV = Terraria.WorldBuilding.GenVars;
    const rng = W.genRand;
    const plan = createTemplePlan({
        worldWidth: M.maxTilesX, worldHeight: M.maxTilesY,
        minY: M.worldSurface + 80, maxY: M.UnderworldLayer - 35,
        originX: ox, originY: oy,
        rand: (a, b) => rng['int Next(int minValue, int maxValue)'](a, b + 1)
    });

    // Resolva as assinaturas antes da primeira escrita no mundo.
    const tiles = M.tile;
    const addAltar = W['void AddLihzahrdAltar(int x, int y)'];
    const addChest = W['bool AddBuriedChest(int i, int j, int mainItemInChest, bool notNearOtherChests, int chestStyle, bool trySlope, ushort chestTileType)'];
    const addWire = W['bool AddWireFromPointToPoint(int x, int y, int x2, int y2, int wireColor, bool debugPaint)'];
    const at = (x, y) => tiles['Tile get_Item(int x, int y)'](x + plan.dx, y + plan.dy);
    const { grid, width, height, left, cell } = plan;
    bl.log(`Templo Pirâmide: construindo ${width}x${height}, ${plan.levels} andares...`);
    for (let y = 0; y < height; y++) {
        for (let col = 0; col < width; col++) {
            const c = grid[y * width + col];
            if (c === Cell.OUTSIDE) continue;
            const x = left + col, t = at(x, y);
            t.ClearEverything();
            if (c === Cell.BRICK) {
                t.sTileHeader = 0x20;
                t.type = 226; // TileID.LihzahrdBrick, confirmado no dump local
            } else if (c === Cell.SPIKES) {
                t.sTileHeader = 0x20;
                t.type = 232;
            }
            if (c === Cell.AIR || (cell(x - 1, y) && cell(x + 1, y) && cell(x, y - 1) && cell(x, y + 1))) {
                t.wall = 87; // WallID.LihzahrdBrickUnsafe
            }
        }
    }

    GV.lAltarX = plan.altar.x + plan.dx;
    GV.lAltarY = plan.altar.y + plan.dy;
    GV.tLeft = plan.bounds.left + plan.dx;
    GV.tRight = plan.bounds.right + plan.dx;
    GV.tTop = plan.dy;
    GV.tBottom = height - 1 + plan.dy;
    GV.tRooms = plan.roomCount;

    const d = plan.door;
    // Mesmo enquadramento de WorldGen.PlaceDoor no binário local:
    // style % 36 escolhe a linha de 54 pixels; style / 36 escolhe a coluna.
    // A variante visual 0 é válida. O vão e os apoios já vêm da planta.
    for (let n = 0; n < 3; n++) {
        const t = at(d.x, d.y - 1 + n);
        t.ClearEverything();
        t.sTileHeader = 0x20;
        t.type = 10;
        t.frameX = Math.floor(d.style / 36) * 54;
        t.frameY = (d.style % 36) * 54 + n * 18;
        t.wall = 87;
    }
    // O método nativo monta os seis tiles do altar, preservando a parede.
    const a = plan.altar;
    addAltar(a.x + plan.dx, a.y + plan.dy);
    for (let y = 0; y < 2; y++) {
        for (let x = 0; x < 3; x++) {
            const t = at(a.x + x, a.y + y);
            if (!(t.sTileHeader & 0x20) || t.type !== 237 || t.frameX !== x * 18 || t.frameY !== y * 18) {
                throw new Error('Templo Pirâmide: altar incompleto');
            }
        }
    }
    // Mesma chamada de templePart2: baú estilo 16 com Célula de Energia 1293.
    // A rotina nativa cria o registro Chest e preenche o restante do loot.
    let chestCount = 0;
    const chestLevels = new Set();
    for (const c of plan.chests) {
        for (const x of [c.x, c.x + 2, c.x - 2, c.x + 4, c.x - 4]) {
            if (x - 1 < c.x0 + 2 || x > c.x1 - 2) continue;
            if ([x - 1, x].some(cx => at(cx, c.floor).type !== 226 || !(at(cx, c.floor).sTileHeader & 0x20)
                || (at(cx, c.floor - 1).sTileHeader & 0x20) || (at(cx, c.floor - 2).sTileHeader & 0x20))) continue;
            if (addChest(x + plan.dx, c.y + plan.dy, 1293, true, 16, false, 0)) {
                chestCount++; chestLevels.add(c.level);
                break;
            }
        }
    }
    if (chestCount !== plan.chests.length || chestLevels.size !== plan.levels) {
        bl.log(`Templo Pirâmide: baús colocados ${chestCount}/${plan.chests.length}, em ${chestLevels.size}/${plan.levels} andares.`);
    }

    const singleTile = (p, type, frameX, frameY) => {
        const t = at(p.x, p.y);
        t.sTileHeader = t.sTileHeader | 0x20;
        t.type = type; t.frameX = frameX; t.frameY = frameY; t.wall = 87;
    };
    let trapCount = 0;
    for (const trap of plan.traps) {
        singleTile(trap.plate, 135, 0, 6 * 18);
        singleTile(trap.emitter, 137, trap.emitter.frameX, trap.emitter.style * 18);
        if (!addWire(trap.plate.x + plan.dx, trap.plate.y + plan.dy,
            trap.emitter.x + plan.dx, trap.emitter.y + plan.dy, trap.color, false)) {
            throw new Error('Falha ao ligar um circuito de armadilha');
        }
        trapCount++;
    }
    bl.log(`Templo Pirâmide: ${plan.levels} andares, ${plan.roomCount} câmaras; ` +
        `arena ${plan.arena.x1 - plan.arena.x0 + 1}x${plan.arena.floor - plan.arena.top}; ` +
        `${chestCount} baús, ${trapCount} circuitos; altar ${GV.lAltarX},${GV.lAltarY}; porta trancada; original suprimido.`);
}
