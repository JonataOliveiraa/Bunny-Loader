using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using N = BunnyLoader.Interop.DropRules;

namespace Terraria.GameContent.ItemDropRules;

public class OneFromOptionsNotScaledWithLuckDropRule : NativeDropRule
{
    internal OneFromOptionsNotScaledWithLuckDropRule(IntPtr pointer) : base(pointer, typeof(OneFromOptionsNotScaledWithLuckDropRule)) { }
    public OneFromOptionsNotScaledWithLuckDropRule(int chanceDenominator, int chanceNumerator, int[] options) : base(N.OneFromOptionsNotScaledWithLuckDropRule.New(chanceDenominator, chanceNumerator, DropRuleInterop.ToNative(options)).Pointer, typeof(OneFromOptionsNotScaledWithLuckDropRule)) { }
    public int[] dropIds { get => DropRuleInterop.ToManaged(new N.OneFromOptionsNotScaledWithLuckDropRule(Prototype).dropIds); set => new N.OneFromOptionsNotScaledWithLuckDropRule(Prototype).dropIds = DropRuleInterop.ToNative(value); }
    public ref int chanceDenominator => ref new N.OneFromOptionsNotScaledWithLuckDropRule(Prototype).chanceDenominator;
    public ref int chanceNumerator => ref new N.OneFromOptionsNotScaledWithLuckDropRule(Prototype).chanceNumerator;
}
