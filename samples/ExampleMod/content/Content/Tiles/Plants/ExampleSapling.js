const { TileID } = Terraria.ID;
const { TileObjectData } = Terraria.ObjectData;
const { AnchorType } = Terraria.Enums;
const { SpriteEffects } = Microsoft.Xna.Framework.Graphics;
const Main = Terraria.Main;
const W = Terraria.WorldGen;

// A muda da ExampleTree: a bolota plantada no Bloco de Exemplo vira esta
// muda (o SaplingGrowthType da árvore), que cresce sozinha com o tempo.
export class ExampleSapling extends ModTile {
    SetStaticDefaults() {
        Main.tileFrameImportant[this.Type] = true;
        Main.tileNoAttach[this.Type] = true;
        Main.tileLavaDeath[this.Type] = true;

        const tile = TileObjectData.newTile;
        tile.Width = 1;
        tile.Height = 2;
        tile.Origin = Point16.new(0, 1);
        tile.AnchorBottom = AnchorData.new(AnchorType.SolidTile, tile.Width, 0);
        tile.UsesCustomCanPlace = true;
        tile.CoordinateHeights = [16, 18];
        tile.CoordinateWidth = 16;
        tile.CoordinatePadding = 2;
        tile.AnchorValidTiles = [ModContent.TileType('ExampleTile'), TileID.Gold];
        tile.StyleHorizontal = true;
        tile.DrawFlipHorizontal = true;
        tile.LavaDeath = true;
        // 3 desenhos sorteados ao plantar.
        tile.RandomStyleRange = 3;
        tile.StyleMultiplier = 3;
        TileObjectData.addTile(this.Type);

        this.AddMapEntry(Color.new(200, 200, 200), 'MapObject.Sapling');

        TileID.Sets.TreeSapling[this.Type] = true;
        TileID.Sets.CommonSapling[this.Type] = true;
        TileID.Sets.SwaysInWindBasic[this.Type] = true;
        this.AdjTiles = [TileID.Saplings];
    }

    NumDust(i, j, fail, num) {
        num.value = fail ? 1 : 3;
    }

    // Uma chance em 20 a cada atualização aleatória, para não crescer rápido
    // demais. O AttemptToGrowTreeFromSapling chama o GrowSapling, que faz a
    // árvore (a ExampleTree, pelo bloco embaixo).
    RandomUpdate(i, j) {
        if (!Rand.NextBool(20)) return;
        W['bool AttemptToGrowTreeFromSapling(int x, int y, bool underground, int treeHeightAddon, bool ignoreWalls)'](i, j, j > Main.worldSurface, 0, false);
    }

    // Uma coluna sim, outra não, desenhada espelhada.
    SetSpriteEffects(i, j, spriteEffects) {
        if (i % 2 === 0) spriteEffects.value = SpriteEffects.FlipHorizontally;
    }
}
