// Editor (Mod Menu): cole e rode ANTES de criar o mundo. Instala um gancho no
// WorldGen.ShimmerCleanUp, no passo final da geração, que gera o bioma de
// exemplo na superfície no estilo do Aquatic Depths do ThoriumMod: elipse de
// Bloco de Exemplo com borda em degradê, Paredes de Pedra, veios de Minério de
// Exemplo, cavernas, morros e rampas. Precisa do ExampleMod ligado.
//
// Vale até fechar o jogo. Rodar de novo só troca o gerador: o gancho é um só.

// O TileRunner do Thorium (WorldGenerationSystem.CustomTileRunner): um pincel
// que anda, gira um pouco a cada passo e encolhe de startStrength até
// stopStrength. O formato de cada pincelada vem do `dither`.
function runner(next, chance, ox, oy, startStrength, stopStrength, steps, vx, vy, action, dither) {
    const Main = Terraria.Main;
    const spread = () => chance() * 2 - 1;
    let left = steps, x = ox, y = oy;
    if (vx === 0 && vy === 0) { vx = spread(); vy = spread(); }
    while (left > 0) {
        const strength = startStrength * (left / steps);
        if (strength <= 0 || strength < stopStrength) break;
        left -= 1;
        const half = strength * 0.5;
        const xa = Math.max(1, Math.floor(x - half)), xb = Math.min(Main.maxTilesX, Math.floor(x + half));
        const ya = Math.max(1, Math.floor(y - half)), yb = Math.min(Main.maxTilesY, Math.floor(y + half));
        const squash = 1.3 + chance() * 0.7;
        for (let i = xa; i < xb; i++) {
            for (let j = ya; j < yb; j++) {
                let outside = false;
                if (dither === DIAMOND) outside = Math.abs(i - x) + Math.abs(j - y) >= half;
                else if (dither === CIRCLE) outside = (i - x) ** 2 + (j - y) ** 2 >= half * half;
                else if (dither === ELLIPSE) outside = (i - x) ** 2 + ((j - y) * squash) ** 2 >= half * half;
                if (!outside) action(i, j);
            }
        }
        const turns = Math.floor(strength / 50) + 1;
        left -= turns;
        x += vx; y += vy;
        for (let k = 0; k < turns; k++) {
            x += vx; y += vy;
            vx += spread() * 0.5; vy += spread() * 0.5;
        }
        vx = Math.max(-1, Math.min(1, vx + spread() * 0.5));
        vy = Math.max(-1, Math.min(1, vy + spread() * 0.5));
    }
}

const ACTIVE = 0x20;          // o bit de "tem bloco" do sTileHeader
const STONE_WALL = 1;         // WallID.Stone
const START = 0.76;           // onde o degradê da borda começa (fração do raio)
const WALL_DEPTH = 3;         // a parede começa 3 blocos abaixo do chão
const ORE_DEPTH = 10;         // minério e caverna só daqui para baixo
const CAVE_DEPTH = 6;         // a caverna não fura o chão
const DIAMOND = 1, CIRCLE = 2, ELLIPSE = 3;

// Estrutura: o bioma desvia (com folga) em vez de converter.
const PROTECTED = new Set([
    10, 11, 19, 30, 38, 39, 41, 43, 44, 45, 46, 47, 48, 54, 124, 137, 189, 196, 202, 226, 232,
]);
// Ilha flutuante: nuvem, nuvem de chuva, placa solar.
const ISLAND = new Set([189, 196, 202]);
// Árvore, palmeira, cogumelo gigante, muda.
const TREES = new Set([5, 20, 72, 323]);
// Plantas que o tileCut não marca, e o girassol.
const PLANTS = new Set([3, 24, 27, 61, 71, 73, 74, 110, 113, 201, 233]);

