using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using N = BunnyLoader.Interop.DropRules;

namespace Terraria.GameContent.ItemDropRules;

public class CommonDropNotScalingWithLuck : CommonDrop
{
    internal CommonDropNotScalingWithLuck(IntPtr pointer) : base(pointer, typeof(CommonDropNotScalingWithLuck)) { }
    public CommonDropNotScalingWithLuck(int itemId, int chanceDenominator, int amountDroppedMinimum, int amountDroppedMaximum) : base(N.CommonDropNotScalingWithLuck.New(itemId, chanceDenominator, amountDroppedMinimum, amountDroppedMaximum).Pointer, typeof(CommonDropNotScalingWithLuck)) { }
}
