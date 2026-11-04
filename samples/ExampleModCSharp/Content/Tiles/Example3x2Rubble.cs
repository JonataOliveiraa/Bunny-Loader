using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace ExampleMod.Content.Tiles;

public abstract class Example3x2RubbleBase : ModTile
{
    public override string Texture => "ExampleMod/Content/Tiles/Example3x2Rubble";

    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileNoFail[Type] = true;
        Main.tileObsidianKill[Type] = true;

        DustType = DustID.Stone;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x2);
        TileObjectData.newTile.DrawYOffset = 2;
        TileObjectData.addTile(Type);

        AddMapEntry(new Color(152, 171, 198));
    }
}

public class Example3x2RubbleFake : Example3x2RubbleBase
{
    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();

        FlexibleTileWand.RubblePlacementLarge.AddVariations(ModContent.ItemType<Items.Placeable.ExampleBlock>(), Type, 0, 1, 2, 3, 4, 5);

        RegisterItemDrop(ModContent.ItemType<Items.Placeable.ExampleBlock>());
    }
}

public class Example3x2RubbleNatural : Example3x2RubbleBase
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
        wormChance = 6;
    }
}
