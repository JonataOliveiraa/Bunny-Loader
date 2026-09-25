// Tiles de mod: tabelas, colocar, quebrar, drop, e o minerio com picareta
// minima e resistencia. Precisa do Example Mod (ExampleTile = 753, ExampleOre =
// 754, os unicos tiles de mod). Deixa uma fileira de blocos ao lado do jogador
// para ver na tela. Loga "tiles <caso>: ok | FALHOU".
const Main = Terraria.Main;
const W = Terraria.WorldGen;
const TILE = 753, ORE = 754;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('tiles ' + label + ': ok');
        else { fails++; bl.log('tiles ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('tiles ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const place = (x, y, type) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, type, true, false, -1, 0);
const kill = (x, y, fail = false, noItem = false) => W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](x, y, fail, false, noItem);
const tileAt = (x, y) => Main.tile['Tile get_Item(int x, int y)'](x, y);
function itemOfClass(name) {
    for (let t = bl.items.vanillaCount; bl.items.isModItem(t); t++) {
        const m = ModItem.getModItem(t);
        if (m && m.constructor.name === name) return t;
    }
    return -1;
}
function droppedNear(type, x, y) {
    for (let i = 0; i < Main.item.length; i++) {
        const w = Main.item[i];
        if (w.active && w.type === type && Math.abs(w.position.X - x * 16) < 64 && Math.abs(w.position.Y - y * 16) < 64) {
            w['void TurnToAir(bool fullReset)'](false);
            return true;
        }
    }
    return false;
}

