using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Weapons
{

    public class ExampleSpecificAmmoGun : ModItem
    {
        public static readonly int FreeAmmoChance1 = 20;
        public static readonly int FreeAmmoChance2 = 63;
        public static readonly int FreeAmmoChance3 = 36;
        public static readonly int AmmoUseDamageBoost = 20;

        private bool consumptionDamageBoost = false;

        public override string Texture => "ExampleMod/Content/Items/Weapons/ExampleGun";

        public override LocalizedText Tooltip => base.Tooltip.WithFormatArgs(FreeAmmoChance1, FreeAmmoChance2, FreeAmmoChance3, AmmoUseDamageBoost);

        public override void SetDefaults() {

            Item.width = 62;
            Item.height = 32;
            Item.scale = 0.75f;
            Item.rare = ItemRarityID.Green;

            Item.useTime = 5;
            Item.useAnimation = 15;
            Item.reuseDelay = 5;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.autoReuse = true;
            Item.UseSound = SoundID.Item11;

            Item.DamageType = DamageClass.Ranged;
            Item.damage = 20;
            Item.knockBack = 5f;
            Item.noMelee = true;

            Item.shoot = ProjectileID.PurificationPowder;
            Item.shootSpeed = 16f;
            Item.useAmmo = AmmoID.Bullet;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<ExampleItem>()
                .AddTile<Tiles.Furniture.ExampleWorkbench>()
                .Register();
        }

        public override Vector2? HoldoutOffset() {
            return new Vector2(2f, -2f);
        }

        public override void UpdateInventory(Player player) {
            consumptionDamageBoost = false;
        }

        public override bool? CanChooseAmmo(Item ammo, Player player) {

            if (ammo.type == ItemID.CursedBullet)
                return false;

            return null;
        }

        public override bool CanConsumeAmmo(Item ammo, Player player) {

            if (player.ItemUsesThisAnimation == 0)
                return Main.rand.NextFloat() >= FreeAmmoChance1 / 100f;
            else if (player.ItemUsesThisAnimation == 1)
                return Main.rand.NextFloat() >= FreeAmmoChance2 / 100f;
            else if (player.ItemUsesThisAnimation == 2)
                return Main.rand.NextFloat() >= FreeAmmoChance3 / 100f;

            return true;
        }

        public override void OnConsumeAmmo(Item ammo, Player player) {

            consumptionDamageBoost = true;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            if (consumptionDamageBoost) {
                damage = damage * (100 + AmmoUseDamageBoost) / 100;
            }
        }
    }
}
