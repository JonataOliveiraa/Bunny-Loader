using System;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

internal static class ItemResearchHooks
{
    public static void Install(Func<string, bool> has)
    {
        if (!has(nameof(ModItem.ModifyResearchSorting))) return;
        ItemHooks.Once("ResearchSorting", () => On_ContentSamples_CreativeHelper.GetItemGroup += GetItemGroup);
    }

    static ContentSamples.CreativeHelper.ItemGroup GetItemGroup(On_ContentSamples_CreativeHelper.orig_GetItemGroup orig, Item item, out int orderInGroup)
    {
        ContentSamples.CreativeHelper.ItemGroup group = orig(item, out orderInGroup);
        if (item.IsNull || item.type <= 0) return group;
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { m.ModifyResearchSorting(ref group); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.ModifyResearchSorting", e); }
        }
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try { g.ModifyResearchSorting(item, ref group); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.ModifyResearchSorting", e); }
        }
        return group;
    }
}
