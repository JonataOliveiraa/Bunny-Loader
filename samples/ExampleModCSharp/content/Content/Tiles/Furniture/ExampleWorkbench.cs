using ExampleMod.Content.Dusts;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace ExampleMod.Content.Tiles.Furniture;

public sealed class ExampleWorkbench : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileTable[Type] = true;
        Main.tileSolidTop[Type] = true;
        Main.tileNoAttach[Type] = true;
        Main.tileLavaDeath[Type] = true;
        Main.tileFrameImportant[Type] = true;
        TileID.Sets.DisableSmartCursor[Type] = true;
        TileID.Sets.HasOutlines[Type] = true;
        TileID.Sets.IgnoredByNpcStepUp[Type] = true;
        DustType = ModContent.DustType<Sparkle>();
        AdjTiles = new[]
        {
            (int)TileID.WorkBenches
        };
        TileObjectData.newTile.CopyFrom(TileObjectData.Style2x1);
        TileObjectData.newTile.CoordinateHeights = new[]
        {
            18
        };
        TileObjectData.addTile(Type);
        TileID.Sets.RoomNeeds.CountsAsTable[Type] = true;
        AddMapEntry(new Color(200, 200, 200), CreateMapEntryName());
    }

    public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 3;
}
