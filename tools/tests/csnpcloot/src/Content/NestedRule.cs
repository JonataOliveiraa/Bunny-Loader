using System;
using System.Collections.Generic;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;

namespace CsNpcLoot.Content;

public sealed class NestedRule : INestedItemDropRule
{
    readonly IItemDropRule child = ItemDropRule.Common(ItemID.Amber);
    public List<IItemDropRuleChainAttempt> ChainedRules { get; } = new();
    public bool CanDrop(DropAttemptInfo info) => true;
    public ItemDropAttemptResult TryDroppingItem(DropAttemptInfo info) => throw new InvalidOperationException("The nested overload must run.");
    public ItemDropAttemptResult TryDroppingItem(DropAttemptInfo info, ItemDropRuleResolveAction resolveAction) => resolveAction(child, info);
    public void ReportDroprates(List<DropRateInfo> drops, DropRateInfoChainFeed ratesInfo) => child.ReportDroprates(drops, ratesInfo);
}
