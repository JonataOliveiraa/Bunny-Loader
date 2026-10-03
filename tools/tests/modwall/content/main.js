const Main = Terraria.Main;
const W = Terraria.WorldGen;
const WF = Terraria.IO.WorldFile;
const PLACE = 'void PlaceWall(int i, int j, int type, bool mute)';
const KILL = 'void KillWall(int i, int j, bool fail)';
const SAVE = 'void SaveWorld(WorldSaveContext saveContext)';
const PAINT = 3;
let fails = 0;
let frames = 0;
let done = false;
let stage = 0;
let advanced = null;
let lightCalls = 0;
const framesSeen = new Set();

function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('modwall ' + label + ': ok');
        else { fails++; bl.log('modwall ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) { fails++; bl.log('modwall ' + label + ': FALHOU com ' + e); }
}

function spotFile(mod) {
    return bl.path.join(mod.root, 'spot.json');
}

function clearDrops(itemType) {
    let n = 0;
    for (let k = 0; k < 400; k++) {
        const w = Main.item[k];
        if (w.active && w.type === itemType) {
            w.TurnToAir();
            n++;
        }
    }
    return n;
}

function free(x, y, itemType) {
    if (bl.walls.typeAt(x, y) > 0) {
        W[KILL](x, y, false);
        clearDrops(itemType);
    }
    return bl.walls.typeAt(x, y) <= 0;
}

function run(mod) {
    const found = new Ref();
    if (!ModContent.TryFind(ModWall, 'examplemod/ExampleWall', found)) {
        bl.log('modwall FIM: sem o Example Mod');
        return;
    }
    const wall = found.value;
    const type = wall.Type;
    const item = ModContent.ItemType('examplemod/ExampleWall');
    const p = Main.player[Main.myPlayer];
    const px = Math.floor(p.Center.X / 16), py = Math.floor(p.Center.Y / 16);

    check('tipo depois dos do jogo', () => (type >= bl.walls.vanillaCount && bl.walls.isModWall(type)) || type);
    check('WallID.Count aumentado', () => Terraria.ID.WallID.Count > type || Terraria.ID.WallID.Count);
    check('textura na TextureAssets.Wall', () => !!Terraria.GameContent.TextureAssets.Wall[type].Value || 'sem textura');
    check('SetStaticDefaults rodou (Main.wallHouse)', () => Main.wallHouse[type] === true || 'false');
    check('wallBlend do proprio tipo', () => Main.wallBlend[type] === type || Main.wallBlend[type]);
    check('o item coloca a parede', () => {
        const it = Terraria.Item.new();
        it['void .ctor()']();
        it['void SetDefaults(int Type, ItemVariant variant)'](item, null);
        return it.createWall === type || it.createWall;
    });
    check('GetContent acha a parede', () => ModContent.GetContent(ModWall).includes(wall) || 'nao');
    const MapHelper = Terraria.Map.MapHelper;
    check('mapa: wallLookup aponta para a entrada da parede', () => {
        const index = MapHelper.wallLookup[type];
        if (!(index > 0) || MapHelper.wallOptionCounts[type] !== 1) return 'indice ' + index + ', opcoes ' + MapHelper.wallOptionCounts[type];
        const c = MapHelper.colorLookup[index];
        return (c.R === 150 && c.G === 150 && c.B === 150) || `cor ${c.R},${c.G},${c.B}`;
    });

    const adv = new Ref();
    if (ModContent.TryFind(ModWall, 'examplemod/ExampleWallAdvanced', adv)) {
        const proto = adv.value.constructor.prototype;
        const light = proto.ModifyLight;
        proto.ModifyLight = function (...args) {
            lightCalls++;
            return light.apply(this, args);
        };
        const ax = px - 6, ay = py - 3;
        free(ax, ay, ModContent.ItemType('examplemod/ExampleWallAdvanced'));
        W[PLACE](ax, ay, adv.value.Type, true);
        advanced = { type: adv.value.Type, x: ax, y: ay, item: ModContent.ItemType('examplemod/ExampleWallAdvanced') };
    } else {
        fails++;
        bl.log('modwall ExampleWallAdvanced: FALHOU (nao achada)');
    }

    const file = spotFile(mod);
    const saved = bl.file.exists(file) ? JSON.parse(bl.file.read(file)) : null;
    if (saved) {
        check('load: a parede salva voltou do .walls.bl', () => bl.walls.typeAt(saved.x, saved.y) === type || bl.walls.typeAt(saved.x, saved.y));
        check('load: a tinta voltou', () => {
            const c = Main.tile['Tile get_Item(int x, int y)'](saved.x, saved.y)['byte wallColor()']();
            return c === PAINT || 'cor ' + c;
        });
        W[KILL](saved.x, saved.y, false);
        clearDrops(item);
        bl.file.delete(file);
        stage = 1;
        return;
    }

    const x = px + 6, y = py - 3;
    check('lugar livre', () => free(x, y, item) || 'parede la');
    W[PLACE](x, y, type, true);
    check('PlaceWall coloca a parede de mod', () => bl.walls.typeAt(x, y) === type || bl.walls.typeAt(x, y));
    check('Main.tile ve o tipo', () => Main.tile['Tile get_Item(int x, int y)'](x, y).wall === type || 'outro');

    const before = clearDrops(item);
    W[KILL](x, y, false);
    check('KillWall tira a parede', () => bl.walls.typeAt(x, y) <= 0 || 'ficou');
    check('KillWall deixa o item da parede', () => clearDrops(item) === 1 || 'sem drop (antes ' + before + ')');

    const cx = px + 4, cy = py - 5;
    free(cx, cy, item);
    Terraria.ID.WallID.Sets.Conversion.Stone[type] = true;
    W[PLACE](cx, cy, type, true);
    const activeBefore = new Set();
    for (let k = 0; k < 400; k++) if (Main.item[k].active) activeBefore.add(k);
    W['void Convert(int i, int j, int conversionType, int size, bool tiles, bool walls)'](cx, cy, 1, 0, false, true);
    const converted = bl.walls.typeAt(cx, cy);
    check('conversao: Conversion.Stone faz a corrupcao trocar a parede', () => (converted > 0 && converted !== type && converted < bl.walls.vanillaCount) || converted);
    Terraria.ID.WallID.Sets.Conversion.Stone[type] = false;
    if (converted > 0) W[KILL](cx, cy, false);
    for (let k = 0; k < 400; k++) if (Main.item[k].active && !activeBefore.has(k)) Main.item[k].TurnToAir();

    const sx = px + 8, sy = py - 3;
    free(sx, sy, item);
    W[PLACE](sx, sy, type, true);
    Main.tile['Tile get_Item(int x, int y)'](sx, sy)['void wallColor(byte wallColor)'](PAINT);
    WF[SAVE](0);
    const side = Main.ActiveWorldFileData.Path + '.walls.bl';
    check('save: a parede vai para o .walls.bl', () => {
        const text = bl.file.read(side) || '';
        return text.includes(sx + '\t' + sy + '\t' + PAINT + '\t') && text.includes('/ExampleWall') || JSON.stringify(text.slice(0, 160));
    });
    check('a parede continua no mundo depois do save', () => bl.walls.typeAt(sx, sy) === type || bl.walls.typeAt(sx, sy));
    bl.file.write(file, JSON.stringify({ x: sx, y: sy }));
    stage = 1;
}

function finish() {
    if (advanced) {
        check('AnimateWall troca o Main.wallFrame', () => (framesSeen.has(0) && framesSeen.has(1)) || [...framesSeen].join(','));
        check('ModifyLight e chamado', () => lightCalls > 0 || 'nenhuma');
        W[KILL](advanced.x, advanced.y, false);
        clearDrops(advanced.item);
    }
    WF[SAVE](0);
    bl.log('modwall FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
}

export class Probe extends ModSystem {
    PostUpdateEverything() {
        if (done || Main.gameMenu) return;
        ++frames;
        if (advanced) framesSeen.add(Main.wallFrame[advanced.type]);
        if (stage === 0 && frames >= 90) {
            stage = -1;
            run(this.Mod);
        } else if (stage === 1 && frames >= 400) {
            done = true;
            finish();
        }
    }
}

export default class TestModWall extends Mod {}
