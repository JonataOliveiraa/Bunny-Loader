using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using N = BunnyLoader.Interop.DropRules;

namespace Terraria.GameContent.ItemDropRules;

public class DropBasedOnMasterMode : NativeDropRule
{
    internal DropBasedOnMasterMode(IntPtr pointer) : base(pointer, typeof(DropBasedOnMasterMode)) { }
    public DropBasedOnMasterMode(IItemDropRule ruleForDefault, IItemDropRule ruleForMasterMode) : base(N.DropBasedOnMasterMode.New(DropRuleInterop.ToNative(ruleForDefault), DropRuleInterop.ToNative(ruleForMasterMode)).Pointer, typeof(DropBasedOnMasterMode)) { }
    public IItemDropRule ruleForDefault { get => DropRuleInterop.Wrap(new N.DropBasedOnMasterMode(Prototype).ruleForDefault); set => new N.DropBasedOnMasterMode(Prototype).ruleForDefault = DropRuleInterop.ToNative(value); }
    public IItemDropRule ruleForMasterMode { get => DropRuleInterop.Wrap(new N.DropBasedOnMasterMode(Prototype).ruleForMasterMode); set => new N.DropBasedOnMasterMode(Prototype).ruleForMasterMode = DropRuleInterop.ToNative(value); }
}
