using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader;

namespace Terraria.ModLoader.Core;

internal static class ItemHitHooks
{
    struct Attack
    {
        public Player Player;
        public Item Item;
        public NPC Target;
        public bool? CanHit;
        public bool Friendly;
        public bool RestoreFriendly;
        public bool Struck;
    }

    static bool globalHit, npcHit;

    [ThreadStatic] static Attack attack;
    [ThreadStatic] static bool hasAttack;

    [ThreadStatic] static int hidingText, critText;

    const float CritKnockback = 1.4f;

    public static void Install(Func<string, bool> has, bool global)
    {
        bool canHit = has(nameof(ModItem.CanHitNPC));
        if (canHit || has(nameof(ModItem.CanMeleeAttackCollideWithNPC)) || has(nameof(ModItem.ModifyHitNPC)) || has(nameof(ModItem.OnHitNPC)))
        {
            if (global) globalHit = true;
            WantAttack();
        }
        if (canHit)
            ItemHooks.Once("ItemCanHit", () => On_Player.CanNPCBeHitByPlayerOrPlayerProjectile += CanNPCBeHit);
    }

    internal static void InstallNPC(NPCMethod methods)
    {
        if ((methods & NPCLoader.ItemHitMethods) == 0) return;
        npcHit = true;
        WantAttack();
        if ((methods & NPCMethod.CanBeHitByItem) != 0)
            ItemHooks.Once("ItemCanHit", () => On_Player.CanNPCBeHitByPlayerOrPlayerProjectile += CanNPCBeHit);
    }

    static void WantAttack() => ItemHooks.Once("ItemAttack", () =>
    {
        On_Player.ProcessHitAgainstNPC += ProcessHitAgainstNPC;
        On_NPC.StrikeNPC += StrikeNPC;
    });

    static bool? CanHitNPC(Item item, Player player, NPC target)
    {
        if (!PlayerLoader.All(player, nameof(ModPlayer.CanHitNPC), target, static (m, t) => m.CanHitNPC(t))) return false;
        bool? result = PlayerLoader.Nullable(player, nameof(ModPlayer.CanHitNPCWithItem), (item, target), static (m, s) => m.CanHitNPCWithItem(s.item, s.target));
        if (result == false) return false;
        bool? own = CanItemHitNPC(item, player, target);
        result ??= own;
        if (result == false) return false;
        bool? npc = NPCLoader.CanBeHitByItem(target, player, item);
        return result ?? npc;
    }

    static bool? CanItemHitNPC(Item item, Player player, NPC target)
    {
        bool? flag = null;
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
        bool? result = PlayerLoader.Nullable(player, nameof(ModPlayer.CanMeleeAttackCollideWithNPC), (item, meleeAttackHitbox, target),
            static (m, s) => m.CanMeleeAttackCollideWithNPC(s.item, s.meleeAttackHitbox, s.target));
        if (result == false) return false;
        bool? own = CanItemMeleeAttackCollideWithNPC(item, meleeAttackHitbox, player, target);
        result ??= own;
        if (result == false) return false;
        bool? npc = NPCLoader.CanCollideWithPlayerMeleeAttack(target, player, item, meleeAttackHitbox);
        return result ?? npc;
    }

    static bool? CanItemMeleeAttackCollideWithNPC(Item item, Rectangle meleeAttackHitbox, Player player, NPC target)
    {
        bool? flag = null;
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
        if ((uint)npcIndex >= 200u)
        {
            orig(self, sItem, itemRectangle, originalDamage, knockBack, npcIndex);
            return;
        }
        NPC target = Main.npc[npcIndex];
        if (!ItemCombatHooks.Wanted(globalHit, sItem) && !(npcHit && NPCLoader.HasHooks(target, NPCLoader.ItemHitMethods)))
        {
            orig(self, sItem, itemRectangle, originalDamage, knockBack, npcIndex);
            return;
        }
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
        Attack outer = attack;
        bool outerActive = hasAttack;
        attack = new Attack { Player = self, Item = sItem, Target = target, CanHit = canHit };
        hasAttack = true;
        try { orig(self, sItem, rect, originalDamage, knockBack, npcIndex); }
        finally
        {
            if (attack.RestoreFriendly) target.friendly = attack.Friendly;
            attack = outer;
            hasAttack = outerActive;
        }
    }

