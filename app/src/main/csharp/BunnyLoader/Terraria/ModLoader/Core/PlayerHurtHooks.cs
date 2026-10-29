using System;
using BunnyLoader.Interop;
using Terraria.DataStructures;

namespace Terraria.ModLoader.Core;

internal static class PlayerHurtHooks
{
    internal static readonly string[] Names =
    {
        nameof(ModPlayer.ImmuneTo), nameof(ModPlayer.FreeDodge), nameof(ModPlayer.ConsumableDodge), nameof(ModPlayer.ModifyHurt),
        nameof(ModPlayer.OnHurt), nameof(ModPlayer.PostHurt), nameof(ModPlayer.CanBeHitByNPC), nameof(ModPlayer.CanBeHitByProjectile),
        nameof(ModPlayer.ModifyHitByNPC), nameof(ModPlayer.ModifyHitByProjectile), nameof(ModPlayer.OnHitByNPC), nameof(ModPlayer.OnHitByProjectile),
        nameof(ModPlayer.CanHitPvpWithProj),
    };

    internal static void Install(Func<string, bool> has)
    {
        if (Array.Exists(Names, n => has(n))) Want();
        if (has(nameof(ModPlayer.CanHitPvp))) ItemPvpHooks.WantCanHit();
    }

    internal static void Want() => ItemHooks.Once("PlayerHurt", () => On_Player.Hurt += Hurt);

    static double Hurt(On_Player.orig_Hurt orig, Player self, PlayerDeathReason damageSource, int Damage, int hitDirection, bool pvp, bool quiet,
                       bool Crit, int cooldownCounter, bool dodgeable)
    {
        GameHandle? pin = null;
        try { return Hurt(orig, self, damageSource, Damage, hitDirection, pvp, quiet, Crit, cooldownCounter, dodgeable, ref pin); }
        finally { pin?.Dispose(); }
    }

