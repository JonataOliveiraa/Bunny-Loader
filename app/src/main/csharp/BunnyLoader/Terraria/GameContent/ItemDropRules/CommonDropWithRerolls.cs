using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using N = BunnyLoader.Interop.DropRules;

namespace Terraria.GameContent.ItemDropRules;

public class CommonDropWithRerolls : CommonDrop
{
    internal CommonDropWithRerolls(IntPtr pointer) : base(pointer, typeof(CommonDropWithRerolls)) { }
    public CommonDropWithRerolls(int itemId, int chanceDenominator, int amountDroppedMinimum, int amountDroppedMaximum, int rerolls) : base(N.CommonDropWithRerolls.New(itemId, chanceDenominator, amountDroppedMinimum, amountDroppedMaximum, rerolls).Pointer, typeof(CommonDropWithRerolls)) { }
    public ref int timesToRoll => ref new N.CommonDropWithRerolls(Prototype).timesToRoll;
}
