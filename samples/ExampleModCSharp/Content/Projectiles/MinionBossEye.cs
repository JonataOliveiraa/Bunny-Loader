using Microsoft.Xna.Framework.Graphics;
using ExampleMod.Content.Buffs;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Projectiles
{
    public class MinionBossEye : ModProjectile
    {
        public bool FadedIn {
            get => Projectile.localAI[0] == 1f;
            set => Projectile.localAI[0] = value ? 1f : 0f;
        }

        public bool PlayedSpawnSound {
            get => Projectile.localAI[1] == 1f;
            set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.alpha = 255;
            Projectile.timeLeft = 90;
            Projectile.penetrate = -1;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.aiStyle = -1;
            CooldownSlot = ImmunityCooldownID.BossNoCheese;
        }

        public override Color? GetAlpha(Color lightColor) {
            return Color.White * Projectile.Opacity;
        }

        private void FadeInAndOut() {
            int fadeSpeed = 10;
            if (!FadedIn && Projectile.alpha > 0) {
                Projectile.alpha -= fadeSpeed;
                if (Projectile.alpha < 0) {
                    FadedIn = true;
                    Projectile.alpha = 0;
                }
            }
            else if (FadedIn && Projectile.timeLeft < 255f / fadeSpeed) {
                Projectile.alpha += fadeSpeed;
                if (Projectile.alpha > 255) {
                    Projectile.alpha = 255;
                }
            }
        }

        public override void AI() {
            FadeInAndOut();

            if (!PlayedSpawnSound) {
                PlayedSpawnSound = true;

                SoundEngine.PlaySound(SoundID.Item8, Projectile.position);
            }

            Projectile.velocity *= 1.01f;

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<ExampleCrowdControlledDebuff>(), 90);
        }
    }
}
