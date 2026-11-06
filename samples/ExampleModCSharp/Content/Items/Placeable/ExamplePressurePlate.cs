using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Placeable
{
    public class ExamplePressurePlate : ModItem
    {
        public override void SetStaticDefaults() {
            ItemID.Sets.SortingPriorityWiring[Type] = 87;
        }

        public override void SetDefaults() {
            Item.DefaultToPlaceableTile(ModContent.TileType<Tiles.ExamplePressurePlate>());
            Item.rare = ItemRarityID.Blue;

            Item.mech = true;
        }
    }
}
