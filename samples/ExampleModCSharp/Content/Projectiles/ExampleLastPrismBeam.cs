using System;
using System.IO;
using BunnyLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Projectiles;

public class ExampleLastPrismBeam : ModProjectile
{
    const float PiBeamDivisor = MathF.PI / ExampleLastPrismHoldout.NumBeams;
    const float MaxDamageMultiplier = 1.5f;
    const float MaxBeamScale = 1.8f;
    const float MaxBeamSpread = 2f;
    const float MaxBeamLength = 2400f;
    const float BeamHitboxCollisionWidth = 22f;
    const int NumSamplePoints = 3;
    const float BeamLengthChangeFactor = 0.75f;
    const float VisualEffectThreshold = 0.1f;
    const float OuterBeamOpacityMultiplier = 0.75f;
    const float InnerBeamOpacityMultiplier = 0.1f;
    const float BeamLightBrightness = 0.75f;
    const float LightStep = 96f;
    const float BeamColorHue = 0.57f;
    const float BeamHueVariance = 0.18f;
    const float BeamColorSaturation = 0.66f;
    const float BeamColorLightness = 0.53f;

    readonly float[] laserScanResults = new float[NumSamplePoints];
    ScreenPresets.Ripple? ripple;

    float BeamID => Projectile.ai[0];

    int HostPrism => (int)Projectile.ai[1];

    float BeamLength
    {
        get => Projectile.localAI[1];
        set => Projectile.localAI[1] = value;
    }

    public override void SetDefaults()
    {
        Projectile.width = 18;
        Projectile.height = 18;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = -1;
        Projectile.alpha = 255;
        Projectile.tileCollide = false;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 10;
    }

    public override void SendExtraAI(BinaryWriter writer) => writer.Write(BeamLength);

    public override void ReceiveExtraAI(BinaryReader reader) => BeamLength = reader.ReadSingle();

    public override void AI()
    {
        Projectile hostPrism = Main.projectile[HostPrism];
        if (!hostPrism.active || hostPrism.type != ModContent.ProjectileType<ExampleLastPrismHoldout>())
        {
            Projectile.Kill();
            return;
        }

        Vector2 hostPrismDir = hostPrism.velocity.SafeNormalize(-Vector2.UnitY);
        float chargeRatio = MathHelper.Clamp(hostPrism.ai[0] / ExampleLastPrismHoldout.MaxCharge, 0f, 1f);
        Projectile.damage = (int)(hostPrism.damage * GetDamageMultiplier(chargeRatio));
        Projectile.friendly = hostPrism.ai[0] > ExampleLastPrismHoldout.DamageStart;

        float beamIdOffset = BeamID - ExampleLastPrismHoldout.NumBeams / 2f + 0.5f;
        float beamSpread, spinRate, beamStartSidewaysOffset, beamStartForwardsOffset;
        if (chargeRatio < 1f)
        {
            Projectile.scale = MathHelper.Lerp(0f, MaxBeamScale, chargeRatio);
            beamSpread = MathHelper.Lerp(MaxBeamSpread, 0f, chargeRatio);
            beamStartSidewaysOffset = MathHelper.Lerp(20f, 6f, chargeRatio);
            beamStartForwardsOffset = MathHelper.Lerp(-21f, -17f, chargeRatio);
            if (chargeRatio <= 0.66f)
            {
                float phaseRatio = chargeRatio * 1.5f;
                Projectile.Opacity = MathHelper.Lerp(0f, 0.4f, phaseRatio);
                spinRate = MathHelper.Lerp(20f, 16f, phaseRatio);
            }
            else
            {
                float phaseRatio = (chargeRatio - 0.66f) * 3f;
                Projectile.Opacity = MathHelper.Lerp(0.4f, 1f, phaseRatio);
                spinRate = MathHelper.Lerp(16f, 6f, phaseRatio);
            }
        }
        else
        {
            Projectile.scale = MaxBeamScale;
            Projectile.Opacity = 1f;
            beamSpread = 0f;
            spinRate = 6f;
            beamStartSidewaysOffset = 6f;
            beamStartForwardsOffset = -17f;
        }

        float deviationAngle = (hostPrism.ai[0] + beamIdOffset * spinRate) / (spinRate * ExampleLastPrismHoldout.NumBeams) * MathHelper.TwoPi;
        Vector2 unitRot = Vector2.UnitY.RotatedBy(deviationAngle);
        Vector2 yVec = new(4f, beamStartSidewaysOffset);
        float hostPrismAngle = hostPrism.velocity.ToRotation();
        Vector2 beamSpanVector = (unitRot * yVec).RotatedBy(hostPrismAngle);
        float sinusoidYOffset = unitRot.Y * PiBeamDivisor * beamSpread;

        Projectile.Center = hostPrism.Center + hostPrismDir * (16f + beamStartForwardsOffset) + new Vector2(0f, -hostPrism.gfxOffY) + beamSpanVector;
        Projectile.velocity = hostPrismDir.RotatedBy(sinusoidYOffset).SafeNormalize(-Vector2.UnitY);
        Projectile.rotation = Projectile.velocity.ToRotation();

        float hitscanBeamLength = PerformBeamHitscan(hostPrism, chargeRatio >= 1f);
        BeamLength = MathHelper.Lerp(BeamLength, hitscanBeamLength, BeamLengthChangeFactor);

        Color beamColor = GetOuterBeamColor();
        if (chargeRatio >= VisualEffectThreshold)
        {
            ProduceBeamDust(beamColor);
            if (Main.netMode != NetmodeID.Server)
                ProduceRipples(chargeRatio);
        }
        CastLight(beamColor, chargeRatio);
    }

