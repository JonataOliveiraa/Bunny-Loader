using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using N = BunnyLoader.Interop.DropRules;

namespace Terraria.GameContent.ItemDropRules;

public class DropBasedOnMasterAndExpertMode : NativeDropRule
{
    internal DropBasedOnMasterAndExpertMode(IntPtr pointer) : base(pointer, typeof(DropBasedOnMasterAndExpertMode)) { }
    public DropBasedOnMasterAndExpertMode(IItemDropRule ruleForDefault, IItemDropRule ruleForExpertMode, IItemDropRule ruleForMasterMode) : base(N.DropBasedOnMasterAndExpertMode.New(DropRuleInterop.ToNative(ruleForDefault), DropRuleInterop.ToNative(ruleForExpertMode), DropRuleInterop.ToNative(ruleForMasterMode)).Pointer, typeof(DropBasedOnMasterAndExpertMode)) { }
    public IItemDropRule ruleForDefault { get => DropRuleInterop.Wrap(new N.DropBasedOnMasterAndExpertMode(Prototype).ruleForDefault); set => new N.DropBasedOnMasterAndExpertMode(Prototype).ruleForDefault = DropRuleInterop.ToNative(value); }
    public IItemDropRule ruleForExpertmode { get => DropRuleInterop.Wrap(new N.DropBasedOnMasterAndExpertMode(Prototype).ruleForExpertmode); set => new N.DropBasedOnMasterAndExpertMode(Prototype).ruleForExpertmode = DropRuleInterop.ToNative(value); }
    public IItemDropRule ruleForMasterMode { get => DropRuleInterop.Wrap(new N.DropBasedOnMasterAndExpertMode(Prototype).ruleForMasterMode); set => new N.DropBasedOnMasterAndExpertMode(Prototype).ruleForMasterMode = DropRuleInterop.ToNative(value); }
}
