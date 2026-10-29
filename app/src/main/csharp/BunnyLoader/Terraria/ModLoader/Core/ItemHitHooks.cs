using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader;

namespace Terraria.ModLoader.Core;

internal static class ItemHitHooks
{
    sealed class Attack
    {
        public Player Player;
        public Item Item;
        public NPC Target;
        public bool? CanHit;
        public bool Friendly;
        public bool RestoreFriendly;
    }

    static bool globalHit;

    static Attack? attack;

    static bool hidingText;

    public static void Install(Func<string, bool> has, bool global)
    {
        bool canHit = has(nameof(ModItem.CanHitNPC));
        if (canHit || has(nameof(ModItem.CanMeleeAttackCollideWithNPC)) || has(nameof(ModItem.ModifyHitNPC)) || has(nameof(ModItem.OnHitNPC)))
        {
            if (global) globalHit = true;
            ItemHooks.Once("ItemAttack", () =>
            {
                On_Player.ProcessHitAgainstNPC += ProcessHitAgainstNPC;
                On_NPC.StrikeNPC += StrikeNPC;
            });
        }
        if (canHit)
            ItemHooks.Once("ItemCanHit", () => On_Player.CanNPCBeHitByPlayerOrPlayerProjectile += CanNPCBeHit);
    }

    static bool? CanHitNPC(Item item, Player player, NPC target)
    {
        if (!PlayerLoader.All(player, nameof(ModPlayer.CanHitNPC), target, static (m, t) => m.CanHitNPC(t))) return false;
        bool? flag = PlayerLoader.Nullable(player, nameof(ModPlayer.CanHitNPCWithItem), (item, target), static (m, s) => m.CanHitNPCWithItem(s.item, s.target));
        if (flag == false) return false;
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            bool? canHit = null;
            try { canHit = g.CanHitNPC(item, player, target); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.CanHitNPC", e); }
            if (canHit == false) return false;
            if (canHit == true) flag = true;
        }
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            bool? canHit = null;
            try { canHit = m.CanHitNPC(player, target); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.CanHitNPC", e); }
            if (canHit == false) return false;
            if (canHit == true) flag = true;
        }
        return flag;
    }

    static bool? CanMeleeAttackCollideWithNPC(Item item, Rectangle meleeAttackHitbox, Player player, NPC target)
    {
        bool? flag = PlayerLoader.Nullable(player, nameof(ModPlayer.CanMeleeAttackCollideWithNPC), (item, meleeAttackHitbox, target),
            static (m, s) => m.CanMeleeAttackCollideWithNPC(s.item, s.meleeAttackHitbox, s.target));
        if (flag == false) return false;
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            bool? canCollide = null;
            try { canCollide = g.CanMeleeAttackCollideWithNPC(item, meleeAttackHitbox, player, target); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.CanMeleeAttackCollideWithNPC", e); }
            if (canCollide == false) return false;
            if (canCollide == true) flag = true;
        }
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            bool? canCollide = null;
            try { canCollide = m.CanMeleeAttackCollideWithNPC(meleeAttackHitbox, player, target); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.CanMeleeAttackCollideWithNPC", e); }
            if (canCollide == false) return false;
            if (canCollide == true) flag = true;
        }
        return flag;
    }

    static void ProcessHitAgainstNPC(On_Player.orig_ProcessHitAgainstNPC orig, Player self, Item sItem, Rectangle itemRectangle, int originalDamage, float knockBack, int npcIndex)
    {
        if (!ItemCombatHooks.Wanted(globalHit, sItem) || (uint)npcIndex >= 200u)
        {
            orig(self, sItem, itemRectangle, originalDamage, knockBack, npcIndex);
            return;
        }
        NPC target = Main.npc[npcIndex];
        bool? canHit = CanHitNPC(sItem, self, target);
        if (canHit == false) return;
        bool? collide = CanMeleeAttackCollideWithNPC(sItem, itemRectangle, self, target);
        if (collide == false) return;
        Rectangle rect = itemRectangle;
        if (collide == true)
        {
            Rectangle hitbox = target.Hitbox;
            int left = Math.Min(rect.X, hitbox.X), top = Math.Min(rect.Y, hitbox.Y);
            int right = Math.Max(rect.X + rect.Width, hitbox.X + hitbox.Width), bottom = Math.Max(rect.Y + rect.Height, hitbox.Y + hitbox.Height);
            rect = new Rectangle(left, top, right - left, bottom - top);
        }
        Attack? outer = attack;
        var current = new Attack { Player = self, Item = sItem, Target = target, CanHit = canHit };
        attack = current;
        try { orig(self, sItem, rect, originalDamage, knockBack, npcIndex); }
        finally
        {
            if (current.RestoreFriendly) target.friendly = current.Friendly;
            attack = outer;
        }
    }

