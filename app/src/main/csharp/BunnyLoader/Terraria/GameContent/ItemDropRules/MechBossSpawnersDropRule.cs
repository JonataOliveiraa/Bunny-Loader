using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using N = BunnyLoader.Interop.DropRules;

namespace Terraria.GameContent.ItemDropRules;

public class MechBossSpawnersDropRule : NativeDropRule
{
    internal MechBossSpawnersDropRule(IntPtr pointer) : base(pointer, typeof(MechBossSpawnersDropRule)) { }
    public MechBossSpawnersDropRule() : base(N.MechBossSpawnersDropRule.New().Pointer, typeof(MechBossSpawnersDropRule)) { }
}
