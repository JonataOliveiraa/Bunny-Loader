using System;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace Terraria;

public readonly partial struct Player
{
    public ref ExtraJumpState GetJumpState<T>(T baseInstance) where T : ExtraJump => ref ExtraJumpLoader.StateOf(this, baseInstance);

    public ref ExtraJumpState GetJumpState<T>() where T : ExtraJump => ref GetJumpState(ModContent.GetInstance<T>());

    public Span<ExtraJumpState> ExtraJumps => ExtraJumpLoader.StatesOf(this);

    public ref bool blockExtraJumps => ref ExtraJumpLoader.Of(this).Block;

    public bool AnyExtraJumpUsable() => ExtraJumpLoader.AnyUsable(this);

    public void StopExtraJumpInProgress()
    {
        ExtraJumpLoader.StopActiveJump(this, out bool anyJumpCancelled);
        if (anyJumpCancelled) jump = 0;
    }

    public void ConsumeAllExtraJumps() => ExtraJumpLoader.ConsumeAllJumps(this);
}
