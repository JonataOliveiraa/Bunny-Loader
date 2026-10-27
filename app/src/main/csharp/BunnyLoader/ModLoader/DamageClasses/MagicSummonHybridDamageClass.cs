namespace Terraria.ModLoader;

/// <summary>Magica e invocacao ao mesmo tempo.</summary>
public class MagicSummonHybridDamageClass : DamageClass
{
    internal override int VanillaTip => 4;

    public override StatInheritanceData GetModifierInheritance(DamageClass damageClass) =>
        damageClass == Generic || damageClass == Magic || damageClass == Summon ? StatInheritanceData.Full : StatInheritanceData.None;

    public override bool GetEffectInheritance(DamageClass damageClass) => damageClass == Magic || damageClass == Summon;
}
