using ExampleMod.Content.Tiles;
using Terraria.Enums;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Placeable
{
    public class ExamplePylonItemAdvanced : ModItem
    {
        public override void SetDefaults() {
            Item.DefaultToPlaceableTile(ModContent.TileType<ExamplePylonTileAdvanced>());

            Item.SetShopValues(ItemRarityColor.LightRed4, Terraria.Item.buyPrice(gold: 20));
        }
    }
}