    float GetDamageMultiplier(float chargeRatio)
    {
        float f = chargeRatio * chargeRatio * chargeRatio;
        return MathHelper.Lerp(1f, MaxDamageMultiplier, f);
    }

    float PerformBeamHitscan(Projectile prism, bool fullCharge)
    {
        Vector2 samplingPoint = fullCharge ? prism.Center : Projectile.Center;
        Player player = Main.player[Projectile.owner];
        if (!Collision.CanHitLine(player.Center, 0, 0, prism.Center, 0, 0))
            samplingPoint = player.Center;
        Collision.LaserScan(samplingPoint, Projectile.velocity, 0, MaxBeamLength, laserScanResults);
        float averageLengthSample = 0f;
        foreach (float sample in laserScanResults)
            averageLengthSample += sample;
        return averageLengthSample / NumSamplePoints;
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        if (projHitbox.Intersects(targetHitbox))
            return true;
        Vector2 beamEndPos = Projectile.Center + Projectile.velocity * BeamLength;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, beamEndPos);
    }

    public override bool PreDraw(Player player, ref Color lightColor)
    {
        if (Projectile.velocity == Vector2.Zero)
            return false;
        Texture2D texture = TextureAssets.Projectile[Type].Value;
        Vector2 centerFloored = Projectile.Center.Floor() + Projectile.velocity * Projectile.scale * 10.5f;
        float visualBeamLength = BeamLength - 14.5f * Projectile.scale * Projectile.scale;
        Vector2 startPosition = centerFloored - Main.screenPosition;
        Vector2 endPosition = startPosition + Projectile.velocity * visualBeamLength;
        DrawBeam(texture, startPosition, endPosition, Projectile.scale, GetOuterBeamColor() * (OuterBeamOpacityMultiplier * Projectile.Opacity));
        DrawBeam(texture, startPosition, endPosition, Projectile.scale * 0.5f, Color.White * (InnerBeamOpacityMultiplier * Projectile.Opacity));
        return false;
    }

    static void DrawBeam(Texture2D texture, Vector2 start, Vector2 end, float scale, Color color)
    {
        Vector2 delta = end - start;
        float length = MathF.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
        if (!(length > 0))
            return;
        Vector2 dir = delta / length;
        float rotation = dir.ToRotation() - MathHelper.PiOver2;
        void Draw(Vector2 position, Rectangle frame, Vector2 origin, float scaleY) =>
            Main.spriteBatch.Draw(texture, position, frame, color, rotation, origin, new Vector2(scale, scaleY), SpriteEffects.None, 0f);
        Draw(start, new Rectangle(0, 0, 26, 22), new Vector2(13, 11), scale);
        float left = length - 33 * scale;
        Vector2 at = start + dir * (11 * scale);
        if (left > 0)
        {
            Draw(at, new Rectangle(0, 25, 26, 28), new Vector2(13, 0), left / 28);
            at += dir * left;
        }
        Draw(at, new Rectangle(0, 56, 26, 22), new Vector2(13, 1), scale);
    }

    Color GetOuterBeamColor()
    {
        float hue = BeamID / ExampleLastPrismHoldout.NumBeams % BeamHueVariance + BeamColorHue;
        Color c = Main.hslToRgb(hue, BeamColorSaturation, BeamColorLightness);
        c.A = 64;
        return c;
    }

    void ProduceBeamDust(Color beamColor)
    {
        const int type = 15;
        Vector2 endPosition = Projectile.Center + Projectile.velocity * (BeamLength - 14.5f * Projectile.scale);
        float angle = Projectile.rotation + (Main.rand.NextBool() ? 1f : -1f) * MathHelper.PiOver2;
        float startDistance = Main.rand.NextFloat(1f, 1.8f);
        float scale = Main.rand.NextFloat(0.7f, 1.1f);
        Vector2 velocity = angle.ToRotationVector2() * startDistance;
        Dust dust = Dust.NewDustDirect(endPosition, 0, 0, type, velocity.X, velocity.Y, 0, beamColor, scale);
        dust.color = beamColor;
        dust.noGravity = true;
        if (Projectile.scale > 1f)
        {
            dust.velocity *= Projectile.scale;
            dust.scale *= Projectile.scale;
        }
    }

    void ProduceRipples(float chargeRatio)
    {
        if (BeamID != 0)
            return;
        Vector2 end = Projectile.Center + Projectile.velocity * (BeamLength - 14.5f * Projectile.scale);
        ripple ??= ScreenEffects.Spawn<ScreenPresets.Ripple>(end, fx =>
        {
            fx.Radius = 72;
            fx.Duration = 0;
            fx.Strength = 3;
            fx.Wavelength = 14;
            fx.Speed = 2.5f;
            fx.FadeIn = 20;
        });
        ripple.Center = end;
        ripple.Intensity = chargeRatio;
    }

    void CastLight(Color beamColor, float chargeRatio)
    {
        float k = BeamLightBrightness * chargeRatio / 255f;
        if (k <= 0)
            return;
        for (float d = 0; d <= BeamLength; d += LightStep)
            Lighting.AddLight(Projectile.Center + Projectile.velocity * d, beamColor.R * k, beamColor.G * k, beamColor.B * k);
    }

    public override void OnKill(int timeLeft) => ripple?.Kill();
}
