using Terraria;
using Terraria.Enums;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Weapons
{
    public class ExampleJoustingLance : ModItem
    {
        public override void SetDefaults() {

            Item.DefaultToSpear(ModContent.ProjectileType<Projectiles.ExampleJoustingLanceProjectile>(), 1f, 24);

            Item.DamageType = DamageClass.MeleeNoSpeed;

            Item.SetWeaponValues(60, 12f, 0);

            Item.SetShopValues(ItemRarityColor.LightRed4, Item.buyPrice(0, 6));

            Item.channel = true;

            Item.StopAnimationOnHurt = true;
        }

        public override bool MeleePrefix() {
            return true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<ExampleItem>(5)
                .AddTile<Tiles.Furniture.ExampleWorkbench>()
                .Register();
        }
    }
}
