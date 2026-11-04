using ExampleMod.Content.Items.Weapons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Tile_Entities;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Projectiles
{

    public class ExampleHeldProjectileWeaponProjectile : ModProjectile
    {
        public ref float HoldTimer => ref Projectile.ai[0];
        public ref float ShootTimer => ref Projectile.ai[1];

        public int ShootCount = 0;

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = 6;
        }

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.usesOwnerLight = true;
            Projectile.drawLayer = ProjectileDrawLayerID.HeldProj;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.ignoreWater = true;

            DrawOffsetX = -17;
            DrawOriginOffsetY = -4;
        }

        public override bool? CanDamage() => false;

        public override void AI() {

            Player player = Main.player[Projectile.owner];
            Vector2 playerCenter = player.RotatedRelativePoint(player.MountedCenter);

            HoldTimer += 1f;
            int animationSpeed = Math.Min((int)HoldTimer / 40, 3);
            int initialShootDelay = 24;
            int shootDelayAdjustmentRate = 6;

            ShootTimer += 1f;
            bool shouldShootArrow = false;
            if (ShootTimer >= initialShootDelay - shootDelayAdjustmentRate * animationSpeed) {
                ShootTimer = 0f;
                shouldShootArrow = true;
            }

            Projectile.frameCounter += 1 + animationSpeed;
            if (Projectile.frameCounter >= 4) {
                Projectile.frameCounter = 0;
                Projectile.frame = ++Projectile.frame % Main.projFrames[Type];
            }

            if (Projectile.soundDelay <= 0) {
                Projectile.soundDelay = initialShootDelay - shootDelayAdjustmentRate * animationSpeed;

                if (HoldTimer != 1f) {
                    SoundEngine.PlaySound(SoundID.Item5, Projectile.position);
                }
            }

            if (ShootTimer == 1f && HoldTimer != 1f) {
                Vector2 dustSpawnLocation = Projectile.Center + new Vector2(30, 0).RotatedBy(Projectile.rotation - (Projectile.direction == 1 ? 0 : MathHelper.Pi)) - new Vector2(8, 8);
                for (int i = 0; i < 2; i++) {
                    var dust = Dust.NewDustDirect(dustSpawnLocation, 16, 16, DustID.IceTorch, Projectile.velocity.X / 2f, Projectile.velocity.Y / 2f, 100);
                    dust.velocity *= 0.66f;
                    dust.noGravity = true;
                    dust.scale = 1.4f;
                }
            }

            if (shouldShootArrow && Main.myPlayer == Projectile.owner) {
                Item heldItem = player.HeldItem;
                if (player.channel && player.HasAmmo(heldItem) && !player.noItems && !player.CCed) {
                    float holdoutDistance = ExampleHeldProjectileWeapon.HoldoutDistance * Projectile.scale;
                    Vector2 holdoutOffset = holdoutDistance * Vector2.Normalize(Main.MouseWorld - playerCenter);
                    if (holdoutOffset.X != Projectile.velocity.X || holdoutOffset.Y != Projectile.velocity.Y) {
                        Projectile.netUpdate = true;
                    }

                    Projectile.velocity = holdoutOffset;

                    int projectileCount = 2;
                    if (ShootCount == 3) {

                        projectileCount = 1;
                    }

                    for (int j = 0; j < projectileCount; j++) {

                        var spawnLocation = playerCenter + holdoutOffset + Main.rand.NextVector2Circular(6, 6);
                        bool ammoConsumed = player.PickAmmo(heldItem, out int projToShoot, out float speed, out int damage, out float knockBack, out int usedAmmoItemId);

                        if (ammoConsumed) {
                            if (ShootCount == 3) {
                                projToShoot = ProjectileID.BeeArrow;
                            }
                            var source = player.GetSource_ItemUse_WithPotentialAmmo(heldItem, usedAmmoItemId);
                            Projectile.NewProjectile(source, spawnLocation, Vector2.Normalize(Projectile.velocity) * speed, projToShoot, damage, knockBack, Projectile.owner);
                        }
                    }
                    ShootCount = (ShootCount + 1) % 4;
                }
                else {
                    Projectile.Kill();
                }
            }

            Projectile.direction = Projectile.velocity.X < 0 ? -1 : 1;
            Projectile.spriteDirection = Projectile.direction;
            player.ChangeDir(Projectile.direction);
            player.heldProj = Projectile.whoAmI;
            player.SetDummyItemTime(2);
            Projectile.Center = playerCenter;
            float rotationOffset = Projectile.spriteDirection == -1 ? MathHelper.Pi : 0;
            Projectile.rotation = Projectile.velocity.ToRotation() + rotationOffset;
            player.itemRotation = (Projectile.velocity * Projectile.direction).ToRotation();
            Projectile.timeLeft = 2;
        }

        public override bool DisplayDollSettings(Player doll, TEDisplayDoll.DisplayDollPose pose, ref int aiStyle, ref int aiType) {
            Projectile.spriteDirection = Projectile.direction;
            Vector2 projectileDirection = Vector2.UnitX * ExampleHeldProjectileWeapon.HoldoutDistance;
            float armRotation = 0f;
            if (pose.ItemAimRadians.HasValue)
                armRotation = pose.ItemAimRadians.Value;

            projectileDirection = projectileDirection.RotatedBy(armRotation);
            if (Projectile.direction == -1) {
                projectileDirection.X *= -1f;
            }

            Projectile.velocity = projectileDirection;
            Projectile.position += projectileDirection;
            Projectile.rotation = projectileDirection.ToRotation();
            Projectile.rotation += Projectile.spriteDirection == -1 ? MathHelper.Pi : 0;

            return false;
        }
    }
}
