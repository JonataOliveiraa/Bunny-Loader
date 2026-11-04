using ExampleMod.Content.Items;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Projectiles
{

    public class ExamplePiercingProjectile : ModProjectile
    {
        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;

            AIType = ProjectileID.Bullet;

            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 60;

            Projectile.penetrate = -1;

        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {

        }
    }

    internal class ExamplePiercingProjectileItem : ModItem
    {
        public override string Texture => $"Terraria/Images/Item_{ItemID.FlintlockPistol}";

        public override void SetDefaults() {
            Item.CloneDefaults(ItemID.FlintlockPistol);
            Item.useAmmo = AmmoID.None;
            Item.shoot = ModContent.ProjectileType<ExamplePiercingProjectile>();
        }
        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<ExampleItem>()
                .AddTile<Tiles.Furniture.ExampleWorkbench>()
                .Register();
        }
    }
}
