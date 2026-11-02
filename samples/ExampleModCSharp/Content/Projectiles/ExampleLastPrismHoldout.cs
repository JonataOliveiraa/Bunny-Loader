using ExampleMod.Content.Items.Weapons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Projectiles;

public class ExampleLastPrismHoldout : ModProjectile
{
    public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.LastPrism;

    const int NumAnimationFrames = 5;

    public const int NumBeams = 10;

    public const float MaxCharge = 180f;

    public const float DamageStart = 30f;

    const float AimResponsiveness = 0.08f;

    const int SoundInterval = 20;

    const float MaxManaConsumptionDelay = 15f;

    const float MinManaConsumptionDelay = 5f;

    float FrameCounter
    {
        get => Projectile.ai[0];
        set => Projectile.ai[0] = value;
    }

    float NextManaFrame
    {
        get => Projectile.ai[1];
        set => Projectile.ai[1] = value;
    }

    float ManaConsumptionRate
    {
        get => Projectile.localAI[0];
        set => Projectile.localAI[0] = value;
    }

    public override void SetStaticDefaults() => Main.projFrames[Type] = NumAnimationFrames;

    public override void SetDefaults() => Projectile.CloneDefaults(ProjectileID.LastPrism);

    public override bool? CanDamage() => false;

    public override void AI()
    {
        Player player = Main.player[Projectile.owner];
        Vector2 rrp = player.RotatedRelativePoint(player.MountedCenter, true);

        UpdateDamageForManaSickness(player);
        FrameCounter += 1f;
        UpdateAnimation();
        PlaySounds();
        UpdatePlayerVisuals(player, rrp);

        if (Projectile.owner == Main.myPlayer)
        {
            UpdateAim(rrp, player.HeldItem.shootSpeed);
            bool manaIsAvailable = !ShouldConsumeMana() || player.CheckMana(player.HeldItem.mana, true, false);
            bool stillInUse = player.channel && manaIsAvailable && !player.noItems && !player.CCed;
            if (stillInUse && FrameCounter == 1f)
                FireBeams();
            else if (!stillInUse)
                Projectile.Kill();
        }

        Projectile.timeLeft = 2;
    }

    void UpdateDamageForManaSickness(Player player) =>
        Projectile.damage = (int)player.GetDamage(DamageClass.Magic).ApplyTo(player.HeldItem.damage);

    void UpdateAnimation()
    {
        Projectile.frameCounter++;
        int framesPerAnimationUpdate = FrameCounter >= MaxCharge ? 2 : FrameCounter >= MaxCharge * 0.66f ? 3 : 4;
        if (Projectile.frameCounter >= framesPerAnimationUpdate)
        {
            Projectile.frameCounter = 0;
            if (++Projectile.frame >= NumAnimationFrames)
                Projectile.frame = 0;
        }
    }

    void PlaySounds()
    {
        if (Projectile.soundDelay <= 0)
        {
            Projectile.soundDelay = SoundInterval;
            if (FrameCounter > 1f)
                SoundEngine.PlaySound(SoundID.Item15, Projectile.position);
        }
    }

    void UpdatePlayerVisuals(Player player, Vector2 playerHandPos)
    {
        Projectile.Center = playerHandPos;
        Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        Projectile.spriteDirection = Projectile.direction;
        player.ChangeDir(Projectile.direction);
        player.heldProj = Projectile.whoAmI;
        player.itemTime = 2;
        player.itemAnimation = 2;
        player.itemRotation = (Projectile.velocity * Projectile.direction).ToRotation();
    }

    bool ShouldConsumeMana()
    {
        if (ManaConsumptionRate == 0f)
        {
            NextManaFrame = ManaConsumptionRate = MaxManaConsumptionDelay;
            return true;
        }
        bool consume = FrameCounter == NextManaFrame;
        if (consume)
        {
            ManaConsumptionRate = MathHelper.Clamp(ManaConsumptionRate - 1f, MinManaConsumptionDelay, MaxManaConsumptionDelay);
            NextManaFrame += ManaConsumptionRate;
        }
        return consume;
    }

    void UpdateAim(Vector2 source, float speed)
    {
        Vector2 aim = (Main.MouseWorld - source).SafeNormalize(-Vector2.UnitY);
        aim = Vector2.Lerp(Projectile.velocity.SafeNormalize(-Vector2.UnitY), aim, AimResponsiveness).SafeNormalize(-Vector2.UnitY);
        aim *= speed;
        if (aim != Projectile.velocity)
            Projectile.netUpdate = true;
        Projectile.velocity = aim;
    }

    void FireBeams()
    {
        Vector2 beamVelocity = Projectile.velocity.SafeNormalize(-Vector2.UnitY);
        int damage = Projectile.damage;
        float knockback = Projectile.knockBack;
        for (int b = 0; b < NumBeams; ++b)
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, beamVelocity, ModContent.ProjectileType<ExampleLastPrismBeam>(), damage, knockback, Projectile.owner, b, Projectile.whoAmI);
        Projectile.netUpdate = true;
    }

    public override bool PreDraw(Player player, ref Color lightColor)
    {
        SpriteEffects effects = Projectile.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        Texture2D texture = TextureAssets.Projectile[Type].Value;
        int frameHeight = texture.Height / Main.projFrames[Type];
        int spriteSheetOffset = frameHeight * Projectile.frame;
        Vector2 sheetInsertPosition = (Projectile.Center + Vector2.UnitY * Projectile.gfxOffY - Main.screenPosition).Floor();
        Main.EntitySpriteDraw(texture, sheetInsertPosition, new Rectangle(0, spriteSheetOffset, texture.Width, frameHeight), ExampleLastPrism.OverrideColor, Projectile.rotation, new Vector2(texture.Width / 2f, frameHeight / 2f), Projectile.scale, effects, 0f);
        return false;
    }
}
