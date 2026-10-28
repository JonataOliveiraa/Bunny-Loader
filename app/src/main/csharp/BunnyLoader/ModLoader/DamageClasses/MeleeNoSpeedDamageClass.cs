using Terraria.Localization;

namespace Terraria.ModLoader;

public class MeleeNoSpeedDamageClass : DamageClass
{
    internal override int VanillaTip => 2;

    public override LocalizedText DisplayName => Lang.tip[2];

    public override StatInheritanceData GetModifierInheritance(DamageClass damageClass) =>
        damageClass == Generic || damageClass == Melee ? new StatInheritanceData(attackSpeedInheritance: 0f) : StatInheritanceData.None;

    public override bool GetEffectInheritance(DamageClass damageClass) => damageClass == Melee;
}
