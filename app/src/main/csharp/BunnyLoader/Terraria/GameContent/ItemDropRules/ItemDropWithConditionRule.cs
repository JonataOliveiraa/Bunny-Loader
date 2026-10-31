using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using N = BunnyLoader.Interop.DropRules;

namespace Terraria.GameContent.ItemDropRules;

public class ItemDropWithConditionRule : CommonDrop
{
    internal ItemDropWithConditionRule(IntPtr pointer) : base(pointer, typeof(ItemDropWithConditionRule)) { }
    public ItemDropWithConditionRule(int itemId, int chanceDenominator, int amountDroppedMinimum, int amountDroppedMaximum, IItemDropRuleCondition condition, int chanceNumerator = 1) : base(N.ItemDropWithConditionRule.New(itemId, chanceDenominator, amountDroppedMinimum, amountDroppedMaximum, DropRuleInterop.ToNative(condition), chanceNumerator).Pointer, typeof(ItemDropWithConditionRule)) { }
    public IItemDropRuleCondition condition { get => DropRuleInterop.Wrap(new N.ItemDropWithConditionRule(Prototype).condition); set => new N.ItemDropWithConditionRule(Prototype).condition = DropRuleInterop.ToNative(value); }
}
