using Terraria.GameContent;
using Terraria.Map;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static class PylonLoader
{
    public static readonly TeleportPylonType VanillaPylonCount = (TeleportPylonType)PylonHooks.FirstPylonType;

    public static ModPylon? GetModPylon(TeleportPylonType pylonType) => PylonHooks.OfPylonType((int)pylonType);

    public static bool? PreCanPlacePylon(int x, int y, int tileType, TeleportPylonType pylonType)
    {
        bool? result = null;
        foreach (GlobalPylon global in PylonHooks.Globals)
        {
            bool? answer = global.PreCanPlacePylon(x, y, tileType, pylonType);
            if (answer == false)
                return false;
            if (answer == true)
                result = true;
        }

        return result;
    }

    public static bool? ValidTeleportCheck_PreNPCCount(TeleportPylonInfo pylonInfo, ref int defaultNecessaryNPCCount)
    {
        bool? result = null;
        foreach (GlobalPylon global in PylonHooks.Globals)
        {
            bool? answer = global.ValidTeleportCheck_PreNPCCount(pylonInfo, ref defaultNecessaryNPCCount);
            if (answer == false)
                return false;
            if (answer == true)
                result = true;
        }

        return result;
    }

    public static bool? ValidTeleportCheck_PreBiomeRequirements(TeleportPylonInfo pylonInfo, SceneMetrics sceneData)
    {
        bool? result = null;
        foreach (GlobalPylon global in PylonHooks.Globals)
        {
            bool? answer = global.ValidTeleportCheck_PreBiomeRequirements(pylonInfo, sceneData);
            if (answer == false)
                return false;
            if (answer == true)
                result = true;
        }

        return result;
    }

    public static void PostValidTeleportCheck(TeleportPylonInfo destinationPylonInfo, TeleportPylonInfo nearbyPylonInfo, ref bool destinationPylonValid, ref bool validNearbyPylonFound, ref string errorKey)
    {
        foreach (GlobalPylon global in PylonHooks.Globals)
            global.PostValidTeleportCheck(destinationPylonInfo, nearbyPylonInfo, ref destinationPylonValid, ref validNearbyPylonFound, ref errorKey);
    }
}
