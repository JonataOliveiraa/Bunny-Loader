using System.Collections.Generic;

namespace Terraria.GameContent.ItemDropRules;

public struct DropRateInfo
{
    public int itemId;
    public int stackMin;
    public int stackMax;
    public float dropRate;
    public List<IItemDropRuleCondition>? conditions;

    public DropRateInfo(int itemId, int stackMin, int stackMax, float dropRate, List<IItemDropRuleCondition>? conditions = null)
    {
        this.itemId = itemId;
        this.stackMin = stackMin;
        this.stackMax = stackMax;
        this.dropRate = dropRate;
        this.conditions = conditions is { Count: > 0 } ? new(conditions) : null;
    }

    public void AddCondition(IItemDropRuleCondition condition) => (conditions ??= new()).Add(condition);
}
