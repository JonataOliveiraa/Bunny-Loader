using System;
using System.Collections.Generic;
using Terraria.DataStructures;
using Terraria.ObjectData;

namespace Terraria.ModLoader.Core;

internal static class PlacementHooks
{
    const int Marker = 0x5C000000;

    static readonly List<Func<int, int, int, int, int, int, int>> Hooks = new();

    internal static int Register(Func<int, int, int, int, int, int, int> hook)
    {
        int index;
        lock (Hooks)
        {
            index = Hooks.IndexOf(hook);
            if (index < 0)
            {
                index = Hooks.Count;
                Hooks.Add(hook);
            }
        }
        ItemHooks.Once("PlacementHooks", () => On_TileObjectData.CallPostPlacementPlayerHook += CallPostPlacementPlayerHook);
        return Marker + index;
    }

    static bool CallPostPlacementPlayerHook(On_TileObjectData.orig_CallPostPlacementPlayerHook orig, int tileX, int tileY, int type, int style, int dir, int alternate, TileObject data)
    {
        bool result = orig(tileX, tileY, type, style, dir, alternate, data);
        try
        {
            TileObjectData tileData = TileObjectData.GetTileData(type, style, data.alternate);
            if (tileData.IsNull) return result;
            PlacementHook hook = tileData.HookPostPlaceMyPlayer;
            int index = hook.badReturn - Marker;
            Func<int, int, int, int, int, int, int>? call;
            lock (Hooks) call = hook.hook.IsNull && (uint)index < (uint)Hooks.Count ? Hooks[index] : null;
            if (call == null) return result;
            if (hook.processedCoordinates)
            {
                Point16 origin = tileData.Origin;
                tileX -= origin.X;
                tileY -= origin.Y;
            }
            call(tileX, tileY, type, style, dir, data.alternate);
        }
        catch (Exception e) { ItemHooks.Report("PlacementHook (mod placement hook)", e); }
        return result;
    }
}
