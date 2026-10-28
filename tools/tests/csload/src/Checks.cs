using System.Collections.Generic;
using CsLoad.Content.Items;
using log4net;
using Terraria;
using Terraria.ModLoader;

namespace CsLoad;

public static class Checks
{
    public static readonly List<string> Events = new();

    public static int Pass, Fail;

    public static ILog Log => ModContent.GetInstance<CsLoadMod>().Logger;

    public static void Note(string step) => Events.Add(step);

    public static void RunInstances()
    {
        var fresh = ModContent.GetInstance<OrderItem>();
        Item a = Item.New();
        a.SetDefaults(fresh.Type, default);
        Report("fresh instance", OrderItem.SeenMarker == 1 && OrderItem.SeenType == fresh.Type && OrderItem.SeenItemOk && OrderItem.SeenMod,
            $"marker={OrderItem.SeenMarker} type={OrderItem.SeenType}/{fresh.Type} item={OrderItem.SeenItemOk} mod={OrderItem.SeenMod}");

        var clone = ModContent.GetInstance<CloneItem>();
        Item b = Item.New();
        b.SetDefaults(clone.Type, default);
        Report("cloned instance", CloneItem.SeenMarker == 42 && CloneItem.SeenType == clone.Type && b.type == clone.Type,
            $"marker={CloneItem.SeenMarker} type={CloneItem.SeenType}/{clone.Type}");

        Report("template has no item", fresh.Item.IsNull && fresh.Marker == 42, $"marker={fresh.Marker}");
    }

    public static void Report(string name, bool ok, string detail)
    {
        if (ok) Pass++;
        else Fail++;
        Log.Info($"csload {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
