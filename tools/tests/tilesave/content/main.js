// Tile de mod no mundo salvo, em rodadas (reabrindo o jogo entre elas):
//   com o Example Mod, sem tile no lugar -> poe a fileira e salva o mundo;
//   sem o Example Mod                   -> o mundo abriu; ali ha ar; salva;
//   com o Example Mod, tile no lugar    -> voltou do arquivo ao lado.
// A fileira fica acima do spawn: 5 ExampleTile (753) e 1 ExampleOre (754).
const Main = Terraria.Main;
const W = Terraria.WorldGen;
const TILE = 753, ORE = 754;

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('tilesave ' + label + ': ok');
        else { fails++; bl.log('tilesave ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('tilesave ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const place = (x, y, type) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, type, true, false, -1, 0);
const save = () => Terraria.IO.WorldFile['void SaveWorld(WorldSaveContext saveContext)'](0);
const expected = (k) => (k < 5 ? TILE : ORE);

function run() {
    const sx = Main.spawnTileX - 3, sy = Main.spawnTileY - 8;
    const row = () => Array.from({ length: 6 }, (_, k) => bl.tiles.typeAt(sx + k, sy));
    const hasMod = bl.tiles.isModTile(TILE);
    bl.log(`tilesave: fileira em ${sx},${sy}: ${row().join(' ')} (Example Mod ${hasMod ? 'ligado' : 'desligado'})`);

    if (!hasMod) {
        check('sem o mod: o mundo abriu e ali ha ar', () => row().every((t) => t === -1) || row().join(' '));
        save();
        check('sem o mod: salvar de novo nao some com a fileira do arquivo', () => true);
        bl.log('tilesave rodada: sem-mod');
        return;
    }
    if (row()[0] === TILE) {
        check('com o mod de novo: a fileira voltou', () => row().every((t, k) => t === expected(k)) || row().join(' '));
        bl.log('tilesave rodada: volta');
        return;
    }
    for (let k = 0; k < 6; k++) place(sx + k, sy, expected(k));
    check('colocou a fileira', () => row().every((t, k) => t === expected(k)) || row().join(' '));
    save();
    check('depois de salvar a fileira continua no jogo', () => row().every((t, k) => t === expected(k)) || row().join(' '));
    bl.log('tilesave rodada: salvou');
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    if (++frames === 90) {
        done = true;
        check('preparo', run);
        bl.log('tilesave FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('tilesave: carregado');
