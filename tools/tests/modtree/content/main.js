// ModTree: uma árvore de mod num chão de mod (texturas do ExampleTree do tModLoader).
// Longe do spawn, no alto: uma plataforma de TreeSoil e
//   - o chão serve para árvore e o jogo o vê como TreeTypes.Custom;
//   - a bolota (muda 20 do jogo) colocada nele vira a muda de mod (SaplingGrowthType);
//   - a muda cresce (AttemptToGrowTreeFromSapling -> GrowSapling -> GrowTree);
//   - a copa e os galhos usam o estilo do mod (GetCommonTreeFoliageData) e a
//     folha é a do mod (GetTreeLeaf);
//   - o jogador fica ao lado (log "modtree pose": hora da captura);
//   - sacudir chama o Shake uma vez só; cortar dá a madeira do mod, não a do jogo.
// Tudo sai no fim. Loga "modtree <caso>: ok | FALHOU".
const Main = Terraria.Main;
const W = Terraria.WorldGen;
const { TileObjectData } = Terraria.ObjectData;
const { TileID, ItemID } = Terraria.ID;

let shakes = 0, dusts = 0, leaves = 0;

export class TreeSoil extends ModTile {
    Texture = 'Textures/TreeSoil';

    SetStaticDefaults() {
        Main.tileSolid[this.Type] = true;
        Main.tileBlockLight[this.Type] = true;
        this.AddMapEntry(Color.new(120, 90, 160), 'Tree Soil');
    }
}

export class TestSapling extends ModTile {
    Texture = 'Textures/TestSapling';

    SetStaticDefaults() {
        Main.tileFrameImportant[this.Type] = true;
        Main.tileNoAttach[this.Type] = true;
        Main.tileLavaDeath[this.Type] = true;
        const d = TileObjectData.newTile;
        d.Width = 1;
        d.Height = 2;
        d.Origin = Point16.new(0, 1);
        d.AnchorBottom = AnchorData.new(1, 1, 0);   // AnchorType.SolidTile
        d.UsesCustomCanPlace = true;
        d.CoordinateHeights = [16, 18];
        d.CoordinateWidth = 16;
        d.CoordinatePadding = 2;
        d.AnchorValidTiles = [ModContent.TileType(TreeSoil)];
        d.StyleHorizontal = true;
        d.DrawFlipHorizontal = true;
        TileObjectData.addTile(this.Type);
        TileID.Sets.TreeSapling[this.Type] = true;
        TileID.Sets.CommonSapling[this.Type] = true;
        this.AddMapEntry(Color.new(200, 200, 200), 'Test Sapling');
    }
}

export class TestTree extends ModTree {
    Texture = 'Textures/TestTree';

    SetStaticDefaults() { this.GrowsOnTileId = [ModContent.TileType(TreeSoil)]; }
    SaplingGrowthType(style) {
        style.value = 0;
        return ModContent.TileType(TestSapling);
    }
    DropWood() { return ItemID.Gel; }
    CreateDust() { dusts++; return 6; }
    TreeLeaf() { leaves++; return 910; }
    Shake(x, y, createLeaves) {
        shakes++;
        return false;
    }
}

let fails = 0;
function check(label, fn) {
    try {
        const r = fn();
        if (r === true || r === undefined) bl.log('modtree ' + label + ': ok');
        else { fails++; bl.log('modtree ' + label + ': FALHOU (' + r + ')'); }
    } catch (e) {
        fails++;
        bl.log('modtree ' + label + ': FALHOU com ' + e + (e && e.stack ? ' | ' + e.stack.replace(/\n/g, ' | ') : ''));
    }
}

const SOIL = () => ModContent.TileType(TreeSoil);
const SAPLING = () => ModContent.TileType(TestSapling);
const kill = (x, y, fail = false, noItem = true) => W['void KillTile(int i, int j, bool fail, bool effectOnly, bool noItem)'](x, y, fail, false, noItem);
const placeTile = (x, y, type) => W['bool PlaceTile(int i, int j, int Type, bool mute, bool forced, int plr, int style)'](x, y, type, true, true, -1, 0);

function area() {
    return { x: Main.spawnTileX + 80, floor: Main.spawnTileY - 30 };
}

function clear() {
    const { x, floor } = area();
    for (let i = x - 4; i <= x + 4; i++) {
        for (let j = floor - 32; j <= floor; j++) if (bl.tiles.typeAt(i, j) >= 0) kill(i, j);
    }
}

function itemsNear(type) {
    const { x, floor } = area();
    let n = 0;
    for (let k = 0; k < Main.item.length; k++) {
        const it = Main.item[k];
        if (!it || !it.active || it.type !== type) continue;
        if (Math.abs(it.position.X - x * 16) > 20 * 16 || Math.abs(it.position.Y - (floor - 10) * 16) > 30 * 16) continue;
        n += it.stack;
    }
    return n;
}

function clearItems() {
    const { x, floor } = area();
    for (let k = 0; k < Main.item.length; k++) {
        const it = Main.item[k];
        if (!it || !it.active) continue;
        if (Math.abs(it.position.X - x * 16) > 20 * 16 || Math.abs(it.position.Y - (floor - 10) * 16) > 30 * 16) continue;
        it['void TurnToAir()']();
    }
}

function placeSapling() {
    const { x, floor } = area();
    const data = new Ref();
    const can = Terraria.TileObject['bool CanPlace(int x, int y, int type, int style, int dir, out TileObject objectData, bool onlyCheck, Nullable<int> forcedRandom)'](
        x, floor - 1, TileID.Saplings, 0, 1, data, false, null);
    if (!can) return false;
    Main.player[Main.myPlayer]['TileObject PlaceThing_Tiles_PlaceIt(bool newObjectType, TileObject data, int tileToCreate)'](true, data.value, TileID.Saplings);
    return bl.tiles.typeAt(x, floor - 1);
}

