using Terraria.ModLoader;

namespace ExampleMod.Common.Players;

public class ExampleExtraJumpModificationPlayer : ModPlayer
{
    public override void ModifyExtraJumpDurationMultiplier(ExtraJump jump, ref float duration)
    {
        if (jump == ExtraJump.SandstormInABottle)
            duration *= 2f;
    }
}
