using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using N = BunnyLoader.Interop.DropRules;

namespace Terraria.GameContent.ItemDropRules;

public class SlimeBodyItemDropRule : NativeDropRule
{
    internal SlimeBodyItemDropRule(IntPtr pointer) : base(pointer, typeof(SlimeBodyItemDropRule)) { }
    public SlimeBodyItemDropRule() : base(N.SlimeBodyItemDropRule.New().Pointer, typeof(SlimeBodyItemDropRule)) { }
}
