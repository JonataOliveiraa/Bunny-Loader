using System;
using Microsoft.Xna.Framework;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Terraria.ModLoader.Core;

internal static class ItemHeldDrawHooks
{
    internal static void Install(Func<string, bool> has)
    {
        if (has(nameof(ModItem.HoldoutOrigin)) || has(nameof(ModItem.ModifyItemDraw)) || has(nameof(GlobalItem.PreModifyItemDraw)) || has(nameof(GlobalItem.PostModifyItemDraw)))
            ItemHooks.Once("HeldItemDraw", () => On_PlayerDrawLayers.DrawPlayer_27_HeldItem += HeldItem);
    }

    static void HeldItem(On_PlayerDrawLayers.orig_DrawPlayer_27_HeldItem orig, PlayerDrawSet drawInfo)
    {
        int before = drawInfo.DrawDataCacheCount;
        orig(drawInfo);
        int added = drawInfo.DrawDataCacheCount - before;
        if (added <= 0)
            return;
        Item item = drawInfo.heldItem;
        if (item.IsNull)
            return;
        try
        {
            DrawDataList cache = drawInfo.DrawDataCache;
            if (item.useStyle == 5 && Item.staff[item.type])
            {
                Vector2 origin = default;
                ItemLoader.HoldoutOrigin(item, drawInfo.drawPlayer, ref origin);
                if (origin.X != 0 || origin.Y != 0)
                    for (int k = before; k < before + added; k++)
                    {
                        ref DrawData entry = ref cache[k];
                        entry.origin += origin;
                        entry.position.X += origin.X;
                    }
            }

            DrawData normal = cache[before];
            DrawData? colored = null, glow = null;
            int next = before + 1;
            if (next < before + added && item.color != default)
                colored = cache[next++];
            if (next < before + added)
                glow = cache[next++];
            int rest = before + added - next;
            var extras = new DrawData[rest];
            for (int k = 0; k < rest; k++)
                extras[k] = cache[next + k];
            drawInfo.DrawDataCacheCount = before;
            ItemLoader.ModifyItemDraw(item, drawInfo, normal, colored, glow);
            foreach (DrawData extra in extras)
                cache.Add(extra);
        }
        catch (Exception e)
        {
            ItemHooks.Report("ModifyItemDraw", e);
        }
    }
}
