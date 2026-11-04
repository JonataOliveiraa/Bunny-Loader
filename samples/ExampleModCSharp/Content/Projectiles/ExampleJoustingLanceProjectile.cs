using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Tile_Entities;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Projectiles
{
    public class ExampleJoustingLanceProjectile : ModProjectile
    {
        public override void SetStaticDefaults() {

            ProjectileID.Sets.DismountsPlayersOnHit[Type] = true;

            ProjectileID.Sets.NoMeleeSpeedVelocityScaling[Type] = true;

            ProjectileID.Sets.AllowsContactDamageFromJellyfish[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.netImportant = true;

            Projectile.width = 25;
            Projectile.height = 25;

            Projectile.aiStyle = -1;

            Projectile.alpha = 255;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.drawLayer = ProjectileDrawLayerID.HeldProj;
            Projectile.usesOwnerLight = true;
            Projectile.scale = 1f;
            Projectile.ownerHitCheck = true;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;

        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            Projectile.direction = owner.direction;
            owner.heldProj = Projectile.whoAmI;

            int itemAnimationMax = owner.itemAnimationMax;

            int holdOutFrame = (int)(itemAnimationMax * 0.34f);
            if (owner.channel && owner.itemAnimation < holdOutFrame) {
                owner.SetDummyItemTime(holdOutFrame);
            }

            if (owner.ItemAnimationEndingOrEnded) {
                Projectile.Kill();
                return;
            }

            int itemAnimation = owner.itemAnimation;

            float extension = 1 - Math.Max(itemAnimation - holdOutFrame, 0) / (float)(itemAnimationMax - holdOutFrame);
            float retraction = 1 - Math.Min(itemAnimation, holdOutFrame) / (float)holdOutFrame;

            float extendDist = 24;
            float retractDist = extendDist / 2;
            float tipDist = 98 + extension * extendDist - retraction * retractDist;

            Vector2 center = owner.RotatedRelativePoint(owner.MountedCenter);
            Projectile.Center = center;
            Projectile.position += Projectile.velocity * tipDist;

            Projectile.rotation = (float)Math.Atan2(Projectile.velocity.Y, Projectile.velocity.X) + (float)Math.PI * 3 / 4f;

            Projectile.alpha -= 40;
            if (Projectile.alpha < 0) {
                Projectile.alpha = 0;
            }

            float minimumDustVelocity = 6f;

            float movementInLanceDirection = Vector2.Dot(Projectile.velocity.SafeNormalize(Vector2.UnitX * owner.direction), owner.velocity.SafeNormalize(Vector2.UnitX * owner.direction));

            float playerVelocity = owner.velocity.Length();

            if (playerVelocity > minimumDustVelocity && movementInLanceDirection > 0.8f) {

                int dustChance = 8;
                if (playerVelocity > minimumDustVelocity + 1f) {
                    dustChance = 5;
                }
                if (playerVelocity > minimumDustVelocity + 2f) {
                    dustChance = 2;
                }

                int dustTypeCommon = ModContent.DustType<Dusts.Sparkle>();
                int dustTypeRare = DustID.WhiteTorch;

                int offset = 4;

                if (Main.rand.NextBool(dustChance)) {
                    int newDust = Dust.NewDust(Projectile.Center - new Vector2(offset, offset), offset * 2, offset * 2, dustTypeCommon, Projectile.velocity.X * 0.2f + (Projectile.direction * 3), Projectile.velocity.Y * 0.2f, 100, default, 1.2f);
                    Main.dust[newDust].noGravity = true;
                    Main.dust[newDust].velocity *= 0.25f;
                    newDust = Dust.NewDust(Projectile.Center - new Vector2(offset, offset), offset * 2, offset * 2, dustTypeCommon, 0f, 0f, 150, default, 1.4f);
                    Main.dust[newDust].velocity *= 0.25f;
                }

                if (Main.rand.NextBool(dustChance + 3)) {
                    Dust.NewDust(Projectile.Center - new Vector2(offset, offset), offset * 2, offset * 2, dustTypeRare, 0f, 0f, 150, default, 1.4f);
                }
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {

            modifiers.Knockback *= Main.player[Projectile.owner].velocity.Length() / 7f;

            modifiers.SourceDamage *= 0.1f + Main.player[Projectile.owner].velocity.Length() / 7f * 0.9f;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float rotationFactor = Projectile.rotation - (3 * MathHelper.PiOver4) - ((Projectile.spriteDirection == 1) ? MathHelper.Pi : MathHelper.PiOver2);
            float scaleFactor = 95f;
            float widthMultiplier = 23f;
            float collisionPoint = 0f;

            Rectangle lanceHitboxBounds = new Rectangle(0, 0, 300, 300);

            lanceHitboxBounds.X = (int)Projectile.position.X - lanceHitboxBounds.Width / 2;
            lanceHitboxBounds.Y = (int)Projectile.position.Y - lanceHitboxBounds.Height / 2;

            Vector2 hitLineEnd = Projectile.Center + rotationFactor.ToRotationVector2() * scaleFactor;

            if (lanceHitboxBounds.Intersects(targetHitbox)
                && Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, hitLineEnd, widthMultiplier * Projectile.scale, ref collisionPoint)) {
                return true;
            }
            return false;
        }

        public override bool PreDraw(Player player, ref Color lightColor) {

            SpriteEffects spriteEffects = SpriteEffects.None;

            Texture2D texture = TextureAssets.Projectile[Type].Value;

            Rectangle sourceRectangle = texture.Frame(1, Main.projFrames[Type], frameY: Projectile.frame);

            Vector2 origin = Vector2.Zero;

            float rotation = Projectile.rotation;

            if (Projectile.direction > 0) {
                rotation -= (float)Math.PI / 2f;
                origin.X += sourceRectangle.Width;
                spriteEffects = SpriteEffects.FlipHorizontally;
            }

            Vector2 position = new(Projectile.Center.X, Projectile.Center.Y - Projectile.gfxOffY);

            Color drawColor = Projectile.GetAlpha(lightColor);

            Main.EntitySpriteDraw(texture,
                position - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY),
                sourceRectangle, drawColor, rotation, origin, Projectile.scale, spriteEffects, 0);

            return false;
        }

        public override bool DisplayDollSettings(Player doll, TEDisplayDoll.DisplayDollPose pose, ref int aiStyle, ref int aiType) {
            Projectile.direction = doll.direction;
            Projectile.spriteDirection = -Projectile.direction;
            Vector2 projectileDirection = Vector2.UnitX;
            float armRotation = 0f;
            if (pose.ItemAimRadians.HasValue)
                armRotation = pose.ItemAimRadians.Value;

            projectileDirection = projectileDirection.RotatedBy(armRotation);
            if (Projectile.direction == -1)
                projectileDirection.X *= -1f;

            Projectile.velocity = projectileDirection;

            Projectile.alpha = 0;

            int forwardOffset = 122;
            Projectile.position += Projectile.velocity * forwardOffset;
            Projectile.rotation = (float)Math.Atan2(projectileDirection.Y, projectileDirection.X) + (3f * MathHelper.PiOver4);

            return false;
        }
    }
}
