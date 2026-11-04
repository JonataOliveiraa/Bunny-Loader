using ExampleMod.Content.Items.Weapons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Projectiles
{

    public class ExampleCloneProjectile : ModProjectile
    {
        public override void SetDefaults() {

            Projectile.CloneDefaults(ProjectileID.Meowmere);

            AIType = ProjectileID.Meowmere;

            Projectile.penetrate += 3;
        }

        public override void OnKill(int timeLeft) {
            Vector2 launchVelocity = new Vector2(-4, 0);
            for (int i = 0; i < 4; i++) {

                launchVelocity = launchVelocity.RotatedBy(MathHelper.PiOver4);

                Projectile.NewProjectile(Projectile.InheritSource(Projectile), Projectile.Center, launchVelocity, ProjectileID.Meowmere, Projectile.damage / 2, Projectile.knockBack, Projectile.owner);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {

            SoundEngine.PlaySound(Main.rand.NextBool() ? SoundID.Item57 : SoundID.Item58, Projectile.position);

            return base.OnTileCollide(oldVelocity);
        }
    }
}
