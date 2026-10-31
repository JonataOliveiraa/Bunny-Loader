using System;
using BunnyLoader;
using BunnyLoader.Interop;

namespace Terraria.ModLoader.Core;

internal static class TownShopHooks
{
    internal static void Install() => ItemHooks.Once("TownShops", () => On_InventoryStorage.SetupShop += SetupShop);
    internal static void InstallTravel() => ItemHooks.Once("TownTravelShop", () => On_InventoryStorage.SetupTravelShop += Travel);

    internal static void ContentReady()
    {
        NPCShopDatabase.Initialize();
    }

    static void Travel(On_InventoryStorage.orig_SetupTravelShop orig)
    {
        orig();
        var native = Main.travelShop;
        int[] items = new int[native.Length];
        int slot = 0;
        for (int i = 0; i < items.Length; i++) items[i] = native[i];
        while (slot < items.Length && items[slot] != 0) slot++;
        NPCLoader.SetupTravelShop(items, ref slot);
        for (int i = 0; i < items.Length; i++) native[i] = items[i];
    }

    static void SetupShop(On_InventoryStorage.orig_SetupShop orig, InventoryStorage storage, int type)
    {
        orig(storage, type);
        if (!NPCShopDatabase.ByIndex.TryGetValue(type, out NPCShop? shop)) return;
        try { Fill(storage, shop); }
        catch (Exception e) { ItemHooks.Report($"NPC shop {shop.FullName}", e); }
    }

    static void Fill(InventoryStorage storage, NPCShop shop)
    {
        var native = storage.item;
        NPC npc = TownNPCHooks.TalkingTo();
        if (npc.IsNull || npc.type != shop.NpcType) npc = default;
        if (shop.Vanilla && !shop.Modified)
        {
            shop.Modified = true;
            for (int i = 0; i < native.Length; i++)
            {
                Item item = native[i];
                if (!item.IsNull && item.type > 0) shop.Add(new NPCShop.Entry(item.Clone()) { Native = true });
            }
            NPCLoader.ModifyShop(shop);
            shop.FinishSetup();
        }
        Item[] items = new Item[native.Length];
        int slot = 0, limit = Math.Max(0, items.Length - (shop.FillLastSlot ? 0 : 1));
        if (shop.Vanilla)
        {
            for (int i = 0; i < native.Length && slot < limit; i++)
            {
                Item item = native[i];
                if (item.IsNull || item.type <= 0) continue;
                NPCShop.Entry? entry = null;
                foreach (NPCShop.Entry candidate in shop.Entries) if (candidate.Native && candidate.Item.type == item.type) { entry = candidate; break; }
                if (entry != null)
                {
                    if (entry.Disabled || !entry.ConditionsMet()) continue;
                    item = entry.Item.Clone();
                    native[slot] = item;
                    entry.OnShopOpen(item, npc);
                }
                native[slot] = item;
                items[slot++] = item;
            }
        }
        foreach (NPCShop.Entry entry in shop.Entries)
        {
            if (entry.Native || entry.Disabled) continue;
            bool available = entry.ConditionsMet();
            if (!available && !entry.SlotReserved) continue;
            if (slot >= limit) { Log.Warn($"NPC shop {shop.FullName}: item {entry.Item.type} exceeds the mobile inventory capacity"); break; }
            Item item = available ? entry.Item.Clone() : NPCShop.Entry.Create(0);
            native[slot] = item;
            if (available) entry.OnShopOpen(item, npc);
            items[slot++] = item;
        }
        if (!npc.IsNull) NPCLoader.ModifyActiveShop(npc, shop.FullName, items);
        for (int i = 0; i < items.Length; i++)
        {
            Item item = items[i].IsNull ? NPCShop.Entry.Create(0) : items[i];
            item.isAShopItem = true;
            native[i] = item;
        }
    }
}
