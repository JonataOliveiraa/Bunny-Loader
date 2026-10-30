using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;

namespace Terraria.ModLoader.Core;

internal static class ProjectileHooks
{
    sealed class Movement
    {
        public Projectile Projectile;
        public Vector2 Velocity;
        public bool Reported, PendingKill, Kept;
        public Movement? Outer;
    }

    static Movement? moving;
    static int spawning;
    static (IntPtr Projectile, Color Light)? drawLight;

    internal static void Install(Type type)
    {
        bool Has(string name) => ItemHooks.Overrides(type, typeof(ModProjectile), name);
        if (Has(nameof(ModProjectile.OnSpawn)))
            ItemHooks.Once("ProjectileSpawn", () =>
            {
                On_Projectile.NewProjectile_IEntitySource_float_float_float_float_int_int_float_int_float_float_float_NewProjectileModifier += NewProjectile;
                On_Projectile.NewProjectile_IEntitySource_Vector2_Vector2_int_int_float_int_float_float_float_NewProjectileModifier += NewProjectile;
            });
        ItemHooks.Once("ProjectileAI", () => On_Projectile.AI += AI);
        if (Has(nameof(ModProjectile.OnTileCollide)) || Has(nameof(ModProjectile.PreKill)) || Has(nameof(ModProjectile.OnKill)) || Has(nameof(ModProjectile.ShouldUpdatePosition)))
            ItemHooks.Once("ProjectileMovement", () =>
            {
                On_Projectile.HandleMovement += HandleMovement;
                On_Projectile.UpdatePosition += UpdatePosition;
                On_Projectile.Kill += Kill;
            });
        if (Has(nameof(ModProjectile.Colliding))) ItemHooks.Once("ProjectileColliding", () => On_Projectile.Colliding += Colliding);
        if (Has(nameof(ModProjectile.CanDamage)) || Has(nameof(ModProjectile.MinionContactDamage))) ItemHooks.Once("ProjectileDamage", () => On_Projectile.Damage += Damage);
        if (Has(nameof(ModProjectile.ModifyDamageHitbox))) ItemHooks.Once("ProjectileHitbox", () => On_Projectile.Damage_GetHitbox += DamageHitbox);
        if (Has(nameof(ModProjectile.GetAlpha)) || Has(nameof(ModProjectile.PreDraw))) ItemHooks.Once("ProjectileAlpha", () => On_Projectile.GetAlpha += GetAlpha);
        if (Has(nameof(ModProjectile.PreDraw)) || Has(nameof(ModProjectile.PostDraw))) ItemHooks.Once("ProjectileDraw", () => On_Main.DrawProjDirect += DrawProjDirect);
    }

    static ModProjectile? Of(Projectile projectile) => ProjectileLoader.InstanceOf(projectile);

    static bool Wants(ModProjectile m, string method) => ItemHooks.Overrides(m.GetType(), typeof(ModProjectile), method);

    static void Report(Exception e, ModProjectile m, string method) => ItemHooks.Report($"{m.FullName}.{method}", e);

    static void Spawned(int index, IEntitySource source)
    {
        if (spawning > 0 || (uint)index >= 1000u) return;
        if (Of(Main.projectile[index]) is not ModProjectile m || !Wants(m, nameof(ModProjectile.OnSpawn))) return;
        try { m.OnSpawn(source); }
        catch (Exception e) { Report(e, m, nameof(ModProjectile.OnSpawn)); }
    }

    static int NewProjectile(On_Projectile.orig_NewProjectile_IEntitySource_float_float_float_float_int_int_float_int_float_float_float_NewProjectileModifier orig,
        IEntitySource spawnSource, float X, float Y, float SpeedX, float SpeedY, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)
    {
        int index;
        spawning++;
        try { index = orig(spawnSource, X, Y, SpeedX, SpeedY, Type, Damage, KnockBack, Owner, ai0, ai1, ai2, modifer); }
        finally { spawning--; }
        Spawned(index, spawnSource);
        return index;
    }

