// Tile de mod no mapa (precisa do Example Mod ligado), em tres rodadas, cada
// uma numa abertura do jogo (o `Main.Map.Load()` no meio do jogo nao serve: os
// pedacos do mapa ja abertos seguem com o que tinham):
//   grava  -> poe 3 Example Tile longe da tela e revela no mapa: indice proprio
//             depois do inferno, cor e nome do AddMapEntry; salva o mapa e o
//             mundo; o <mapa>.map.bl guarda as celulas; e ele sai do lugar;
//   escuro -> sem o .map.bl, o .map trouxe as celulas escuras; ele volta;
//   volta  -> as celulas voltaram do .map.bl; tira os tiles e salva o mundo.
// Longe da tela: perto do jogador, o jogo redesenharia as celulas sozinho.
const Main = Terraria.Main;
const W = Terraria.WorldGen;
const MapHelper = Terraria.Map.MapHelper;
const STATE = bl.path.join(bl.mod.dataDirectory, 'state.json');

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
const saveWorld = () => Terraria.IO.WorldFile['void SaveWorld(WorldSaveContext saveContext)'](0);
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
    const state = JSON.parse(bl.file.read(STATE) || '{"round":"grava"}');
    // Longe da tela do jogador, que nasce no spawn.
    const sx = Main.spawnTileX + 150, sy = Main.spawnTileY - 40;
    const cells = [0, 1, 2].map((k) => [sx + k, sy]);
    const at = () => cells.map(([x, y]) => describe(bl.tiles.mapTileAt(x, y))).join(' ');
    const map = mapFile();
    const side = map && map + '.bl';
    bl.log(`tilemap rodada ${state.round}: Example Tile ${TILE} em ${sx},${sy}; no mapa: ${at()}`);

    if (state.round === 'grava') {
        for (const [x, y] of cells) {
            if (bl.tiles.typeAt(x, y) >= 0) kill(x, y);
            place(x, y, TILE);
        }
        check('colocou', () => cells.every(([x, y]) => bl.tiles.typeAt(x, y) === TILE) ||
            cells.map(([x, y]) => bl.tiles.typeAt(x, y)).join(' '));
        for (const [x, y] of cells) reveal(x, y);
        const index = bl.tiles.mapTileAt(sx, sy)?.Type ?? -1;
        const hell = MapHelper.hellPosition;
        bl.log(`tilemap: no mapa: ${at()}; inferno em ${hell}`);
        check('indice proprio, depois do inferno', () => index > hell || `indice ${index}, inferno ${hell}`);
        check('nome no mapa', () => {
            const name = Terraria.Lang['string GetMapObjectName(int id)'](index);
            return name === 'Example Block' || name === 'Bloco de Exemplo' || JSON.stringify(name);
        });
        check('cor do AddMapEntry', () => {
            const c = MapHelper.colorLookup[index];
            return (c.R === 0 && c.G === 200 && c.B === 255) || `${c.R} ${c.G} ${c.B}`;
        });
        saveMap();
        check('depois de salvar a celula continua no jogo', () =>
            cells.every(([x, y]) => bl.tiles.mapTileAt(x, y)?.Type === index) || at());
        const current = mapFile();
        const currentSide = current && current + '.bl';
        const text = currentSide && bl.file.read(currentSide);
        check('o .map.bl guarda as celulas', () =>
            (text !== undefined && cells.every(([x, y]) => text.includes(`\n${x}\t${y}\t`))) || String(text).slice(0, 200));
        saveWorld();
        if (text !== undefined) {
            bl.file.write(currentSide + '.off', text);
            bl.file.delete(currentSide);
        }
        bl.file.write(STATE, JSON.stringify({ round: 'escuro', index }));
        return;
    }
    if (state.round === 'escuro') {
        check('sem o .map.bl: o .map trouxe as celulas escuras', () =>
            cells.every(([x, y]) => { const c = bl.tiles.mapTileAt(x, y); return c && c.Type === 0 && c.Light === 0; }) || at());
        const off = side && bl.file.read(side + '.off');
        check('o .map.bl guardado existe', () => off !== undefined || String(side));
        if (off !== undefined) {
            bl.file.write(side, off);
            bl.file.delete(side + '.off');
        }
        bl.file.write(STATE, JSON.stringify({ round: 'volta', index: state.index }));
        return;
    }
    check('com o .map.bl: as celulas voltaram', () =>
        cells.every(([x, y]) => bl.tiles.mapTileAt(x, y)?.Type === state.index) || `${at()} (esperado ${state.index})`);
    for (const [x, y] of cells) kill(x, y);
    saveWorld();
    bl.file.delete(STATE);
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
