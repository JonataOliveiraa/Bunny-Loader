using Terraria.Localization;

namespace Terraria.ModLoader;

public class SummonMeleeSpeedDamageClass : DamageClass
{
    internal override int VanillaTip => 53;

    public override LocalizedText DisplayName => Lang.tip[53];

    public override StatInheritanceData GetModifierInheritance(DamageClass damageClass)
    {
        if (damageClass == Melee) return new StatInheritanceData(0f, 0f, 1f, 0f, 0f);
        return damageClass == Generic || damageClass == Summon ? StatInheritanceData.Full : StatInheritanceData.None;
    }

    public override bool GetEffectInheritance(DamageClass damageClass) => damageClass == Summon;

    public override bool UseStandardCritCalcs => false;

    public override bool ShowStatTooltipLine(Player player, string lineName) => lineName != "CritChance";

    public override bool GetPrefixInheritance(DamageClass damageClass) => damageClass == Melee;
}
