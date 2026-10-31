using System.Collections.Generic;

namespace Terraria.ModLoader;

[Autoload(false)]
public abstract class VanillaExtraJump : ExtraJump
{
    public sealed override Position GetDefaultPosition() => null!;

    public sealed override IEnumerable<Position> GetModdedConstraints() => null!;

    internal virtual bool Native => true;

    internal abstract ref bool HasOption(Player player);

    internal abstract ref bool CanJumpAgain(Player player);

    internal abstract ref bool IsPerforming(Player player);
}

public sealed class FlipperJump : VanillaExtraJump
{
    static bool unused;

    public override float GetDurationMultiplier(Player player) => 1f;

    public override bool CanStart(Player player) => (!player.mount.Active || !player.mount.Cart) && player.wet;

    internal override bool Native => false;

    internal override ref bool HasOption(Player player) => ref unused;

    internal override ref bool CanJumpAgain(Player player) => ref unused;

    internal override ref bool IsPerforming(Player player) => ref unused;
}

public sealed class BasiliskMountJump : VanillaExtraJump
{
    public override float GetDurationMultiplier(Player player) => 0.75f;

    internal override ref bool HasOption(Player player) => ref player.hasJumpOption_Basilisk;

    internal override ref bool CanJumpAgain(Player player) => ref player.canJumpAgain_Basilisk;

    internal override ref bool IsPerforming(Player player) => ref player.isPerformingJump_Basilisk;
}

public sealed class GoatMountJump : VanillaExtraJump
{
    public override float GetDurationMultiplier(Player player) => 2f;

    internal override ref bool HasOption(Player player) => ref player.hasJumpOption_WallOfFleshGoat;

    internal override ref bool CanJumpAgain(Player player) => ref player.canJumpAgain_WallOfFleshGoat;

    internal override ref bool IsPerforming(Player player) => ref player.isPerformingJump_WallOfFleshGoat;
}

public sealed class SantankMountJump : VanillaExtraJump
{
    public override float GetDurationMultiplier(Player player) => 2f;

    internal override ref bool HasOption(Player player) => ref player.hasJumpOption_Santank;

    internal override ref bool CanJumpAgain(Player player) => ref player.canJumpAgain_Santank;

    internal override ref bool IsPerforming(Player player) => ref player.isPerformingJump_Santank;
}

public sealed class UnicornMountJump : VanillaExtraJump
{
    public override float GetDurationMultiplier(Player player) => 2f;

    internal override ref bool HasOption(Player player) => ref player.hasJumpOption_Unicorn;

    internal override ref bool CanJumpAgain(Player player) => ref player.canJumpAgain_Unicorn;

    internal override ref bool IsPerforming(Player player) => ref player.isPerformingJump_Unicorn;
}

public sealed class SandstormInABottleJump : VanillaExtraJump
{
    public override float GetDurationMultiplier(Player player) => 3f;

    internal override ref bool HasOption(Player player) => ref player.hasJumpOption_Sandstorm;

    internal override ref bool CanJumpAgain(Player player) => ref player.canJumpAgain_Sandstorm;

    internal override ref bool IsPerforming(Player player) => ref player.isPerformingJump_Sandstorm;
}

public sealed class BlizzardInABottleJump : VanillaExtraJump
{
    public override float GetDurationMultiplier(Player player) => 1.5f;

    internal override ref bool HasOption(Player player) => ref player.hasJumpOption_Blizzard;

    internal override ref bool CanJumpAgain(Player player) => ref player.canJumpAgain_Blizzard;

    internal override ref bool IsPerforming(Player player) => ref player.isPerformingJump_Blizzard;
}

public sealed class FartInAJarJump : VanillaExtraJump
{
    public override float GetDurationMultiplier(Player player) => 2f;

    internal override ref bool HasOption(Player player) => ref player.hasJumpOption_Fart;

    internal override ref bool CanJumpAgain(Player player) => ref player.canJumpAgain_Fart;

    internal override ref bool IsPerforming(Player player) => ref player.isPerformingJump_Fart;
}

public sealed class TsunamiInABottleJump : VanillaExtraJump
{
    public override float GetDurationMultiplier(Player player) => 1.25f;

    internal override ref bool HasOption(Player player) => ref player.hasJumpOption_Sail;

    internal override ref bool CanJumpAgain(Player player) => ref player.canJumpAgain_Sail;

    internal override ref bool IsPerforming(Player player) => ref player.isPerformingJump_Sail;
}

public sealed class CloudInABottleJump : VanillaExtraJump
{
    public override float GetDurationMultiplier(Player player) => 0.75f;

    internal override ref bool HasOption(Player player) => ref player.hasJumpOption_Cloud;

    internal override ref bool CanJumpAgain(Player player) => ref player.canJumpAgain_Cloud;

    internal override ref bool IsPerforming(Player player) => ref player.isPerformingJump_Cloud;
}
