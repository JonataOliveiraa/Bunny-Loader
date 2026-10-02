// Revelar mapa e Remover inimigos no multijogador. Os cheats são tocados no
// menu do CLIENTE (à mão ou pelo script); este mod só mede:
//   anfitrião: cria zumbis perto do jogador 1 e diz quantos inimigos tem;
//   cliente: a cada segundo, os inimigos que ele vê, quantas partes do mundo
//     ele tem (WorldSections) e, numa grade sobre o mundo inteiro, quantos
//     pontos o mapa revelou e quantos têm bloco de verdade na memória dele.
// Loga "mpcheats <lado>: ...".
const Main = Terraria.Main;

const isClient = () => Main.netMode === 1;
const newNpc = Terraria.NPC['int NewNPC(IEntitySource source, int X, int Y, int Type, int Start, float ai0, float ai1, float ai2, float ai3, int Target)'];

function hostiles() {
    let n = 0;
    for (let i = 0; i < 200; i++) {
        const npc = Main.npc[i];
        if (npc.active && !npc.friendly && !npc.townNPC && npc.damage > 0) n++;
    }
    return n;
}

function sections() {
    const manager = Main.sectionManager;
    const sx = Main.maxSectionsX, sy = Main.maxSectionsY;
    let loaded = 0;
    for (let y = 0; y < sy; y++) for (let x = 0; x < sx; x++) if (manager.SectionLoaded(x, y)) loaded++;
    return `${loaded}/${sx * sy}`;
}

// Grade de 60 x 30 pontos sobre o mundo (sem a borda preta).
function mapSample() {
    const map = Main.Map;
    let revealed = 0, solid = 0, both = 0;
    for (let gy = 0; gy < 30; gy++) {
        for (let gx = 0; gx < 60; gx++) {
            const x = 50 + Math.floor((Main.maxTilesX - 100) * gx / 60);
            const y = 50 + Math.floor((Main.maxTilesY - 100) * gy / 30);
            const r = map.IsRevealed(x, y);
            const s = bl.tiles.typeAt(x, y) >= 0;
            if (r) revealed++;
            if (s) solid++;
            if (r && s) both++;
        }
    }
    return `revelados ${revealed}/1800, com bloco ${solid}, revelados com bloco ${both}`;
}

let frames = 0, spawned = false, last = '';
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu) return;
    if (++frames % 60 !== 0) return;

    if (isClient()) {
        const line = `inimigos ${hostiles()}, partes ${sections()}, ${mapSample()}`;
        if (line !== last) bl.log('mpcheats cliente: ' + line);
        last = line;
        return;
    }
    if (Main.netMode === 0) return;

    const guest = Main.player[1];
    if (!spawned && guest && guest.active && frames > 600) {
        spawned = true;
        for (let k = 0; k < 6; k++) {
            const idx = newNpc(Terraria.DataStructures.EntitySource_DebugCommand.new(), Math.floor(guest.Center.X) + (k - 3) * 48,
                               Math.floor(guest.position.Y) - 64, Terraria.ID.NPCID.Zombie, 0, 0, 0, 0, 0, 1);
            const npc = Main.npc[idx];
            npc.damage = 1;
            npc.lifeMax = npc.life = 5000;
        }
    }
    const line = `inimigos ${hostiles()}`;
    if (line !== last) bl.log('mpcheats anfitrião: ' + line);
    last = line;
});
bl.log('mpcheats: carregado');

export default class TestMpCheats extends Mod {}
