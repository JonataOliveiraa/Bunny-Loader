using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using N = BunnyLoader.Interop.DropRules;

namespace Terraria.GameContent.ItemDropRules;

internal sealed class NativeDropChain : IItemDropRuleChainAttempt
{
    readonly GameHandle handle;
    internal NativeDropChain(IntPtr pointer) => handle = new GameHandle(pointer);
    internal IntPtr Pointer => handle.Target;
    public IItemDropRule RuleToChain => DropRuleInterop.Wrap(new N.IItemDropRuleChainAttempt(Pointer).RuleToChain);
    public bool CanChainIntoRule(ItemDropAttemptResult parentResult) => new N.IItemDropRuleChainAttempt(Pointer).CanChainIntoRule(DropRuleInterop.ToNative(parentResult));
    public void ReportDroprates(float personalDropRate, List<DropRateInfo> drops, DropRateInfoChainFeed ratesInfo)
    {
        if (N.Chains.TryIfSucceeded.Is(new GameObject(Pointer))) new Chains.TryIfSucceeded(RuleToChain, new N.Chains.TryIfSucceeded(Pointer).hideLootReport).ReportDroprates(personalDropRate, drops, ratesInfo);
        else if (N.Chains.TryIfFailedRandomRoll.Is(new GameObject(Pointer))) new Chains.TryIfFailedRandomRoll(RuleToChain, new N.Chains.TryIfFailedRandomRoll(Pointer).hideLootReport).ReportDroprates(personalDropRate, drops, ratesInfo);
        else if (N.Chains.TryIfDoesntFillConditions.Is(new GameObject(Pointer))) new Chains.TryIfDoesntFillConditions(RuleToChain, new N.Chains.TryIfDoesntFillConditions(Pointer).hideLootReport).ReportDroprates(personalDropRate, drops, ratesInfo);
    }
}