function run(p) {
    check('tipos registrados', () => (bl.tiles.isModTile(TILE) && bl.tiles.isModTile(ORE) && !bl.tiles.isModTile(752)) ||
        `753 ${bl.tiles.isModTile(TILE)}, 754 ${bl.tiles.isModTile(ORE)}`);
    check('tabelas cresceram e o tipo novo nasce zerado', () => {
        const n = Main.tileSolid.length;
        return (n >= 755 && Main.tileSolid[TILE] === true && Main.tileBrick.length === n &&
                Terraria.ID.TileID.Sets.Conversion.Dirt[TILE] === false && Main.tileLighted[ORE] === false &&
                Main.tileGlowMask[TILE] === Main.tileGlowMask[1]) ||
            `len ${n}, solid ${Main.tileSolid[TILE]}, dirt ${Terraria.ID.TileID.Sets.Conversion.Dirt[TILE]}, glow ${Main.tileGlowMask[TILE]} ` +
            `(pedra ${Main.tileGlowMask[1]}, -1: ${Array.from({ length: 753 }, (_, k) => Main.tileGlowMask[k]).filter((v) => v === -1).length}, ` +
            `0: ${Array.from({ length: 753 }, (_, k) => Main.tileGlowMask[k]).filter((v) => v === 0).length})`;
    });
    check('tileMerge: linhas proprias e do tamanho novo', () => {
        const m = Main.tileMerge;
        return (m.length >= 755 && m[0].length >= 755 && m[ORE].length >= 755 && m[ORE][ORE] === true &&
                m[0][ORE] === false && m[TILE][ORE] === false) ||
            `outer ${m.length}, [0] ${m[0].length}, [ore] ${m[ORE].length}, ore/ore ${m[ORE][ORE]}, 0/ore ${m[0][ORE]}`;
    });
    check('por instancia: adjTile e contagem de bioma', () => {
        const counts = Main.SceneMetrics._tileCounts;
        return (p.adjTile.length >= 755 && counts.length >= 755) || `adjTile ${p.adjTile.length}, bioma ${counts.length}`;
    });
    check('mapa: cor de tile do jogo mais proxima', () => {
        const M = Terraria.Map.MapHelper;
        const lt = M.tileLookup[TILE], lo = M.tileLookup[ORE];
        const dist = (i, r, g, b) => { const c = M.colorLookup[i]; return (c.R - r) ** 2 + (c.G - g) ** 2 + (c.B - b) ** 2; };
        let best = 1;
        for (let i = 1; i < M.wallPosition; i++) if (dist(i, 0, 200, 255) < dist(best, 0, 200, 255)) best = i;
        const c = M.colorLookup[lt];
        return (lt > 0 && lo > 0 && lt < M.wallPosition && lo < M.wallPosition &&
                dist(lt, 0, 200, 255) === dist(best, 0, 200, 255)) ||
            `lookup ${lt}/${lo} (paredes a partir de ${M.wallPosition}), cor ${c && c.R},${c && c.G},${c && c.B}, melhor ${best}`;
    });
    check('textura', () => {
        const t = Terraria.GameContent.TextureAssets.Tile[TILE];
        return (t && t.Value && t.Value.Width === 288) || 'sem textura';
    });

    const px = Math.floor(p.Center.X / 16), py = Math.floor(p.position.Y / 16) - 4;
    const x = px + 2, y = py;
    for (let k = -1; k < 4; k++) kill(x + k, y, false, true);
    check('colocar', () => place(x, y, TILE) === true && bl.tiles.typeAt(x, y) === TILE ||
        `typeAt ${bl.tiles.typeAt(x, y)}`);
    check('quadro de bloco sozinho e junto', () => {
        const frame = () => { const t = tileAt(x, y); return t.frameX + ',' + t.frameY; };
        const alone = frame();
        const ok = place(x + 1, y, TILE);
        const joined = frame();
        return (alone !== joined) ||
            `sozinho ${alone}, junto ${joined}, colocou ${ok}, vizinho ${bl.tiles.typeAt(x + 1, y)}, em ${x},${y}`;
    });
    const blockItem = itemOfClass('ExampleTileItem');
    check('quebrar: some e cai o item do tile', () => {
        kill(x + 1, y);
        return (bl.tiles.typeAt(x + 1, y) === -1 && droppedNear(blockItem, x + 1, y)) ||
            `typeAt ${bl.tiles.typeAt(x + 1, y)}, item ${blockItem}`;
    });
    check('golpe (fail) nao quebra', () => {
        kill(x, y, true);
        return bl.tiles.typeAt(x, y) === TILE || 'quebrou';
    });
    kill(x, y, false, true);

    const damage = (power) => p['int GetPickaxeDamage(int x, int y, int pickPower, int hitBufferIndex, Tile tileTarget)'](x, y, power, 0, tileAt(x, y));
    place(x, y, ORE);
    check('minerio: picareta abaixo da minima nao quebra', () => damage(100) === 0 || 'dano ' + damage(100));
    check('minerio: resistencia divide o dano', () => {
        const d = damage(225);
        place(x + 2, y, TILE);
        const plain = (() => { const t = tileAt(x + 2, y); return p['int GetPickaxeDamage(int x, int y, int pickPower, int hitBufferIndex, Tile tileTarget)'](x + 2, y, 225, 0, t); })();
        kill(x + 2, y, false, true);
        return (d > 0 && d === Math.floor(plain / 4)) || `minerio ${d}, bloco ${plain}`;
    });
    check('minerio: cai o proprio item', () => {
        kill(x, y);
        return droppedNear(itemOfClass('ExampleOreItem'), x, y) || 'sem item';
    });
    check('terra continua de fora do JS', () => {
        place(x, y, 0);
        const d = damage(35);
        kill(x, y, false, true);
        return d > 0 || 'dano ' + d;
    });

    const fy = py - 1;
    for (let k = -3; k < 2; k++) place(px + k, fy, TILE);
    place(px + 2, fy, ORE);
    place(px + 3, fy, ORE);
    bl.log('tiles: fileira de mostra em ' + (px - 3) + '..' + (px + 3) + ', ' + fy);
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    if (++frames === 30) { Main.dayTime = true; Main.time = 27000; }
    if (frames === 90) {
        done = true;
        check('preparo', () => run(self));
        bl.log('tiles FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('tiles: carregado');
