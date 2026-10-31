using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using N = BunnyLoader.Interop.DropRules;

namespace Terraria.GameContent.ItemDropRules;

public class OneFromOptionsDropRule : NativeDropRule
{
    internal OneFromOptionsDropRule(IntPtr pointer) : base(pointer, typeof(OneFromOptionsDropRule)) { }
    public OneFromOptionsDropRule(int chanceDenominator, int chanceNumerator, int[] options) : base(N.OneFromOptionsDropRule.New(chanceDenominator, chanceNumerator, DropRuleInterop.ToNative(options)).Pointer, typeof(OneFromOptionsDropRule)) { }
    public int[] dropIds { get => DropRuleInterop.ToManaged(new N.OneFromOptionsDropRule(Prototype).dropIds); set => new N.OneFromOptionsDropRule(Prototype).dropIds = DropRuleInterop.ToNative(value); }
    public ref int chanceDenominator => ref new N.OneFromOptionsDropRule(Prototype).chanceDenominator;
    public ref int chanceNumerator => ref new N.OneFromOptionsDropRule(Prototype).chanceNumerator;
}
