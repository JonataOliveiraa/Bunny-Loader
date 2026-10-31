using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.Localization;

namespace ExampleMod.Common.ItemDropRules.DropConditions;

public sealed class ExampleDropCondition : IItemDropRuleCondition
{
    public bool CanDrop(DropAttemptInfo info) => Main.dayTime;
    public bool CanShowItemDropInUI() => true;
    public string GetConditionDescription() => Language.GetTextValue("Mods.ExampleMod.DropConditions.Example") ?? "";
}
