using ExampleMod.Content.Projectiles.Rockets;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Ammo
{
    public class ExampleRocket : ModItem
    {

        public override void SetStaticDefaults() {
            AmmoID.Sets.IsSpecialist[Type] = true;

            AmmoID.Sets.SpecificLauncherAmmoProjectileMatches[ItemID.RocketLauncher].Add(Type, ModContent.ProjectileType<ExampleRocketProjectile>());
            AmmoID.Sets.SpecificLauncherAmmoProjectileMatches[ItemID.GrenadeLauncher].Add(Type, ModContent.ProjectileType<ExampleGrenadeProjectile>());
            AmmoID.Sets.SpecificLauncherAmmoProjectileMatches[ItemID.ProximityMineLauncher].Add(Type, ModContent.ProjectileType<ExampleProximityMineProjectile>());
            AmmoID.Sets.SpecificLauncherAmmoProjectileMatches[ItemID.SnowmanCannon].Add(Type, ModContent.ProjectileType<ExampleSnowmanRocketProjectile>());

            AmmoID.Sets.SpecificLauncherAmmoProjectileMatches[ItemID.Celeb2].Add(Type, ProjectileID.Celeb2Rocket);

        }

        public override void SetDefaults() {
            Item.width = 20;
            Item.height = 14;
            Item.damage = 40;
            Item.knockBack = 4f;
            Item.consumable = true;
            Item.DamageType = DamageClass.Ranged;
            Item.maxStack = Item.CommonMaxStack;
            Item.value = Item.buyPrice(copper: 50);
            Item.ammo = AmmoID.Rocket;

        }

        public override void AddRecipes() {
            CreateRecipe(100)
                .AddIngredient(ItemID.RocketI, 100)
                .AddIngredient<ExampleItem>()
                .AddTile(TileID.Anvils)
                .AddCondition(Condition.NpcIsPresent(NPCID.Cyborg))
                .Register();
        }
    }
}
