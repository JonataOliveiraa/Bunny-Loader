using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using N = BunnyLoader.Interop.DropRules;

namespace Terraria.GameContent.ItemDropRules;

public class FromOptionsWithoutRepeatsDropRule : NativeDropRule
{
    internal FromOptionsWithoutRepeatsDropRule(IntPtr pointer) : base(pointer, typeof(FromOptionsWithoutRepeatsDropRule)) { }
    public FromOptionsWithoutRepeatsDropRule(int dropCount, int[] options) : base(N.FromOptionsWithoutRepeatsDropRule.New(dropCount, DropRuleInterop.ToNative(options)).Pointer, typeof(FromOptionsWithoutRepeatsDropRule)) { }
    public int[] dropIds { get => DropRuleInterop.ToManaged(new N.FromOptionsWithoutRepeatsDropRule(Prototype).dropIds); set => new N.FromOptionsWithoutRepeatsDropRule(Prototype).dropIds = DropRuleInterop.ToNative(value); }
    public ref int dropCount => ref new N.FromOptionsWithoutRepeatsDropRule(Prototype).dropCount;
}
