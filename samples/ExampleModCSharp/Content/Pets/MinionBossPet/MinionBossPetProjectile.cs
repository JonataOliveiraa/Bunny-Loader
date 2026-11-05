using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Pets.MinionBossPet
{
    public class MinionBossPetProjectile : ModProjectile
    {
        public ref float AlphaForVisuals => ref Projectile.ai[0];

        public static Asset<Texture2D> EyeAsset;

        public override void Load() {
            EyeAsset = ModContent.Request<Texture2D>(Texture + "_Eye");
        }

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = 6;
            Main.projPet[Type] = true;

            ProjectileID.Sets.CharacterPreviewAnimations[Type] = ProjectileID.Sets.SimpleLoop(0, Main.projFrames[Type], 5)
                .WithOffset(-2, -22f)
                .WithCode(CharacterPreviewCustomization);
        }

        public static void CharacterPreviewCustomization(Projectile proj, bool walking) {
            float half = 0.5f;
            float timer = (float)Main.timeForVisualEffects % 60f / 60f;
            float speed = 1f;
            proj.position.Y += 0f - half + (float)(Math.Cos(timer * MathHelper.TwoPi * speed) * half * 2f);

            MinionBossPetProjectile minion = (MinionBossPetProjectile)proj.ModProjectile;

            minion.AlphaForVisuals = 1f;
        }

        public override void SetDefaults() {
            Projectile.CloneDefaults(ProjectileID.EyeOfCthulhuPet);

            Projectile.aiStyle = -1;
        }

        public override Color? GetAlpha(Color lightColor) {
            return Color.White * AlphaForVisuals * Projectile.Opacity;
        }

        public override void PostDraw(Player player, Color lightColor) {
            Texture2D eyeTexture = EyeAsset.Value;

            Vector2 offset = new Vector2(0, Projectile.gfxOffY);
            Vector2 orbitingCenter = Projectile.Center + offset;

            if (AlphaForVisuals >= 1) {
                return;
            }

            int eyeCount = 10;
            for (int i = 0; i < eyeCount; i++) {
                Vector2 origin = Vector2.Zero;
                Vector2 rotatedPos = (Vector2.UnitY * 24).RotatedBy(i / (float)eyeCount * MathHelper.TwoPi);
                Vector2 drawPos = orbitingCenter - Main.screenPosition + origin + rotatedPos;
                Color color = Color.White * (1f - AlphaForVisuals) * Projectile.Opacity;

                Main.EntitySpriteDraw(eyeTexture, drawPos, eyeTexture.Bounds, color, 0f, origin, 1f, SpriteEffects.None, 0);
            }
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];

            CheckActive(player);

            bool movesFast = Movement(player);

            Animate(movesFast);

            AlphaForVisuals = GetAlphaForVisuals(player);
        }

        private void CheckActive(Player player) {
            if (!player.dead && player.HasBuff(ModContent.BuffType<MinionBossPetBuff>())) {
                Projectile.timeLeft = 2;
            }
        }

        private bool Movement(Player player) {
            float velDistanceChange = 2f;

            int dir = player.direction;
            Projectile.direction = Projectile.spriteDirection = dir;

            Vector2 desiredCenterRelative = new Vector2(dir * 30, -30f);

            desiredCenterRelative.Y += (float)Math.Sin(Main.GameUpdateCount / 120f * MathHelper.TwoPi) * 5;

            Vector2 desiredCenter = player.MountedCenter + desiredCenterRelative;
            Vector2 betweenDirection = desiredCenter - Projectile.Center;
            float betweenSQ = betweenDirection.LengthSquared();

            if (betweenSQ > 1000f * 1000f || betweenSQ < velDistanceChange * velDistanceChange) {
                Projectile.Center = desiredCenter;
                Projectile.velocity = Vector2.Zero;
            }

            if (betweenDirection != Vector2.Zero) {
                Projectile.velocity = betweenDirection * 0.1f * 2;
            }

            bool movesFast = Projectile.velocity.LengthSquared() > 6f * 6f;

            if (movesFast) {
                float rotationVel = Projectile.velocity.X * 0.08f + Projectile.velocity.Y * Projectile.spriteDirection * 0.02f;
                if (Math.Abs(Projectile.rotation - rotationVel) >= MathHelper.Pi) {
                    if (rotationVel < Projectile.rotation) {
                        Projectile.rotation -= MathHelper.TwoPi;
                    }
                    else {
                        Projectile.rotation += MathHelper.TwoPi;
                    }
                }

                float rotationInertia = 12f;
                Projectile.rotation = (Projectile.rotation * (rotationInertia - 1f) + rotationVel) / rotationInertia;
            }
            else {
                if (Projectile.rotation > MathHelper.Pi) {
                    Projectile.rotation -= MathHelper.TwoPi;
                }

                if (Projectile.rotation > -0.005f && Projectile.rotation < 0.005f) {
                    Projectile.rotation = 0f;
                }
                else {
                    Projectile.rotation *= 0.96f;
                }
            }

            return movesFast;
        }

        private void Animate(bool movesFast) {
            int animationSpeed = 7;

            if (movesFast) {
                animationSpeed = 4;
            }

            Projectile.frameCounter++;
            if (Projectile.frameCounter > animationSpeed) {
                Projectile.frameCounter = 0;
                Projectile.frame++;

                if (Projectile.frame >= Main.projFrames[Type]) {
                    Projectile.frame = 0;
                }
            }
        }

        private static float GetAlphaForVisuals(Player player) {
            float lifeRatio = player.statLife / (float)player.statLifeMax2;
            return Utils.Clamp(2 * (1f - lifeRatio), 0f, 1f);
        }
    }
}
