using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items
{
    public class ExamplePaperAirplane : ModItem
    {
        public override void SetDefaults() {
            Item.width = 22;
            Item.height = 16;

            Item.value = Item.sellPrice(0, 0, 10);

            Item.DefaultToThrownWeapon(ModContent.ProjectileType<Projectiles.ExamplePaperAirplaneProjectile>(), 17, 5f);

            Item.SetWeaponValues(4, 2f);
        }

        public override void AddRecipes() {
            CreateRecipe(10)
                .AddIngredient(ModContent.ItemType<ExampleItem>())
                .AddTile(TileID.WorkBenches)
                .Register();
        }
    }
}
