using System.Collections.Generic;
using BunnyLoader.Interop;
using CsInstances.Content.Items;
using log4net;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsInstances;

public static class Checks
{
    static int pass, fail;

    static ILog Log => ModContent.GetInstance<CsInstancesMod>().Logger;

    public static void Run()
    {
        int counter = ModContent.ItemType<CounterItem>();
        Item a = Item.New();
        a.SetDefaults(counter, default);
        ModItem? first = a.ModItem;
        Report("SetDefaults attaches an instance", first is CounterItem && first.Item == a && first.Type == counter,
            $"{first?.GetType().Name} type={first?.Type}");
        Report("the same instance on every read", ReferenceEquals(first, a.ModItem), "");
        Report("SetDefaults ran on that instance", first is CounterItem { RanSetDefaults: true }, "");

        ((CounterItem)first!).Count = 5;
        Report("its data lasts", a.ModItem is CounterItem { Count: 5 }, "");

        Item other = Item.New();
        other.SetDefaults(counter, default);
        Report("each item has its own", !ReferenceEquals(other.ModItem, a.ModItem) && other.ModItem is CounterItem { Count: 0 }, "");

        Item copy = a.Clone();
        Report("Item.Clone clones the instance",
            copy.ModItem is CounterItem { Count: 5 } cloned && !ReferenceEquals(cloned, a.ModItem) && cloned.Item == copy, "");
        ((CounterItem)copy.ModItem!).Count = 9;
        Report("the clone is independent", a.ModItem is CounterItem { Count: 5 }, "");

        int list = ModContent.ItemType<ListItem>();
        Item b = Item.New();
        b.SetDefaults(list, default);
        ((ListItem)b.ModItem!).Values.Add(1);
        Item bCopy = b.Clone();
        ((ListItem)bCopy.ModItem!).Values.Add(2);
        Report("a Clone override copies deep", ((ListItem)b.ModItem!).Values.Count == 1 && ((ListItem)bCopy.ModItem!).Values.Count == 2,
            $"{((ListItem)b.ModItem!).Values.Count} / {((ListItem)bCopy.ModItem!).Values.Count}");

        a.SetDefaults(ItemID.DirtBlock, default);
        Report("a vanilla SetDefaults drops it", a.ModItem == null, "");

        Item vanilla = Item.New();
        vanilla.SetDefaults(ItemID.Minishark, default);
        Report("a vanilla item has none", vanilla.ModItem == null, "");

        Item bare = Item.New();
        bare.type = counter;
        Report("an item of the type without SetDefaults gets a new one", bare.ModItem is CounterItem { Count: 0, RanSetDefaults: false }, "");

        CounterItem template = ModContent.GetInstance<CounterItem>();
        Report("the template is untouched by the items' instances", template.Count == 0 && !template.Item.IsNull, $"count={template.Count}");

        var many = new List<Item>();
        var kept = new List<GameHandle>();
        for (int i = 0; i < 2000; i++)
        {
            Item item = Item.New();
            kept.Add(new GameHandle(item.Pointer));
            item.SetDefaults(counter, default);
            many.Add(item);
        }
        Report("many items", many.TrueForAll(i => i.ModItem is CounterItem), "");
        foreach (GameHandle handle in kept) handle.Dispose();
        Log.Info($"csinstances END pass={pass} fail={fail}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csinstances {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
