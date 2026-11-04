using System;
using Terraria.GameContent;

namespace Terraria.ModLoader.Core;

internal static class ItemWorldHooks
{
    static bool globalUpdate, globalExtract;

    public static void Install(Func<string, bool> has, bool global)
    {
        if (has(nameof(ModItem.ExtractinatorUse)))
        {
            if (global) globalExtract = true;
            ItemHooks.Once("ItemExtractinator", () => On_Player.ExtractinatorUse += ExtractinatorUse);
        }
        if (!has(nameof(ModItem.PostUpdate))) return;
        if (global) globalUpdate = true;
        ItemHooks.Once("ItemPostUpdate", () => On_WorldItem.UpdateItem += UpdateItem);
    }

    static void ExtractinatorUse(On_Player.orig_ExtractinatorUse orig, Player self, int extractType, int extractinatorBlockType)
    {
        ExtractinatorHelper.RollExtractinatorDrop(extractType, extractinatorBlockType, out int resultType, out int resultStack);
        ID.ContentSamples.ItemsByType.TryGetValue(extractType, out Item sample);
        if (ItemLoader.GetItem(extractType) is ModItem m)
        {
            try { m.ExtractinatorUse(extractinatorBlockType, ref resultType, ref resultStack); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.ExtractinatorUse", e); }
        }
        if (globalExtract && !sample.IsNull)
        {
            foreach (GlobalItem? g in ItemHooks.GlobalsOf(sample))
            {
                if (g == null) continue;
                try { g.ExtractinatorUse(extractType, extractinatorBlockType, ref resultType, ref resultStack); }
                catch (Exception e) { ItemHooks.Report($"{g.FullName}.ExtractinatorUse", e); }
            }
        }
        if (resultType > 0) self.DropItemFromExtractinator(resultType, resultStack);
    }

    static void UpdateItem(On_WorldItem.orig_UpdateItem orig, WorldItem self, int i)
    {
        orig(self, i);
        if (!self.active) return;
        Item item = self.inner;
        if (item.IsNull) return;
        if (ItemHooks.ModItemOf(item) is ModItem m)
        {
            try { m.PostUpdate(self); }
            catch (Exception e) { ItemHooks.Report($"{m.FullName}.PostUpdate", e); }
        }
        if (!globalUpdate) return;
        foreach (GlobalItem? g in ItemHooks.GlobalsOf(item))
        {
            if (g == null) continue;
            try { g.PostUpdate(self); }
            catch (Exception e) { ItemHooks.Report($"{g.FullName}.PostUpdate", e); }
        }
    }
}
