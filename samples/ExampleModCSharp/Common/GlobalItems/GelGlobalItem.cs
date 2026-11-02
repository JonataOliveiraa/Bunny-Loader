using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Common.GlobalItems;

public class GelGlobalItem : GlobalItem
{
    public static LocalizedText SpecialShiftClickText { get; private set; }

    public override void SetStaticDefaults()
    {
        SpecialShiftClickText = Mod.GetLocalization($"{nameof(GelGlobalItem)}.SpecialShiftClick");
    }

    public override bool AppliesToEntity(Item entity, bool lateInstantiation) => entity.type == ItemID.Gel;

    public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
    {
        tooltips.Add(new(Mod, "SpecialShiftClick", SpecialShiftClickText.Value));
    }
}
