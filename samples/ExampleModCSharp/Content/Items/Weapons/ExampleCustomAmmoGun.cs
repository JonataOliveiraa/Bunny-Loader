using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Weapons
{

    public class ExampleCustomAmmoGun : ModItem
    {
        public override void SetDefaults() {
            Item.width = 42;
            Item.height = 30;

            Item.autoReuse = true;
            Item.damage = 12;
            Item.DamageType = DamageClass.Ranged;
            Item.knockBack = 4f;
            Item.noMelee = true;
            Item.rare = ItemRarityID.Yellow;
            Item.shootSpeed = 10f;
            Item.useAnimation = 35;
            Item.useTime = 35;
            Item.UseSound = SoundID.Item11;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.value = Item.buyPrice(gold: 1);

            Item.shoot = ModContent.ProjectileType<Projectiles.ExampleHomingProjectile>();
            Item.useAmmo = ModContent.TryFind<ModItem>("ExampleMod/ExampleCustomAmmo", out var ammo) ? ammo.Type : 0;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<ExampleItem>()
                .AddTile<Tiles.Furniture.ExampleWorkbench>()
                .Register();
        }
    }
}
