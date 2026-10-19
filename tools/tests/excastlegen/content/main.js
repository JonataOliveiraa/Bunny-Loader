// O castelo do Example Mod gerado com o mundo (ExampleStructureSystem): cria
// um mundo pequeno pelo menu (nome, semente e tamanho fixos), confere que a
// etapa "Example Structure" rodou logo depois de "Sunflowers", acha o castelo
// pelo baú e compara os tijolos com o mapa, confere a área protegida no
// GenVars.structures e, já no mundo, leva o jogador até ele para a captura.
// Loga "excastlegen ..." e "excastlegen FIM".
const Main = Terraria.Main;
const { TileID } = Terraria.ID;
const building = Terraria.WorldBuilding;
const worldName = 'BL_Castle_gen';
const log = (s) => bl.log('excastlegen ' + s);
let fails = 0;
function check(label, ok, detail) {
    if (!ok) fails++;
    log(label + ': ' + (ok ? 'ok' : 'FALHOU') + (detail === undefined ? '' : ' (' + detail + ')'));
}

const Tiles = [
    '# # # #                   # # # #',
    '#######                   #######',
    '#     #                   #     #',
    '#     #                   #     #',
    '#  t  #                   #  t  #',
    '#     #                   #     #',
    '#     ## # # # # # # # # ##     #',
    '#=====#####################=====#',
    '#     #  B     L      B   #     #',
    '#     #                   #     #',
    '#==   D     t       t     D   ==#',
    '#   t                       t   #',
    '#                               #',
    '#=====#EEEEEEEE===EEEEEEEE#=====#',
    '#     #                   #     #',
    '#  t  #      t       t    #  t  #',
    'D==   D           ==      D   ==D',
    '        hT  H         C          ',
    '                   W             ',
    '#################################',
];
const Width = 33, Height = 20;
const expectedBricks = Tiles.join('').split('#').length - 1;
const TileAt = (x, y) => Main.tile['Tile get_Item(int x, int y)'](x, y);
const isTestWorld = () => Main.ActiveWorldFileData && Main.ActiveWorldFileData.Name === worldName;

const passes = [];
let castle = null;

function bricksAt(left, top) {
    let n = 0;
    for (let row = 0; row < Height; row++) {
        for (let col = 0; col < Width; col++) {
            if (Tiles[row][col] !== '#') continue;
            const t = TileAt(left + col, top + row);
            if (t['bool active()']() && t.type === TileID.GrayBrick) n++;
        }
    }
    return n;
}

// Acha o castelo pela fundação (33 tijolos seguidos) e confere o resto do
// mapa; o baú é conferido à parte (canto de cima à esquerda na coluna 22,
// linha 17).
function findCastles() {
    const chest = ModContent.TileType('examplemod/ExampleChest');
    const found = [];
    for (let y = 20; y < Main.worldSurface + 30; y++) {
        let run = 0;
        for (let x = 0; x < Main.maxTilesX; x++) {
            run = bl.tiles.typeAt(x, y) === TileID.GrayBrick ? run + 1 : 0;
            if (run < Width) continue;
            const left = x - Width + 1, top = y - Height + 1;
            const bricks = bricksAt(left, top);
            if (bricks > expectedBricks * 0.8) found.push({ left, top, bricks, chest: bl.tiles.typeAt(left + 22, top + 17) === chest });
        }
    }
    return found;
}

// As mesmas checagens do ExampleStructure.CanPlace, contando o motivo de cada
// recusa ao longo do mundo (depois da geração: com árvores e o resto).
function diagnose() {
    const natural = [TileID.Dirt, TileID.Grass, TileID.Stone, TileID.ClayBlock, TileID.SnowBlock];
    const ground = (x, a, b) => {
        for (let y = a; y <= b; y++) {
            const t = TileAt(x, y);
            if (t['bool active()']() && Main.tileSolid[t.type] && !Main.tileSolidTop[t.type]) return y;
        }
        return -1;
    };
    const structures = building.GenVars.structures;
    const top = Math.max(10, Math.floor(building.GenVars.worldSurfaceLow) - 30);
    const reasons = {}, samples = {};
    const note = (r, x, extra) => { reasons[r] = (reasons[r] || 0) + 1; if (!samples[r]) samples[r] = x + (extra ? ':' + extra : ''); };
    for (let x = 300; x < Main.maxTilesX - 300; x += 3) {
        if (Math.abs(x - Main.spawnTileX) < 100) { note('spawn', x); continue; }
        const g = ground(x, top, Math.floor(Main.worldSurface));
        if (g < 0) { note('semchao', x); continue; }
        const left = x - 16, areaTop = g - Height + 1;
        if (structures && !structures['bool CanPlace(Rectangle area, int padding)'](Rectangle.new(left, areaTop, Width, Height), 10)) { note('structures', x, g); continue; }
        let bad = null;
        for (let cx = left; cx < left + Width && !bad; cx++) {
            const cg = ground(cx, areaTop - 10, g + 6);
            if (cg < 0 || Math.abs(cg - g) > 4) bad = 'plano';
            else if (!natural.includes(TileAt(cx, cg).type)) bad = 'tipo' + TileAt(cx, cg).type;
        }
        if (bad) { note(bad.startsWith('tipo') ? 'naonatural' : bad, x, bad); continue; }
        let wet = false;
        for (let cx = left; cx < left + Width && !wet; cx++) for (let cy = areaTop; cy <= g; cy++) if (TileAt(cx, cy).liquid > 0) { wet = true; break; }
        if (wet) { note('liquido', x, g); continue; }
        note('ok', x, g);
    }
    log('DIAG top=' + top + ' worldSurfaceLow=' + building.GenVars.worldSurfaceLow + ' worldSurface=' + Main.worldSurface + ' structures=' + !!structures);
    log('DIAG motivos ' + JSON.stringify(reasons) + ' exemplos ' + JSON.stringify(samples));
}

