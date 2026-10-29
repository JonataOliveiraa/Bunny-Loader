using Terraria.Localization;

namespace Terraria.ModLoader;

[Autoload(false)]
public abstract class VanillaDamageClass : DamageClass
{
    public override LocalizedText DisplayName => Lang.tip[LangKey];

    protected abstract int LangKey { get; }
}

public class DefaultDamageClass : VanillaDamageClass
{
    protected override int LangKey => 55;

    public override StatInheritanceData GetModifierInheritance(DamageClass damageClass) => StatInheritanceData.None;
}

public class GenericDamageClass : VanillaDamageClass
{
    protected override int LangKey => 55;

    public override StatInheritanceData GetModifierInheritance(DamageClass damageClass) => StatInheritanceData.None;
}

public class MeleeDamageClass : VanillaDamageClass
{
    protected override int LangKey => 2;
}

public class MeleeNoSpeedDamageClass : VanillaDamageClass
{
    protected override int LangKey => 2;

    public override StatInheritanceData GetModifierInheritance(DamageClass damageClass)
    {
        if (damageClass == Generic || damageClass == Melee)
            return StatInheritanceData.Full with { attackSpeedInheritance = 0 };
        return StatInheritanceData.None;
    }

    public override bool GetEffectInheritance(DamageClass damageClass) => damageClass == Melee;
}

public class RangedDamageClass : VanillaDamageClass
{
    protected override int LangKey => 3;
}

public class MagicDamageClass : VanillaDamageClass
{
    protected override int LangKey => 4;
}

public class SummonDamageClass : VanillaDamageClass
{
    protected override int LangKey => 53;

    public override bool UseStandardCritCalcs => false;

    public override bool ShowStatTooltipLine(Player player, string lineName) => lineName != "CritChance" && lineName != "Speed";

    public override bool GetPrefixInheritance(DamageClass damageClass) => damageClass == Magic;
}

public class SummonMeleeSpeedDamageClass : VanillaDamageClass
{
    protected override int LangKey => 53;

    public override StatInheritanceData GetModifierInheritance(DamageClass damageClass)
    {
        if (damageClass == Melee) return new StatInheritanceData(attackSpeedInheritance: 1f);
        if (damageClass == Generic || damageClass == Summon) return StatInheritanceData.Full;
        return StatInheritanceData.None;
    }

    public override bool GetEffectInheritance(DamageClass damageClass) => damageClass == Summon;

    public override bool UseStandardCritCalcs => false;

    public override bool ShowStatTooltipLine(Player player, string lineName) => lineName != "CritChance";

    public override bool GetPrefixInheritance(DamageClass damageClass) => damageClass == Melee;
}

public class MagicSummonHybridDamageClass : VanillaDamageClass
{
    protected override int LangKey => 4;

    public override StatInheritanceData GetModifierInheritance(DamageClass damageClass)
    {
        if (damageClass == Generic || damageClass == Magic || damageClass == Summon) return StatInheritanceData.Full;
        return StatInheritanceData.None;
    }

    public override bool GetEffectInheritance(DamageClass damageClass) => damageClass == Magic || damageClass == Summon;
}

public class ThrowingDamageClass : VanillaDamageClass
{
    protected override int LangKey => 58;

    public override bool GetPrefixInheritance(DamageClass damageClass) => damageClass == Ranged;
}
