using System;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace Terraria.ModLoader.Core;

internal static class ItemCombatHooks
{
    static bool globalDamage, globalCrit, globalKnockback, globalScale, globalHitbox, globalEffects;

    static bool inHitbox;

    public static void Install(Func<string, bool> has, bool global)
    {
        if (has(nameof(ModItem.ModifyWeaponDamage)))
        {
            if (global) globalDamage = true;
            ItemHooks.Once("WeaponDamage", () => On_Player.GetWeaponDamage += GetWeaponDamage);
        }
        if (has(nameof(ModItem.ModifyWeaponCrit)))
        {
            if (global) globalCrit = true;
            ItemHooks.Once("WeaponCrit", () => On_Player.GetWeaponCrit += GetWeaponCrit);
        }
        if (has(nameof(ModItem.ModifyWeaponKnockback)))
        {
            if (global) globalKnockback = true;
            ItemHooks.Once("WeaponKnockback", () => On_Player.GetWeaponKnockback += GetWeaponKnockback);
        }
        bool scale = has(nameof(ModItem.ModifyItemScale));
        if (scale)
        {
            if (global) globalScale = true;
            ItemHooks.Once("ItemScale", () => On_Player.GetAdjustedItemScale += GetAdjustedItemScale);
        }
        if (scale || has(nameof(ModItem.UseItemHitbox)))
        {
            if (global) globalHitbox = true;
            ItemHooks.Once("ItemHitbox", () => On_Player.ItemCheck_GetMeleeHitbox += GetMeleeHitbox);
        }
        if (has(nameof(ModItem.MeleeEffects)))
        {
            if (global) globalEffects = true;
            ItemHooks.Once("MeleeEffects", () => On_Player.ItemCheck_EmitUseVisuals += EmitUseVisuals);
        }
    }

    internal static bool Wanted(bool global, Item item) => !item.IsNull && item.type > 0 && (global || ItemLoader.Get(item.type) != null);

    static int GetWeaponDamage(On_Player.orig_GetWeaponDamage orig, Player self, Item sItem)
    {
        int value = orig(self, sItem);
        if (!Wanted(globalDamage, sItem) || sItem.stack <= 0) return value;
        try
        {
            int baseDamage = sItem.damage;
            StatModifier start = baseDamage > 0 ? new StatModifier((float)value / baseDamage, 1f) : StatModifier.Default;
            StatModifier damage = start;
            ModifyWeaponDamage(sItem, self, ref damage);
            if (damage == start) return value;
            float at = Math.Max(baseDamage, 0);
            double result = value + ((double)damage.ApplyTo(at) - start.ApplyTo(at));
            if (!double.IsFinite(result)) return value;
            int nearest = (int)Math.Round(result);
            return Math.Max(0, Math.Abs(result - nearest) < 1e-3 ? nearest : (int)result);
        }
        catch (Exception e)
        {
            ItemHooks.Report("ModifyWeaponDamage", e);
            return value;
        }
    }

