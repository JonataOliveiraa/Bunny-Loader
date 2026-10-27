namespace Terraria.ModLoader;

/// <summary>Corpo a corpo sem o bonus de velocidade.</summary>
public class MeleeNoSpeedDamageClass : DamageClass
{
    internal override int VanillaTip => 2;

    public override StatInheritanceData GetModifierInheritance(DamageClass damageClass) =>
        damageClass == Generic || damageClass == Melee ? new StatInheritanceData(attackSpeedInheritance: 0f) : StatInheritanceData.None;

    public override bool GetEffectInheritance(DamageClass damageClass) => damageClass == Melee;
}
