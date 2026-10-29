using System;
using Terraria.ID;
using Terraria.ModLoader;

namespace Terraria.ModLoader.Core;

internal static class ItemTimingHooks
{
    static bool globalTime, globalAnimation, globalAutoReuse, globalAlt;

    static int timing;

    public static void Install(Func<string, bool> has, bool global)
    {
        bool speed = has(nameof(ModItem.UseSpeedMultiplier));
        if (speed || has(nameof(ModItem.UseTimeMultiplier)))
        {
            if (global) globalTime = true;
            ItemHooks.Once("UseTimeMultiplier", () =>
            {
                DamageClassHooks.Install();
                On_Player.ApplyItemTime_Item += ApplyItemTime;
                On_Player.ApplyItemTime_Item_float += ApplyItemTimeMultiplied;
            });
        }
        if (speed || has(nameof(ModItem.UseAnimationMultiplier)))
        {
            if (global) globalAnimation = true;
            ItemHooks.Once("UseAnimationMultiplier", () =>
            {
                DamageClassHooks.Install();
                On_Player.ApplyItemAnimation += ApplyItemAnimation;
            });
        }
        if (has(nameof(ModItem.CanAutoReuseItem)))
        {
            if (global) globalAutoReuse = true;
            ItemHooks.Once("AutoReuse", () => On_Player.ItemCheck_AutoReuseLogic += AutoReuseLogic);
        }
        if (has(nameof(ModItem.AltFunctionUse)))
        {
            if (global) globalAlt = true;
            ItemHooks.Once("AltFunctionUse", () => On_Player.ItemCheck_ManageRightClickFeatures += ManageRightClickFeatures);
        }
    }

