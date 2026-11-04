using System;
using System.Collections.Generic;

namespace Terraria.ModLoader.Core;

internal static class PlayerItemUseState
{
    static readonly Dictionary<int, int> Counts = new();
    [ThreadStatic] static int applying;

    internal static int Uses(Player player)
    {
        ItemHooks.Once("ItemUseCounts", () =>
        {
            On_Player.ApplyItemAnimation += ApplyAnimation;
            On_Player.ApplyItemTime_Item += ApplyTime;
            On_Player.ApplyItemTime_Item_float += ApplyTime;
        });
        return Counts.TryGetValue(player.whoAmI, out int count) ? count : 0;
    }

    static void ApplyAnimation(On_Player.orig_ApplyItemAnimation orig, Player player, Item item)
    {
        orig(player, item);
        Counts[player.whoAmI] = 0;
    }

    static void ApplyTime(On_Player.orig_ApplyItemTime_Item orig, Player player, Item item)
    {
        bool outer = applying++ == 0;
        try { orig(player, item); }
        finally { applying--; }
        if (outer) Counts[player.whoAmI] = Uses(player) + 1;
    }

    static void ApplyTime(On_Player.orig_ApplyItemTime_Item_float orig, Player player, Item item, float multiplier)
    {
        bool outer = applying++ == 0;
        try { orig(player, item, multiplier); }
        finally { applying--; }
        if (outer) Counts[player.whoAmI] = Uses(player) + 1;
    }
}
