using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using N = BunnyLoader.Interop.DropRules;

namespace Terraria.GameContent.ItemDropRules;

public class DropNothing : NativeDropRule
{
    internal DropNothing(IntPtr pointer) : base(pointer, typeof(DropNothing)) { }
    public DropNothing() : base(N.DropNothing.New().Pointer, typeof(DropNothing)) { }
}
