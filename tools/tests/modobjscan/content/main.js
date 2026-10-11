// Varredura do mundo: todo objeto de mod com TileObjectData (móvel 2x2, 3x2,
// 2x5...) que está pela metade (falta célula ou há outra coisa no lugar) ou
// sem apoio embaixo. Não mexe em nada: só loga onde está, em relação ao spawn.
// Loga "objscan ..." e "objscan FIM".
const Main = Terraria.Main;
const { TileObjectData } = Terraria.ObjectData;

const shapes = new Map();   // tipo -> forma do TileObjectData
function shapeOf(type) {
    if (shapes.has(type)) return shapes.get(type);
    const d = TileObjectData['TileObjectData GetTileData(int type, int style, int alternate)'](type, 0, 0);
    const s = d && d.Width * d.Height > 1 ? {
        w: d.Width, h: d.Height, cw: d.CoordinateWidth, pad: d.CoordinatePadding,
        fullW: d.CoordinateFullWidth, fullH: d.CoordinateFullHeight, heights: Array.from(d.CoordinateHeights),
    } : null;
    shapes.set(type, s);
    return s;
}

function partOf(s, frameX, frameY) {
    const px = Math.floor((frameX % s.fullW) / (s.cw + s.pad));
    let py = 0;
    for (let rest = frameY % s.fullH; py + 1 < s.h && rest - s.heights[py] - s.pad >= 0; py++) rest -= s.heights[py] + s.pad;
    return { px, py };
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    if (++frames !== 30) return;
    done = true;

    const first = bl.tiles.vanillaCount, count = Main.tileFrameImportant.length;
    let marked = 0;
    for (let t = first; t < count; t++) if (shapeOf(t)) { bl.hookMarks.set('objscan', t); marked++; }
    const found = bl.tiles.find('objscan', 0, 0, Main.maxTilesX - 1, Main.maxTilesY - 1, true);

    // Agrupa as células pelo canto de cima à esquerda do objeto.
    const objects = new Map();
    for (let k = 0; k < found.length; k += 4) {
        const x = found[k], y = found[k + 1], type = bl.tiles.typeAt(x, y);
        const s = shapeOf(type);
        if (!s) continue;
        const { px, py } = partOf(s, found[k + 2], found[k + 3]);
        const key = `${x - px},${y - py},${type}`;
        if (!objects.has(key)) objects.set(key, { left: x - px, top: y - py, type, s, cells: 0 });
        objects.get(key).cells++;
    }

    const sx = Main.spawnTileX, sy = Main.spawnTileY;
    let partial = 0, floating = 0;
    for (const o of objects.values()) {
        const total = o.s.w * o.s.h;
        const missing = [];
        for (let i = o.left; i < o.left + o.s.w; i++) {
            for (let j = o.top; j < o.top + o.s.h; j++) {
                if (bl.tiles.typeAt(i, j) !== o.type) missing.push(`${i - sx},${j - sy}=${bl.tiles.typeAt(i, j)}`);
            }
        }
        let below = 0;
        for (let i = o.left; i < o.left + o.s.w; i++) if (bl.tiles.typeAt(i, o.top + o.s.h) >= 0) below++;
        const where = `tipo ${o.type} (${o.s.w}x${o.s.h}) em ${o.left - sx},${o.top - sy} do spawn`;
        if (o.cells < total || missing.length) {
            partial++;
            bl.log(`objscan pela metade: ${where}, ${o.cells}/${total} células; no lugar das que faltam: ${missing.join(' ')}`);
        } else if (below === 0) {
            floating++;
            bl.log(`objscan sem nada embaixo: ${where}`);
        }
    }
    bl.log(`objscan FIM: ${objects.size} objeto(s) de mod (${marked} tipo(s)), ${partial} pela metade, ${floating} sem nada embaixo; spawn ${sx},${sy}`);
});
bl.log('objscan: carregado');

export default class TestObjScan extends Mod {}
