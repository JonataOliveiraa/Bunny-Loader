// Bordas do mundo conectadas: nadando para fora de um oceano, você sai no
// outro, sem atravessar o mapa.
//
// NA GERAÇÃO (mundo novo, criado depois de rodar isto):
//   1. o oceano de água mais alta é esvaziado até a camada do outro: os dois
//      ficam com a água no mesmo y;
//   2. as últimas colunas jogáveis da direita viram cópia das primeiras da
//      esquerda (a "faixa gêmea"), com o fundo emendado aos poucos.
// NO JOGO: passando do meio da faixa, jogador e câmera andam um período
// inteiro. Os dois lados são iguais, então o salto não aparece; a luz, os
// render targets e as nuvens andam junto, e o fundo em paralaxe vê uma câmera
// contínua (não pula).
//
// Rodar no Editor, criar um mundo novo e jogar nele (um jogador). Vale até o
// jogo fechar. Mundo antigo, sem a faixa, continua com a borda fechada.

const WRAP = {
    TOP: 41,       // limite do jogo: leftWorld + 640 px
    BLEND: 36,     // colunas de emenda entre a faixa e o oceano original
    MARGIN: 12,    // folga da faixa além da largura da tela
};

// Largura da faixa: a tela inteira + folga; do meio dela a câmera nunca vê
// onde a cópia acaba. A mesma conta na geração e no jogo.
WRAP.bandWidth = () => Math.ceil(Terraria.Main.screenWidth / 16) + WRAP.MARGIN;

WRAP.generate = () => {
    const M = Terraria.Main;
    const tileAt = (x, y) => M.tile['Tile get_Item(int x, int y)'](x, y);
    const solidAt = (x, y) => { const t = tileAt(x, y); return (t.sTileHeader & 0x20) && M.tileSolid[t.type]; };
    const t0 = Date.now();
    const { TOP, BLEND } = WRAP;
    const K = WRAP.bandWidth(), L = 41, R = M.maxTilesX - 41;
    const SEAM = Math.floor(M.worldSurface) + 60;
    const measureD = L + K + BLEND + 4;                 // onde o oceano ainda é o original
    const colOf = (side, d) => side < 0 ? d : M.maxTilesX - 1 - d;

    const surface = (x) => {
        let water = -1;
        for (let y = TOP; y < SEAM; y++) {
            if (water < 0 && tileAt(x, y).liquid > 0) water = y;
            if (solidAt(x, y)) return { ground: y, water: water < 0 ? y : water };
        }
        return { ground: SEAM, water: water < 0 ? SEAM : water };
    };

    // 1. Água na mesma camada: esvazia o oceano mais alto até o nível do outro
    const left = surface(colOf(-1, measureD)), right = surface(colOf(1, measureD));
    const level = Math.max(left.water, right.water);
    const high = left.water < right.water ? -1 : right.water < left.water ? 1 : 0;
    let drained = 0;
    if (high) {
        for (let d = 0; d < M.maxTilesX / 2; d++) {
            const x = colOf(high, d);
            let any = false;
            for (let y = TOP; y < level; y++) {
                const t = tileAt(x, y);
                if (t.liquid > 0) { t.liquid = 0; any = true; drained++; }
            }
            if (!any && d > measureD) break;            // passou da praia
        }
    }

    // 2. Faixa gêmea. Baús na área sobrescrita saem pelo jogo (senão viram fantasma)
    const B0 = R - K - BLEND;
    const chests = M.chest;
    for (let id = 0; id < chests.length; id++) {
        const c = chests[id];
        if (c && c.x >= B0 - 1 && c.x < R + 1 && c.y <= SEAM) Terraria.Chest['void DestroyChestDirect(int X, int Y, int id)'](c.x, c.y, id);
    }

    // Emenda: o fundo vai do oceano original até o da cópia; a água já está no mesmo nível
    const a = surface(B0 - 1), b = surface(L);
    for (let x = B0; x < R - K; x++) {
        const f = (x - B0 + 1) / (BLEND + 1);
        const ground = Math.round(a.ground + (b.ground - a.ground) * f);
        for (let y = TOP; y < SEAM; y++) {
            const t = tileAt(x, y);
            if (y < ground) {
                t.ClearEverything();
                if (y >= level) t.liquid = 255;
            } else if (!solidAt(x, y)) {
                t.ClearEverything();
                t.sTileHeader = 0x20;
                t.type = Terraria.ID.TileID.Sand;
            }
        }
    }

    // A cópia (móveis não entram: baú e placa precisam de registro à parte)
    for (let dx = 0; dx < K; dx++) {
        for (let y = TOP; y < SEAM; y++) {
            const s = tileAt(L + dx, y), d = tileAt(R - K + dx, y);
            const header = s.sTileHeader, type = s.type;
            const furniture = (header & 0x20) && M.tileFrameImportant[type];
            d.ClearEverything();
            if (!furniture) {
                d.sTileHeader = header;
                d.bTileHeader = s.bTileHeader;
                d.bTileHeader3 = s.bTileHeader3;
                d.type = type;
                d.frameX = s.frameX;
                d.frameY = s.frameY;
            }
            d.bTileHeader2 = s.bTileHeader2;   // tipo do líquido
            d.wall = s.wall;
            d.liquid = s.liquid;
        }
    }

    bl.log(`bordas: água no y ${level} (esvaziados ${drained} tiles do oceano da ${high < 0 ? 'esquerda' : high > 0 ? 'direita' : 'nenhum'}), ` +
        `faixa de ${K} colunas (${Date.now() - t0} ms)`);
};

