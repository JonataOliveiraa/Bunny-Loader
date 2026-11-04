using ExampleMod.Content.Dusts;
using ExampleMod.Content.Items.Placeable;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace ExampleMod.Content.Walls;

public class ExampleWallUnsafe : ModWall
{
    public override void SetStaticDefaults()
    {
        Main.wallBlend[Type] = ModContent.WallType<ExampleWall>();

        DustType = ModContent.DustType<Sparkle>();

        AddMapEntry(new Color(150, 150, 150));

        RegisterItemDrop(ModContent.ItemType<Items.Placeable.ExampleWall>());
    }

    public override void NumDust(int i, int j, bool fail, ref int num)
    {
        num = fail ? 1 : 3;
    }
}
