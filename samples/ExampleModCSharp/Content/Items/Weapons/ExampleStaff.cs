using ExampleMod.Content.Projectiles;
using ExampleMod.Content.Tiles.Furniture;
using Terraria;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Weapons
{

    public class ExampleStaff : ModItem
    {
        public override void SetStaticDefaults() {
            Item.staff[Type] = true;
        }

        public override void SetDefaults() {

            Item.DefaultToStaff(ModContent.ProjectileType<SparklingBall>(), 16, 25, 12);

            Item.UseSound = SoundID.Item20;

            Item.SetWeaponValues(20, 5);

            Item.SetShopValues(ItemRarityColor.Green2, 10000);
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<ExampleItem>()
                .AddTile<ExampleWorkbench>()
                .Register();
        }
    }
}
