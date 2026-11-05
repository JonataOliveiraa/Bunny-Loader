using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Enums;
using Terraria.GameContent.Tile_Entities;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Projectiles
{
    public class ExampleShortswordProjectile : ModProjectile
    {
        public const int FadeInDuration = 7;
        public const int FadeOutDuration = 4;

        public const int TotalDuration = 16;

        public float CollisionWidth => 10f * Projectile.scale;

        public int Timer {
            get => (int)Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }

        public override void SetDefaults() {
            Projectile.Size = new Vector2(18);
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.scale = 1f;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.ownerHitCheck = true;
            Projectile.extraUpdates = 1;
            Projectile.timeLeft = 360;
            Projectile.usesOwnerLight = true;
            Projectile.drawLayer = ProjectileDrawLayerID.HeldProj;
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];

            Timer += 1;
            if (Timer >= TotalDuration) {
                Projectile.Kill();
                return;
            }
            else {
                player.heldProj = Projectile.whoAmI;
            }

            Projectile.Opacity = Utils.GetLerpValue(0f, FadeInDuration, Timer, clamped: true) * Utils.GetLerpValue(TotalDuration, TotalDuration - FadeOutDuration, Timer, clamped: true);

            Vector2 playerCenter = player.RotatedRelativePoint(player.MountedCenter, reverseRotation: false);
            Projectile.Center = playerCenter + Projectile.velocity * (Timer - 1f);

            Projectile.spriteDirection = (Vector2.Dot(Projectile.velocity, Vector2.UnitX) >= 0f).ToDirectionInt();

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2 - MathHelper.PiOver4 * Projectile.spriteDirection;

            SetVisualOffsets();
        }

        private void SetVisualOffsets() {
            const int HalfSpriteWidth = 32 / 2;
            const int HalfSpriteHeight = 32 / 2;

            int HalfProjWidth = Projectile.width / 2;
            int HalfProjHeight = Projectile.height / 2;

            DrawOriginOffsetX = 0;
            DrawOffsetX = -(HalfSpriteWidth - HalfProjWidth);
            DrawOriginOffsetY = -(HalfSpriteHeight - HalfProjHeight);
        }

        public override bool ShouldUpdatePosition() {
            return false;
        }

        public override void CutTiles() {
            DelegateMethods.tilecut_0 = TileCuttingContext.AttackProjectile;
            Vector2 start = Projectile.Center;
            Vector2 end = start + Projectile.velocity.SafeNormalize(-Vector2.UnitY) * 10f;
            Utils.PlotTileLine(start, end, CollisionWidth, DelegateMethods.CutTiles);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 start = Projectile.Center;
            Vector2 end = start + Projectile.velocity * 6f;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, CollisionWidth, ref collisionPoint);
        }

        public override bool DisplayDollSettings(Player doll, TEDisplayDoll.DisplayDollPose pose, ref int aiStyle, ref int aiType) {
            SetVisualOffsets();

            Projectile.spriteDirection = Projectile.direction;
            Vector2 projectileDirection = Vector2.UnitX * 12f;
            float armRotation = 0f;
            if (pose.ItemAimRadians.HasValue)
                armRotation = pose.ItemAimRadians.Value;

            projectileDirection = projectileDirection.RotatedBy(armRotation);
            if (Projectile.direction == -1)
                projectileDirection.X *= -1f;

            Projectile.velocity = projectileDirection;
            Projectile.position += projectileDirection + (projectileDirection * 1.5f);

            Projectile.rotation = projectileDirection.ToRotation() + MathHelper.PiOver2;
            Projectile.rotation -= MathHelper.PiOver4 * Projectile.spriteDirection;

            return false;
        }
    }
}
