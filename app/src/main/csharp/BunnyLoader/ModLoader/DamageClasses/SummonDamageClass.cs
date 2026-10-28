using Terraria.Localization;

namespace Terraria.ModLoader;

public class SummonDamageClass : DamageClass
{
    internal override int VanillaTip => 53;

    public override LocalizedText DisplayName => Lang.tip[53];

    public override bool UseStandardCritCalcs => false;

    public override bool ShowStatTooltipLine(Player player, string lineName) => lineName != "CritChance" && lineName != "Speed";

    public override bool GetPrefixInheritance(DamageClass damageClass) => damageClass == Magic;
}