    static void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
    {
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { m.ModifyWeaponDamage(player, ref damage); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.ModifyWeaponDamage", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try { g.ModifyWeaponDamage(item, player, ref damage); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.ModifyWeaponDamage", e); }
        }
        PlayerLoader.Each(player, nameof(ModPlayer.ModifyWeaponDamage), item, ref damage, static (m, i, ref d) => m.ModifyWeaponDamage(i, ref d));
    }

    static int GetWeaponCrit(On_Player.orig_GetWeaponCrit orig, Player self, Item sItem)
    {
        int value = orig(self, sItem);
        if (!Wanted(globalCrit, sItem) || sItem.stack <= 0) return value;
        float crit = value;
        if (ItemHooks.ModItemOf(sItem) is ModItem m)
        {
            try { m.ModifyWeaponCrit(self, ref crit); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.ModifyWeaponCrit", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(sItem))
        {
            if (g == null) continue;
            try { g.ModifyWeaponCrit(sItem, self, ref crit); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.ModifyWeaponCrit", e); }
        }
        PlayerLoader.Each(self, nameof(ModPlayer.ModifyWeaponCrit), sItem, ref crit, static (m, i, ref c) => m.ModifyWeaponCrit(i, ref c));
        if (crit == value || !float.IsFinite(crit)) return value;
        return (int)(crit + 5E-06f);
    }

    static float GetWeaponKnockback(On_Player.orig_GetWeaponKnockback orig, Player self, Item sItem, float knockBack)
    {
        float value = orig(self, sItem, knockBack);
        if (!Wanted(globalKnockback, sItem) || sItem.stack <= 0) return value;
        StatModifier start = knockBack != 0 ? new StatModifier(value / knockBack, 1f) : StatModifier.Default;
        StatModifier knockback = start;
        if (ItemHooks.ModItemOf(sItem) is ModItem m)
        {
            try { m.ModifyWeaponKnockback(self, ref knockback); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.ModifyWeaponKnockback", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(sItem))
        {
            if (g == null) continue;
            try { g.ModifyWeaponKnockback(sItem, self, ref knockback); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.ModifyWeaponKnockback", e); }
        }
        PlayerLoader.Each(self, nameof(ModPlayer.ModifyWeaponKnockback), sItem, ref knockback, static (m, i, ref k) => m.ModifyWeaponKnockback(i, ref k));
        if (knockback == start) return value;
        float result = value + (knockback.ApplyTo(knockBack) - start.ApplyTo(knockBack));
        return float.IsFinite(result) ? Math.Max(0f, result) : value;
    }

    static void ModifyItemScale(Item item, Player player, ref float scale)
    {
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { m.ModifyItemScale(player, ref scale); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.ModifyItemScale", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try { g.ModifyItemScale(item, player, ref scale); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.ModifyItemScale", e); }
        }
        PlayerLoader.Each(player, nameof(ModPlayer.ModifyItemScale), item, ref scale, static (m, i, ref s) => m.ModifyItemScale(i, ref s));
    }

    static float GetAdjustedItemScale(On_Player.orig_GetAdjustedItemScale orig, Player self, Item item)
    {
        float value = orig(self, item);
        if (inHitbox || !Wanted(globalScale, item)) return value;
        float scale = value;
        ModifyItemScale(item, self, ref scale);
        return float.IsFinite(scale) ? Math.Max(0f, scale) : value;
    }

    static void GetMeleeHitbox(On_Player.orig_ItemCheck_GetMeleeHitbox orig, Player self, Item sItem, Rectangle heldItemFrame, out bool dontAttack, out Rectangle itemRectangle)
    {
        if (!Wanted(globalHitbox, sItem))
        {
            orig(self, sItem, heldItemFrame, out dontAttack, out itemRectangle);
            return;
        }
        float previous = sItem.scale;
        bool changed = false;
        if (globalScale || ItemHooks.ModItemOf(sItem) is ModItem)
        {
            try
            {
                float vanilla = 1f;
                if (sItem.melee) self.ApplyMeleeScale(ref vanilla);
                float scale = previous * vanilla, before = scale;
                ModifyItemScale(sItem, self, ref scale);
                if (scale != before && float.IsFinite(scale) && vanilla > 0)
                {
                    sItem.scale = Math.Max(0f, scale) / vanilla;
                    changed = true;
                }
            }
            catch (Exception e) { ItemHooks.Report("ModifyItemScale (hitbox)", e); }
        }
        bool outer = inHitbox;
        inHitbox = true;
        try { orig(self, sItem, heldItemFrame, out dontAttack, out itemRectangle); }
        finally
        {
            inHitbox = outer;
            if (changed) sItem.scale = previous;
        }
        if (ItemHooks.ModItemOf(sItem) is ModItem m)
        {
            try { m.UseItemHitbox(self, ref itemRectangle, ref dontAttack); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.UseItemHitbox", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(sItem))
        {
            if (g == null) continue;
            try { g.UseItemHitbox(sItem, self, ref itemRectangle, ref dontAttack); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.UseItemHitbox", e); }
        }
    }

    static void EmitUseVisuals(On_Player.orig_ItemCheck_EmitUseVisuals orig, Player self, Item sItem, Rectangle itemRectangle)
    {
        orig(self, sItem, itemRectangle);
        if (!Wanted(globalEffects, sItem)) return;
        if (ItemHooks.ModItemOf(sItem) is ModItem m)
        {
            try { m.MeleeEffects(self, itemRectangle); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.MeleeEffects", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(sItem))
        {
            if (g == null) continue;
            try { g.MeleeEffects(sItem, self, itemRectangle); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.MeleeEffects", e); }
        }
        PlayerLoader.Each(self, nameof(ModPlayer.MeleeEffects), (sItem, itemRectangle), static (m, s) => m.MeleeEffects(s.sItem, s.itemRectangle));
    }
}
