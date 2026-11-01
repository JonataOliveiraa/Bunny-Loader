using System;
using Terraria.ID;
using Terraria.ObjectData;

namespace Terraria.DataStructures;

public partial struct PlacementHook
{
    public PlacementHook(Func<int, int, int, int, int, int, int> hook, int badReturn, int badResponse, bool processedCoordinates)
    {
        this = default;
        this.hook = GameHook(hook);
        this.badReturn = badReturn;
        this.badResponse = badResponse;
        this.processedCoordinates = processedCoordinates;
    }

    static HookFormat GameHook(Func<int, int, int, int, int, int, int> hook)
    {
        if (hook.Method.DeclaringType == typeof(Chest))
        {
            TileObjectData chest = TileObjectData.GetTileData(TileID.Containers, 0, 0);
            if (hook.Method.Name == nameof(Chest.FindEmptyChest)) return chest.HookCheckIfCanPlace.hook;
            if (hook.Method.Name == nameof(Chest.AfterPlacement_Hook)) return chest.HookPostPlaceMyPlayer.hook;
        }
        throw new NotSupportedException($"PlacementHook: only the game's Chest.FindEmptyChest and Chest.AfterPlacement_Hook can be used on the phone, not {hook.Method.DeclaringType?.Name}.{hook.Method.Name}");
    }
}
