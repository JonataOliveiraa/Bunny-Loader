using System;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

internal static class ItemVisibilityHooks
{
    internal static void Install(Func<string, bool> has)
    {
        if (has(nameof(ModItem.UpdateVisibleAccessory)))
            ItemHooks.Once("ItemVisibleAccessory", () =>
            {
                ItemHooks.TypeGate<On_Player.hook_UpdateVisibleAccessory>(2, static type => ItemHooks.WantsType(type, nameof(ModItem.UpdateVisibleAccessory)));
                On_Player.UpdateVisibleAccessory += Visible;
            });
        if (has(nameof(ModItem.UpdateItemDye)))
            ItemHooks.Once("ItemDye", () =>
            {
                ItemHooks.TypeGate<On_Player.hook_UpdateItemDye>(3, static type => ItemHooks.WantsType(type, nameof(ModItem.UpdateItemDye)));
                On_Player.UpdateItemDye += Dye;
            });
        if (has(nameof(ModItem.UpdateInfoAccessory))) ItemHooks.Once("ItemInfoAccessory", () => On_Player.RefreshInfoAccs += RefreshInfo);
    }

    static void Visible(On_Player.orig_UpdateVisibleAccessory orig, Player player, int itemSlot, Item item)
    {
        orig(player, itemSlot, item);
        if (item.IsNull) return;
        int type = item.type;
        if (type <= 0 || item.stack <= 0) return;
        short[] overriding = ItemLoader.Globals.Overriding(nameof(GlobalItem.UpdateVisibleAccessory));
        ModItem? m = ItemHooks.Overrider(item, type, nameof(ModItem.UpdateVisibleAccessory));
        if (m == null && overriding.Length == 0) return;
        var hide = player.hideVisibleAccessory;
        bool hidden = !hide.IsNull && (uint)itemSlot < (uint)hide.Length && hide[itemSlot];
        if (m != null)
        {
            try { m.UpdateVisibleAccessory(player, hidden); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.UpdateVisibleAccessory", e); }
        }
        if (overriding.Length == 0) return;
        GlobalItem?[]? slots = ItemLoader.Globals.Slots(item);
        foreach (short index in overriding)
        {
            if (ItemHooks.Applied(slots, index) is not GlobalItem g) continue;
            try { g.UpdateVisibleAccessory(item, player, hidden); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.UpdateVisibleAccessory", e); }
        }
    }

    static void Dye(On_Player.orig_UpdateItemDye orig, Player player, bool functional, bool hidden, Item item, Item dyeItem)
    {
        orig(player, functional, hidden, item, dyeItem);
        if (item.IsNull) return;
        int type = item.type;
        if (type <= 0 || item.stack <= 0) return;
        short[] overriding = ItemLoader.Globals.Overriding(nameof(GlobalItem.UpdateItemDye));
        ModItem? m = ItemHooks.Overrider(item, type, nameof(ModItem.UpdateItemDye));
        if (m == null && overriding.Length == 0) return;
        int dye = dyeItem.IsNull ? 0 : dyeItem.dye;
        if (m != null)
        {
            try { m.UpdateItemDye(player, dye, functional && hidden); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.UpdateItemDye", e); }
        }
        if (overriding.Length == 0) return;
        GlobalItem?[]? slots = ItemLoader.Globals.Slots(item);
        foreach (short index in overriding)
        {
            if (ItemHooks.Applied(slots, index) is not GlobalItem g) continue;
            try { g.UpdateItemDye(item, player, dye, functional && hidden); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.UpdateItemDye", e); }
        }
    }

    static void RefreshInfo(On_Player.orig_RefreshInfoAccs orig, Player player)
    {
        orig(player);
        try
        {
            short[] overriding = ItemLoader.Globals.Overriding(nameof(GlobalItem.UpdateInfoAccessory));
            bool voidBag = false;
            var inventory = player.inventory;
            for (int i = 0; i < 58 && i < inventory.Length; i++)
            {
                Item item = inventory[i];
                if (item.IsNull) continue;
                if (item.type == 4131) voidBag = true;
                ItemHooks.UpdateInfoAccessory(item, player, overriding);
            }
            for (int slot = 0; slot < 20; slot++)
            {
                if (player.IsItemSlotUnlockedAndUsable(slot)) ItemHooks.UpdateInfoAccessory(player.GetEffectiveArmor(slot), player, overriding);
            }
            if (!voidBag) return;
            var bank = player.bank4;
            if (bank.IsNull) return;
            var bag = bank.item;
            if (bag.IsNull) return;
            var works = ItemID.Sets.WorksInVoidBag;
            for (int i = 0; i < bag.Length; i++)
            {
                Item item = bag[i];
                if (item.IsNull) continue;
                int type = item.type;
                if (type > ItemID.Count || (!works.IsNull && (uint)type < (uint)works.Length && works[type])) ItemHooks.UpdateInfoAccessory(item, player, overriding);
            }
        }
        catch (Exception e) { ItemHooks.Report("UpdateInfoAccessory", e); }
    }
}
