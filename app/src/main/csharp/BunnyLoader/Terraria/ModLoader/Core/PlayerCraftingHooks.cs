using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using Terraria.GameContent;

namespace Terraria.ModLoader.Core;

internal static class PlayerCraftingHooks
{
    const int ChunkSize = 200;

    sealed class Source
    {
        public IntPtr Player;
        public Item[] Items = Array.Empty<Item>();
        public int[] Indexes = Array.Empty<int>();
        public ModPlayer.ItemConsumedCallback? Callback;
        public string Label = "";
        public GameHandle[] Pins = Array.Empty<GameHandle>();
    }

    static readonly Dictionary<IntPtr, Source> Sources = new();

    internal static void Install(Func<string, bool> has)
    {
        if (!has(nameof(ModPlayer.AddMaterialsForCrafting))) return;
        ItemHooks.Once("PlayerCraftingMaterials", () =>
        {
            On_Recipe.BuildPlayerChestSourceList += BuildPlayerChestSourceList;
            On_CraftingRequests.ConsumeItemsFrom_ItemArray_int_RequiredItemEntry_refint_List_int += ConsumeItemsFrom;
        });
    }

    static void Forget(IntPtr player)
    {
        var stale = new List<IntPtr>();
        foreach ((IntPtr key, Source source) in Sources)
        {
            if (source.Player != player) continue;
            stale.Add(key);
            foreach (GameHandle pin in source.Pins) pin.Dispose();
        }
        foreach (IntPtr key in stale) Sources.Remove(key);
    }

    static void BuildPlayerChestSourceList(On_Recipe.orig_BuildPlayerChestSourceList orig, Player player, GameObject outputList)
    {
        orig(player, outputList);
        lock (Sources)
        {
            Forget(player.Pointer);
            var list = new GameList(outputList);
            if (list.IsNull) return;
            var seen = new HashSet<IntPtr>();
            foreach (ModPlayer m in PlayerLoader.Hooked(nameof(ModPlayer.AddMaterialsForCrafting)))
            {
                ModPlayer instance = PlayerLoader.Of(player)[m.Index];
                IEnumerable<Item>? materials;
                ModPlayer.ItemConsumedCallback? callback;
                try { materials = instance.AddMaterialsForCrafting(out callback); }
                catch (Exception e)
                {
                    ItemHooks.Report($"{instance.FullName}.AddMaterialsForCrafting", e);
                    continue;
                }
                if (materials == null) continue;
                var items = new List<Item>();
                var indexes = new List<int>();
                int index = 0;
                foreach (Item item in materials)
                {
                    if (!item.IsNull && item.type > 0 && item.stack > 0 && seen.Add(item.Pointer))
                    {
                        items.Add(item);
                        indexes.Add(index);
                    }
                    index++;
                }
                for (int start = 0; start < items.Count; start += ChunkSize)
                {
                    int count = Math.Min(ChunkSize, items.Count - start);
                    var array = GameArray<Item>.New(Item.Class, count);
                    for (int i = 0; i < count; i++) array[i] = items[start + i];
                    InventoryStorage storage = InventoryStorage.New();
                    storage.maxItems = count;
                    storage.item = array;
                    DestinationInventory destination = DestinationInventory.New(storage, player.Center);
                    list.Add(destination);
                    Sources[array.Pointer] = new Source
                    {
                        Player = player.Pointer,
                        Items = items.GetRange(start, count).ToArray(),
                        Indexes = indexes.GetRange(start, count).ToArray(),
                        Callback = callback,
                        Label = instance.FullName,
                        Pins = new[] { new GameHandle(array.Pointer), new GameHandle(storage.Pointer) },
                    };
                }
            }
        }
    }

    static void ConsumeItemsFrom(On_CraftingRequests.orig_ConsumeItemsFrom_ItemArray_int_RequiredItemEntry_refint_List_int orig,
        GameArray<Item> inventory, int maxItems, Recipe.RequiredItemEntry req, ref int toConsume, GameObject consumedItems, int chestIndex)
    {
        Source? source;
        lock (Sources) Sources.TryGetValue(inventory.Pointer, out source);
        if (source == null)
        {
            orig(inventory, maxItems, req, ref toConsume, consumedItems, chestIndex);
            return;
        }
        var before = new int[source.Items.Length];
        for (int i = 0; i < before.Length; i++) before[i] = source.Items[i].stack;
        var consumed = new GameList(consumedItems);
        int consumedStart = consumed.IsNull ? 0 : consumed.Count;
        orig(inventory, maxItems, req, ref toConsume, consumedItems, chestIndex);
        for (int i = 0; i < source.Items.Length; i++)
        {
            Item item = source.Items[i];
            if (inventory[i] != item)
            {
                if (!consumed.IsNull)
                {
                    for (int c = consumedStart; c < consumed.Count; c++)
                    {
                        if (consumed[c] == item.Pointer) consumed[c] = item.Clone().Pointer;
                    }
                }
                item.TurnToAir();
                inventory[i] = item;
            }
            if (item.stack >= before[i] || source.Callback == null) continue;
            try { source.Callback(item, source.Indexes[i]); }
            catch (Exception e) { ItemHooks.Report($"{source.Label}.ItemConsumedCallback", e); }
        }
    }
}
