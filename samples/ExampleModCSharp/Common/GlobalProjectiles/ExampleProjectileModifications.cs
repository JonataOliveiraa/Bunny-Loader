using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Common.GlobalProjectiles;

public class ExampleProjectileModifications : GlobalProjectile
{
    public override bool InstancePerEntity => true;
    public bool applyBuffOnHit;
    private Color trailColor;
    private bool trailActive;

    public void SetTrail(Color color)
    {
        trailColor = color;
        trailActive = true;
    }

    public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (applyBuffOnHit)
        {
            target.AddBuff(BuffID.OnFire, 50);
        }
    }

    public override void PostAI(Projectile projectile)
    {
        if (trailActive)
        {
            Dust.NewDustDirect(projectile.position, projectile.width, projectile.height, DustID.TintableDustLighted, default, default, default, trailColor);
        }
    }
}
