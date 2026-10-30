using System;
using BunnyLoader;

namespace Terraria.ModLoader.Core;

internal static unsafe class ProjectileHitHooks
{
    static bool projectileCanHit, playerCanHit;
    static IntPtr striking;

    internal static void Install(Func<string, bool> has, bool player = false)
    {
        if (has(nameof(ModProjectile.CanHitNPC)))
        {
            if (player) playerCanHit = true;
            else projectileCanHit = true;
            ItemHooks.Once("ProjectileCanHit", () => On_Player.CanNPCBeHitByPlayerOrPlayerProjectile += CanNPCBeHit);
        }
        if (has(nameof(ModProjectile.ModifyHitNPC)) || has(nameof(ModProjectile.OnHitNPC)))
            ItemHooks.Once("ProjectileStrike", () =>
            {
                if (Host.Native()->WatchProjectileDamage() == 0)
                {
                    Log.Error("projectile hits: Projectile.Damage_PVE is not watched, ModifyHitNPC/OnHitNPC will not run for projectiles");
                    return;
                }
                On_NPC.StrikeNPC += StrikeNPC;
            });
        if (has(nameof(ModProjectile.CanHitPlayer)) || has(nameof(ModProjectile.CanHitPvp)) || has(nameof(ModProjectile.ModifyHitPlayer)) ||
            has(nameof(ModProjectile.OnHitPlayer)))
            PlayerHurtHooks.Want();
    }

    static bool? CanHitNPCWithProj(Projectile projectile, NPC npc)
    {
        bool? result = null;
        if (playerCanHit && projectile.TryGetOwner(out Player player))
        {
            if (!PlayerLoader.All(player, nameof(ModPlayer.CanHitNPC), npc, static (m, n) => m.CanHitNPC(n))) return false;
            result = PlayerLoader.Nullable(player, nameof(ModPlayer.CanHitNPCWithProj), (projectile, npc),
                static (m, s) => m.CanHitNPCWithProj(s.projectile, s.npc));
            if (result == false) return false;
        }
        if (!projectileCanHit) return result;
        bool? own = ProjectileLoader.CanHitNPC(projectile, npc);
        return result ?? own;
    }

    static bool CanNPCBeHit(On_Player.orig_CanNPCBeHitByPlayerOrPlayerProjectile orig, Player self, NPC npc, Projectile projectile)
    {
        if (projectile.IsNull || !playerCanHit && !ProjectileLoader.HasHooks(projectile)) return orig(self, npc, projectile);
        bool? decision;
        try { decision = CanHitNPCWithProj(projectile, npc); }
        catch (Exception e)
        {
            ItemHooks.Report("CanHitNPC (projectile)", e);
            decision = null;
        }
        return decision ?? orig(self, npc, projectile);
    }

    static int StrikeNPC(On_NPC.orig_StrikeNPC orig, NPC self, int Damage, float knockBack, int hitDirection, bool crit, bool fromNet, int owner)
    {
        IntPtr pointer = fromNet ? IntPtr.Zero : Host.Native()->ProjectileDamaging();
        if (pointer == IntPtr.Zero || pointer == striking) return orig(self, Damage, knockBack, hitDirection, crit, fromNet, owner);
        IntPtr outer = striking;
        striking = pointer;
        try { return Strike(orig, self, Damage, knockBack, hitDirection, crit, owner, new Projectile(pointer)); }
        finally { striking = outer; }
    }

    static int Strike(On_NPC.orig_StrikeNPC orig, NPC npc, int damage, float knockBack, int hitDirection, bool crit, int owner, Projectile projectile)
    {
        NPC.HitInfo hit;
        try
        {
            var modifiers = new NPC.HitModifiers { DamageType = DamageClassLoader.ProjectileClass(projectile), HitDirection = hitDirection };
            modifiers.Defense.Base = npc.defense;
            ModifyHitNPCWithProj(projectile, npc, ref modifiers);
            hit = modifiers.ToHitInfo(damage, crit, knockBack);
        }
        catch (Exception e)
        {
            ItemHooks.Report("ModifyHitNPC (projectile)", e);
            return orig(npc, damage, knockBack, hitDirection, crit, false, owner);
        }
        int raw = ItemHitHooks.RawDamage(npc, hit);
        bool hide = hit.HideCombatText;
        if (hide) ItemHitHooks.HideText();
        int done;
        try { done = orig(npc, raw, hit.Knockback, hit.HitDirection, hit.Crit && !hit.InstantKill, false, owner); }
        finally
        {
            if (hide) ItemHitHooks.ShowText();
        }
        if (done <= 0) return done;
        hit.Damage = done;
        OnHitNPCWithProj(projectile, npc, hit, done);
        return done;
    }

    static void ModifyHitNPCWithProj(Projectile projectile, NPC npc, ref NPC.HitModifiers modifiers)
    {
        ProjectileLoader.ModifyHitNPC(projectile, npc, ref modifiers);
        if (!projectile.TryGetOwner(out Player player)) return;
        PlayerLoader.Each(player, nameof(ModPlayer.ModifyHitNPC), npc, ref modifiers, static (m, n, ref h) => m.ModifyHitNPC(n, ref h));
        PlayerLoader.Each(player, nameof(ModPlayer.ModifyHitNPCWithProj), (projectile, npc), ref modifiers,
            static (m, s, ref h) => m.ModifyHitNPCWithProj(s.projectile, s.npc, ref h));
    }

    static void OnHitNPCWithProj(Projectile projectile, NPC npc, NPC.HitInfo hit, int damageDone)
    {
        ProjectileLoader.OnHitNPC(projectile, npc, hit, damageDone);
        if (!projectile.TryGetOwner(out Player player)) return;
        PlayerLoader.Each(player, nameof(ModPlayer.OnHitNPC), (npc, hit, damageDone), static (m, s) => m.OnHitNPC(s.npc, s.hit, s.damageDone));
        PlayerLoader.Each(player, nameof(ModPlayer.OnHitNPCWithProj), (projectile, npc, hit, damageDone),
            static (m, s) => m.OnHitNPCWithProj(s.projectile, s.npc, s.hit, s.damageDone));
    }
}
