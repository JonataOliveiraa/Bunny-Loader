using ExampleMod.Content.Tiles;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Placeable;

public class ExampleMusicBox : ModItem
{
    public override void SetStaticDefaults()
    {
        ItemID.Sets.CanGetPrefixes[Type] = false;
        ItemID.Sets.ShimmerTransformToItem[Type] = ItemID.MusicBox;

        MusicLoader.AddMusicBox(Mod, MusicLoader.GetMusicSlot(Mod, "Assets/Music/MysteriousMystery"), ModContent.ItemType<ExampleMusicBox>(), ModContent.TileType<ExampleMusicBoxTile>());
    }

    public override void SetDefaults()
    {
        Item.DefaultToMusicBox(ModContent.TileType<ExampleMusicBoxTile>(), 0);
    }
}