function generateExampleBiome() {
    const Main = Terraria.Main, WorldGen = Terraria.WorldGen;
    const GenVars = Terraria.WorldBuilding.GenVars;
    const biome = ModContent.TileType('ExampleTile');
    const ore = ModContent.TileType('ExampleOre');
    const rng = WorldGen.genRand;
    const next = (min, max) => rng['int Next(int minValue, int maxValue)'](min, max); // [min, max)
    const chance = () => next(0, 1 << 30) / (1 << 30);
    const tileAt = (x, y) => Main.tile['Tile get_Item(int x, int y)'](x, y);
    const has = (t) => (t.sTileHeader & ACTIVE) !== 0;

    // ---------------------------------------------------- onde
    const width = Main.maxTilesX;
    const rx = Math.max(55, Math.min(140, Math.round(width / 55)));   // meia largura
    const ry = Math.round(rx * 0.6);                                  // profundidade
    const third = width / 6;
    const middle = width / 2;
    const spawnX = Main.spawnTileX;

    // Da altura do terreno mais alto para baixo: as ilhas flutuantes ficam
    // pelo menos 50 blocos acima (WorldGen, "Floating Islands").
    const scanTop = Math.max(20, Math.floor(GenVars.worldSurfaceLow) - 25);
    const scanBottom = Math.floor(Main.worldSurface) + 60;
    const groundAt = (x) => {
        for (let y = scanTop; y < scanBottom; y++) {
            const t = tileAt(x, y);
            if (has(t) && Main.tileSolid[t.type] && !Main.tileSolidTop[t.type] && !ISLAND.has(t.type)) return y;
        }
        return -1;
    };

    // Até 12 lugares de um lado ou do outro do spawn; fica o que tem
    // menos estrutura no caminho (casa, dungeon, templo).
    let best = null;
    for (let tries = 0; tries < 12; tries++) {
        const dir = next(0, 2) === 0 ? -1 : 1;
        const near = rx + 25;
        const far = Math.max(near + 1, Math.floor(third - rx * 0.4));
        const cx = spawnX + dir * next(near, far);
        if (cx - rx < 50 || cx + rx > width - 50 || Math.abs(cx - middle) > third - rx * 0.4) continue;
        const g = groundAt(cx);
        if (g < 0) continue;
        let hits = 0;
        for (let x = cx - rx; x <= cx + rx; x += 3) {
            for (let y = g - 30; y <= g + ry; y += 3) {
                const t = tileAt(x, y);
                if (has(t) && (PROTECTED.has(t.type) || Main.tileFrameImportant[t.type])) hits++;
            }
        }
        if (!best || hits < best.hits) best = { cx, hits };
        if (hits === 0) break;
    }
    if (!best) {
        bl.log('ExampleBiomeGen: sem lugar no meio do mapa; bioma não gerado.');
        return;
    }
    const cx = best.cx;
    const x0 = cx - rx - 2, x1 = cx + rx + 2;
    const ground = new Int32Array(x1 - x0 + 1);
    for (let x = x0; x <= x1; x++) ground[x - x0] = groundAt(x);
    const groundOf = (x) => ground[x - x0];

    // ---------------------------------------------------- 1. a elipse
    // O contorno balança com três senos de fase sorteada: sem eles a
    // borda seria uma elipse perfeita.
    const p1 = chance() * 6.283, p2 = chance() * 6.283, p3 = chance() * 6.283;
    const wobble = (a) => 1 + 0.07 * Math.sin(2 * a + p1) + 0.05 * Math.sin(5 * a + p2) + 0.03 * Math.sin(9 * a + p3);
    const key = (x, y) => x * 8192 + y;
    const morph = new Set();
    const avoid = [];
    for (let x = x0; x <= x1; x++) {
        const g = groundOf(x);
        if (g < 0) continue;
        const nx = (x - cx) / rx;
        for (let y = g; y <= g + Math.ceil(ry * 1.2); y++) {
            const ny = (y - g) / ry;
            const d = Math.hypot(nx, ny) / wobble(Math.atan2(ny, nx));
            // O Scan_SelectEliptic do Thorium: dentro de START vai sempre;
            // na faixa, o limite é sorteado a cada bloco.
            if (d > START + chance() * (1 - START)) continue;
            const t = tileAt(x, y);
            if (!has(t)) continue;                    // ar e caverna ficam
            if (PROTECTED.has(t.type) || Main.tileFrameImportant[t.type]) {
                avoid.push(x, y);
                continue;
            }
            if (!Main.tileSolid[t.type] || Main.tileSolidTop[t.type]) continue;
            morph.add(key(x, y));
        }
    }
    // MorphStep_1 do Thorium: dois blocos de folga em volta do que é
    // estrutura, para não encostar bloco de bioma em baú, porta ou tijolo.
    for (let i = 0; i < avoid.length; i += 2) {
        for (let dx = -2; dx <= 2; dx++) {
            for (let dy = -2; dy <= 2; dy++) morph.delete(key(avoid[i] + dx, avoid[i + 1] + dy));
        }
    }
    if (morph.size === 0) {
        bl.log('ExampleBiomeGen: nada para converter em ' + cx + '; bioma não gerado.');
        return;
    }

    // ---------------------------------------------------- 2. bloco e parede
    const deep = [];
    for (const k of morph) {
        const x = Math.floor(k / 8192), y = k % 8192;
        const t = tileAt(x, y);
        t.type = biome;
        if (y - groundOf(x) >= WALL_DEPTH) {
            t.wall = STONE_WALL;
            if (y - groundOf(x) >= ORE_DEPTH) deep.push(x, y);
        }
    }

    // ---------------------------------------------------- 3. veios e cavernas
    const pick = () => {
        const i = next(0, deep.length / 2) * 2;
        return [deep[i], deep[i + 1]];
    };
    let veins = 0, caves = 0;
    if (deep.length) {
        // MorphStep_5: pedaços de minério em losango, só sobre o bloco.
        const toOre = (x, y) => {
            const t = tileAt(x, y);
            if (has(t) && t.type === biome) t.type = ore;
        };
        const veinCount = Math.round(rx / 7);
        for (let i = 0; i < veinCount; i++) {
            const [ox, oy] = pick();
            runner(next, chance, ox, oy, next(3, 7), 0, next(4, 8), 0, 0, toOre, DIAMOND);
            veins++;
        }

        // GenerateBiomeCaveRoom: cavernas deitadas, com a borda áspera
        // (70% um bloco, 30% um quadrado 5x5) e a Parede de Pedra à mostra.
        const dig = (x, y) => {
            if (!morph.has(key(x, y)) || y - groundOf(x) < CAVE_DEPTH) return;
            const t = tileAt(x, y);
            t.sTileHeader = t.sTileHeader & ~ACTIVE;
            t.wall = STONE_WALL;
        };
        const blotch = (sx, sy) => {
            if (chance() > 0.3) return dig(sx, sy);
            for (let x = sx - 2; x <= sx + 2; x++) for (let y = sy - 2; y <= sy + 2; y++) dig(x, y);
        };
        const caveCount = 2 + next(0, 3);
        for (let i = 0; i < caveCount; i++) {
            const [ox, oy] = pick();
            const vx = (next(0, 2) * 2 - 1) * 2.5, vy = (chance() * 2 - 1) * 0.3 * 2.5;
            runner(next, chance, ox, oy, 7 + chance() * 7, 3, next(15, 30), vx, vy, blotch,
                next(0, 2) ? DIAMOND : ELLIPSE);
            caves++;
        }
    }

    // ---------------------------------------------------- 4. o chão de cima
    // As plantas e árvores sobre o bloco novo: o bloco não é grama, e elas
    // quebrariam na primeira atualização do bloco de baixo.
    const clear = (x, y) => { const t = tileAt(x, y); t.sTileHeader = t.sTileHeader & ~ACTIVE; };
    const isBiomeGround = (x) => {
        const g = groundOf(x);
        return g >= 0 && morph.has(key(x, g));
    };
    for (let x = x0; x <= x1; x++) {
        if (!isBiomeGround(x)) continue;
        const g = groundOf(x);
        const above = tileAt(x, g - 1);
        if (!has(above)) continue;
        if (TREES.has(above.type)) {
            // Tronco na coluna e galhos nas vizinhas, até 60 de altura.
            for (let tx = x - 1; tx <= x + 1; tx++) {
                for (let y = g - 1; y >= g - 60; y--) {
                    const t = tileAt(tx, y);
                    if (has(t) && TREES.has(t.type)) clear(tx, y);
                }
            }
        } else if (Main.tileCut[above.type] || PLANTS.has(above.type)) {
            // O girassol tem 2x4: sai inteiro.
            for (let tx = x - 1; tx <= x + 1; tx++) {
                for (let y = g - 4; y <= g - 1; y++) {
                    const t = tileAt(tx, y);
                    if (has(t) && t.type === above.type) clear(tx, y);
                }
            }
        }
    }

    // Morros: uma parábola por morro, de baixo para cima, sem bloco solto.
    let mounds = 0, added = 0;
    const moundCount = Math.round(rx / 12) + 2;
    for (let i = 0; i < moundCount; i++) {
        const mx = cx + Math.round((chance() * 2 - 1) * rx * 0.7);
        const w = next(5, 13), h = next(3, 10);
        let built = false;
        for (let x = mx - w; x <= mx + w; x++) {
            if (x < x0 || x > x1 || !isBiomeGround(x)) continue;
            const k = 1 - ((x - mx) / w) ** 2;
            const tall = Math.round(h * k + chance() * 1.5 - 0.5);
            const g = groundOf(x);
            let top = g;
            for (let y = g - 1; y >= g - tall; y--) {
                const t = tileAt(x, y);
                if (has(t)) break;
                const wall = t.wall;
                t.ClearEverything();
                t.sTileHeader = ACTIVE;
                t.type = biome;
                t.wall = wall;
                top = y;
                added++;
            }
            if (top < g) {
                ground[x - x0] = top;
                morph.add(key(x, top));
                built = true;
            }
        }
        if (built) mounds++;
    }

    // MorphStep_7: as rampas. Só no topo exposto, onde dá para ver.
    let slopes = 0;
    try {
        const smooth = Terraria.Tile['void SmoothSlope(int x, int y, bool applyToNeighbors, bool sync)'];
        for (let x = x0; x <= x1; x++) {
            if (!isBiomeGround(x)) continue;
            smooth(x, groundOf(x), false, false);
            slopes++;
        }
    } catch (error) {
        bl.log('ExampleBiomeGen: sem rampas (' + error + ')');
    }

    bl.log(`ExampleBiomeGen: bioma em x=${cx} (${cx - rx}..${cx + rx}), ${morph.size + added} blocos, ` +
        `${mounds} morros, ${veins} veios, ${caves} cavernas, ${slopes} rampas` +
        (best.hits ? `, ${best.hits} pontos de estrutura evitados` : '') + '.');
}

globalThis.__exampleBiomeGen = generateExampleBiome;
if (!globalThis.__exampleBiomeGenHooked) {
    Terraria.WorldGen['void ShimmerCleanUp()'].hook((original) => {
        original();
        // O erro não pode escapar: se o callback lança, o hook chama o
        // original de novo.
        try {
            globalThis.__exampleBiomeGen();
        } catch (error) {
            bl.log('ExampleBiomeGen: geração interrompida (' + error + ')');
        }
    });
    globalThis.__exampleBiomeGenHooked = true;
}
ModContent.TileType('ExampleTile')
    ? 'Gancho pronto: crie um mundo novo. O log mostra onde o bioma ficou.'
    : 'ExampleTile não encontrado: ligue o ExampleMod e reinicie antes.';
