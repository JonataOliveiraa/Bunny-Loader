using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using N = BunnyLoader.Interop.DropRules;

namespace Terraria.GameContent.ItemDropRules;

public class LeadingConditionRule : NativeDropRule
{
    internal LeadingConditionRule(IntPtr pointer) : base(pointer, typeof(LeadingConditionRule)) { }
    public LeadingConditionRule(IItemDropRuleCondition condition) : base(N.LeadingConditionRule.New(DropRuleInterop.ToNative(condition)).Pointer, typeof(LeadingConditionRule)) { }
    public IItemDropRuleCondition condition { get => DropRuleInterop.Wrap(new N.LeadingConditionRule(Prototype).condition); set => new N.LeadingConditionRule(Prototype).condition = DropRuleInterop.ToNative(value); }
}
