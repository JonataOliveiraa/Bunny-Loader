using ExampleMod.Common.Configs.CustomDataTypes;
using System.Collections.Generic;
using Terraria.ID;
using Terraria.ModLoader.Config;

namespace ExampleMod.Common.Configs.ModConfigShowcases
{
    [BackgroundColor(154, 152, 181)]
    public class ModConfigShowcaseLabels : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ClientSide;

        [SliderColor(255, 0, 127)]
        public float SomeFloat;

        [LabelKey("$Mods.ExampleMod.Configs.Common.LocalizedLabel")]
        [TooltipKey("$Mods.ExampleMod.Configs.Common.LocalizedTooltip")]
        public int LocalizedLabel;

        const string InterpolatedLabel = "$Mods.ExampleMod.Configs.Common.InterpolatedLabel";
        const string InterpolatedTooltip = "$Mods.ExampleMod.Configs.Common.InterpolatedTooltip";

        [LabelKey(InterpolatedLabel), TooltipKey(InterpolatedTooltip)]
        [LabelArgs("ExampleMod/ExampleYoyo", 1, "=>", "$Mods.ExampleMod.Items.ExampleYoyo.DisplayName")]
        [TooltipArgs("$Mods.ExampleMod.Items.ExampleYoyo.DisplayName", "FF55AA", "22a2dd")]
        public bool InterpolatedTextA;

        [LabelKey(InterpolatedLabel), TooltipKey(InterpolatedTooltip)]
        [LabelArgs("ExampleMod/ExampleSword", 2, "=>", "$Items.ExampleSword.DisplayName")]
        [TooltipArgs("$Mods.ExampleMod.Items.ExampleSword.DisplayName", "77bd8e", "88AADD")]
        public bool InterpolatedTextB;

        [LabelKey(InterpolatedLabel), TooltipKey(InterpolatedTooltip)]
        [LabelArgs(ItemID.Meowmere, 3, "=>", $"$ItemName.{nameof(ItemID.Meowmere)}")]
        [TooltipArgs($"$ItemName.{nameof(ItemID.Meowmere)}", "c441c6", "deeb55")]
        public bool InterpolatedTextC;

        [LabelArgs(.15753f, 1234567890, 12, 1.77777f)]
        public bool StringFormatting;

        [BackgroundColor(255, 0, 255)]

        public Pair pairExample = new Pair();

        [BackgroundColor(255, 0, 0)]
        public List<Pair> ListOfPair = new List<Pair>();

        [Header("HeaderIdentifier")]
        public int TypicalHeader;

        [Header("$Mods.ExampleMod.Configs.Common.LocalizedHeader")]
        public int LocalizedHeader;

        [Header("ChatTagExample")]
        public int CoolHeader;

        [BackgroundColor(85, 107, 47)]
        public SimpleData simpleDataExample2 = new SimpleData();
    }
}
