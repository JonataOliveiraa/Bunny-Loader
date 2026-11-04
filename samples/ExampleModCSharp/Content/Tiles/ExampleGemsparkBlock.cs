using Microsoft.Xna.Framework.Graphics;
using ExampleMod.Content.Biomes;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Tiles;

public class ExampleGemsparkBlock : ModTile, ICustomAutoload
{
    public static void Autoload(Mod mod)
    {
        var onVariant = new ExampleGemsparkBlock(onVariant: true);
        var offVariant = new ExampleGemsparkBlock(onVariant: false);
        mod.AddContent(onVariant);
        mod.AddContent(offVariant);
        onVariant.otherVariantTileType = offVariant.Type;
        offVariant.otherVariantTileType = onVariant.Type;
    }

    bool isOnVariant;
    int otherVariantTileType;

    public override string Name => base.Name + (isOnVariant ? "On" : "Off");

    public ExampleGemsparkBlock(bool onVariant)
    {
        this.isOnVariant = onVariant;
    }

    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true;
        if (isOnVariant)
        {
            Main.tileLighted[Type] = true;
            Main.tileShine2[Type] = true;
        }

        Main.tileBrick[Type] = true;
        TileID.Sets.GemsparkFramingTypes[Type] = Type;
        TileID.Sets.ForcedDirtMerging[Type] = true;

        TileID.Sets.AllBlocksWithSmoothBordersToResolveHalfBlockIssue[Type] = true;

        RegisterItemDrop(ModContent.ItemType<Items.Placeable.ExampleGemsparkBlock>());
        DustType = DustID.GemDiamond;
        VanillaFallbackOnModDeletion = isOnVariant ? TileID.DiamondGemspark : TileID.DiamondGemsparkOff;

        AddMapEntry(new Color(200, 200, 200));
    }

    public override bool TileFrame(int i, int j, ref bool resetFrame, ref bool noBreak)
    {
        Framing.SelfFrame8Way(i, j, Main.tile[i, j], resetFrame);
        return false;
    }

    public override void HitWire(int i, int j)
    {
        Tile tile = Main.tile[i, j];
        if (!tile.HasActuator)
        {
            tile.TileType = (ushort)otherVariantTileType;

            WorldGen.SquareTileFrame(i, j);
            NetMessage.SendTileSquare(-1, i, j);
        }
    }

}
