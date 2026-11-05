using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Consumables
{
    public class ExampleFoodItem : ModItem
    {
        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 5;

            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(-1, 3) {
                NotActuallyAnimating = true
            });

            ItemID.Sets.FoodParticleColors[Type] = [
                new Color(249, 230, 136),
                new Color(152, 93, 95),
                new Color(174, 192, 192)
            ];

            ItemID.Sets.IsFood[Type] = true;
        }

        public override void SetDefaults() {
            Item.DefaultToFood(22, 22, BuffID.WellFed3, 57600);
            Item.value = Item.buyPrice(0, 3);
            Item.rare = ItemRarityID.Blue;
        }

        public override void OnConsumeItem(Player player) {
            player.AddBuff(BuffID.SugarRush, 3600);
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<ExampleItem>()
                .AddIngredient(ItemID.Apple, 3)
                .AddTile<Tiles.Furniture.ExampleWorkbench>()
                .Register();
        }
    }
}
