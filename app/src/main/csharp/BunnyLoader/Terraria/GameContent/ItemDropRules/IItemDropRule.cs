using System.Collections.Generic;

namespace Terraria.GameContent.ItemDropRules;

public interface IItemDropRule
{
    List<IItemDropRuleChainAttempt> ChainedRules { get; }
    bool CanDrop(DropAttemptInfo info);
    ItemDropAttemptResult TryDroppingItem(DropAttemptInfo info);
    void ReportDroprates(List<DropRateInfo> drops, DropRateInfoChainFeed ratesInfo);
}
