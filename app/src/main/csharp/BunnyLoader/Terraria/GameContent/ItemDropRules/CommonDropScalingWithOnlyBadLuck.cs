using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using N = BunnyLoader.Interop.DropRules;

namespace Terraria.GameContent.ItemDropRules;

public class CommonDropScalingWithOnlyBadLuck : CommonDrop
{
    internal CommonDropScalingWithOnlyBadLuck(IntPtr pointer) : base(pointer, typeof(CommonDropScalingWithOnlyBadLuck)) { }
    public CommonDropScalingWithOnlyBadLuck(int itemId, int chanceDenominator, int amountDroppedMinimum = 1, int amountDroppedMaximum = 1, int chanceNumerator = 1) : base(N.CommonDropScalingWithOnlyBadLuck.New(itemId, chanceDenominator, amountDroppedMinimum, amountDroppedMaximum, chanceNumerator).Pointer, typeof(CommonDropScalingWithOnlyBadLuck)) { }
    public CommonDropScalingWithOnlyBadLuck(int itemId, int chanceDenominator, int amountDroppedMinimum, int amountDroppedMaximum) : base(N.CommonDropScalingWithOnlyBadLuck.New(itemId, chanceDenominator, amountDroppedMinimum, amountDroppedMaximum).Pointer, typeof(CommonDropScalingWithOnlyBadLuck)) { }
}
