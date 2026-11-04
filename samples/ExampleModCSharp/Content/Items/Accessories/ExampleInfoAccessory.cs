using ExampleMod.Common.Players;
using ExampleMod.Content.Tiles.Furniture;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Accessories
{

    public class ExampleInfoAccessory : ModItem
    {
        public override void SetStaticDefaults() {

        }

        public override void SetDefaults() {

            Item.CloneDefaults(ItemID.Radar);
        }

        public override void UpdateInfoAccessory(Player player) {
            player.GetModPlayer<ExampleInfoDisplayPlayer>().showMinionCount = true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<ExampleItem>()
                .AddTile<ExampleWorkbench>()
                .Register();
        }
    }
}
