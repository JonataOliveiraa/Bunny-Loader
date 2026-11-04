using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace ExampleMod.Content.Tiles;

public abstract class Example1x1RubbleBase : ModTile
{
    public override string Texture => "ExampleMod/Content/Tiles/Example1x1Rubble";

    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileNoFail[Type] = true;
        Main.tileObsidianKill[Type] = true;

        DustType = DustID.Stone;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.newTile.DrawYOffset = 2;
        TileObjectData.addTile(Type);

        AddMapEntry(new Color(152, 171, 198));
    }
}

public class Example1x1RubbleFake : Example1x1RubbleBase
{
    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();

        FlexibleTileWand.RubblePlacementSmall.AddVariations(ModContent.ItemType<Items.Placeable.ExampleBlock>(), Type, 0, 1, 2, 3, 4, 5);

        RegisterItemDrop(ModContent.ItemType<Items.Placeable.ExampleBlock>());
    }
}

public class Example1x1RubbleNatural : Example1x1RubbleBase
{
    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();

        TileID.Sets.BreakableWhenPlacing[Type] = true;
        TileID.Sets.ReplaceTileBreakUp[Type] = true;

        TileObjectData.GetTileData(Type, 0).LavaDeath = false;
    }

    public override void DropCritterChance(int i, int j, ref int wormChance, ref int grassHopperChance, ref int jungleGrubChance)
    {
        wormChance = 8;
    }
}
