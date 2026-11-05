using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.Localization;

namespace ExampleMod.Common.ItemDropRules.DropConditions
{
    
    public class ExampleJourneyModeDropCondition : IItemDropRuleCondition
    {
        private static LocalizedText Description;

        public ExampleJourneyModeDropCondition() {
            if (Description.IsNull) Description = Language.GetOrRegister("Mods.ExampleMod.DropConditions.JourneyMode");
        }

        public bool CanDrop(DropAttemptInfo info) {
            return Main.IsJourneyMode;
        }

        public bool CanShowItemDropInUI() {
            return Main.IsJourneyMode; 
        }

        public string GetConditionDescription() {
            return Description.Value;
        }
    }
}
