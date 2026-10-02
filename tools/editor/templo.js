// Templo Pirâmide: execute no Editor e CRIE UM MUNDO NOVO sem fechar o jogo.
// Gerado por tools/temple/package.py a partir de samples/TemploPiramide/content/.
// Execute apenas o script OU o pacote do mod. Reexecutar atualiza o desenho.
// Compatível com o hook do script anterior: buildTemple e templeHooked.
(() => {
// Planta independente do jogo: coordenadas locais, em tiles.
const Cell = Object.freeze({ OUTSIDE: 0, BRICK: 1, AIR: 2, SPIKES: 3 });

function createTemplePlan({ worldWidth, worldHeight, minY, maxY, originX, originY, rand }) {
    for (const n of [worldWidth, worldHeight, minY, maxY, originX, originY]) {
        if (!Number.isFinite(n)) throw new Error('Dimensões do mundo inválidas');
    }
    if (typeof rand !== 'function') throw new Error('Gerador aleatório ausente');
    const scale = Math.max(1, Math.min(2, worldWidth / 4200));
    const spacing = Math.round(64 + (scale - 1) * 6);
    const arenaHeight = Math.round(58 + (scale - 1) * 10);
    const arenaWidth = Math.round(170 + (scale - 1) * 40);
    const shell = Math.round(12 * 0.62);
    const firstFloor = 64;
    let levels = worldWidth >= 8000 ? 6 : worldWidth >= 6000 ? 5 : 4;
    minY = Math.max(50, Math.ceil(minY));
    maxY = Math.min(worldHeight - 50, Math.floor(maxY));
    const heightFor = n => firstFloor + (n - 1) * spacing + 36 + arenaHeight + shell + 1;
    while (levels > 2 && heightFor(levels) > maxY - minY + 1) levels--;
    const height = heightFor(levels);
    if (height > maxY - minY + 1) throw new Error('Não há profundidade para a pirâmide');
    const halfAt = y => 10 + Math.floor(y * 0.91);
    const halfBase = halfAt(height - 1);
    const width = 2 * (halfBase + 14) + 1;
    if (width > worldWidth - 100) throw new Error('Não há largura para a pirâmide');
    const left = -(width >> 1);
    const grid = new Uint8Array(width * height);
    const cell = (x, y) => x < left || x >= left + width || y < 0 || y >= height
        ? Cell.OUTSIDE : grid[y * width + x - left];
    const paint = (x0, y0, x1, y1, value, allowOutside = false) => {
        if (x0 > x1 || y0 > y1) throw new Error('Retângulo invertido na planta');
        for (let y = y0; y <= y1; y++) {
            for (let x = x0; x <= x1; x++) {
                if (x < left || x >= left + width || y < 0 || y >= height) {
                    throw new Error('Desenho ultrapassou a grade');
                }
                if (!allowOutside && cell(x, y) === Cell.OUTSIDE) {
                    throw new Error('Câmara ultrapassou a casca da pirâmide');
                }
                grid[y * width + x - left] = value;
            }
        }
    };
    for (let y = 0; y < height; y++) {
        paint(-halfAt(y), y, halfAt(y), y, Cell.BRICK, true);
    }
    const air = (x0, y0, x1, y1) => paint(x0, y0, x1, y1, Cell.AIR);
    const brick = (x0, y0, x1, y1) => paint(x0, y0, x1, y1, Cell.BRICK);
    const rows = [];
    const entranceSide = rand(0, 1) ? 1 : -1;
    const makeRoom = (x0, w, floor, h, kind) => ({ x0, x1: x0 + w - 1, floor, top: floor - h, kind });
    let kinds = [], previousKind = -1;
    const nextKind = () => {
        if (!kinds.length) {
            kinds = [0, 1, 2, 3, 4, 5, 6, 7];
            for (let n = kinds.length - 1; n > 0; n--) {
                const j = rand(0, n);
                [kinds[n], kinds[j]] = [kinds[j], kinds[n]];
            }
            if (kinds[0] === previousKind) [kinds[0], kinds[1]] = [kinds[1], kinds[0]];
        }
        previousKind = kinds.shift();
        return previousKind;
    };
    const roomHeight = kind => kind === 4 ? rand(29, 34) : kind === 5 || kind === 6 ? rand(14, 18) : rand(20, 27);

    for (let i = 0; i < levels; i++) {
        const floor = firstFloor + i * spacing;
        const count = 1 + i * 2;
        const rooms = [];
        const sizes = [];
        let gaps = 0, weights = 0;
        for (let k = 0; k < count; k++) {
            const kind = nextKind(), gap = k === count - 1 ? 0 : rand(12, 17);
            const weight = kind === 2 || kind === 6 ? 3 : kind === 4 || kind === 7 ? 2 : 1;
            sizes.push({ gap, weight, h: i === 0 ? rand(20, 24) : roomHeight(kind), kind });
            gaps += gap; weights += weight;
        }
        // A largura total cresce regularmente para que as descidas continuem
        // suaves, mas cada câmara recebe uma largura conforme sua função.
        const total = i === 0 ? rand(32, 42) : rand(130 + (i - 1) * 100, 150 + (i - 1) * 100);
        const extra = total - gaps - count * 24;
        let assigned = gaps;
        for (const s of sizes) { s.w = 24 + Math.floor(extra * s.weight / weights); assigned += s.w; }
        for (let n = 0; assigned < total; n++, assigned++) sizes[n % count].w++;
        let x = -Math.floor(total / 2);
        for (let k = 0; k < sizes.length; k++) {
            const s = sizes[k];
            const offset = k === 0 || k === count - 1 ? 0 : s.kind === 2 ? 4 : s.kind === 7 ? -3 : 0;
            rooms.push(makeRoom(x, s.w, floor + offset, s.h, s.kind));
            x += s.w + s.gap;
        }
        rows.push({ floor, rooms, left: rooms[0].x0, right: rooms[rooms.length - 1].x1 });
    }

    const arenaFloor = height - shell - 1;
    const arena = makeRoom(-Math.floor(arenaWidth / 2), arenaWidth, arenaFloor, arenaHeight, -1);
    const wings = levels - 1;
    const finalRooms = [];
    for (let k = wings; k >= 1; k--) {
        const kind = nextKind();
        finalRooms.push(makeRoom(arena.x0 - k * 51, rand(30, 40), arenaFloor, roomHeight(kind), kind));
    }
    finalRooms.push(arena);
    for (let k = 0; k < wings; k++) {
        const kind = nextKind();
        finalRooms.push(makeRoom(arena.x1 + 16 + k * 51, rand(30, 40), arenaFloor, roomHeight(kind), kind));
    }
    rows.push({ floor: arenaFloor, rooms: finalRooms, left: finalRooms[0].x0, right: finalRooms[finalRooms.length - 1].x1 });

    const carveRoom = room => {
        air(room.x0, room.top, room.x1, room.floor - 1);
        const wantedSteps = room === arena ? 7 : room.kind === 5 || room.kind === 6 ? 0 : room.kind === 4 ? 5 : 3;
        const inset = room === arena ? 8 : 3;
        const steps = Math.min(wantedSteps, Math.floor((room.x1 - room.x0 - 2) / (2 * inset)));
        for (let n = 1; n <= steps; n++) air(room.x0 + n * inset, room.top - n, room.x1 - n * inset, room.top - n);
    };
    for (const row of rows) {
        for (let i = 0; i < row.rooms.length; i++) {
            const r = row.rooms[i];
            carveRoom(r);
            if (i > 0) {
                const prev = row.rooms[i - 1], distance = r.x0 - prev.x1;
                for (let x = prev.x1 + 1; x < r.x0; x++) {
                    const floor = Math.round(prev.floor + (r.floor - prev.floor) * (x - prev.x1) / distance);
                    air(x, floor - 7, x, floor - 1);
                }
            }
        }
    }

    // Corredores diagonais escavados: o tijolo logo abaixo forma degraus.
    // A abertura de 9 tiles evita poços de queda livre e permite o retorno.
    const stairs = [];
    for (let i = 0; i < rows.length - 1; i++) {
        const a = rows[i], b = rows[i + 1];
        const side = i % 2 === 0 ? -entranceSide : entranceSide;
        const x0 = side > 0 ? a.right - 4 : a.left + 4;
        const x1 = side > 0 ? b.right - 4 : b.left + 4;
        for (let y = a.floor; y <= b.floor; y++) {
            const x = Math.round(x0 + (x1 - x0) * (y - a.floor) / (b.floor - a.floor));
            air(x - 4, y - 8, x + 4, y - 1);
        }
        stairs.push({ x0, y0: a.floor, x1, y1: b.floor, side });
    }

    // Enfeites altos deixam pelo menos oito tiles livres acima do piso.
    for (const row of rows) {
        for (const r of row.rooms) {
            if (r === arena) continue;
            if (r.kind === 0) {
                for (let x = r.x0 + 7; x <= r.x1 - 7; x += 11) {
                    brick(x, r.top, x + 1, r.floor - 10);
                    brick(x - 1, r.floor - 10, x + 2, r.floor - 10);
                }
            } else if (r.kind === 1) {
                const cx = Math.floor((r.x0 + r.x1) / 2);
                air(cx - 3, r.floor, cx + 3, r.floor + 2);
                paint(cx - 3, r.floor + 3, cx + 3, r.floor + 3, Cell.SPIKES);
            } else if (r.kind === 2) {
                brick(r.x0, r.floor - 2, r.x0 + 3, r.floor - 1);
                brick(r.x1 - 3, r.floor - 2, r.x1, r.floor - 1);
                brick(r.x0 + 5, r.top + 6, r.x0 + 10, r.top + 7);
            } else if (r.kind === 3) {
                air(r.x0 - 3, r.floor - 14, r.x0 - 1, r.floor - 10);
                air(r.x1 + 1, r.floor - 14, r.x1 + 3, r.floor - 10);
            } else if (r.kind === 4) {
                brick(r.x0, r.floor - 13, r.x0 + 6, r.floor - 13);
                brick(r.x1 - 6, r.floor - 19, r.x1, r.floor - 19);
                air(r.x0 + 4, r.top - 5, r.x1 - 4, r.top - 3);
            } else if (r.kind === 5) {
                for (let x = r.x0 + 5; x < r.x1 - 4; x += 9) brick(x, r.top, x + 2, r.top + 3);
            } else if (r.kind === 6) {
                for (let x = r.x0 + 8; x < r.x1 - 8; x += 13) brick(x, r.top, x + 3, r.top + 4);
            } else if (r.kind === 7) {
                const cx = Math.floor((r.x0 + r.x1) / 2);
                brick(cx - 5, r.floor - 1, cx + 5, r.floor - 1);
                brick(cx - 2, r.floor - 2, cx + 2, r.floor - 2);
                brick(r.x1 - 7, r.top, r.x1 - 5, r.floor - 10);
            }
        }
    }
    for (const x of [arena.x0 + 22, arena.x0 + 43, arena.x1 - 44, arena.x1 - 23]) {
        brick(x, arena.top, x + 2, arena.top + 18);
        brick(x - 2, arena.top + 18, x + 4, arena.top + 19);
    }
    brick(arena.x0, arena.floor - 14, arena.x0 + 10, arena.floor - 14);
    brick(arena.x1 - 10, arena.floor - 14, arena.x1, arena.floor - 14);
    brick(-15, arena.floor - 1, 15, arena.floor - 1);
    brick(-10, arena.floor - 2, 10, arena.floor - 2);
    brick(-5, arena.floor - 3, 5, arena.floor - 3);
    const altar = { x: -1, y: arena.floor - 5 }; // canto superior esquerdo do 3x2

    const first = rows[0];
    const edge = entranceSide > 0 ? first.right : first.left;
    const outsideX = entranceSide * (halfAt(first.floor) + 10);
    const door = { x: entranceSide * (halfAt(first.floor - 6) - 5), y: first.floor - 2, style: 11 };
    paint(Math.min(edge, outsideX), first.floor - 6, Math.max(edge, outsideX), first.floor - 1, Cell.AIR, true);
    paint(Math.min(edge, outsideX), first.floor, Math.max(edge, outsideX), first.floor, Cell.BRICK, true);
    brick(door.x, first.floor - 6, door.x, first.floor - 4);

    // Tesouros: pelo menos um local em cada andar, com piso plano 2x2.
    // AddBuriedChest recebe a coluna direita e procura o primeiro piso abaixo.
    const chestCandidates = [];
    for (let level = 0; level < rows.length; level++) {
        for (const r of rows[level].rooms) {
            if (r === arena) continue;
            for (const x of [r.x0 + 6, r.x1 - 5]) {
                if ([x - 1, x].every(cx => cell(cx, r.floor) === Cell.BRICK
                    && cell(cx, r.floor - 1) === Cell.AIR && cell(cx, r.floor - 2) === Cell.AIR
                    && cell(cx, r.floor - 3) === Cell.AIR)) {
                    chestCandidates.push({ x, y: r.floor - 3, floor: r.floor, level, kind: r.kind, x0: r.x0, x1: r.x1 });
                    break;
                }
            }
        }
    }
    const roomCount = rows.reduce((n, r) => n + r.rooms.length, 0);
    const chests = [], remaining = chestCandidates.slice();
    const takeChest = index => chests.push(...remaining.splice(index, 1));
    for (let level = 0; level < rows.length; level++) {
        let index = remaining.findIndex(c => c.level === level && c.kind === 3);
        if (index < 0) index = remaining.findIndex(c => c.level === level);
        if (index < 0) throw new Error('Andar sem espaço reservado para baú');
        takeChest(index);
    }
    const desiredChests = Math.ceil(roomCount * 0.35);
    for (let level = 1; chests.length < desiredChests && remaining.length; level = (level + 1) % rows.length) {
        const index = remaining.findIndex(c => c.level === level);
        if (index >= 0) takeChest(index);
    }

    // Circuitos reais: placa Lihzahrd (135, estilo 6) e emissores 137.
    // Os estilos e orientações vêm de mayanTrap/PlaceTile/Wiring no fonte.
    const traps = [];
    for (let level = 0; level < rows.length; level++) {
        for (const r of rows[level].rooms) {
            if (r === arena) continue;
            let placed = 0;
            for (let x = r.x0 + 5; x <= r.x1 - 5 && placed < (r.kind === 6 ? 3 : 2); x += 5) {
                let floor = r.floor - 5;
                while (cell(x, floor) === Cell.AIR && floor <= r.floor + 4) floor++;
                const py = floor - 1;
                if (cell(x, floor) !== Cell.BRICK || cell(x - 1, floor) !== Cell.BRICK || cell(x + 1, floor) !== Cell.BRICK) continue;
                if (chests.some(c => Math.abs(c.x - x) < 5 && Math.abs(c.floor - floor) < 5)) continue;
                if (traps.some(t => Math.abs(t.plate.x - x) < 5 && t.plate.y === py)) continue;
                let clear = true;
                for (let yy = py - 2; yy <= py; yy++) for (let xx = x - 1; xx <= x + 1; xx++) if (cell(xx, yy) !== Cell.AIR) clear = false;
                if (!clear) continue;
                let style = 1 + traps.length % 4, tx = x, ty = py - 2, frameX = 0;
                if (style <= 2) {
                    let lx = x - 1, rx = x + 1;
                    while (cell(lx, ty) === Cell.AIR && x - lx < 49) lx--;
                    while (cell(rx, ty) === Cell.AIR && rx - x < 49) rx++;
                    if (x - lx > 5 && x - lx < 49 && cell(lx, ty) === Cell.BRICK) { tx = lx; frameX = 18; }
                    else if (rx - x > 5 && rx - x < 49 && cell(rx, ty) === Cell.BRICK) tx = rx;
                    else style = style === 1 ? 3 : 4;
                }
                if (style >= 3) {
                    tx = x; ty = py - 3;
                    while (ty > r.top - 7 && cell(tx, ty) === Cell.AIR) ty--;
                    if (cell(tx, ty) !== Cell.BRICK) continue;
                    frameX = 0;
                }
                if (traps.some(t => t.emitter.x === tx && t.emitter.y === ty)) continue;
                traps.push({ plate: { x, y: py }, emitter: { x: tx, y: ty, style, frameX }, color: traps.length % 3, level });
                placed++;
            }
        }
    }
    if (traps.length < roomCount) throw new Error('A planta ficou sem circuitos suficientes');

    // Reduza 38% da margem além do envelope mínimo das câmaras/escadas.
    // O envelope é linear para preservar a silhueta de pirâmide.
    let requiredSlope = 0;
    for (let y = 1; y < height; y++) {
        for (let x = left; x < left + width; x++) {
            if (y >= first.floor - 6 && y <= first.floor && (entranceSide > 0 ? x >= edge : x <= edge)) continue;
            if (cell(x, y) === Cell.AIR || cell(x, y) === Cell.SPIKES) requiredSlope = Math.max(requiredSlope, (Math.abs(x) + 8 - 10) / y);
        }
    }
    for (const t of traps) requiredSlope = Math.max(requiredSlope, (Math.abs(t.emitter.x) + 4 - 10) / t.emitter.y);
    requiredSlope = Math.min(0.91, requiredSlope);
    const outerHalf = [];
    let boundLeft = 0, boundRight = 0;
    for (let y = 0; y < height; y++) {
        const minimum = 10 + requiredSlope * y;
        const half = Math.ceil(minimum + 0.62 * (halfAt(y) - minimum));
        outerHalf.push(half);
        for (let x = left; x < left + width; x++) {
            const entry = y >= first.floor - 6 && y <= first.floor && (entranceSide > 0 ? x >= edge && x <= outsideX : x <= edge && x >= outsideX);
            if (Math.abs(x) > half && cell(x, y) === Cell.BRICK && !entry) grid[y * width + x - left] = Cell.OUTSIDE;
            if (cell(x, y) !== Cell.OUTSIDE) { boundLeft = Math.min(boundLeft, x); boundRight = Math.max(boundRight, x); }
        }
    }

    const dx = Math.max(50 - left, Math.min(worldWidth - 51 - (left + width - 1), Math.round(originX)));
    const dy = Math.max(minY, Math.min(maxY - height + 1, Math.round(originY) - 20));
    return { grid, width, height, left, dx, dy, rows, arena, altar, door, stairs, chests, traps, outerHalf,
        bounds: { left: boundLeft, right: boundRight }, exteriorMarginScale: 0.62, requiredSlope, shell,
        entrance: { x: outsideX, y: first.floor - 2, side: entranceSide },
        levels: rows.length, roomCount, cell };
}


// JsHook.cpp executa o original quando o callback lança. Contenha aqui todo
// erro, inclusive os anteriores à primeira escrita, para suprimir makeTemple.
function buildTemple(ox, oy) {
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

globalThis.buildTemple = buildTemple;
if (!globalThis.templeHooked) {
    Terraria.WorldGen['void makeTemple(int x, int y, GenerationProgress progress)'].hook((_original, x, y) => {
        globalThis.buildTemple(x, y);
    });
    globalThis.templeHooked = true;
}
})();
'Templo Pirâmide pronto: crie um mundo novo';
