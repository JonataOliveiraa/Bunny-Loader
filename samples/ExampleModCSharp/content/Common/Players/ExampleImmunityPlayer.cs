using Terraria;
using Terraria.ModLoader;

namespace ExampleMod.Common.Players;

public class ExampleImmunityPlayer : ModPlayer
{
    public bool HasExampleImmunityAcc;

    public override void ResetEffects()
    {
        HasExampleImmunityAcc = false;
    }

    public override void PostHurt(Player.HurtInfo info)
    {
        if (!HasExampleImmunityAcc)
        {
            return;
        }

        if (!info.PvP)
        {
            Player.AddImmuneTime(info.CooldownCounter, 60);
        }
    }
}
