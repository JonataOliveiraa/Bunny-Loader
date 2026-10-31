using Terraria.GameContent.ItemDropRules;
using Terraria.ModLoader;

namespace CsNpcLoot.Content;

public sealed class BagCondition : IItemDropRuleCondition
{
    public static bool SawContext;
    public bool CanDrop(DropAttemptInfo info)
    {
        SawContext = info.npc.IsNull && info.item == ModContent.ItemType<LootBag>() && !info.player.IsNull && info.IsExpertMode;
        return SawContext;
    }
    public bool CanShowItemDropInUI() => true;
    public string GetConditionDescription() => "Test bag";
}
