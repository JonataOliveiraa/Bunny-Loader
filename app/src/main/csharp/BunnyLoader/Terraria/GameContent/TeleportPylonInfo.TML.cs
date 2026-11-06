using Terraria.ModLoader;

namespace Terraria.GameContent;

public unsafe partial struct TeleportPylonInfo
{
    public ModPylon? ModPylon => PylonLoader.GetModPylon(TypeOfPylon);
}
