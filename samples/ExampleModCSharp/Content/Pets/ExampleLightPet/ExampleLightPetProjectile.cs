using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Pets.ExampleLightPet;

public class ExampleLightPetProjectile : ModProjectile
{
    private const int DashCooldown = 1000;
    private const float DashSpeed = 20f;
    private const int FadeInTicks = 30;
    private const int FullBrightTicks = 200;
    private const int FadeOutTicks = 30;
    private const float Range = 500f;

    private static readonly float RangeHypotenuse = (float)(Math.Sqrt(2.0) * Range);
    private static readonly float RangeHypotenuseSquared = RangeHypotenuse * RangeHypotenuse;

    public ref float AIFadeProgress => ref Projectile.ai[0];
    public ref float AIDashCharge => ref Projectile.ai[1];

    public override void SetStaticDefaults()
    {
        Main.projFrames[Type] = 1;
        Main.projPet[Type] = true;

        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.LightPet[Type] = true;
    }

    public override void SetDefaults()
    {
        Projectile.width = 30;
        Projectile.height = 30;
        Projectile.penetrate = -1;
        Projectile.netImportant = true;
        Projectile.timeLeft *= 5;
        Projectile.friendly = true;
        Projectile.ignoreWater = true;
        Projectile.scale = 0.8f;
        Projectile.tileCollide = false;
    }

    public override void AI()
    {
        Player player = Main.player[Projectile.owner];

        if (!player.active)
        {
            Projectile.active = false;
            return;
        }

        if (!player.dead && player.HasBuff(ModContent.BuffType<ExampleLightPetBuff>()))
        {
            Projectile.timeLeft = 2;
        }

        UpdateDash(player);
        UpdateFading(player);
        UpdateExtraMovement();

        Projectile.rotation += Projectile.velocity.X / 20f;

        if (!Main.dedServ)
        {
            Lighting.AddLight(Projectile.Center, Projectile.Opacity * 0.9f, Projectile.Opacity * 0.1f, Projectile.Opacity * 0.3f);
        }
    }

    private void UpdateDash(Player player)
    {
        AIDashCharge++;
        if (AIDashCharge <= DashCooldown || (int)AIFadeProgress % 100 != 0)
        {
            return;
        }

        foreach (var npc in Main.ActiveNPCs)
        {
            if (npc.friendly)
            {
                continue;
            }

            if (player.DistanceSQ(npc.Center) >= RangeHypotenuseSquared)
            {
                continue;
            }

            Projectile.velocity += Vector2.Normalize(npc.Center - Projectile.Center) * DashSpeed;
            AIDashCharge = 0f;

            if (!Main.dedServ)
            {
                SoundEngine.PlaySound(SoundID.Item42, Projectile.Center);
            }

            break;
        }
    }

    private void UpdateFading(Player player)
    {
        var playerCenter = player.Center;

        AIFadeProgress++;
        if (AIFadeProgress < FadeInTicks)
        {
            Projectile.alpha = (int)(255 - 255 * AIFadeProgress / FadeInTicks);
        }
        else if (AIFadeProgress < FadeInTicks + FullBrightTicks)
        {
            Projectile.alpha = 0;

            if (Main.rand.NextBool(6))
            {
                var dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.PinkFairy, 0f, 0f, 200, default, 0.8f);
                dust.velocity *= 0.3f;
            }
        }
        else if (AIFadeProgress < FadeInTicks + FullBrightTicks + FadeOutTicks)
        {
            Projectile.alpha = (int)(255 * (AIFadeProgress - FadeInTicks - FullBrightTicks) / FadeOutTicks);
        }
        else
        {
            Projectile.Center = playerCenter + Main.rand.NextVector2Circular(Range, Range);
            AIFadeProgress = 0f;

            Projectile.velocity = 2f * Vector2.Normalize(playerCenter - Projectile.Center);
        }

        if (Vector2.Distance(playerCenter, Projectile.Center) > RangeHypotenuse)
        {
            Projectile.Center = playerCenter + Main.rand.NextVector2Circular(Range, Range);
            AIFadeProgress = 0f;

            Projectile.velocity += 2f * Vector2.Normalize(playerCenter - Projectile.Center);
        }

        if ((int)AIFadeProgress % 100 == 0)
        {
            Projectile.velocity = Projectile.velocity.RotatedByRandom(MathHelper.ToRadians(90));
        }
    }

    private void UpdateExtraMovement()
    {
        if (Projectile.velocity.Length() > 1f)
        {
            Projectile.velocity *= 0.98f;
        }

        if (Projectile.velocity == Vector2.Zero)
        {
            Projectile.velocity = Vector2.UnitX.RotatedBy(Main.rand.NextFloat() * MathHelper.TwoPi) * 2f;
        }
    }
}