    static int NewProjectile(On_Projectile.orig_NewProjectile_IEntitySource_Vector2_Vector2_int_int_float_int_float_float_float_NewProjectileModifier orig,
        IEntitySource spawnSource, Vector2 position, Vector2 velocity, int Type, int Damage, float KnockBack, int Owner, float ai0, float ai1, float ai2, NewProjectileModifier modifer)
    {
        int index;
        spawning++;
        try { index = orig(spawnSource, position, velocity, Type, Damage, KnockBack, Owner, ai0, ai1, ai2, modifer); }
        finally { spawning--; }
        Spawned(index, spawnSource);
        return index;
    }

    static void AI(On_Projectile.orig_AI orig, Projectile self)
    {
        if (Of(self) is not ModProjectile m)
        {
            orig(self);
            return;
        }
        bool go = true;
        try { go = m.PreAI(); }
        catch (Exception e) { Report(e, m, nameof(ModProjectile.PreAI)); }
        if (go)
        {
            int aiType = m.AIType;
            if (aiType > 0)
            {
                int type = self.type;
                self.type = aiType;
                try { orig(self); }
                finally { self.type = type; }
            }
            else orig(self);
            try { m.AI(); }
            catch (Exception e) { Report(e, m, nameof(ModProjectile.AI)); }
        }
        try { m.PostAI(); }
        catch (Exception e) { Report(e, m, nameof(ModProjectile.PostAI)); }
    }

    static bool Collided(Projectile p, Movement move) => p.velocity.X != move.Velocity.X || p.velocity.Y != move.Velocity.Y;

    static void ReportCollision(ModProjectile m, Projectile p, Movement move)
    {
        move.Reported = true;
        bool allowed = true;
        try { allowed = m.OnTileCollide(move.Velocity); }
        catch (Exception e) { Report(e, m, nameof(ModProjectile.OnTileCollide)); }
        if (!move.PendingKill) return;
        if (!allowed) move.Kept = true;
        else if (p.active) p.Kill();
    }

    static void HandleMovement(On_Projectile.orig_HandleMovement orig, Projectile self, Vector2 wetVelocity)
    {
        if (!self.tileCollide || Of(self) is not ModProjectile m || !Wants(m, nameof(ModProjectile.OnTileCollide)))
        {
            orig(self, wetVelocity);
            return;
        }
        float startX = self.position.X, startY = self.position.Y;
        var move = new Movement { Projectile = self, Velocity = new Vector2(self.velocity.X, self.velocity.Y), Outer = moving };
        moving = move;
        try
        {
            orig(self, wetVelocity);
            if (!move.Reported && self.active && (move.PendingKill || self.tileCollide && Collided(self, move))) ReportCollision(m, self, move);
            if (move.Kept && self.active) self.position = new Vector2(startX + self.velocity.X, startY + self.velocity.Y);
        }
        finally { moving = move.Outer; }
    }

    static void UpdatePosition(On_Projectile.orig_UpdatePosition orig, Projectile self, Vector2 wetVelocity)
    {
        Movement? move = moving;
        ModProjectile? m = Of(self);
        if (move != null && move.Projectile == self && m != null && !move.Reported && self.active && self.tileCollide && Collided(self, move))
            ReportCollision(m, self, move);
        if (m != null && Wants(m, nameof(ModProjectile.ShouldUpdatePosition)))
        {
            bool update = true;
            try { update = m.ShouldUpdatePosition(); }
            catch (Exception e) { Report(e, m, nameof(ModProjectile.ShouldUpdatePosition)); }
            if (!update) return;
        }
        orig(self, wetVelocity);
    }

