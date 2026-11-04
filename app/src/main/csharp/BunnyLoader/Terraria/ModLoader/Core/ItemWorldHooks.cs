using System;

namespace Terraria.ModLoader.Core;

internal static class ItemWorldHooks
{
    static bool globalUpdate;

    public static void Install(Func<string, bool> has, bool global)
    {
        ItemSpawnHooks.Install(has);
        if (!has(nameof(ModItem.PostUpdate))) return;
        if (global) globalUpdate = true;
        ItemHooks.Once("ItemPostUpdate", () => On_WorldItem.UpdateItem += UpdateItem);
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
