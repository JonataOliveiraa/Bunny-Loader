using ExampleMod.Content.Projectiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Weapons
{

    public class ExampleHeldProjectileWeapon : ModItem
    {
        public const int HoldoutDistance = 20;

        private static Asset<Texture2D> glowTexture;

        public override void Load() {
            glowTexture = ModContent.Request<Texture2D>(Texture + "_Glow");
        }

        public override void SetDefaults() {
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useAnimation = 20;
            Item.useTime = 20;

            Item.shootSpeed = 6f;
            Item.knockBack = 2f;
            Item.width = 56;
            Item.height = 26;
            Item.damage = 60;
            Item.shoot = ModContent.ProjectileType<ExampleHeldProjectileWeaponProjectile>();
            Item.useAmmo = AmmoID.Arrow;
            Item.rare = ItemRarityID.Yellow;
            Item.value = Item.sellPrice(gold: 10);
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.DamageType = DamageClass.Ranged;
            Item.channel = true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            type = ModContent.ProjectileType<ExampleHeldProjectileWeaponProjectile>();

            velocity = Vector2.Normalize(velocity) * HoldoutDistance;

            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, Main.myPlayer);
            return false;
        }

        public override bool CanConsumeAmmo(Item ammo, Player player) {

            if (player.ItemTimeIsZero) {
                return false;
            }
            return true;
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI) {

            WorldItem item = Main.item[whoAmI];
            Texture2D texture = glowTexture.Value;
            spriteBatch.Draw
            (
                texture,
                new Vector2
                (
                    item.position.X - Main.screenPosition.X + Item.width * 0.5f,
                    item.position.Y - Main.screenPosition.Y + Item.height - texture.Height * 0.5f
                ),
                new Rectangle(0, 0, texture.Width, texture.Height),
                Color.White,
                rotation,
                texture.Size() * 0.5f,
                scale,
                SpriteEffects.None,
                0f
            );
        }
    }
}