    static void Kill(On_Projectile.orig_Kill orig, Projectile self)
    {
        if (!self.active || Of(self) is not ModProjectile m)
        {
            orig(self);
            return;
        }
        Movement? move = moving;
        if (move != null && move.Projectile == self && !move.Reported && self.tileCollide && Collided(self, move))
        {
            move.PendingKill = true;
            return;
        }
        int timeLeft = self.timeLeft;
        bool go = true;
        try { go = m.PreKill(timeLeft); }
        catch (Exception e) { Report(e, m, nameof(ModProjectile.PreKill)); }
        if (!go)
        {
            self.active = false;
            return;
        }
        try { m.OnKill(timeLeft); }
        catch (Exception e) { Report(e, m, nameof(ModProjectile.OnKill)); }
        orig(self);
    }

    static bool Colliding(On_Projectile.orig_Colliding orig, Projectile self, Rectangle myRect, Rectangle targetRect)
    {
        if (Of(self) is ModProjectile m)
        {
            bool? result = null;
            try { result = m.Colliding(myRect, targetRect); }
            catch (Exception e) { Report(e, m, nameof(ModProjectile.Colliding)); }
            if (result.HasValue) return result.Value;
        }
        return orig(self, myRect, targetRect);
    }

    static void Damage(On_Projectile.orig_Damage orig, Projectile self)
    {
        if (Of(self) is not ModProjectile m)
        {
            orig(self);
            return;
        }
        bool? can = null;
        try { can = m.CanDamage(); }
        catch (Exception e) { Report(e, m, nameof(ModProjectile.CanDamage)); }
        if (can == false) return;
        int type = self.type;
        bool pet = Main.projPet[type];
        bool contact = false;
        if (pet)
        {
            try { contact = m.MinionContactDamage(); }
            catch (Exception e) { Report(e, m, nameof(ModProjectile.MinionContactDamage)); }
        }
        if (!contact)
        {
            orig(self);
            return;
        }
        Main.projPet[type] = false;
        try { orig(self); }
        finally { Main.projPet[type] = pet; }
    }

    static Rectangle DamageHitbox(On_Projectile.orig_Damage_GetHitbox orig, Projectile self)
    {
        Rectangle box = orig(self);
        if (Of(self) is not ModProjectile m) return box;
        try { m.ModifyDamageHitbox(ref box); }
        catch (Exception e) { Report(e, m, nameof(ModProjectile.ModifyDamageHitbox)); }
        return box;
    }

    static Color GetAlpha(On_Projectile.orig_GetAlpha orig, Projectile self, Color newColor)
    {
        if (drawLight is { } light && light.Projectile == self.Pointer) newColor = light.Light;
        if (Of(self) is ModProjectile m && Wants(m, nameof(ModProjectile.GetAlpha)))
        {
            Color? result = null;
            try { result = m.GetAlpha(newColor); }
            catch (Exception e) { Report(e, m, nameof(ModProjectile.GetAlpha)); }
            if (result.HasValue) return result.Value;
        }
        return orig(self, newColor);
    }

    static void DrawProjDirect(On_Main.orig_DrawProjDirect orig, Main self, Projectile proj, Player overridePlayer)
    {
        if (Of(proj) is not ModProjectile m)
        {
            orig(self, proj, overridePlayer);
            return;
        }
        Player player = overridePlayer.IsNull ? Main.player[proj.owner] : overridePlayer;
        Vector2 center = proj.Center;
        Color light = float.IsFinite(center.X) && float.IsFinite(center.Y) ? Lighting.GetColor((int)(center.X / 16f), (int)(center.Y / 16f)) : Color.White;
        Color original = light;
        bool draw = true;
        try { draw = m.PreDraw(player, ref light); }
        catch (Exception e) { Report(e, m, nameof(ModProjectile.PreDraw)); }
        if (draw)
        {
            var outer = drawLight;
            if (!light.Equals(original)) drawLight = (proj.Pointer, light);
            try { orig(self, proj, overridePlayer); }
            finally { drawLight = outer; }
        }
        try { m.PostDraw(player, light); }
        catch (Exception e) { Report(e, m, nameof(ModProjectile.PostDraw)); }
    }
}
