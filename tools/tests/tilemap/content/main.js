// Tile de mod no mapa (precisa do Example Mod ligado), numa rodada:
//   poe 3 Example Tile e revela no mapa -> indice proprio depois do inferno,
//   com a cor do AddMapEntry e o nome dele (e nao o de um tile do jogo);
//   salva o mapa -> a celula continua no jogo e o <mapa>.map.bl a guarda;
//   recarrega -> volta; recarrega sem o .map.bl -> o .map trouxe escuro;
//   recarrega com o .map.bl de volta -> volta de novo.
const Main = Terraria.Main;
const W = Terraria.WorldGen;
const MapHelper = Terraria.Map.MapHelper;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('tilemap ' + label + ': ok');
        else { fails++; bl.log('tilemap ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('tilemap ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const place = (x, y, type) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, type, true, false, -1, 0);
const kill = (x, y) => W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](x, y, false, false, true);
const reveal = (x, y) => Main.Map['bool UpdateLighting(int x, int y, byte light)'](x, y, 255);
const saveMap = () => MapHelper['void SaveMap(bool forceSave)'](true);
const loadMap = () => Main.Map['void Load()']();
const describe = (c) => (c ? `${c.Type}/${c.Light}` : 'fora');

/** O .map do jogador e mundo abertos (o nome pelo Guid ou pelo worldID). */
function mapFile() {
    const player = Main.playerPathName;
    const dir = player.substring(0, player.length - 4) + '/';
    const world = Main.ActiveWorldFileData;
    for (const name of [String(world.MapFileName), String(Main.worldID)]) {
        if (bl.file.exists(dir + name + '.map')) return dir + name + '.map';
    }
    return undefined;
}

function run() {
    const TILE = ModContent.TileType('examplemod/ExampleTile');
    if (!(TILE >= bl.tiles.vanillaCount)) {
        check('Example Mod ligado', () => 'ExampleTile = ' + TILE);
        return;
    }
    // Longe da fileira do tilesave (spawnY - 8) e do spawn.
    const sx = Main.spawnTileX + 6, sy = Main.spawnTileY - 12;
    const cells = [0, 1, 2].map((k) => [sx + k, sy]);
    for (const [x, y] of cells) {
        if (bl.tiles.typeAt(x, y) >= 0) kill(x, y);
        place(x, y, TILE);
    }
    check('colocou', () => cells.every(([x, y]) => bl.tiles.typeAt(x, y) === TILE) ||
        cells.map(([x, y]) => bl.tiles.typeAt(x, y)).join(' '));
    for (const [x, y] of cells) reveal(x, y);

    const [x0, y0] = cells[0];
    const index = bl.tiles.mapTileAt(x0, y0)?.Type ?? -1;
    const hell = MapHelper.hellPosition;
    const at = () => cells.map(([x, y]) => describe(bl.tiles.mapTileAt(x, y))).join(' ');
    const same = () => cells.every(([x, y]) => bl.tiles.mapTileAt(x, y)?.Type === index) || at();
    bl.log(`tilemap: Example Tile ${TILE} em ${sx},${sy}; no mapa: ${at()}; inferno em ${hell}`);

    check('indice proprio, depois do inferno', () => index > hell || `indice ${index}, inferno ${hell}`);
    check('as 3 celulas com o mesmo indice', same);
    check('nome no mapa', () => {
        const name = Terraria.Lang['string GetMapObjectName(int id)'](index);
        return name === 'Example Block' || name === 'Bloco de Exemplo' || JSON.stringify(name);
    });
    check('cor do AddMapEntry', () => {
        const c = MapHelper.colorLookup[index];
        return (c.R === 0 && c.G === 200 && c.B === 255) || `${c.R} ${c.G} ${c.B}`;
    });

    saveMap();
    check('depois de salvar a celula continua no jogo', same);
    const map = mapFile();
    const side = map && map + '.bl';
    check('o .map existe', () => !!map || Main.playerPathName);
    if (!map) return;
    const text = bl.file.read(side);
    check('o .map.bl guarda as celulas', () =>
        (text !== undefined && cells.every(([x, y]) => text.includes(`\n${x}\t${y}\t`))) || String(text).slice(0, 200));

    loadMap();
    check('recarregado: voltou do .map.bl', same);

    bl.file.delete(side);
    loadMap();
    check('sem o .map.bl: o .map trouxe escuro', () =>
        cells.every(([x, y]) => { const c = bl.tiles.mapTileAt(x, y); return c && c.Type === 0 && c.Light === 0; }) || at());

    bl.file.write(side, text);
    loadMap();
    check('com o .map.bl de volta: voltou', same);

    for (const [x, y] of cells) kill(x, y);
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    if (++frames === 90) {
        done = true;
        check('preparo', run);
        bl.log('tilemap FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('tilemap: carregado');

// A classe do mod, obrigatória no arquivo de entrada.
export default class TestTilemap extends Mod {}
