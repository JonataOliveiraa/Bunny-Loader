// Tile de mod durante a gravacao automatica (precisa do Example Mod ligado).
// A gravacao automatica (WorldGen.saveAndPlay) roda numa thread com o jogo
// andando. O save do mundo tirava os tiles de mod do mundo enquanto gravava:
// eles sumiam por uns quadros e o jogador caia dentro deles. Aqui: 5 Example
// Tile, a gravacao disparada, e a cada quadro, ate o <mundo>.wld.tiles.bl
// trazer os 5, a conferencia de que continuam no lugar.
// Loga 'tileautosave ...'; sem o Example Mod, so avisa e termina.
const Main = Terraria.Main;
const W = Terraria.WorldGen;
const START_FRAME = 150;       // depois do tilesave (quadro 90), que tambem salva
const TIMEOUT_FRAMES = 1200;   // 20 s

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('tileautosave ' + label + ': ok');
        else { fails++; bl.log('tileautosave ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('tileautosave ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const place = (x, y, type) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, type, true, false, -1, 0);
const kill = (x, y) => W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](x, y, false, false, true);

let TILE = -1, cells = [], sideFile = '', frames = 0, waited = 0, missingFrames = 0, firstMissing = '';
let phase = 'wait';   // wait -> saving -> done

function start() {
    TILE = ModContent.TileType('examplemod/ExampleTile');
    if (!(TILE >= bl.tiles.vanillaCount)) {
        bl.log('tileautosave: sem o Example Mod (ExampleTile = ' + TILE + '); nada a conferir');
        return false;
    }
    // Longe da fileira do tilesave (spawnY - 8) e do tilemap.
    const sx = Main.spawnTileX - 20, sy = Main.spawnTileY - 14;
    cells = [0, 1, 2, 3, 4].map((k) => [sx + k, sy]);
    for (const [x, y] of cells) {
        if (bl.tiles.typeAt(x, y) >= 0) kill(x, y);
        place(x, y, TILE);
    }
    check('colocou', () => cells.every(([x, y]) => bl.tiles.typeAt(x, y) === TILE) ||
        cells.map(([x, y]) => bl.tiles.typeAt(x, y)).join(' '));
    sideFile = String(Main.ActiveWorldFileData.Path) + '.tiles.bl';
    bl.log(`tileautosave: ${cells.length} Example Tile em ${sx},${sy}; gravacao automatica disparada`);
    W['void saveAndPlay()']();
    return true;
}

/** A gravacao terminou: o arquivo ao lado ja traz os 5. */
function saved() {
    const text = bl.file.read(sideFile);
    return text !== undefined && cells.every(([x, y]) => text.includes(`\n${x}\t${y}\t`));
}

function tick() {
    const now = cells.map(([x, y]) => bl.tiles.typeAt(x, y));
    if (!now.every((t) => t === TILE)) {
        missingFrames++;
        if (!firstMissing) firstMissing = `quadro ${waited}: ${now.join(' ')}`;
    }
    waited++;
    if (!saved() && waited < TIMEOUT_FRAMES) return;
    phase = 'done';
    check('a gravacao terminou', () => waited < TIMEOUT_FRAMES || `sem o ${sideFile} com os tiles em ${waited} quadros`);
    bl.log(`tileautosave: a gravacao levou ${waited} quadro(s)`);
    check('os tiles de mod nao sumiram durante a gravacao', () =>
        missingFrames === 0 || `${missingFrames} quadro(s) sem eles; o primeiro: ${firstMissing}`);
    check('continuam no lugar depois', () => cells.every(([x, y]) => bl.tiles.typeAt(x, y) === TILE) ||
        cells.map(([x, y]) => bl.tiles.typeAt(x, y)).join(' '));
    bl.log('tileautosave FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
}

Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || phase === 'done') return;
    if (phase === 'saving') { tick(); return; }
    if (++frames !== START_FRAME) return;
    let ok = false;
    check('preparo', () => { ok = start(); });
    if (ok) phase = 'saving';
    else { phase = 'done'; bl.log('tileautosave FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)')); }
});
bl.log('tileautosave: carregado');

// A classe do mod, obrigatória no arquivo de entrada.
export default class TestTileautosave extends Mod {}
