// Templo da Selva redesenhado: andares de câmaras ligados por corredores,
// poços com degraus entre os andares e, no fundo, a arena do Golem com o altar
// num estrado.
//
// Rodar no Editor e depois CRIAR UM MUNDO NOVO (sem fechar o jogo): o hook vive
// até o processo acabar. Rodar de novo só troca o desenho; o hook é um só.
//
// O contrato do jogo é mantido: tijolo lihzahrd (226) por fora, parede de
// templo (87) por dentro, porta trancada na entrada, altar e os limites
// GenVars.t*: com eles, o templePart2 do jogo ainda espalha armadilhas, baús
// (com a Célula de Energia), estátuas e móveis.

globalThis.buildTemple = (ox, oy) => {
    const M = Terraria.Main, W = Terraria.WorldGen, GV = Terraria.WorldBuilding.GenVars;
    const rng = W.genRand;
    const rand = (a, b) => rng['int Next(int minValue, int maxValue)'](a, b + 1); // inclusivo
    const tiles = M.tile;
    const tileAt = (x, y) => tiles['Tile get_Item(int x, int y)'](x, y);
    const activeType = (x, y) => { const t = tileAt(x, y); return (t.sTileHeader & 0x20) ? t.type : -1; };

    const BRICK = 226, SPIKES = 232, ALTAR = 237, DOOR = 10, TEMPLE_WALL = 87;
    const C_BRICK = 1, C_AIR = 2, C_SPIKES = 3;
    const SHELL = 8, LEVEL_H = 30, SHAFT_W = 10;
    const scale = M.maxTilesX / 4200;

    // ---- 1. Planta (x relativo ao centro; o deslocamento vem no fim) ----
    const makePlan = (levels) => {
        const p = { air: [], brick: [], spikes: [], rows: [], rooms: 0 };
        const air = (x0, y0, x1, y1) => p.air.push([x0, y0, x1, y1]);
        const brick = (x0, y0, x1, y1) => p.brick.push([x0, y0, x1, y1]);
        const entranceSide = rand(0, 1) ? 1 : -1;

        for (let i = 0, floor = oy + 20; i < levels; i++, floor += LEVEL_H) {
            const rooms = [];
            let total = 0;
            for (let k = 0; k < 2 + i; k++) {
                const r = { w: rand(16, 26), h: rand(10, 15), gap: rand(5, 9) };
                rooms.push(r);
                total += r.w + (k < 1 + i ? r.gap : 0);
            }
            let x = -(total >> 1);
            for (const r of rooms) {
                r.x0 = x; r.x1 = x + r.w - 1; r.floor = floor; r.top = floor - r.h;
                x += r.w + r.gap;
            }
            p.rows.push({ floor, rooms, left: rooms[0].x0, right: rooms[rooms.length - 1].x1 });
        }

        const arenaW = Math.round(80 + 20 * scale), arenaH = Math.round(42 + 4 * scale);
        const last = p.rows[p.rows.length - 1];
        const arena = { x0: -(arenaW >> 1), x1: (arenaW >> 1), floor: last.floor + 10 + arenaH };
        arena.top = arena.floor - arenaH;
        p.arena = arena;

        // Câmaras: salão com abóbada em degraus + um enfeite sorteado
        p.rows.forEach((row, i) => row.rooms.forEach((r, k) => {
            p.rooms++;
            air(r.x0, r.top, r.x1, r.floor - 1);
            const arch = Math.min(3, r.w >> 3);
            for (let a = 1; a <= arch; a++) air(r.x0 + 3 * a, r.top - a, r.x1 - 3 * a, r.top - a);
            const next = row.rooms[k + 1];
            if (next) {
                air(r.x1 + 1, r.floor - 5, next.x0 - 1, r.floor - 1);                 // corredor
                if (rand(0, 2) === 0) air(r.x1 + 2, r.floor - 6, next.x0 - 2, r.floor - 6); // arcada
            }
            const entrance = i === 0 && k === (entranceSide < 0 ? 0 : row.rooms.length - 1);
            const kind = entrance ? 0 : rand(0, 4);
            const cx = (r.x0 + r.x1) >> 1;
            if (kind === 0) {                       // colunata suspensa
                for (let px = r.x0 + 4; px + 1 <= r.x1 - 4; px += 7) {
                    brick(px, r.top - arch, px + 1, r.floor - 6);
                    brick(px - 1, r.floor - 6, px + 2, r.floor - 6);
                }
            } else if (kind === 1) {                // fosso com espinhos
                air(cx - 3, r.floor, cx + 2, r.floor + 2);
                p.spikes.push([cx - 3, r.floor + 3, cx + 2, r.floor + 3]);
            } else if (kind === 2 && r.w >= 20) {   // estrado
                brick(r.x0 + 4, r.floor - 1, r.x1 - 4, r.floor - 1);
                brick(r.x0 + 7, r.floor - 2, r.x1 - 7, r.floor - 2);
            } else if (kind === 3) {                // nichos nas paredes
                air(r.x0 - 3, r.floor - 9, r.x0 - 1, r.floor - 7);
                air(r.x1 + 1, r.floor - 9, r.x1 + 3, r.floor - 7);
            } else {                                // galeria de um lado
                const len = Math.round(r.w / 3);
                if (rand(0, 1)) brick(r.x0, r.floor - 5, r.x0 + len, r.floor - 5);
                else brick(r.x1 - len, r.floor - 5, r.x1, r.floor - 5);
            }
        }));

        // Poços entre os andares (o último desce até o chão da arena)
        p.rows.forEach((row, i) => {
            const side = (i % 2 === 0 ? -entranceSide : entranceSide);
            const target = p.rows[i + 1] || null;
            const bottom = target ? target.floor : arena.floor;
            const x0 = side > 0 ? row.right + 5 : row.left - 5 - (SHAFT_W - 1);
            const x1 = x0 + SHAFT_W - 1;
            if (side > 0) air(row.right + 1, row.floor - 5, x0 - 1, row.floor - 1);
            else air(x1 + 1, row.floor - 5, row.left - 1, row.floor - 1);
            air(x0, row.floor - 6, x1, bottom - 1);
            // Degraus alternados de meia largura: juntos cobrem o poço, então
            // ninguém cai direto até o fundo (a arena fica a ~56 de queda)
            for (let y = bottom - 4, n = 0; y > row.floor + 1; y -= 4, n++) {
                if (n % 2 === 0) brick(x0, y, x0 + 4, y);
                else brick(x1 - 4, y, x1, y);
            }
            const edge = target ? (side > 0 ? target.right : target.left) : (side > 0 ? arena.x1 : arena.x0);
            if (side > 0 && edge < x0) air(edge + 1, bottom - 5, x0 - 1, bottom - 1);
            if (side < 0 && edge > x1) air(x1 + 1, bottom - 5, edge - 1, bottom - 1);
        });

        // Arena do Golem: cúpula em degraus, colunas suspensas, sacadas e o estrado do altar
        const { x0, x1, top, floor } = arena;
        const acx = (x0 + x1) >> 1;
        air(x0, top, x1, floor - 1);
        for (let a = 1; a <= 5; a++) air(x0 + 6 * a, top - a, x1 - 6 * a, top - a);
        for (const px of [x0 + 12, x0 + 26, x1 - 27, x1 - 13]) {
            brick(px, top - 2, px + 1, top + (arenaH >> 1) - 4);
            brick(px - 1, top + (arenaH >> 1) - 4, px + 2, top + (arenaH >> 1) - 4);
        }
        brick(x0, floor - 11, x0 + 7, floor - 11);
        brick(x1 - 7, floor - 11, x1, floor - 11);
        brick(acx - 12, floor - 1, acx + 12, floor - 1);
        brick(acx - 8, floor - 2, acx + 8, floor - 2);
        brick(acx - 4, floor - 3, acx + 4, floor - 3);
        p.altar = { x: acx, y: floor - 4 };
        p.rooms++;

        p.entrance = { side: entranceSide, row: p.rows[0] };
        return p;
    };

    // Menos andares se o templo passar do começo do submundo
    let levels = 3 + (scale > 1.3 ? 1 : 0) + (scale > 1.8 ? 1 : 0);
    let plan = makePlan(levels);
    while (levels > 2 && plan.arena.floor + SHELL > M.UnderworldLayer - 20) plan = makePlan(--levels);

    // ---- 2. Grade: casca de tijolo em volta de todo vão, depois os vãos e enfeites ----
    let bx0 = Infinity, by0 = Infinity, bx1 = -Infinity, by1 = -Infinity;
    for (const [x0, y0, x1, y1] of plan.air) {
        bx0 = Math.min(bx0, x0 - SHELL); by0 = Math.min(by0, y0 - SHELL);
        bx1 = Math.max(bx1, x1 + SHELL); by1 = Math.max(by1, y1 + SHELL);
    }
    const gw = bx1 - bx0 + 1, gh = by1 - by0 + 1;
    const grid = new Uint8Array(gw * gh);
    const inGrid = (x, y) => x >= bx0 && x <= bx1 && y >= by0 && y <= by1;
    const cell = (x, y) => inGrid(x, y) ? grid[(y - by0) * gw + (x - bx0)] : 0;
    const paint = (x0, y0, x1, y1, v) => {
        for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) if (inGrid(x, y)) grid[(y - by0) * gw + (x - bx0)] = v;
    };
    for (const [x0, y0, x1, y1] of plan.air) paint(x0 - SHELL, y0 - SHELL, x1 + SHELL, y1 + SHELL, C_BRICK);
    for (const r of plan.air) paint(...r, C_AIR);
    for (const r of plan.brick) paint(...r, C_BRICK);
    for (const r of plan.spikes) paint(...r, C_SPIKES);

    // Entrada: túnel da câmara até fora da casca; a porta fica 3 tiles para dentro
    const { side, row } = plan.entrance;
    const edge = side > 0 ? row.right : row.left;
    const door = { x: edge + 3 * side, y: row.floor - 2 };
    for (let x = edge + side; inGrid(x, row.floor - 2) && cell(x, row.floor - 2) !== 0; x += side) {
        paint(x, x === door.x ? row.floor - 3 : row.floor - 4, x, row.floor - 1, C_AIR);
        if (x === door.x) paint(x, row.floor - 4, x, row.floor - 4, C_BRICK);
    }

    // ---- 3. No mundo ----
    const minX = 50 - bx0, maxX = M.maxTilesX - 50 - bx1;
    const dx = Math.max(minX, Math.min(maxX, ox));
    for (let y = by0; y <= by1; y++) {
        for (let x = bx0; x <= bx1; x++) {
            const c = grid[(y - by0) * gw + (x - bx0)];
            if (!c) continue;
            const t = tileAt(x + dx, y);
            t.ClearEverything();
            if (c !== C_AIR) {
                t.sTileHeader = 0x20;
                t.type = c === C_SPIKES ? SPIKES : BRICK;
            }
            // A borda de fora fica sem parede, como o jogo faz
            if (cell(x - 1, y) && cell(x + 1, y) && cell(x, y - 1) && cell(x, y + 1)) t.wall = TEMPLE_WALL;
        }
    }

    const doorOk = W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](door.x + dx, door.y, DOOR, true, false, -1, 11)
        && activeType(door.x + dx, door.y) === DOOR;

    const ax = plan.altar.x + dx, ay = plan.altar.y;
    W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](ax, ay, ALTAR, true, false, -1, 0);
    let altarX, altarY;
    if (activeType(ax, ay) === ALTAR) {
        const t = tileAt(ax, ay);
        altarX = ax - Math.floor(t.frameX / 18);
        altarY = ay - Math.floor(t.frameY / 18);
    } else {                                    // o mesmo recurso do jogo: montar à mão
        altarX = ax - 1; altarY = ay - 1;
        for (let i = 0; i <= 2; i++) for (let j = 0; j <= 1; j++) {
            const t = tileAt(altarX + i, altarY + j);
            t.ClearEverything();
            t.sTileHeader = 0x20;
            t.type = ALTAR;
            t.frameX = i * 18;
            t.frameY = j * 18;
            t.wall = TEMPLE_WALL;
        }
    }

    GV.lAltarX = altarX; GV.lAltarY = altarY;
    GV.tLeft = bx0 + dx; GV.tRight = bx1 + dx; GV.tTop = by0; GV.tBottom = by1;
    GV.tRooms = plan.rooms;

    bl.log(`templo: ${levels} andares, ${plan.rooms} câmaras, ${gw}x${gh} em ${bx0 + dx},${by0}; ` +
        `altar ${altarX},${altarY}; porta ${doorOk ? 'trancada' : 'NÃO coube'}`);
};

if (!globalThis.templeHooked) {
    globalThis.templeHooked = true;
    Terraria.WorldGen['void makeTemple(int x, int y, GenerationProgress progress)'].hook((original, x, y, progress) => {
        try {
            globalThis.buildTemple(x, y);
        } catch (e) {
            bl.log('templo: falhou (' + e + '), fica o do jogo');
            original();
        }
    });
}
'hook do templo pronto: crie um mundo novo';
