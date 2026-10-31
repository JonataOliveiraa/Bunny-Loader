using Terraria.ModLoader.Core;

namespace Terraria.GameContent.ItemDropRules;

public readonly partial struct ItemDropResolver
{
    public void TryDropping(DropAttemptInfo info)
    {
        if (info.npc.IsNull && info.item > 0) ItemLootHooks.DropFromItem(this, info);
        else TryDropping(DropRuleInterop.ToNative(info));
    }

    public ItemDropAttemptResult ResolveRule(IItemDropRule rule, DropAttemptInfo info)
    {
        using var scope = new ItemLootHooks.AttemptScope(info);
        return DropRuleInterop.ToManaged(ResolveRule(DropRuleInterop.ToNative(rule), DropRuleInterop.ToNative(info)));
    }
}
