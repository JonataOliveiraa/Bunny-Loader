using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CsJump;

public class TestJump : ExtraJump
{
    public static readonly Dictionary<string, int> Calls = new();

    static void Count(string name) => Calls[name] = Calls.GetValueOrDefault(name) + 1;

    public override Position GetDefaultPosition() => new After(BlizzardInABottle);

    public override float GetDurationMultiplier(Player player) => 2f;

    public override void OnStarted(Player player, ref bool playSound)
    {
        Count(nameof(OnStarted));
        playSound = false;
    }

    public override void OnEnded(Player player) => Count(nameof(OnEnded));

    public override void OnRefreshed(Player player) => Count(nameof(OnRefreshed));

    public override void ShowVisuals(Player player) => Count(nameof(ShowVisuals));

    public override void UpdateHorizontalSpeeds(Player player)
    {
        Count(nameof(UpdateHorizontalSpeeds));
        player.maxRunSpeed *= 2f;
    }
}

public class JumpPlayer : ModPlayer
{
    public static readonly List<string> Log = new();
    public static bool VetoCloud;
    public static float CloudFactor = 1f, TestFactor = 1f;

    static void Note(string hook, ExtraJump jump) => Log.Add($"{hook}:{jump.Name}");

    public override void ModifyExtraJumpDurationMultiplier(ExtraJump jump, ref float duration)
    {
        Note(nameof(ModifyExtraJumpDurationMultiplier), jump);
        if (jump == ExtraJump.CloudInABottle) duration *= CloudFactor;
        if (jump is TestJump) duration *= TestFactor;
    }

    public override bool CanStartExtraJump(ExtraJump jump)
    {
        Note(nameof(CanStartExtraJump), jump);
        return !(VetoCloud && jump == ExtraJump.CloudInABottle);
    }

    public override void OnExtraJumpStarted(ExtraJump jump, ref bool playSound) => Note(nameof(OnExtraJumpStarted), jump);

    public override void OnExtraJumpEnded(ExtraJump jump) => Note(nameof(OnExtraJumpEnded), jump);

    public override void OnExtraJumpRefreshed(ExtraJump jump) => Note(nameof(OnExtraJumpRefreshed), jump);

    public override void ExtraJumpVisuals(ExtraJump jump) => Note(nameof(ExtraJumpVisuals), jump);

    public override void OnExtraJumpCleared(ExtraJump jump) => Note(nameof(OnExtraJumpCleared), jump);
}
