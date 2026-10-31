using System.Collections.Generic;

namespace Terraria.GameContent.ItemDropRules;

public struct DropRateInfoChainFeed
{
    public float parentDroprateChance;
    public List<IItemDropRuleCondition>? conditions;

    public DropRateInfoChainFeed(float droprate)
    {
        parentDroprateChance = droprate;
        conditions = null;
    }

    public void AddCondition(IItemDropRuleCondition condition) => (conditions ??= new()).Add(condition);

    public DropRateInfoChainFeed With(float multiplier) => new(parentDroprateChance * multiplier)
    {
        conditions = conditions == null ? null : new(conditions)
    };
}
