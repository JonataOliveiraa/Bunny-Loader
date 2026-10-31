using Terraria;
using Terraria.ModLoader;

namespace ExampleMod.Common.Players;

public class ExampleLuckPlayer : ModPlayer
{
    public override void ModifyLuck(ref float luck)
    {
        if (Main.hardMode)
        {
            luck += 0.5f;
        }
    }

    public override bool PreModifyLuck(ref float luck)
    {
        Terraria.GameContent.Events.LanternNight.GenuineLanterns = true;
        Player.HasGardenGnomeNearby = true;

        if (Player.ladyBugLuckTimeLeft < 0)
        {
            Player.ladyBugLuckTimeLeft = 0;
        }

        return true;
    }
}
