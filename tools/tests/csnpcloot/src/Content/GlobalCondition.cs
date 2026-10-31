using Terraria.GameContent.ItemDropRules;
using Terraria.ModLoader;

namespace CsNpcLoot.Content;

public sealed class GlobalCondition : IItemDropRuleCondition
{
    public bool CanDrop(DropAttemptInfo info) => !info.npc.IsNull && info.npc.type == ModContent.NPCType<LootNpc>();
    public bool CanShowItemDropInUI() => true;
    public string GetConditionDescription() => "Test NPC";
}
