using Microsoft.Xna.Framework.Graphics;
using ExampleMod.Content.Dusts;
using ExampleMod.Content.Items.Placeable;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Tiles;

public class ExampleLivingFireTile : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileLighted[Type] = true;

        DustType = ModContent.DustType<Sparkle>();

        AddMapEntry(new Color(ExampleLivingFire.LightColor.X, ExampleLivingFire.LightColor.Y, ExampleLivingFire.LightColor.Z));

        AnimationFrameHeight = 90;
    }
    public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
    {
        r = ExampleLivingFire.LightColor.X;
        g = ExampleLivingFire.LightColor.Y;
        b = ExampleLivingFire.LightColor.Z;
    }
    public override void SetDrawPositions(int i, int j, ref int width, ref int offsetY, ref int height, ref short tileFrameX, ref short tileFrameY)
    {
        offsetY = 2;
    }

    public override void AnimateTile(ref int frame, ref int frameCounter)
    {
        frame = Main.tileFrame[TileID.LivingFire];
    }
}
