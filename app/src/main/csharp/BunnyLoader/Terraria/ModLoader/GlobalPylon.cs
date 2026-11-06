using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.Map;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public abstract class GlobalPylon : ModType
{
    public virtual bool PreDrawMapIcon(ref MapOverlayDrawContext context, ref string mouseOverText, ref TeleportPylonInfo pylonInfo, ref bool isNearPylon, ref Color drawColor, ref float deselectedScale, ref float selectedScale) => true;

    public virtual bool? PreCanPlacePylon(int x, int y, int tileType, TeleportPylonType pylonType) => null;

    public virtual bool? ValidTeleportCheck_PreNPCCount(TeleportPylonInfo pylonInfo, ref int defaultNecessaryNPCCount) => null;

    public virtual bool? ValidTeleportCheck_PreBiomeRequirements(TeleportPylonInfo pylonInfo, SceneMetrics sceneData) => null;

    public virtual void PostValidTeleportCheck(TeleportPylonInfo destinationPylonInfo, TeleportPylonInfo nearbyPylonInfo, ref bool destinationPylonValid, ref bool validNearbyPylonFound, ref string errorKey)
    {
    }

    protected sealed override void Register() => PylonHooks.Add(this);

    public sealed override void SetupContent() => SetStaticDefaults();
}
