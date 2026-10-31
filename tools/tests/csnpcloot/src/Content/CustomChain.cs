using System.Collections.Generic;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;

namespace CsNpcLoot.Content;

public sealed class CustomChain : IItemDropRuleChainAttempt
{
    public static int Calls;
    public IItemDropRule RuleToChain { get; } = ItemDropRule.Common(ItemID.Obsidian);
    public bool CanChainIntoRule(ItemDropAttemptResult result) { Calls++; return result.State == ItemDropAttemptResultState.Success; }
    public void ReportDroprates(float personalDropRate, List<DropRateInfo> drops, DropRateInfoChainFeed ratesInfo) => RuleToChain.ReportDroprates(drops, ratesInfo.With(personalDropRate));
}
