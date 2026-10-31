using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using N = BunnyLoader.Interop.DropRules;

namespace Terraria.GameContent.ItemDropRules;

public class OneFromRulesRule : NativeDropRule
{
    internal OneFromRulesRule(IntPtr pointer) : base(pointer, typeof(OneFromRulesRule)) { }
    public OneFromRulesRule(int chanceDenominator, IItemDropRule[] options) : base(N.OneFromRulesRule.New(chanceDenominator, DropRuleInterop.ToNative(options)).Pointer, typeof(OneFromRulesRule)) { }
    public IItemDropRule[] options { get => DropRuleInterop.ToManaged(new N.OneFromRulesRule(Prototype).options); set => new N.OneFromRulesRule(Prototype).options = DropRuleInterop.ToNative(value); }
    public ref int chanceDenominator => ref new N.OneFromRulesRule(Prototype).chanceDenominator;
}
