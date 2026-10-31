using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using N = BunnyLoader.Interop.DropRules;

namespace Terraria.GameContent.ItemDropRules;

public class CommonDrop : NativeDropRule
{
    internal CommonDrop(IntPtr pointer) : base(pointer, typeof(CommonDrop)) { }
    protected CommonDrop(IntPtr pointer, Type nativeType) : base(pointer, nativeType) { }
    public CommonDrop(int itemId, int chanceDenominator, int amountDroppedMinimum = 1, int amountDroppedMaximum = 1, int chanceNumerator = 1) : base(N.CommonDrop.New(itemId, chanceDenominator, amountDroppedMinimum, amountDroppedMaximum, chanceNumerator).Pointer, typeof(CommonDrop)) { }
    public ref int itemId => ref new N.CommonDrop(Prototype).itemId;
    public ref int chanceDenominator => ref new N.CommonDrop(Prototype).chanceDenominator;
    public ref int amountDroppedMinimum => ref new N.CommonDrop(Prototype).amountDroppedMinimum;
    public ref int amountDroppedMaximum => ref new N.CommonDrop(Prototype).amountDroppedMaximum;
    public ref int chanceNumerator => ref new N.CommonDrop(Prototype).chanceNumerator;
}
