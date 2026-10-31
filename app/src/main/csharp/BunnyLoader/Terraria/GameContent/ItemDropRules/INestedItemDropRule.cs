namespace Terraria.GameContent.ItemDropRules;

public delegate ItemDropAttemptResult ItemDropRuleResolveAction(IItemDropRule rule, DropAttemptInfo info);

public interface INestedItemDropRule : IItemDropRule
{
    ItemDropAttemptResult TryDroppingItem(DropAttemptInfo info, ItemDropRuleResolveAction resolveAction);
}
