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
        Main.tileSpelunker[Type] = true; // The tile will be affected by spelunker highlighting
        Main.tileOreFinderPriority[Type] = 410; // Metal Detector value, see https://terraria.wiki.gg/wiki/Metal_Detector
        Main.tileShine2[Type] = true; // Modifies the draw color slightly.
        Main.tileShine[Type] = 975; // How often tiny dust appear off this tile. Larger is less frequently
        Main.tileMergeDirt[Type] = true;
        Main.tileSolid[Type] = true;
        Main.tileBlockLight[Type] = true;
        AddMapEntry(new Color(152, 171, 198), CreateMapEntryName());
        DustType = DustID.Platinum;
        VanillaFallbackOnModDeletion = TileID.Silver;
        HitSound = SoundID.Tink;
        // MineResist = 4f;
        // MinPick = 200;
    }
}
