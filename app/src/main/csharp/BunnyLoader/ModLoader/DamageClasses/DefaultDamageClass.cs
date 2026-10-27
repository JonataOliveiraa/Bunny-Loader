namespace Terraria.ModLoader;

/// <summary>Sem classe (dano puro): nao herda nada.</summary>
public class DefaultDamageClass : DamageClass
{
    internal override int VanillaTip => 55;

    public override StatInheritanceData GetModifierInheritance(DamageClass damageClass) => StatInheritanceData.None;
}