    static double Hurt(On_Player.orig_Hurt orig, Player self, PlayerDeathReason damageSource, int Damage, int hitDirection, bool pvp, bool quiet,
                       bool Crit, int cooldownCounter, bool dodgeable, ref GameHandle? pin)
    {
        NPC npc = default;
        Player attacker = default;
        Item weapon = default;
        Projectile projectile = default;
        Player.HurtModifiers modifiers;
        Player.HurtInfo pending;
        int slot = cooldownCounter, defense;
        float perPoint;
        try
        {
            if (!PlayerLoader.All(self, nameof(ModPlayer.ImmuneTo), (damageSource, cooldownCounter, dodgeable),
                    static (m, s) => !m.ImmuneTo(s.damageSource, s.cooldownCounter, s.dodgeable)))
                return 0;
            if (!damageSource.IsNull)
            {
                int npcIndex = damageSource._sourceNPCIndex, projectileIndex = damageSource._sourceProjectileLocalIndex;
                if ((uint)npcIndex < 200u) npc = Main.npc[npcIndex];
                if ((uint)projectileIndex < 1000u) projectile = Main.projectile[projectileIndex];
            }
            if (!npc.IsNull)
            {
                (bool Allowed, int Slot) gate = (true, slot);
                PlayerLoader.Each(self, nameof(ModPlayer.CanBeHitByNPC), npc, ref gate, static (m, n, ref g) => g.Allowed &= m.CanBeHitByNPC(n, ref g.Slot));
                if (!gate.Allowed) return 0;
                slot = gate.Slot;
            }
            if (!projectile.IsNull && !pvp &&
                !PlayerLoader.All(self, nameof(ModPlayer.CanBeHitByProjectile), projectile, static (m, p) => m.CanBeHitByProjectile(p)))
                return 0;
            if (!projectile.IsNull && pvp && (uint)projectile.owner < 255u)
            {
                Player owner = Main.player[projectile.owner];
                if (!owner.IsNull && !PlayerLoader.All(owner, nameof(ModPlayer.CanHitPvpWithProj), (projectile, self),
                        static (m, s) => m.CanHitPvpWithProj(s.projectile, s.self)))
                    return 0;
            }
            modifiers = new Player.HurtModifiers
            {
                DamageSource = damageSource, PvP = pvp, CooldownCounter = slot, Dodgeable = dodgeable, HitDirection = hitDirection,
            };
            if (pvp && (Main.netMode == 0 || self.whoAmI == Main.myPlayer) && ItemPvpHooks.Source(damageSource, self.whoAmI, out attacker, out weapon, out pin))
                ItemPvpHooks.ModifyHitPvp(weapon, attacker, self, ref modifiers);
            if (!npc.IsNull)
                PlayerLoader.Each(self, nameof(ModPlayer.ModifyHitByNPC), npc, ref modifiers, static (m, n, ref h) => m.ModifyHitByNPC(n, ref h));
            if (!projectile.IsNull)
                PlayerLoader.Each(self, nameof(ModPlayer.ModifyHitByProjectile), projectile, ref modifiers,
                    static (m, p, ref h) => m.ModifyHitByProjectile(p, ref h));
            PlayerLoader.Each(self, nameof(ModPlayer.ModifyHurt), 0, ref modifiers, static (m, _, ref h) => m.ModifyHurt(ref h));
            if (modifiers.Cancelled) return 0;
            defense = self.statDefense;
            perPoint = defense > 0 ? (float)((100000 - Main.CalculateDamagePlayersTake(100000, defense)) / defense) : 0f;
            pending = modifiers.ToHurtInfo(Damage, defense, perPoint, 0f, self.noKnockback);
            if (pending.Dodgeable && self.whoAmI == Main.myPlayer)
            {
                if (Any(self, nameof(ModPlayer.FreeDodge), pending, static (m, i) => m.FreeDodge(i))) return 0;
                if (Any(self, nameof(ModPlayer.ConsumableDodge), pending, static (m, i) => m.ConsumableDodge(i))) return 0;
            }
        }
        catch (Exception e)
        {
            ItemHooks.Report("Player.Hurt (before)", e);
            return orig(self, damageSource, Damage, hitDirection, pvp, quiet, Crit, cooldownCounter, dodgeable);
        }
        int raw = Raw(modifiers, Damage, defense, perPoint, pending.Damage);
        double done = orig(self, damageSource, raw, pending.HitDirection, pvp, quiet, Crit, slot, dodgeable);
        if (done <= 0) return done;
        try
        {
            Player.HurtInfo info = pending;
            info.Damage = (int)done;
            if (!weapon.IsNull) ItemPvpHooks.OnHitPvp(weapon, attacker, self, info);
            if (!npc.IsNull) PlayerLoader.Each(self, nameof(ModPlayer.OnHitByNPC), (npc, info), static (m, s) => m.OnHitByNPC(s.npc, s.info));
            if (!projectile.IsNull)
                PlayerLoader.Each(self, nameof(ModPlayer.OnHitByProjectile), (projectile, info), static (m, s) => m.OnHitByProjectile(s.projectile, s.info));
            PlayerLoader.Each(self, nameof(ModPlayer.OnHurt), info, static (m, i) => m.OnHurt(i));
            if (!self.dead && self.statLife > 0) PlayerLoader.Each(self, nameof(ModPlayer.PostHurt), info, static (m, i) => m.PostHurt(i));
        }
        catch (Exception e) { ItemHooks.Report("Player.Hurt (after)", e); }
        return done;
    }

    static int Raw(Player.HurtModifiers modifiers, int damage, int defense, float perPoint, int target)
    {
        if (!modifiers.ChangesAfterDefense)
            return Math.Max(1, (int)Math.Round(modifiers.SourceDamage.ApplyTo(damage) * modifiers.IncomingDamageMultiplier.Value));
        return Math.Max(1, (int)Math.Ceiling(target + defense * perPoint - 1e-6));
    }

    static bool Any(Player player, string method, Player.HurtInfo info, Func<ModPlayer, Player.HurtInfo, bool> call) =>
        !PlayerLoader.All(player, method, info, (m, i) => !call(m, i));
}
