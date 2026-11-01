using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Tiles;

public sealed class ExampleOre : ModTile
{
    public override void SetStaticDefaults()
    {
        TileID.Sets.Ore[Type] = true;
        TileID.Sets.FriendlyFairyCanLureTo[Type] = true;
        Main.tileSpelunker[Type] = true;
        Main.tileOreFinderPriority[Type] = 410;
        Main.tileShine2[Type] = true;
        Main.tileShine[Type] = 975;
        Main.tileMergeDirt[Type] = true;
        Main.tileSolid[Type] = true;
        Main.tileBlockLight[Type] = true;
        AddMapEntry(new Color(152, 171, 198), CreateMapEntryName());
        DustType = DustID.Platinum;
        VanillaFallbackOnModDeletion = TileID.Silver;
        HitSound = SoundID.Tink;
    }
}
