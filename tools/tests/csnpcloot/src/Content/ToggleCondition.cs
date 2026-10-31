using Terraria.GameContent.ItemDropRules;

namespace CsNpcLoot.Content;

public sealed class ToggleCondition : IItemDropRuleCondition
{
    public static bool Allow = true;
    public static int Calls;
    public bool CanDrop(DropAttemptInfo info) { Calls++; return Allow && !info.npc.IsNull && info.item == 0; }
    public bool CanShowItemDropInUI() => true;
    public string GetConditionDescription() => "Test condition";
}
