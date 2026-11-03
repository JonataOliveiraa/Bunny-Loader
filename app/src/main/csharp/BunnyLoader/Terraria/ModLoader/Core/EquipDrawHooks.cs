using System;
using Terraria.DataStructures;

namespace Terraria.ModLoader.Core;

internal static class EquipDrawHooks
{
    internal static void Install() =>
        ItemHooks.Once("EquipTextureDraw", () =>
        {
            On_PlayerDrawLayers.DrawPlayer_09_Wings += Wings;
            On_PlayerDrawLayers.DrawPlayer_10_BackAcc += BackAcc;
            On_PlayerDrawLayers.DrawPlayer_11_Balloons += Balloons;
            On_PlayerDrawLayers.DrawPlayer_14_Shoes += Shoes;
            On_PlayerDrawLayers.DrawPlayer_19_WaistAcc += WaistAcc;
            On_PlayerDrawLayers.DrawPlayer_20_NeckAcc += NeckAcc;
            On_PlayerDrawLayers.DrawPlayer_25_Shield += Shield;
        });

    static void Wings(On_PlayerDrawLayers.orig_DrawPlayer_09_Wings orig, PlayerDrawSet info)
    {
        int start = info.DrawDataCacheCount;
        orig(info);
        Modify(info, start, EquipType.Wings, info.drawPlayer.wings, "DrawPlayer_09_Wings");
    }

    static void BackAcc(On_PlayerDrawLayers.orig_DrawPlayer_10_BackAcc orig, PlayerDrawSet info)
    {
        int start = info.DrawDataCacheCount;
        orig(info);
        Modify(info, start, EquipType.Back, info.drawPlayer.back, "DrawPlayer_10_BackAcc");
    }

    static void Balloons(On_PlayerDrawLayers.orig_DrawPlayer_11_Balloons orig, PlayerDrawSet info)
    {
        int start = info.DrawDataCacheCount;
        orig(info);
        Modify(info, start, EquipType.Balloon, info.drawPlayer.balloon, "DrawPlayer_11_Balloons");
    }

    static void Shoes(On_PlayerDrawLayers.orig_DrawPlayer_14_Shoes orig, PlayerDrawSet info)
    {
        int start = info.DrawDataCacheCount;
        orig(info);
        Modify(info, start, EquipType.Shoes, info.drawPlayer.shoe, "DrawPlayer_14_Shoes");
    }

    static void WaistAcc(On_PlayerDrawLayers.orig_DrawPlayer_19_WaistAcc orig, PlayerDrawSet info)
    {
        int start = info.DrawDataCacheCount;
        orig(info);
        Modify(info, start, EquipType.Waist, info.drawPlayer.waist, "DrawPlayer_19_WaistAcc");
    }

    static void NeckAcc(On_PlayerDrawLayers.orig_DrawPlayer_20_NeckAcc orig, PlayerDrawSet info)
    {
        int start = info.DrawDataCacheCount;
        orig(info);
        Modify(info, start, EquipType.Neck, info.drawPlayer.neck, "DrawPlayer_20_NeckAcc");
    }

    static void Shield(On_PlayerDrawLayers.orig_DrawPlayer_25_Shield orig, PlayerDrawSet info)
    {
        int start = info.DrawDataCacheCount;
        orig(info);
        Modify(info, start, EquipType.Shield, info.drawPlayer.shield, "DrawPlayer_25_Shield");
    }

    static void Modify(PlayerDrawSet info, int start, EquipType type, int slot, string method)
    {
        try
        {
            if (slot <= 0 || EquipLoader.GetEquipTexture(type, slot) is not EquipTexture texture) return;
            DrawDataList cache = info.DrawDataCache;
            int count = cache.Count;
            if (count <= start) return;
            var added = new DrawData[count - start];
            for (int i = 0; i < added.Length; i++) added[i] = cache[start + i];
            info.DrawDataCacheCount = start;
            for (int i = 0; i < added.Length; i++)
            {
                if (texture.ModifyDraw(ref info, ref added[i], method)) cache.Add(added[i]);
            }
        }
        catch (Exception e) { ItemHooks.Report("ModifyEquipTextureDraw", e); }
    }
}
