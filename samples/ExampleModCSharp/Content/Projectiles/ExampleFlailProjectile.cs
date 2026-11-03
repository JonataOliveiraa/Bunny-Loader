using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Projectiles;

internal class ExampleFlailProjectile : ModProjectile
{
    public override void SetDefaults()
    {
        Projectile.netImportant = true;
        Projectile.width = 22;
        Projectile.height = 22;
        Projectile.friendly = true;
        Projectile.penetrate = -1;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.scale = 0.8f;
        Projectile.drawLayer = ProjectileDrawLayerID.HeldProj;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 10;
        Projectile.aiStyle = ProjAIStyleID.Flail;
        AIType = ProjectileID.Sunfury;

        DrawOffsetX = -8;
        DrawOriginOffsetY = -8;
    }

    public override Color? GetAlpha(Color lightColor) => Color.White;

    public override bool PreDrawExtras(Player player)
    {
        Projectile.type = ProjectileID.Sunfury;
        return base.PreDrawExtras(player);
    }

    public override bool PreDraw(Player player, ref Color lightColor)
    {
        Projectile.type = ModContent.ProjectileType<ExampleFlailProjectile>();

        if (Projectile.ai[0] == 1f)
        {
            Texture2D projectileTexture = TextureAssets.Projectile[Type].Value;
            Vector2 drawPosition = Projectile.position + new Vector2(Projectile.width, Projectile.height) / 2f + Vector2.UnitY * Projectile.gfxOffY - Main.screenPosition;
            Vector2 drawOrigin = new Vector2(projectileTexture.Width, projectileTexture.Height) / 2f;
            Color drawColor = Projectile.GetAlpha(lightColor);
            drawColor.A = 127;
            drawColor *= 0.5f;
            int launchTimer = (int)Projectile.ai[1];
            if (launchTimer > 5)
            {
                launchTimer = 5;
            }

            SpriteEffects spriteEffects = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            for (float transparency = 1f; transparency >= 0f; transparency -= 0.125f)
            {
                float opacity = 1f - transparency;
                Vector2 drawAdjustment = Projectile.velocity * -launchTimer * transparency;
                Main.EntitySpriteDraw(projectileTexture, drawPosition + drawAdjustment, null, drawColor * opacity, Projectile.rotation, drawOrigin, Projectile.scale * 1.15f * MathHelper.Lerp(0.5f, 1f, opacity), spriteEffects, 0);
            }
        }

        return base.PreDraw(player, ref lightColor);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (Main.rand.NextBool(2))
        {
            target.AddBuff(BuffID.OnFire, 300);
        }
    }

    public override void OnHitPlayer(Player target, Player.HurtInfo info)
    {
        if (Main.rand.NextBool(4))
        {
            target.AddBuff(BuffID.OnFire, 180);
        }
    }

    public override void AI()
    {
        if (Main.myPlayer == Projectile.owner && Projectile.ai[0] == 2f && Projectile.ai[1] == 0f)
        {
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Projectile.velocity, ProjectileID.Grenade, Projectile.damage, Projectile.knockBack, Main.myPlayer);
            Projectile.ai[1]++;
        }
    }
}
