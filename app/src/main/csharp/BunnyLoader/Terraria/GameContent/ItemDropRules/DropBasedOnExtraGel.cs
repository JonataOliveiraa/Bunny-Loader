using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using N = BunnyLoader.Interop.DropRules;

namespace Terraria.GameContent.ItemDropRules;

public class DropBasedOnExtraGel : NativeDropRule
{
    internal DropBasedOnExtraGel(IntPtr pointer) : base(pointer, typeof(DropBasedOnExtraGel)) { }
    public DropBasedOnExtraGel(IItemDropRule ruleForNormal, IItemDropRule ruleForExtraGel) : base(N.DropBasedOnExtraGel.New(DropRuleInterop.ToNative(ruleForNormal), DropRuleInterop.ToNative(ruleForExtraGel)).Pointer, typeof(DropBasedOnExtraGel)) { }
    public IItemDropRule ruleForNormal { get => DropRuleInterop.Wrap(new N.DropBasedOnExtraGel(Prototype).ruleForNormal); set => new N.DropBasedOnExtraGel(Prototype).ruleForNormal = DropRuleInterop.ToNative(value); }
    public IItemDropRule ruleForExtraGel { get => DropRuleInterop.Wrap(new N.DropBasedOnExtraGel(Prototype).ruleForExtraGel); set => new N.DropBasedOnExtraGel(Prototype).ruleForExtraGel = DropRuleInterop.ToNative(value); }
}
