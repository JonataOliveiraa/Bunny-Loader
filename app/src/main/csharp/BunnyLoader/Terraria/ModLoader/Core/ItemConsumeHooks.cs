using System;
using System.Collections.Generic;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

internal static class ItemConsumeHooks
{
    internal static void Install(Func<string, bool> has)
    {
        if (has(nameof(ModItem.OnCreated))) PlayerWorldHooks.WantBoughtItems();
        if (has(nameof(ModItem.OnConsumeItem)))
            ItemHooks.Once("ItemOnConsume", () => On_Player.ItemCheck += ItemCheck);
    }

    static void ItemCheck(On_Player.orig_ItemCheck orig, Player self)
    {
        if (self.whoAmI != Main.myPlayer || (uint)self.selectedItem >= 58u)
        {
            orig(self);
            return;
        }
        Item held = self.inventory[self.selectedItem];
        if (held.IsNull || !held.consumable || held.stack <= 0)
        {
            orig(self);
            return;
        }
        int type = held.type, stack = held.stack;
        ModItem? own = ItemHooks.ModItemOf(held);
        List<GlobalItem> globals = new();
        foreach (GlobalItem? global in ItemHooks.GlobalsOf(held))
            if (global != null && ItemHooks.Wants(global, nameof(GlobalItem.OnConsumeItem))) globals.Add(global);
        if (own == null && globals.Count == 0)
        {
            orig(self);
            return;
        }
        orig(self);
        Item now = self.inventory[self.selectedItem];
        if (now.type == type && now.stack >= stack) return;
        if (own != null)
        {
            try { own.OnConsumeItem(self); }
            catch (Exception e) { ItemHooks.Report($"{own.FullName}.OnConsumeItem", e); }
        }
        Item sample = ContentSamples.ItemsByType[type];
        foreach (GlobalItem global in globals)
        {
            try { global.OnConsumeItem(sample, self); }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.OnConsumeItem", e); }
        }
    }
}
