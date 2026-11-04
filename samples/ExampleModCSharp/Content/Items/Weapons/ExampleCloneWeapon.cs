using ExampleMod.Content.Projectiles;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Weapons
{

    public class ExampleCloneWeapon : ModItem
    {
        public override void SetDefaults() {

            Item.CloneDefaults(ItemID.Meowmere);

            Item.shoot = ModContent.ProjectileType<ExampleCloneProjectile>();

            Item.damage *= 2;
            Item.shootSpeed *= 1.25f;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<ExampleItem>()
                .AddTile<Tiles.Furniture.ExampleWorkbench>()
                .Register();
        }
    }
}
