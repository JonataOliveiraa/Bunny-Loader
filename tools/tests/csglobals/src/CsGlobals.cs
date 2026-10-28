using System;
using System.Linq;
using CsGlobals.Common.GlobalItems;
using CsGlobals.Content.Items;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsGlobals;

public sealed class CsGlobalsMod : Mod
{
    int pass, fail;

    public override void Load()
    {
        try
        {
            AddContent<FieldsWithoutInstanceGlobal>();
            Report("instance fields without InstancePerEntity are refused", false, "no exception");
        }
        catch (InvalidOperationException e)
        {
            Report("instance fields without InstancePerEntity are refused", true, e.Message);
        }
    }

    public override void PostSetupContent()
    {
        Report("SetStaticDefaults ran before PostSetupContent", CountingGlobal.StaticDefaults == 1, $"{CountingGlobal.StaticDefaults}");
        Report("each global has its own StaticIndex",
            ModContent.GetInstance<CountingGlobal>().StaticIndex != ModContent.GetInstance<WeaponGlobal>().StaticIndex, "");

        Item shark = Item.New();
        shark.SetDefaults(ItemID.Minishark, default);
        bool hasWeapon = shark.TryGetGlobalItem(out WeaponGlobal weapon);
        Report("late AppliesToEntity sees the defaults", hasWeapon && weapon.SawDamage > 0, $"{(hasWeapon ? weapon.SawDamage : -1)}");
        Report("an instance per entity", hasWeapon && !ReferenceEquals(weapon, ModContent.GetInstance<WeaponGlobal>()), "");
        Report("the same instance on every read", hasWeapon && ReferenceEquals(weapon, shark.GetGlobalItem<WeaponGlobal>()), "");
        weapon.Hits = 3;
        Report("its data lasts", shark.GetGlobalItem<WeaponGlobal>().Hits == 3, "");

        Item other = Item.New();
        other.SetDefaults(ItemID.Minishark, default);
        Report("each item has its own", !ReferenceEquals(other.GetGlobalItem<WeaponGlobal>(), weapon), "");

        Item copy = shark.Clone();
        WeaponGlobal copied = copy.GetGlobalItem<WeaponGlobal>();
        Report("Item.Clone clones it", !ReferenceEquals(copied, weapon) && copied.Hits == 3, $"{copied.Hits}");
        copied.Hits = 7;
        Report("the clone is independent", weapon.Hits == 3, "");

        Report("a global for every item is the template",
            ReferenceEquals(shark.GetGlobalItem<CountingGlobal>(), ModContent.GetInstance<CountingGlobal>()), "");

        Item dirt = Item.New();
        dirt.SetDefaults(ItemID.DirtBlock, default);
        Report("AppliesToEntity false leaves it out", !dirt.TryGetGlobalItem(out WeaponGlobal _), "");
        try
        {
            dirt.GetGlobalItem<WeaponGlobal>();
            Report("GetGlobalItem of a missing global throws", false, "no exception");
        }
        catch (System.Collections.Generic.KeyNotFoundException)
        {
            Report("GetGlobalItem of a missing global throws", true, "");
        }

        shark.SetDefaults(ItemID.DirtBlock, default);
        Report("SetDefaults again drops what no longer applies", !shark.TryGetGlobalItem(out WeaponGlobal _), "");

        int gemType = ModContent.ItemType<GlobalGem>();
        GlobalGem.Order.Clear();
        Item gem = Item.New();
        gem.SetDefaults(gemType, default);
        Report("early AppliesToEntity for a mod item", gem.TryGetGlobalItem(out GemGlobal _) && !gem.TryGetGlobalItem(out WeaponGlobal _), "");
        Report("ModItem.SetDefaults before GlobalItem.SetDefaults",
            GlobalGem.Order.SequenceEqual(new[] { "GlobalGem.SetDefaults", "GemGlobal.SetDefaults" }), string.Join(",", GlobalGem.Order));
        Report("the early global is there during ModItem.SetDefaults", GlobalGem.SawGemGlobal, "");
        Report("EntityGlobals", gem.EntityGlobals.Any(g => g is GemGlobal) && gem.EntityGlobals.Any(g => g is CountingGlobal), "");
        Report("Find a global", ModContent.Find<GlobalItem>("CsGlobals/WeaponGlobal") is WeaponGlobal, "");
        Report("SetDefaults runs for vanilla items", CountingGlobal.SetDefaultsCalls > 0, $"{CountingGlobal.SetDefaultsCalls}");
        Logger.Info($"csglobals END pass={pass} fail={fail}");
    }

    void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Logger.Info($"csglobals {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
