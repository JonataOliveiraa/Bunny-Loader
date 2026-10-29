const Main = Terraria.Main;
const COLUMNS_PER_FRAME = 12;
const GRASS = 2, DIRT = 0;

const tileAt = Main.tile['Tile get_Item(int x, int y)'];

let ground = 0, column = 0, frames = 0, started = 0, done = false;

function start() {
    ground = Math.floor(Main.worldSurface) - 30;
    started = Date.now();
    bl.log(`flatworld: ${Main.maxTilesX} x ${Main.maxTilesY}, ground at ${ground}`);
}

function flattenColumns() {
    const maxY = Main.maxTilesY;
    const end = Math.min(Main.maxTilesX, column + COLUMNS_PER_FRAME);
    for (; column < end; column++) {
        for (let y = 0; y < maxY; y++) {
            const t = tileAt(column, y);
            t['void ClearEverything()']();
            if (y >= ground) {
                t['void active(bool active)'](true);
                t.type = y === ground ? GRASS : DIRT;
            }
        }
    }
    return column >= Main.maxTilesX;
}

function finish() {
    let chests = 0, signs = 0, npcs = 0;
    for (let i = 0; i < Main.chest.length; i++) {
        try { if (Main.chest[i]) { Main.chest[i] = null; chests++; } } catch (e) {}
    }
    for (let i = 0; i < Main.sign.length; i++) {
        try { if (Main.sign[i]) { Main.sign[i] = null; signs++; } } catch (e) {}
    }
    for (let i = 0; i < Main.npc.length; i++) {
        const n = Main.npc[i];
        if (n.active && !n.townNPC) { n.active = false; npcs++; }
    }
    for (let i = 0; i < Main.projectile.length; i++) Main.projectile[i].active = false;
    try { Terraria.Liquid.numLiquid = 0; Terraria.LiquidBuffer.numLiquidBuffer = 0; } catch (e) {}

    Main.spawnTileX = Math.floor(Main.maxTilesX / 2);
    Main.spawnTileY = ground - 1;
    const p = Main.player[Main.myPlayer];
    p.position.X = Main.spawnTileX * 16 - p.width / 2;
    p.position.Y = (ground - 3) * 16;
    p.velocity.X = 0;
    p.velocity.Y = 0;

    Terraria.IO.WorldFile['void SaveWorld(WorldSaveContext saveContext)'](0);
    bl.log(`flatworld: ${chests} chest(s), ${signs} sign(s), ${npcs} npc(s) removed; spawn ${Main.spawnTileX},${Main.spawnTileY}; ${((Date.now() - started) / 1000).toFixed(1)} s; world saved`);
}

Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (done || i !== Main.myPlayer || Main.gameMenu) return;
    if (++frames < 2) return;
    try {
        if (frames === 2) start();
        if (!flattenColumns()) return;
        done = true;
        finish();
    } catch (e) {
        done = true;
        bl.log('flatworld: error ' + e);
    }
    bl.log('flatworld END');
});

export default class FlatWorld extends Mod {}
