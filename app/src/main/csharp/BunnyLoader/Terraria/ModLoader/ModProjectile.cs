using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;
using Terraria.Localization;

namespace Terraria.ModLoader;

public abstract class ModProjectile : ModType<Projectile, ModProjectile>, ILocalizedModType
{
    public ref Projectile Projectile => ref entity;

    public int Type { get; internal set; }

    public virtual string LocalizationCategory => "Projectiles";

    public virtual LocalizedText DisplayName => this.GetLocalization(nameof(DisplayName), PrettyPrintName);

    public int AIType { get; set; }

    public int CooldownSlot { get; set; } = -1;

    public int DrawOffsetX { get; set; }

    public int DrawOriginOffsetY { get; set; }

    public float DrawOriginOffsetX { get; set; }

    public virtual string Texture => (GetType().Namespace + "." + Name).Replace('.', '/');

    public virtual string GlowTexture => Texture + "_Glow";

    protected sealed override Projectile CreateTemplateEntity() => default;

    protected sealed override void Register() => ProjectileLoader.Register(this);

    public sealed override void SetupContent()
    {
        AutoStaticDefaults();
        SetStaticDefaults();
    }

    public virtual void SetDefaults() { }

    public virtual void OnSpawn(IEntitySource source) { }

    public virtual void AutoStaticDefaults()
    {
        Main.projFrames[Type] = 1;
        if (Projectile.hostile) Main.projHostile[Type] = true;
        if (Projectile.aiStyle == 7) Main.projHook[Type] = true;
    }

    public virtual bool PreAI() => true;

    public virtual void AI() { }

    public virtual void PostAI() { }

    public virtual void SendExtraAI(BinaryWriter writer) { }

    public virtual void ReceiveExtraAI(BinaryReader reader) { }

    public virtual void ModifyFishingLine(ref Vector2 lineOriginOffset, ref Color lineColor) { }

    public virtual bool ShouldUpdatePosition() => true;

    public virtual bool TileCollideStyle(ref int width, ref int height, ref bool fallThrough, ref Vector2 hitboxCenterFrac) => true;

    public virtual bool OnTileCollide(Vector2 oldVelocity) => true;

    public virtual bool? CanCutTiles() => null;

    public virtual void CutTiles() { }

    public virtual bool PreKill(int timeLeft) => true;

    public virtual void OnKill(int timeLeft) { }

    public virtual bool? CanDamage() => null;

    public virtual bool MinionContactDamage() => false;

    public virtual void ModifyDamageHitbox(ref Rectangle hitbox) { }

    public virtual bool? CanHitNPC(NPC target) => null;

    public virtual void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) { }

    public virtual void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) { }

    public virtual bool CanHitPvp(Player target) => true;

    public virtual bool CanHitPlayer(Player target) => true;

    public virtual void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers) { }

    public virtual void OnHitPlayer(Player target, Player.HurtInfo info) { }

    public virtual bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) => null;

    public virtual Color? GetAlpha(Color lightColor) => null;

    public virtual bool PreDrawExtras(Player player) => true;

    public virtual bool PreDraw(Player player, ref Color lightColor) => true;

    public virtual void PostDraw(Player player, Color lightColor) { }

    public virtual bool? CanUseGrapple(Player player) => null;

    public virtual void UseGrapple(Player player, ref int type) { }

    public virtual float GrappleRange() => 300f;

    public virtual void NumGrappleHooks(Player player, ref int numHooks) { }

    public virtual void GrappleRetreatSpeed(Player player, ref float speed) { }

    public virtual void GrapplePullSpeed(Player player, ref float speed) { }

    public virtual void GrappleTargetPoint(Player player, ref float grappleX, ref float grappleY) { }

    public virtual bool? GrappleCanLatchOnTo(Player player, int x, int y) => null;
}
