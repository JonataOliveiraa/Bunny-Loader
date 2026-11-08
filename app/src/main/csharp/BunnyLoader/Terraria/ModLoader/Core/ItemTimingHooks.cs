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
            ItemHooks.Once("AltFunctionUse", () =>
            {
                On_Player.ItemCheck_ManageRightClickFeatures += ManageRightClickFeatures;
                On_Player.CanAlternativeFire += CanAlternativeFire;
                On_VirtualControllerInputState.get_ToggleAlternativeFireMode += ToggleAlternativeFireMode;
                On_VirtualControllerInputState.get_SwapAlternativeFiteModeActive += SwapAlternativeFireModeActive;
                AltFunctionTouchHooks.Install();
            });
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

    internal static Item AlternativeItem(Player player)
    {
        if (player.IsNull) return default;
        try
        {
            int slot = player.selectedItem;
            Item held = slot >= 0 && slot < 58 ? player.inventory[slot] : default;
            return !held.IsNull && held.stack > 0 && ItemCombatHooks.Wanted(globalAlt, held) && AltFunctionUse(held, player) ? held : default;
        }
        catch (Exception e)
        {
            ItemHooks.Report("AltFunctionUse", e);
            return default;
        }
    }

    static bool HoldsAlternativeItem(Player player) => !AlternativeItem(player).IsNull;

    static bool CanAlternativeFire(On_Player.orig_CanAlternativeFire orig, Player self, bool checkIfHasMapping) =>
        orig(self, checkIfHasMapping) || HoldsAlternativeItem(self);

    static bool SwapAlternativeFireModeActive(On_VirtualControllerInputState.orig_get_SwapAlternativeFiteModeActive orig, VirtualControllerInputState self) =>
        orig(self) || !self.hasToggleFireInteract && self.hasTouchRightStick && HoldsAlternativeItem(Main.LocalPlayer);

    static bool ToggleAlternativeFireMode(On_VirtualControllerInputState.orig_get_ToggleAlternativeFireMode orig, VirtualControllerInputState self)
    {
        if (orig(self)) return true;
        if (self.hasToggleFireInteract || !self.hasTouchRightStick || !HoldsAlternativeItem(Main.LocalPlayer)) return false;
        return self.TouchToggleFireInteract switch
        {
            VirtualControllerInputState.CursorInputMode.Global => self.globalToggleAlternativeFireModeInternal,
            VirtualControllerInputState.CursorInputMode.AlwaysOn => true,
            VirtualControllerInputState.CursorInputMode.Disabled => false,
            _ => self.toggleAlternativeFireModeInternal,
        };
    }

    static bool AlternativeFireMode()
    {
        GUIInstance gui = GUIInstance.Active;
        return !gui.IsNull && !gui.VirtualControllerInputState.IsNull && gui.VirtualControllerInputState.ToggleAlternativeFireMode;
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
                bool alternative = self.UsingAlternativeFire && self.controlUseItem && AlternativeFireMode();
                check = (self.controlUseTile || alternative) && Main.myPlayer == self.whoAmI && !self.tileInteractionHappened && self.releaseUseItem &&
                    (alternative || !self.controlUseItem) && !self.mouseInterface && !Main.LocalPlayerHasPendingInventoryActions() && (repeat || alternative || Main.mouseRightRelease);
            }
        }
        catch (Exception e) { ItemHooks.Report("AltFunctionUse", e); }
        orig(self);
        if (!check || self.altFunctionUse != 0 || !AltFunctionUse(held, self)) return;
        self.altFunctionUse = 1;
        self.controlUseItem = true;
    }
}
