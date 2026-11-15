using System;
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
        int slot = ItemHooks.Selected(self);
        if ((uint)slot >= 58u || self.whoAmI != Main.myPlayer)
        {
            orig(self);
            return;
        }
        Item held = self.inventory[slot];
        if (held.IsNull || !held.consumable || held.stack <= 0)
        {
            orig(self);
            return;
        }
        int type = held.type, stack = held.stack;
        ModItem? own = ItemHooks.ModItemOf(held);
        short[] overriding = ItemLoader.Globals.Overriding(nameof(GlobalItem.OnConsumeItem));
        GlobalItem?[]? slots = overriding.Length > 0 ? ItemLoader.Globals.Slots(held) : null;
        bool globals = false;
        foreach (short index in overriding)
        {
            if (ItemHooks.Applied(slots, index) == null) continue;
            globals = true;
            break;
        }
        if (own == null && !globals)
        {
            orig(self);
            return;
        }
        orig(self);
        Item now = self.inventory[ItemHooks.Selected(self)];
        if (now.type == type && now.stack >= stack) return;
        if (own != null)
        {
            try { own.OnConsumeItem(self); }
            catch (Exception e) { ItemHooks.Report($"{own.FullName}.OnConsumeItem", e); }
        }
        if (!globals) return;
        Item sample = ContentSamples.ItemsByType[type];
        foreach (short index in overriding)
        {
            if (ItemHooks.Applied(slots, index) is not GlobalItem global) continue;
            try { global.OnConsumeItem(sample, self); }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.OnConsumeItem", e); }
        }
    }
}
