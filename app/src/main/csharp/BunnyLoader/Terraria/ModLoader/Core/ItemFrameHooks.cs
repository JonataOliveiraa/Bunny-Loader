using System;
using Terraria.ModLoader;

namespace Terraria.ModLoader.Core;

internal static class ItemFrameHooks
{
    static bool globalFrames;

    public static void Install(Func<string, bool> has, bool global)
    {
        if (!has(nameof(ModItem.UseItemFrame)) && !has(nameof(ModItem.HoldItemFrame))) return;
        if (global) globalFrames = true;
        ItemHooks.Once("ItemFrames", () => On_Player.PlayerFrame += PlayerFrame);
    }

    static void PlayerFrame(On_Player.orig_PlayerFrame orig, Player self)
    {
        orig(self);
        Item item;
        try
        {
            int slot = self.selectedItem;
            if (slot < 0 || slot >= 59) return;
            item = self.inventory[slot];
            if (item.IsNull || item.stack <= 0 || !ItemCombatHooks.Wanted(globalFrames, item)) return;
        }
        catch (Exception e)
        {
            ItemHooks.Report("UseItemFrame", e);
            return;
        }
        if (self.itemAnimation > 0) UseItemFrame(item, self);
        else if (self.CanVisuallyHoldItem(item)) HoldItemFrame(item, self);
    }

    static void UseItemFrame(Item item, Player player)
    {
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { m.UseItemFrame(player); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.UseItemFrame", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try { g.UseItemFrame(item, player); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.UseItemFrame", e); }
        }
    }

    static void HoldItemFrame(Item item, Player player)
    {
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { m.HoldItemFrame(player); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.HoldItemFrame", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try { g.HoldItemFrame(item, player); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.HoldItemFrame", e); }
        }
    }
}
