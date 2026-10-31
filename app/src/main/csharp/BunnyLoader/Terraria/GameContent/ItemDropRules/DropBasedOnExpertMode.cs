using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using N = BunnyLoader.Interop.DropRules;

namespace Terraria.GameContent.ItemDropRules;

public class DropBasedOnExpertMode : NativeDropRule
{
    internal DropBasedOnExpertMode(IntPtr pointer) : base(pointer, typeof(DropBasedOnExpertMode)) { }
    public DropBasedOnExpertMode(IItemDropRule ruleForNormalMode, IItemDropRule ruleForExpertMode) : base(N.DropBasedOnExpertMode.New(DropRuleInterop.ToNative(ruleForNormalMode), DropRuleInterop.ToNative(ruleForExpertMode)).Pointer, typeof(DropBasedOnExpertMode)) { }
    public IItemDropRule ruleForNormalMode { get => DropRuleInterop.Wrap(new N.DropBasedOnExpertMode(Prototype).ruleForNormalMode); set => new N.DropBasedOnExpertMode(Prototype).ruleForNormalMode = DropRuleInterop.ToNative(value); }
    public IItemDropRule ruleForExpertMode { get => DropRuleInterop.Wrap(new N.DropBasedOnExpertMode(Prototype).ruleForExpertMode); set => new N.DropBasedOnExpertMode(Prototype).ruleForExpertMode = DropRuleInterop.ToNative(value); }
}
