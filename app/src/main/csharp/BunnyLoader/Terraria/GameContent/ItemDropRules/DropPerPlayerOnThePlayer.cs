using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using N = BunnyLoader.Interop.DropRules;

namespace Terraria.GameContent.ItemDropRules;

public class DropPerPlayerOnThePlayer : CommonDrop
{
    internal DropPerPlayerOnThePlayer(IntPtr pointer) : base(pointer, typeof(DropPerPlayerOnThePlayer)) { }
    public DropPerPlayerOnThePlayer(int itemId, int chanceDenominator, int amountDroppedMinimum, int amountDroppedMaximum, IItemDropRuleCondition optionalCondition) : base(N.DropPerPlayerOnThePlayer.New(itemId, chanceDenominator, amountDroppedMinimum, amountDroppedMaximum, DropRuleInterop.ToNative(optionalCondition)).Pointer, typeof(DropPerPlayerOnThePlayer)) { }
    public IItemDropRuleCondition condition { get => DropRuleInterop.Wrap(new N.DropPerPlayerOnThePlayer(Prototype).condition); set => new N.DropPerPlayerOnThePlayer(Prototype).condition = DropRuleInterop.ToNative(value); }
}