// Uma coluna resumida: onde começa a água, onde começa o chão e os 12 tiles de baixo
WRAP.columnKey = (x) => {
    const M = Terraria.Main;
    const tileAt = (xx, y) => M.tile['Tile get_Item(int x, int y)'](xx, y);
    let water = -1, key = '';
    for (let y = WRAP.TOP; y < M.worldSurface + 60; y++) {
        const t = tileAt(x, y);
        if (water < 0 && t.liquid > 0) water = y;
        if ((t.sTileHeader & 0x20) && M.tileSolid[t.type]) {
            key = `${water}:${y}`;
            for (let k = 1; k <= 12; k++) { const u = tileAt(x, y + k); key += ',' + ((u.sTileHeader & 0x20) ? u.type : -1); }
            return { key, ground: y };
        }
    }
    return { key: 'vazia', ground: M.worldSurface + 60 };
};

// No 1º quadro de cada mundo: a faixa existe? (mundo gerado com o script)
WRAP.check = () => {
    const M = Terraria.Main;
    const K = WRAP.bandWidth(), L = 41, R = M.maxTilesX - 41;
    let ok = true, ground = 0;
    for (const dx of [1, K >> 2, K >> 1, K - (K >> 2), K - 2]) {
        const l = WRAP.columnKey(L + dx), r = WRAP.columnKey(R - K + dx);
        if (l.key !== r.key) { ok = false; break; }
        ground = Math.max(ground, l.ground);
    }
    WRAP.state = { world: M.worldID, ok, K, L, R, P: R - K - L, ground, offset: 0 };
    bl.log(ok ? `bordas: conectadas (faixa ${K}, período ${R - K - L})` : 'bordas: este mundo não tem a faixa gêmea; gere um mundo novo com o script');
};

WRAP.shift = (player, tiles) => {
    const M = Terraria.Main, px = tiles * 16;
    const vec = (v) => Vector2.new(v.X + px, v.Y);
    player.position.X += px;
    player.oldPosition.X += px;
    const local = LocalUserGameState.Instance;
    local.screenPosition.X += px;
    // O que já foi desenhado e iluminado vale igual do outro lado: só muda de lugar
    for (const name of ['sceneTilePos', 'sceneTile2Pos', 'sceneWallPos', 'sceneBackgroundPos', 'sceneWaterPos']) M[name] = vec(M[name]);
    try {
        const engine = local._activeEngine;
        engine._activeProcessedArea.X += tiles;
        engine._workingProcessedArea.X += tiles;
    } catch (e) { }
    try {
        const last = Terraria.Cloud.screenLastPosition;
        if (last) Terraria.Cloud.screenLastPosition = vec(last);
    } catch (e) { }
    WRAP.state.offset += px;   // o fundo desenha como se nada tivesse mudado
};

WRAP.tick = (player, i) => {
    const M = Terraria.Main;
    if (i !== M.myPlayer || M.netMode !== 0) return;
    const st = WRAP.state;
    if (!st || st.world !== M.worldID) { WRAP.check(); return; }
    if (!st.ok) return;

    const cx = (player.position.X + player.width / 2) / 16;
    const cy = (player.position.Y + player.height / 2) / 16;
    if (cy > st.ground + 6) return;           // enterrado abaixo da faixa: borda normal
    if (cx > st.R - st.K / 2) WRAP.shift(player, -st.P);
    else if (cx < st.L + st.K / 2) WRAP.shift(player, st.P);
};

// Fundo de superfície e de subsolo: desenhados com a câmera "contínua"
WRAP.drawShifted = (original, self) => {
    const st = WRAP.state;
    if (!st || !st.ok || !st.offset) return original(self);
    const local = LocalUserGameState.Instance;
    local.screenPosition.X -= st.offset;
    try { original(self); } finally { local.screenPosition.X += st.offset; }
};

globalThis.WRAP = WRAP;
if (!globalThis.wrapHooked) {
    globalThis.wrapHooked = true;
    const T = Terraria;
    T.WorldGen['bool GenerateWorld(GenerationProgress customProgressObject, Controller customController)'].hook((original, progress, controller) => {
        const ok = original(progress, controller);
        if (ok) {
            try { globalThis.WRAP.generate(); } catch (e) { bl.log('bordas (geração): ' + e); }
        }
        return ok;
    });
    T.Player['void Update(int i)'].hook((original, self, i) => {
        original(self, i);
        try { globalThis.WRAP.tick(self, i); } catch (e) { bl.log('bordas: ' + e); globalThis.WRAP.tick = () => {}; }
    });
    T.Main['void DrawSurfaceBG()'].hook((original, self) => globalThis.WRAP.drawShifted(original, self));
    T.Main['void DrawBackground()'].hook((original, self) => globalThis.WRAP.drawShifted(original, self));
}
'bordas prontas: crie um mundo novo e vá até um oceano';
