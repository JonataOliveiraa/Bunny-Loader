using System;
using Terraria.ID;
using Terraria.ModLoader.Core;
using Terraria.ObjectData;

namespace Terraria.DataStructures;

public partial struct PlacementHook
{
    public PlacementHook(Func<int, int, int, int, int, int, int> hook, int badReturn, int badResponse, bool processedCoordinates)
    {
        this = default;
        this.badResponse = badResponse;
        this.processedCoordinates = processedCoordinates;
        if (GameHook(hook) is HookFormat game)
        {
            this.hook = game;
            this.badReturn = badReturn;
        }
        else this.badReturn = PlacementHooks.Register(hook);
    }

    static HookFormat? GameHook(Func<int, int, int, int, int, int, int> hook)
    {
        if (hook.Method.DeclaringType != typeof(Chest)) return null;
        TileObjectData chest = TileObjectData.GetTileData(TileID.Containers, 0, 0);
        if (hook.Method.Name == nameof(Chest.FindEmptyChest)) return chest.HookCheckIfCanPlace.hook;
        if (hook.Method.Name == nameof(Chest.AfterPlacement_Hook)) return chest.HookPostPlaceMyPlayer.hook;
        throw new NotSupportedException($"PlacementHook: Chest.{hook.Method.Name} is not available on the phone");
    }
}
