using Terraria;
using Terraria.ModLoader;

namespace CsPvp;

public class PvpPlayer : ModPlayer
{
    public static int CanHitCalls;

    public override bool CanHitPvp(Item item, Player target)
    {
        CanHitCalls++;
        return true;
    }
}
