using System.Collections.Generic;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;

namespace CsNpcLoot.Content;

public sealed class CustomRule : IItemDropRule
{
    public List<IItemDropRuleChainAttempt> ChainedRules { get; } = new();
    public bool CanDrop(DropAttemptInfo info) => true;
    public ItemDropAttemptResult TryDroppingItem(DropAttemptInfo info)
    {
        CommonCode.DropItem(info, ItemID.Amethyst, 2);
        return new() { State = ItemDropAttemptResultState.Success };
    }
    public void ReportDroprates(List<DropRateInfo> drops, DropRateInfoChainFeed ratesInfo)
    {
        drops.Add(new(ItemID.Amethyst, 2, 2, ratesInfo.parentDroprateChance, ratesInfo.conditions));
        Chains.ReportDroprates(ChainedRules, 1f, drops, ratesInfo);
    }
}
