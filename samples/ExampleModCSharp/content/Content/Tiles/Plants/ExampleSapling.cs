using ExampleMod.Content.Dusts;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace ExampleMod.Content.Tiles.Plants;

public class ExampleSapling : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileNoAttach[Type] = true;
        Main.tileLavaDeath[Type] = true;

        TileObjectData.newTile.Width = 1;
        TileObjectData.newTile.Height = 2;
        TileObjectData.newTile.Origin = new Point16(0, 1);
        TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, TileObjectData.newTile.Width, 0);
        TileObjectData.newTile.UsesCustomCanPlace = true;
        TileObjectData.newTile.CoordinateHeights = [16, 18];
        TileObjectData.newTile.CoordinateWidth = 16;
        TileObjectData.newTile.CoordinatePadding = 2;
        TileObjectData.newTile.AnchorValidTiles = [ModContent.TileType<ExampleBlock>(), TileID.Gold];
        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.newTile.DrawFlipHorizontal = true;
        TileObjectData.newTile.WaterPlacement = LiquidPlacement.NotAllowed;
        TileObjectData.newTile.LavaDeath = true;
        TileObjectData.newTile.RandomStyleRange = 3;
        TileObjectData.newTile.StyleMultiplier = 3;

        TileObjectData.addTile(Type);

        AddMapEntry(new Color(200, 200, 200), Language.GetText("MapObject.Sapling"));

        TileID.Sets.TreeSapling[Type] = true;
        TileID.Sets.CommonSapling[Type] = true;
        TileID.Sets.SwaysInWindBasic[Type] = true;

        DustType = ModContent.DustType<Sparkle>();

        AdjTiles = [TileID.Saplings];
    }

    public override void NumDust(int i, int j, bool fail, ref int num)
    {
        num = fail ? 1 : 3;
    }

    public override void RandomUpdate(int i, int j, bool underground)
    {
        if (!WorldGen.genRand.NextBool(20))
        {
            return;
        }

        WorldGen.AttemptToGrowTreeFromSapling(i, j, underground: underground, treeHeightAddon: 0, ignoreWalls: false);
    }

    public override bool GrowSapling(int i, int j, bool underground, int treeHeightAddon, bool ignoreWalls)
    {
        Tile tile = Framing.GetTileSafely(i, j);
        bool growSuccess;

        if (tile.TileFrameX < 54)
        {
            growSuccess = WorldGen.GrowTree(i, j, treeHeightAddon, ignoreWalls);
        }
        else
        {
            growSuccess = WorldGen.GrowPalmTree(i, j, treeHeightAddon, ignoreWalls);
        }

        if (growSuccess && WorldGen.PlayerLOS(i, j))
        {
            WorldGen.TreeGrowFXCheck(i, j);
        }

        return growSuccess;
    }

    public override void SetSpriteEffects(int i, int j, ref SpriteEffects effects)
    {
        if (i % 2 == 0)
        {
            effects = SpriteEffects.FlipHorizontally;
        }
    }
}
