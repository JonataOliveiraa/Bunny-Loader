using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Projectiles
{
    public class ExamplePaperAirplaneProjectile : ModProjectile
    {
        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;

            Projectile.aiStyle = -1;

            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;

            Projectile.manualDirectionChange = true;

        }

        public override void AI() {

            if (Projectile.ai[1] == 0f) {
                Projectile.direction = (Projectile.velocity.X > 0).ToDirectionInt();
                Projectile.rotation = Projectile.velocity.ToRotation();
                Projectile.ai[1] = 1f;
                Projectile.ai[0] = -Main.rand.Next(30, 80);
                Projectile.netUpdate = true;
            }

            if (Projectile.wet && Projectile.owner == Main.myPlayer) {
                Projectile.Kill();
            }

            Projectile.ai[0] += 1f;

            Vector2 rotationVector = Projectile.rotation.ToRotationVector2() * 8f;

            float ySinModifier = (float)Math.Sin((float)Math.PI * 2f * (float)(Main.timeForVisualEffects % 90.0 / 90.0)) * Projectile.direction * Main.WindForVisuals;

            Vector2 newVelocity = rotationVector + new Vector2(Main.WindForVisuals, ySinModifier);

            bool directionSameAsWind = Projectile.direction == Math.Sign(Main.WindForVisuals) && Projectile.velocity.Length() > 3f;
            bool readyForFlip = Projectile.ai[0] >= 20f && Projectile.ai[0] <= 69f;

            if (Projectile.ai[0] == 70f) {
                Projectile.ai[0] = -Main.rand.Next(120, 600);
            }

            if (readyForFlip && directionSameAsWind) {
                float lerpValue = Utils.GetLerpValue(0f, 30f, Projectile.ai[0], clamped: true);
                newVelocity = rotationVector.RotatedBy((-Projectile.direction) * ((float)Math.PI * 2f) * 0.02f * lerpValue);
            }

            Projectile.velocity = newVelocity.SafeNormalize(Vector2.UnitY) * Projectile.velocity.Length();

            if (!(readyForFlip && directionSameAsWind)) {
                float yModifier = MathHelper.Lerp(0.15f, 0.05f, Math.Abs(Main.WindForVisuals));

                if (Projectile.timeLeft % 40 < 20) {
                    Projectile.velocity.Y -= yModifier;
                }

                else {
                    Projectile.velocity.Y += yModifier;
                }

                Projectile.velocity.Y = MathHelper.Clamp(Projectile.velocity.Y, -2f, 2f);

                Projectile.velocity.X = MathHelper.Clamp(Projectile.velocity.X + Main.WindForVisuals * 0.006f, -6f, 6f);

                if (Projectile.velocity.X * Projectile.oldVelocity.X < 0f) {
                    Projectile.direction *= -1;
                    Projectile.ai[0] = -Main.rand.Next(120, 300);
                    Projectile.netUpdate = true;
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.spriteDirection = Projectile.direction;

            if (Projectile.timeLeft % 2 == 0) {
                Dust.NewDustPerfect(new Vector2(Projectile.Center.X - (Projectile.width * Projectile.direction), Projectile.Center.Y), ModContent.DustType<Dusts.Sparkle>(), null, 0, default, 0.5f);
            }
        }

        public override bool PreDraw(Player player, ref Color lightColor) {

            SpriteEffects spriteEffects = ((Projectile.spriteDirection <= 0) ? SpriteEffects.FlipVertically : SpriteEffects.None);

            Texture2D texture = TextureAssets.Projectile[Type].Value;

            Rectangle sourceRectangle = texture.Frame(1, Main.projFrames[Type], frameY: Projectile.frame);

            Vector2 origin = sourceRectangle.Size() / 2f;

            Color drawColor = Projectile.GetAlpha(lightColor);
            Main.EntitySpriteDraw(texture,
                Projectile.Center - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY),
                sourceRectangle, drawColor, Projectile.rotation, origin, Projectile.scale, spriteEffects, 0);

            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item10, Projectile.position);

            if (Projectile.owner == Main.myPlayer && !Projectile.noDropItem) {
                if (!ModContent.TryFind<ModItem>("ExampleMod/ExamplePaperAirplane", out var item)) return;
                int dropItemType = item.Type;

                Item.RequestNewItem(Projectile.GetSource_DropAsItem(), Projectile.Center, dropItemType);
            }

            for (int i = 0; i < 10; i++) {
                Dust.NewDust(Projectile.Center, Projectile.width, Projectile.height, ModContent.DustType<Dusts.Sparkle>());
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.type == NPCID.GraniteGolem) {

                modifiers.FinalDamage *= 20f;
            }
        }
    }
}