function treeTop() {
    const { x, floor } = area();
    let y = floor - 1;
    while (bl.tiles.typeAt(x, y - 1) === TileID.Trees) y--;
    return y;
}

function grow() {
    const { x, floor } = area();
    clear();
    for (let i = x - 2; i <= x + 2; i++) placeTile(i, floor, SOIL());

    check('o chão de mod serve para árvore e é TreeTypes.Custom', () => {
        const fit = W['bool IsTileTypeFitForTree(ushort type)'](SOIL());
        const type = W['TreeTypes GetTreeType(int tileType)'](SOIL());
        return (fit === true && type === TreeTypes.Custom) || `fit ${fit}, type ${type}`;
    });
    check('a bolota no chão de mod vira a muda de mod', () => {
        const placed = placeSapling();
        return placed === SAPLING() || 'colocou ' + placed + ' (muda de mod ' + SAPLING() + ')';
    });
    check('a muda de mod cresce em árvore, com copa', () => {
        // 1 em 13 árvores do jogo nasce sem copa (ponta quebrada): refaz.
        let grew = false, leafy = false, tries = 0;
        while (!(grew && leafy) && tries < 20) {
            tries++;
            if (grew) for (let j = floor - 1; j > floor - 30; j--) if (bl.tiles.typeAt(x, j) >= 0) kill(x, j);
            if (bl.tiles.typeAt(x, floor - 1) !== SAPLING()) placeSapling();
            grew = W['bool AttemptToGrowTreeFromSapling(int x, int y, bool underground, int treeHeightAddon, bool ignoreWalls)'](x, floor - 1, false, 0, false);
            leafy = grew && W['bool IsTileALeafyTreeTop(int i, int j)'](x, treeTop());
        }
        const height = floor - treeTop();
        return (grew && leafy && bl.tiles.typeAt(x, floor - 1) === TileID.Trees && height >= 4) ||
            `cresceu ${grew}, copa ${leafy}, ${tries} tentativa(s), base ${bl.tiles.typeAt(x, floor - 1)}, altura ${height}`;
    });
    check('copa e galhos com o estilo do mod (GetCommonTreeFoliageData)', () => {
        const top = treeTop();
        const frame = new Ref(0), style = new Ref(0), floorY = new Ref(0), w = new Ref(0), h = new Ref(0);
        const ok = W['bool GetCommonTreeFoliageData(int i, int j, int xoffset, ref int treeFrame, ref int treeStyle, out int floorY, out int topTextureFrameWidth, out int topTextureFrameHeight)'](
            x, top, 0, frame, style, floorY, w, h);
        const tops = Terraria.GameContent.TextureAssets.TreeTop;
        const asset = tops[style.value];
        const name = asset ? String(asset.Name) : '';
        return (ok === true && style.value === tops.length - 1 && floorY.value === floor && w.value === 80 && name.includes('TestTree_Tops')) ||
            `ok ${ok}, estilo ${style.value} de ${tops.length}, chão ${floorY.value}, ${w.value}x${h.value}, ${name}`;
    });
    check('a folha é a do mod (GetTreeLeaf -> TreeLeaf)', () => {
        const top = treeTop();
        const t = Main.tile['Tile get_Item(int x, int y)'];
        const pass = new Ref(0);
        W['void GetTreeLeaf(int x, Tile topTile, Tile t, ref int treeHeight, out int treeFrame, out int passStyle)'](x, t(x, top), t(x, floor), new Ref(0), new Ref(0), pass);
        return pass.value === 910 || 'passStyle ' + pass.value;
    });
}

// O jogador em pé na plataforma, ao lado da árvore.
function pose() {
    const { x, floor } = area();
    const p = Main.player[Main.myPlayer];
    p.position.X = (x + 2) * 16;
    p.position.Y = (floor - 3) * 16;
    p.velocity.X = 0;
    p.velocity.Y = 0;
    p.fallStart = Math.floor(p.position.Y / 16);
}

function chop() {
    const { x, floor } = area();
    check('sacudir chama o Shake uma vez por árvore', () => {
        kill(x, floor - 3, true, false);
        kill(x, floor - 3, true, false);
        return shakes === 1 || 'Shake ' + shakes;
    });
    check('cortar dá a madeira do mod (DropWood), não a do jogo', () => {
        clearItems();
        const before = dusts;
        kill(x, floor - 1, false, false);
        const gel = itemsNear(ItemID.Gel), wood = itemsNear(ItemID.Wood);
        return (bl.tiles.typeAt(x, floor - 1) < 0 && gel > 0 && wood === 0 && dusts > before) ||
            `base ${bl.tiles.typeAt(x, floor - 1)}, gel ${gel}, madeira ${wood}, poeira ${dusts - before}`;
    });
    clearItems();
    clear();
    const p = Main.player[Main.myPlayer];
    p.position.X = Main.spawnTileX * 16;
    p.position.Y = (Main.spawnTileY - 3) * 16;
    p.fallStart = Math.floor(p.position.Y / 16);
}

let frames = 0, done = false;
Terraria.Player['void Update(int i)'].hook((original, self, i) => {
    original(self, i);
    if (i !== Main.myPlayer || Main.gameMenu || done) return;
    frames++;
    if (frames === 1) check('preparo', grow);
    if (frames >= 1 && frames < 900) pose();
    if (frames === 300) bl.log('modtree pose');
    if (frames === 900) {
        done = true;
        check('fim', chop);
        bl.log('modtree FIM: ' + (fails === 0 ? 'tudo ok' : fails + ' falha(s)'));
    }
});
bl.log('modtree: carregado');

export default class TestModTree extends Mod {}
