using Terraria.ID;

namespace Terraria;

public static unsafe partial class Wiring
{
    public static void HitSwitchAndSync(int i, int j)
    {
        HitSwitch(i, j);
        NetMessage.SendData(MessageID.HitSwitch, -1, -1, default, i, j);
    }
}