    static bool CanNPCBeHit(On_Player.orig_CanNPCBeHitByPlayerOrPlayerProjectile orig, Player self, NPC npc, Projectile projectile)
    {
        if (!hasAttack || !projectile.IsNull || attack.CanHit != true || attack.Player != self || attack.Target != npc) return orig(self, npc, projectile);
        ref Attack a = ref attack;
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
        if (fromNet || !hasAttack || attack.Struck || attack.Target != self) return orig(self, Damage, knockBack, hitDirection, crit, fromNet, owner);
        attack.Struck = true;
        Attack a = attack;
        if (a.RestoreFriendly)
        {
            self.friendly = a.Friendly;
            a.RestoreFriendly = false;
        }
        hasAttack = false;
        try { return Strike(orig, self, Damage, knockBack, hitDirection, crit, owner, a); }
        finally { attack = a; hasAttack = true; }
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
            NPCLoader.ModifyIncomingHit(npc, ref modifiers);
            ModifyHitNPC(item, player, npc, ref modifiers);
            hit = modifiers.ToHitInfo(damage, crit, knockBack);
        }
        catch (Exception e)
        {
            ItemHooks.Report("ModifyHitNPC", e);
            return orig(npc, damage, knockBack, hitDirection, crit, false, owner);
        }
        StrikePlan strike = PlanStrike(npc, hit);
        bool hide = hit.HideCombatText;
        if (hide) HideText();
        NPC health = (uint)npc.realLife < 200u ? Main.npc[npc.realLife] : npc;
        int lifeBefore = health.life;
        bool immortal = npc.immortal;
        int done;
        using (var context = new NPCEventHooks.HitContext(npc, hit))
        {
            using var crits = new CritTextScope(strike);
            try { done = orig(npc, strike.Raw, strike.Knockback, hit.HitDirection, strike.Crit, false, owner); }
            finally
            {
                if (hide) ShowText();
            }
        }
        if (done <= 0) return done;
        if (!hit.InstantKill) hit.Damage = done;
        done = immortal ? 0 : Math.Min(Math.Max(lifeBefore, 0), done);
        OnHitNPC(item, player, npc, hit, done);
        return done;
    }

    internal static int RawDamage(NPC npc, NPC.HitInfo hit)
    {
        if (hit.InstantKill) return Math.Max(1, npc.life) + (int)Math.Ceiling(npc.defense / 2.0) + 1;
        int factor = hit.Crit ? 2 : 1;
        return Math.Max(1, (int)Math.Ceiling(hit.Damage / (double)factor + npc.defense / 2.0 - 1e-9));
    }

    internal readonly struct StrikePlan
    {
        public readonly int Raw;
        public readonly float Knockback;
        public readonly bool Crit, CritText;

        public StrikePlan(int raw, float knockback, bool crit, bool critText)
        {
            Raw = raw;
            Knockback = knockback;
            Crit = crit;
            CritText = critText;
        }
    }

    static int Predict(int raw, int defense, int factor) => (int)Math.Max(1.0, factor * Math.Max(1.0, raw - defense * 0.5));

    internal static StrikePlan PlanStrike(NPC npc, NPC.HitInfo hit)
    {
        int raw = RawDamage(npc, hit);
        bool crit = hit.Crit && !hit.InstantKill;
        if (!crit || Predict(raw, npc.defense, 2) == hit.Damage) return new StrikePlan(raw, hit.Knockback, crit, false);
        int plain = Math.Max(1, (int)Math.Ceiling(hit.Damage + npc.defense / 2.0 - 1e-9));
        ItemHooks.Once("HideCombatText", () => On_CombatText.NewText_Rectangle_Color_int_bool_bool += NewText);
        return new StrikePlan(plain, hit.Knockback * CritKnockback, false, true);
    }

    internal readonly struct CritTextScope : IDisposable
    {
        readonly bool active;

        internal CritTextScope(in StrikePlan strike)
        {
            active = strike.CritText;
            if (active) critText++;
        }

        public void Dispose()
        {
            if (active && critText > 0) critText--;
        }
    }

    static Color CritColor(Color color)
    {
        if (color.Equals(CombatText.DamagedHostile)) return CombatText.DamagedHostileCrit;
        if (color.Equals(CombatText.OthersDamagedHostile)) return CombatText.OthersDamagedHostileCrit;
        if (color.Equals(CombatText.DamagedFriendly)) return CombatText.DamagedFriendlyCrit;
        return color;
    }

    internal static void ShowText() { if (hidingText > 0) hidingText--; }

    internal static void HideText()
    {
        hidingText++;
        ItemHooks.Once("HideCombatText", () => On_CombatText.NewText_Rectangle_Color_int_bool_bool += NewText);
    }

    static int NewText(On_CombatText.orig_NewText_Rectangle_Color_int_bool_bool orig, Rectangle location, Color color, int amount, bool dramatic, bool dot)
    {
        if (hidingText > 0) return -1;
        if (critText > 0) return orig(location, CritColor(color), amount, true, dot);
        return orig(location, color, amount, dramatic, dot);
    }

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
        NPCLoader.ModifyHitByItem(target, player, item, ref modifiers);
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
        NPCLoader.OnHitByItem(target, player, item, hit, damageDone);
        PlayerLoader.Each(player, nameof(ModPlayer.OnHitNPC), (target, hit, damageDone), static (m, s) => m.OnHitNPC(s.target, s.hit, s.damageDone));
        PlayerLoader.Each(player, nameof(ModPlayer.OnHitNPCWithItem), (item, target, hit, damageDone),
            static (m, s) => m.OnHitNPCWithItem(s.item, s.target, s.hit, s.damageDone));
    }
}
