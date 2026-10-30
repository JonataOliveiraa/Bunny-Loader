using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;

namespace Terraria.ModLoader;

public abstract class GlobalProjectile : GlobalType<Projectile, GlobalProjectile>
{
    protected sealed override void Register() => ProjectileLoader.RegisterGlobal(this);

    public sealed override void SetupContent() => SetStaticDefaults();

    public virtual void SetDefaults(Projectile entity) { }

    public virtual void OnSpawn(Projectile projectile, IEntitySource source) { }

    public virtual bool PreAI(Projectile projectile) => true;

    public virtual void AI(Projectile projectile) { }

    public virtual void PostAI(Projectile projectile) { }

    public virtual bool ShouldUpdatePosition(Projectile projectile) => true;

    public virtual bool TileCollideStyle(Projectile projectile, ref int width, ref int height, ref bool fallThrough, ref Vector2 hitboxCenterFrac) => true;

    public virtual bool OnTileCollide(Projectile projectile, Vector2 oldVelocity) => true;

    public virtual bool PreKill(Projectile projectile, int timeLeft) => true;

    public virtual void OnKill(Projectile projectile, int timeLeft) { }

    public virtual bool? CanCutTiles(Projectile projectile) => null;

    public virtual void CutTiles(Projectile projectile) { }

    public virtual bool? CanDamage(Projectile projectile) => null;

    public virtual bool MinionContactDamage(Projectile projectile) => false;

    public virtual void ModifyDamageHitbox(Projectile projectile, ref Rectangle hitbox) { }

    public virtual bool? CanHitNPC(Projectile projectile, NPC target) => null;

    public virtual void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers) { }

    public virtual void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone) { }

    public virtual bool CanHitPvp(Projectile projectile, Player target) => true;

    public virtual bool CanHitPlayer(Projectile projectile, Player target) => true;

    public virtual void ModifyHitPlayer(Projectile projectile, Player target, ref Player.HurtModifiers modifiers) { }

    public virtual void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info) { }

    public virtual bool? Colliding(Projectile projectile, Rectangle projHitbox, Rectangle targetHitbox) => null;

    public virtual Color? GetAlpha(Projectile projectile, Color lightColor) => null;

    public virtual bool PreDrawExtras(Projectile projectile, Player player) => true;

    public virtual bool PreDraw(Projectile projectile, Player player, ref Color lightColor) => true;

    public virtual void PostDraw(Projectile projectile, Player player, Color lightColor) { }
}
