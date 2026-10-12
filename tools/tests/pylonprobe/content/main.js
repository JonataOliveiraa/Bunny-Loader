// Sonda do pedido de teletransporte de pilar (só loga, não muda nada).
//
// Rodada 1 (2026-10-11, celular): perto do Pilar de Exemplo, o toque no pilar
// da floresta dava "não está perto o bastante de um pilar": no celular o
// IsPlayerNearAPylon percorre o Main.PylonSystem._pylons, e o PylonLoader tirava
// os de mod dessa lista durante o TeleportPylonsMapLayer.Draw, onde o toque
// num ícone do jogo é tratado. Corrigido no PylonLoader.
//
// Rodada 2: sem moradores, o teletransporte entre o de exemplo e a floresta
// ainda ia, nos dois sentidos; e o de exemplo fora do bioma dele ainda servia
// de saída. Loga, durante cada pedido, as checagens que o jogo faz (moradores
// do destino e do pilar de onde se sai, perto de um pilar) e os moradores com
// casa. Loga "pylonprobe ...".
const Main = Terraria.Main;
const System = Terraria.GameContent.TeleportPylonsSystem;
const Reach = Terraria.DataStructures.TileReachCheckSettings;
const at = (p) => p.TypeOfPylon + '@' + p.PositionInTiles.X + ',' + p.PositionInTiles.Y;
let inRequest = false;

function list() {
    const pylons = Main.PylonSystem._pylons, out = [];
    for (let k = 0; k < pylons.Count; k++) out.push(at(pylons.get_Item(k)));
    return out.join(' ');
}

// Os moradores que contam para um pilar: ativos, como morador, com casa.
function townNpcs() {
    const out = [];
    for (let k = 0; k < Main.maxNPCs; k++) {
        const n = Main.npc[k];
        if (n.active && n.isLikeATownNPC) out.push(n.type + (n.homeless ? '(sem casa)' : '@' + n.homeTileX + ',' + n.homeTileY));
    }
    return out.join(' ') || 'nenhum';
}

function report(where, player) {
    const near = System['bool IsPlayerNearAPylon(Player player)'](player);
    const modType = ModContent.Find(ModTile, 'examplemod/ExamplePylonTile').Type;
    const byTile = player['bool IsTileTypeInInteractionRange(int targetTileType, TileReachCheckSettings settings)'](modType, Reach.Pylons);
    const c = player.Center;
    bl.log('pylonprobe ' + where + ': jogador ' + Math.floor(c.X / 16) + ',' + Math.floor(c.Y / 16) +
        ' near=' + near + ' tile' + modType + '=' + byTile + ' lista=[' + list() + ']');
}

System['void HandleTeleportRequest(TeleportPylonInfo info, int playerIndex)'].hook((original, system, info, playerIndex) => {
    const player = Main.player[playerIndex];
    try {
        bl.log('pylonprobe pedido: destino ' + at(info) + ' moradores: ' + townNpcs());
        report('no pedido', player);
        const enough = System['bool DoesPositionHaveEnoughNPCs(int necessaryNPCCount, Point16 centerPoint)'];
        bl.log('pylonprobe no pedido (JS): moradores no destino ' + enough(2, info.PositionInTiles));
        const pylons = system._pylons;
        for (let k = 0; k < pylons.Count; k++) {
            const p = pylons.get_Item(k);
            if (!player['bool InTileEntityInteractionRange(int interactX, int interactY, int tileSizeX, int tileSizeY, TileReachCheckSettings settings)'](
                p.PositionInTiles.X, p.PositionInTiles.Y, 3, 4, Reach.Pylons)) continue;
            bl.log('pylonprobe no pedido (JS): perto de ' + at(p) + ', moradores lá ' + enough(2, p.PositionInTiles));
        }
    } catch (e) {
        bl.log('pylonprobe no pedido: erro ' + e);
    }
    const before = player.Center;
    const bx = Math.floor(before.X / 16), by = Math.floor(before.Y / 16);
    inRequest = true;
    try {
        return original(system, info, playerIndex);
    } finally {
        inRequest = false;
        const after = player.Center;
        const ax = Math.floor(after.X / 16), ay = Math.floor(after.Y / 16);
        bl.log('pylonprobe fim do pedido: ' + (ax !== bx || ay !== by ? 'TELETRANSPORTOU para ' + ax + ',' + ay : 'ficou em ' + ax + ',' + ay));
    }
});

// As chamadas do próprio jogo dentro do pedido (estes hooks ficam por dentro
// dos do PylonLoader: veem o que o jogo responde).
System['bool DoesPositionHaveEnoughNPCs(int necessaryNPCCount, Point16 centerPoint)'].hook((original, count, center) => {
    const result = original(count, center);
    if (inRequest) bl.log('pylonprobe   jogo: DoesPositionHaveEnoughNPCs(' + count + ', ' + center.X + ',' + center.Y + ') = ' + result);
    return result;
});

System['bool IsPlayerNearAPylon(Player player)'].hook((original, player) => {
    const result = original(player);
    if (inRequest) bl.log('pylonprobe   jogo: IsPlayerNearAPylon = ' + result);
    return result;
});

let frames = 0;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || ++frames % 600 !== 0) return;
    try {
        report('fora do mapa', self);
    } catch (e) {
        bl.log('pylonprobe fora do mapa: erro ' + e);
    }
});

bl.log('pylonprobe FIM: carregado');

export default class TestPylonProbe extends Mod {}