    static float Multiplier(Item item, Player player, string name, Func<ModItem, float> own, Func<GlobalItem, float> global)
    {
        float multiplier = 1f;
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { multiplier = own(m); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.{name}", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try { multiplier *= global(g); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.{name}", e); }
        }
        return multiplier;
    }

    static float SpeedMultiplier(Item item, Player player) =>
        Multiplier(item, player, nameof(ModItem.UseSpeedMultiplier), m => m.UseSpeedMultiplier(player), g => g.UseSpeedMultiplier(item, player)) *
        PlayerLoader.Product(player, nameof(ModPlayer.UseSpeedMultiplier), item, static (m, i) => m.UseSpeedMultiplier(i));

    static float TimeFactor(Item item, Player player) =>
        Multiplier(item, player, nameof(ModItem.UseTimeMultiplier), m => m.UseTimeMultiplier(player), g => g.UseTimeMultiplier(item, player)) *
        PlayerLoader.Product(player, nameof(ModPlayer.UseTimeMultiplier), item, static (m, i) => m.UseTimeMultiplier(i)) / SpeedMultiplier(item, player);

    static float AnimationFactor(Item item, Player player) =>
        Multiplier(item, player, nameof(ModItem.UseAnimationMultiplier), m => m.UseAnimationMultiplier(player), g => g.UseAnimationMultiplier(item, player)) *
        PlayerLoader.Product(player, nameof(ModPlayer.UseAnimationMultiplier), item, static (m, i) => m.UseAnimationMultiplier(i)) / SpeedMultiplier(item, player);

    static void ScaleTime(Player self, Item sItem)
    {
        float factor = TimeFactor(sItem, self);
        if (factor == 1f || !float.IsFinite(factor) || factor <= 0) return;
        int frames = Math.Max(1, (int)(self.itemTime * factor));
        self.itemTime = frames;
        self.itemTimeMax = frames;
    }

    static void ApplyItemTime(On_Player.orig_ApplyItemTime_Item orig, Player self, Item sItem)
    {
        if (timing > 0 || !ItemCombatHooks.Wanted(globalTime, sItem))
        {
            orig(self, sItem);
            return;
        }
        timing++;
        try { orig(self, sItem); }
        finally { timing--; }
        ScaleTime(self, sItem);
    }

    static void ApplyItemTimeMultiplied(On_Player.orig_ApplyItemTime_Item_float orig, Player self, Item sItem, float multiplier)
    {
        if (timing > 0 || !ItemCombatHooks.Wanted(globalTime, sItem))
        {
            orig(self, sItem, multiplier);
            return;
        }
        timing++;
        try { orig(self, sItem, multiplier); }
        finally { timing--; }
        ScaleTime(self, sItem);
    }

    static void ApplyItemAnimation(On_Player.orig_ApplyItemAnimation orig, Player self, Item sItem)
    {
        orig(self, sItem);
        if (!ItemCombatHooks.Wanted(globalAnimation, sItem)) return;
        float factor = AnimationFactor(sItem, self);
        if (factor == 1f || !float.IsFinite(factor) || factor <= 0) return;
        int frames = Math.Max(1, (int)(self.itemAnimation * factor));
        self.itemAnimation = frames;
        self.itemAnimationMax = frames;
    }

    static bool? CanAutoReuseItem(Item item, Player player)
    {
        bool? flag = PlayerLoader.Nullable(player, nameof(ModPlayer.CanAutoReuseItem), item, static (m, i) => m.CanAutoReuseItem(i));
        if (flag == false) return false;
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            bool? allow = null;
            try { allow = g.CanAutoReuseItem(item, player); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.CanAutoReuseItem", e); }
            if (allow == false) return false;
            if (allow == true) flag = true;
        }
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            bool? allow = null;
            try { allow = m.CanAutoReuseItem(player); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.CanAutoReuseItem", e); }
            if (allow == false) return false;
            if (allow == true) flag = true;
        }
        return flag;
    }

    static void AutoReuseLogic(On_Player.orig_ItemCheck_AutoReuseLogic orig, Player self, Item sItem)
    {
        bool? decision = ItemCombatHooks.Wanted(globalAutoReuse, sItem) ? CanAutoReuseItem(sItem, self) : null;
        if (decision is not bool allow)
        {
            orig(self, sItem);
            return;
        }
        bool previous = sItem.autoReuse, all = self.autoReuseAllWeapons, glove = self.autoReuseGlove;
        sItem.autoReuse = allow;
        if (!allow)
        {
            self.autoReuseAllWeapons = false;
            self.autoReuseGlove = false;
        }
        try { orig(self, sItem); }
        finally
        {
            sItem.autoReuse = previous;
            self.autoReuseAllWeapons = all;
            self.autoReuseGlove = glove;
        }
    }

    static bool AltFunctionUse(Item item, Player player)
    {
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try
            {
                if (m.AltFunctionUse(player)) return true;
            }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.AltFunctionUse", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try
            {
                if (g.AltFunctionUse(item, player)) return true;
            }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.AltFunctionUse", e); }
        }
        return false;
    }

    static void ManageRightClickFeatures(On_Player.orig_ItemCheck_ManageRightClickFeatures orig, Player self)
    {
        Item held = default;
        bool check = false;
        try
        {
            int slot = self.selectedItem;
            held = slot >= 0 && slot < 58 ? self.inventory[slot] : default;
            if (!held.IsNull && held.stack > 0 && ItemCombatHooks.Wanted(globalAlt, held))
            {
                var repeated = ItemID.Sets.ItemsThatAllowRepeatedRightClick;
                bool repeat = !repeated.IsNull && (uint)held.type < (uint)repeated.Length && repeated[held.type];
                check = self.controlUseTile && Main.myPlayer == self.whoAmI && !self.tileInteractionHappened && self.releaseUseItem &&
                    !self.controlUseItem && !self.mouseInterface && !Main.LocalPlayerHasPendingInventoryActions() && (repeat || Main.mouseRightRelease);
            }
        }
        catch (Exception e) { ItemHooks.Report("AltFunctionUse", e); }
        orig(self);
        if (!check || self.altFunctionUse != 0 || !AltFunctionUse(held, self)) return;
        self.altFunctionUse = 1;
        self.controlUseItem = true;
    }
}
