using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using N = BunnyLoader.Interop.DropRules;

namespace Terraria.GameContent.ItemDropRules;

public class StatueMimicItemDropRule : NativeDropRule
{
    internal StatueMimicItemDropRule(IntPtr pointer) : base(pointer, typeof(StatueMimicItemDropRule)) { }
    public StatueMimicItemDropRule() : base(N.StatueMimicItemDropRule.New().Pointer, typeof(StatueMimicItemDropRule)) { }
}
