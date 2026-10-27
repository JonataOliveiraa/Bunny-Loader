namespace Terraria.ModLoader;

/// <summary>"Todo dano": os bonus dela valem para as outras que a herdam.</summary>
public class GenericDamageClass : DamageClass
{
    internal override int VanillaTip => 55;

    public override StatInheritanceData GetModifierInheritance(DamageClass damageClass) => StatInheritanceData.None;
}