bl.classOf('', 'GUIWorldCreateMenu')['void CreateWorld()'].hook((original, self) => {
    Main.newWorldName = worldName;
    self._worldName = worldName;
    self._worldSeed = '73377337';
    self.worldSize = 0;
    self.difficulty = 0;
    self.evilType = 0;
    log('criando ' + worldName);
    return original(self);
});

// Depois da etapa do castelo, o tipo no canto do baú a cada etapa seguinte.
let chestAt = null, chestType = -2;
building.WorldGenerator['GenPassResult RunPass(GenPass pass)'].hook((original, self, pass) => {
    if (isTestWorld()) passes.push(pass.Name);
    const result = original(self, pass);
    if (isTestWorld() && pass.Name === 'Example Structure') {
        const castles = findCastles();
        if (castles.length) chestAt = { x: castles[0].left + 22, y: castles[0].top + 17 };
    }
    if (chestAt) {
        const type = bl.tiles.typeAt(chestAt.x, chestAt.y);
        if (type !== chestType) log('BAU depois de ' + pass.Name + ': tipo ' + type + ' (ExampleChest ' + ModContent.TileType('examplemod/ExampleChest') + ')');
        chestType = type;
    }
    return result;
});

export class CastleGenChecks extends ModSystem {
    PostWorldGen() {
        if (!isTestWorld()) return;
        const index = passes.indexOf('Example Structure');
        check('a etapa rodou', index >= 0, passes.length + ' etapas');
        check('logo depois de Sunflowers', index > 0 && passes[index - 1] === 'Sunflowers', passes.slice(Math.max(0, index - 2), index + 2).join(' > '));

        const castles = findCastles();
        check('um castelo no mundo', castles.length === 1, JSON.stringify(castles));
        if (!castles.length) {
            diagnose();
            return;
        }

        castle = castles[0];
        check('tijolos iguais ao mapa', castle.bricks === expectedBricks, castle.bricks + '/' + expectedBricks);
        check('baú no lugar', castle.chest === true);
        const spawnDistance = Math.abs(castle.left + 16 - Main.spawnTileX);
        check('longe do spawn e do mar', spawnDistance >= 100 && castle.left >= 300 && castle.left + Width <= Main.maxTilesX - 300, 'spawn a ' + spawnDistance);

        const area = Rectangle.new(castle.left, castle.top, Width, Height);
        const structures = building.GenVars.structures;
        check('área protegida no GenVars.structures', !structures['bool CanPlace(Rectangle area, int padding)'](area, 0));
        log('castelo em ' + castle.left + ',' + castle.top + ' (mundo ' + Main.maxTilesX + 'x' + Main.maxTilesY + ', spawn ' + Main.spawnTileX + ')');
    }
}

let enteredAt = 0, phase = 0;
Main['void DoDraw(GameTime gameTime)'].hook((original, self, gameTime) => {
    if (!Main.gameMenu && isTestWorld() && phase < 3) {
        const p = Main.player[Main.myPlayer];
        p.statLife = p.statLifeMax2;
        if (!enteredAt) enteredAt = Date.now();
        const elapsed = Date.now() - enteredAt;
        try {
            if (phase === 0 && elapsed > 25000) {
                phase = 1;
                if (castle) {
                    p.position = Vector2.new((castle.left + 16) * 16 + 8 - p.width / 2, (castle.top + 19) * 16 - p.height - 1);
                    p.velocity = Vector2.new(0, 0);
                    check('o castelo continua lá depois de abrir o mundo', bricksAt(castle.left, castle.top) === expectedBricks);
                }
                Main.dayTime = true;
                Main.time = 20000;
            } else if (phase === 1 && elapsed > 27000) {
                phase = 2;
                log('SHOT excastlegen');
            } else if (phase === 2 && elapsed > 40000) {
                phase = 3;
                log('FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
            }
            if (phase >= 1) p.fallStart = Math.floor(p.position.Y / 16);
        } catch (e) {
            fails++;
            phase = 3;
            log('erro: ' + e + (e.stack ? ' ' + e.stack : ''));
        }
    }
    original(self, gameTime);
});
log('carregado');

export default class TestExCastleGen extends Mod {}
