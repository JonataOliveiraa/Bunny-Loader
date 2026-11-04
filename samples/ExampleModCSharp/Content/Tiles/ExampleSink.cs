using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace ExampleMod.Content.Tiles;

public class ExampleSink : ModTile
{
    public override void SetStaticDefaults()
    {
        TileID.Sets.CountsAsWaterForCrafting[Type] = true;
        TileID.Sets.CountsAsHoneyForCrafting[Type] = true;
        TileID.Sets.CountsAsLavaForCrafting[Type] = true;

        Main.tileSolid[Type] = false;
        Main.tileLavaDeath[Type] = false;
        Main.tileFrameImportant[Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
        TileObjectData.newTile.CoordinateHeights = [16, 18];
        TileObjectData.addTile(Type);

        AddMapEntry(new Color(100, 100, 100), Language.GetText("MapObject.Sink"));

        DustType = DustID.Platinum;
    }

    public override void NumDust(int i, int j, bool fail, ref int num)
    {
        num = fail ? 1 : 3;
    }
}
