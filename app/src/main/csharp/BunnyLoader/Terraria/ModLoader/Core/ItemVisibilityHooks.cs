using System;
using System.Collections.Generic;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

internal static class ItemVisibilityHooks
{
    sealed class InfoScope
    {
        internal Player Player;
        internal readonly Dictionary<int, Queue<Item>> Items = new();
        readonly Stack<Queue<Item>> spare = new();
        internal void Add(Item item)
        {
            if (item.IsNull || item.IsAir) return;
            if (!Items.TryGetValue(item.type, out var queue)) Items[item.type] = queue = spare.Count > 0 ? spare.Pop() : new();
            queue.Enqueue(item);
        }
        internal void Clear()
        {
            foreach (Queue<Item> queue in Items.Values)
            {
                queue.Clear();
                spare.Push(queue);
            }
            Items.Clear();
            Player = default;
        }
    }
    [ThreadStatic] static InfoScope? infoScope, spareScope;

    internal static void Install(Func<string, bool> has)
    {
        if (has(nameof(ModItem.UpdateVisibleAccessory))) ItemHooks.Once("ItemVisibleAccessory", () => On_Player.UpdateVisibleAccessory += Visible);
        if (has(nameof(ModItem.UpdateItemDye))) ItemHooks.Once("ItemDye", () => On_Player.UpdateItemDye += Dye);
        if (has(nameof(ModItem.UpdateInfoAccessory))) ItemHooks.Once("ItemInfoAccessory", () =>
        {
            On_Player.RefreshInfoAccs += RefreshInfo;
            On_Player.RefreshInfoAccsFromItemType += Info;
        });
    }

    static void Visible(On_Player.orig_UpdateVisibleAccessory orig, Player player, int itemSlot, Item item)
    {
        orig(player, itemSlot, item);
        if (item.IsNull || item.IsAir) return;
        bool hidden = (uint)itemSlot < (uint)player.hideVisibleAccessory.Length && player.hideVisibleAccessory[itemSlot];
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { m.UpdateVisibleAccessory(player, hidden); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.UpdateVisibleAccessory", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try { g.UpdateVisibleAccessory(item, player, hidden); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.UpdateVisibleAccessory", e); }
        }
    }

    static void Dye(On_Player.orig_UpdateItemDye orig, Player player, bool functional, bool hidden, Item item, Item dyeItem)
    {
        orig(player, functional, hidden, item, dyeItem);
        if (item.IsNull || item.IsAir) return;
        int dye = dyeItem.IsNull ? 0 : dyeItem.dye;
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { m.UpdateItemDye(player, dye, functional && hidden); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.UpdateItemDye", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try { g.UpdateItemDye(item, player, dye, functional && hidden); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.UpdateItemDye", e); }
        }
    }

    static void RefreshInfo(On_Player.orig_RefreshInfoAccs orig, Player player)
    {
        InfoScope? outer = infoScope;
        InfoScope scope = spareScope ?? new InfoScope();
        spareScope = null;
        scope.Player = player;
        bool voidBag = false;
        var inventory = player.inventory;
        for (int i = 0; i < 58 && i < inventory.Length; i++)
        {
            scope.Add(inventory[i]);
            if (!inventory[i].IsNull && inventory[i].type == 4131) voidBag = true;
        }
        for (int slot = 0; slot < 20; slot++) if (player.IsItemSlotUnlockedAndUsable(slot)) scope.Add(player.GetEffectiveArmor(slot));
        if (voidBag && !player.bank4.IsNull)
        {
            foreach (Item item in player.bank4.item)
            {
                if (item.IsNull || item.IsAir) continue;
                if (item.type > ItemID.Count || ((uint)item.type < (uint)ItemID.Sets.WorksInVoidBag.Length && ItemID.Sets.WorksInVoidBag[item.type])) scope.Add(item);
            }
        }
        infoScope = scope;
        try { orig(player); }
        finally
        {
            infoScope = outer;
            scope.Clear();
            spareScope = scope;
        }
    }

    static void Info(On_Player.orig_RefreshInfoAccsFromItemType orig, Player player, int type)
    {
        orig(player, type);
        Item item = default;
        if (infoScope is { } scope && scope.Player == player && scope.Items.TryGetValue(type, out var queue) && queue.Count > 0) item = queue.Dequeue();
        ModItem? m = item.IsNull ? ItemLoader.GetItem(type) : ItemHooks.ModItemOf(item);
        if (m != null)
        {
            try { m.UpdateInfoAccessory(player); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.UpdateInfoAccessory", e); }
        }
        if (item.IsNull) return;
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try { g.UpdateInfoAccessory(item, player); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.UpdateInfoAccessory", e); }
        }
    }
}
