using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Placeable
{
    public class ExampleSandBlock : ModItem
    {
        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 100;

            ItemID.Sets.SandgunAmmoProjectileData[Type] = new(ModContent.ProjectileType<Projectiles.ExampleSandBallGunProjectile>(), 10);
        }

        public override void SetDefaults() {
            Item.DefaultToPlaceableTile(ModContent.TileType<Tiles.ExampleSand>());
            Item.width = 12;
            Item.height = 12;
            Item.ammo = AmmoID.Sand;

            Item.notAmmo = true;
        }

        public override void AddRecipes() {
            CreateRecipe(10)
                .AddIngredient<ExampleItem>()
                .AddTile<Tiles.Furniture.ExampleWorkbench>()
                .Register();
        }
    }
}