    static bool CanNPCBeHit(On_Player.orig_CanNPCBeHitByPlayerOrPlayerProjectile orig, Player self, NPC npc, Projectile projectile)
    {
        Attack? a = attack;
        if (a == null || !projectile.IsNull || a.CanHit != true || a.Player != self || a.Target != npc) return orig(self, npc, projectile);
        if (npc.friendly && !a.RestoreFriendly)
        {
            a.Friendly = true;
            a.RestoreFriendly = true;
            npc.friendly = false;
        }
        return true;
    }

    static int StrikeNPC(On_NPC.orig_StrikeNPC orig, NPC self, int Damage, float knockBack, int hitDirection, bool crit, bool fromNet, int owner)
    {
        Attack? a = attack;
        if (fromNet || a == null || a.Target != self) return orig(self, Damage, knockBack, hitDirection, crit, fromNet, owner);
        if (a.RestoreFriendly)
        {
            self.friendly = a.Friendly;
            a.RestoreFriendly = false;
        }
        attack = null;
        try { return Strike(orig, self, Damage, knockBack, hitDirection, crit, owner, a); }
        finally { attack = a; }
    }

    static int Strike(On_NPC.orig_StrikeNPC orig, NPC npc, int damage, float knockBack, int hitDirection, bool crit, int owner, Attack a)
    {
        Item item = a.Item;
        Player player = a.Player;
        NPC.HitInfo hit;
        try
        {
            var modifiers = new NPC.HitModifiers { DamageType = DamageClassLoader.ItemClass(item), HitDirection = hitDirection };
            modifiers.Defense.Base = npc.defense;
            ModifyHitNPC(item, player, npc, ref modifiers);
            hit = modifiers.ToHitInfo(damage, crit, knockBack);
        }
        catch (Exception e)
        {
            ItemHooks.Report("ModifyHitNPC", e);
            return orig(npc, damage, knockBack, hitDirection, crit, false, owner);
        }
        int raw = RawDamage(npc, hit);
        bool hide = hit.HideCombatText;
        if (hide) HideText();
        int done;
        try { done = orig(npc, raw, hit.Knockback, hit.HitDirection, hit.Crit && !hit.InstantKill, false, owner); }
        finally
        {
            if (hide) hidingText = false;
        }
        if (done <= 0) return done;
        hit.Damage = done;
        OnHitNPC(item, player, npc, hit, done);
        return done;
    }

    static int RawDamage(NPC npc, NPC.HitInfo hit)
    {
        if (hit.InstantKill) return Math.Max(1, npc.life) + (int)Math.Ceiling(npc.defense / 2.0) + 1;
        int factor = hit.Crit ? 2 : 1;
        return Math.Max(1, (int)Math.Ceiling(hit.Damage / (double)factor + npc.defense / 2.0 - 1e-9));
    }

    static void HideText()
    {
        hidingText = true;
        ItemHooks.Once("HideCombatText", () => On_CombatText.NewText_Rectangle_Color_int_bool_bool += NewText);
    }

    static int NewText(On_CombatText.orig_NewText_Rectangle_Color_int_bool_bool orig, Rectangle location, Color color, int amount, bool dramatic, bool dot) =>
        hidingText ? -1 : orig(location, color, amount, dramatic, dot);

    static void ModifyHitNPC(Item item, Player player, NPC target, ref NPC.HitModifiers modifiers)
    {
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { m.ModifyHitNPC(player, target, ref modifiers); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.ModifyHitNPC", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try { g.ModifyHitNPC(item, player, target, ref modifiers); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.ModifyHitNPC", e); }
        }
        PlayerLoader.Each(player, nameof(ModPlayer.ModifyHitNPC), target, ref modifiers, static (m, t, ref h) => m.ModifyHitNPC(t, ref h));
        PlayerLoader.Each(player, nameof(ModPlayer.ModifyHitNPCWithItem), (item, target), ref modifiers,
            static (m, s, ref h) => m.ModifyHitNPCWithItem(s.item, s.target, ref h));
    }

    static void OnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { m.OnHitNPC(player, target, hit, damageDone); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.OnHitNPC", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try { g.OnHitNPC(item, player, target, hit, damageDone); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.OnHitNPC", e); }
        }
        PlayerLoader.Each(player, nameof(ModPlayer.OnHitNPC), (target, hit, damageDone), static (m, s) => m.OnHitNPC(s.target, s.hit, s.damageDone));
        PlayerLoader.Each(player, nameof(ModPlayer.OnHitNPCWithItem), (item, target, hit, damageDone),
            static (m, s) => m.OnHitNPCWithItem(s.item, s.target, s.hit, s.damageDone));
    }
}
