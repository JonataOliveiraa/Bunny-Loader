// Tira as lápides do mundo de teste. Jogador que morre num teste deixa uma, e
// com algumas por perto o jogo liga o cemitério (ZoneGraveyard): a música, a
// luz, a névoa e o spawn mudam e atrapalham os outros testes.
// No 2º quadro dentro do mundo: as paradas (bloco 85, 2 x 2) num retângulo em
// volta do spawn e do jogador, e as que ainda estão caindo (projétil com
// aiStyle 17); depois salva o mundo, se tirou alguma.
// Loga "cleanworld: N lápide(s) ..." e "cleanworld FIM".
const Main = Terraria.Main;
const GRAVE = Terraria.ID.TileID.Tombstones;   // 85
const RANGE_X = 200, ABOVE = 80, BELOW = 40;   // em blocos

const kill = Terraria.WorldGen['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'];

function clean() {
    const p = Main.player[Main.myPlayer];
    const areas = [
        [Math.floor(p.Center.X / 16), Math.floor(p.Center.Y / 16)],
        [Main.spawnTileX, Main.spawnTileY],
    ];
    let graves = 0;
    for (const [cx, cy] of areas) {
        const x0 = Math.max(1, cx - RANGE_X), x1 = Math.min(Main.maxTilesX - 2, cx + RANGE_X);
        const y0 = Math.max(1, cy - ABOVE), y1 = Math.min(Main.maxTilesY - 2, cy + BELOW);
        for (let x = x0; x <= x1; x++) {
            for (let y = y0; y <= y1; y++) {
                if (bl.tiles.typeAt(x, y) !== GRAVE) continue;
                kill(x, y, false, false, true);   // sem o item; o 2 x 2 sai inteiro
                if (bl.tiles.typeAt(x, y) !== GRAVE) graves++;
            }
        }
    }

    let falling = 0;
    for (let i = 0; i < Main.projectile.length; i++) {
        const proj = Main.projectile[i];
        if (proj.active && proj.aiStyle === 17) { proj.active = false; falling++; }
    }

    if (graves > 0) Terraria.IO.WorldFile['void SaveWorld(WorldSaveContext saveContext)'](0);
    bl.log(`cleanworld: ${graves} lápide(s) tirada(s), ${falling} caindo${graves > 0 ? '; mundo salvo' : ''}`);
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (done || i !== Main.myPlayer || Main.gameMenu) return;
    if (++frames < 2) return;
    done = true;
    try {
        clean();
    } catch (e) {
        bl.log('cleanworld: erro ' + e);
    }
    bl.log('cleanworld FIM');
});

export default class CleanWorld extends Mod {}
