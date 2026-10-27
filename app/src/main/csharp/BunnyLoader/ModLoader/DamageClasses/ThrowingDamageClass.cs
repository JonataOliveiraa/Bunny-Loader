namespace Terraria.ModLoader;

/// <summary>Arremesso: aceita os prefixos de arma a distancia.</summary>
public class ThrowingDamageClass : DamageClass
{
    internal override int VanillaTip => 58;

    public override bool GetPrefixInheritance(DamageClass damageClass) => damageClass == Ranged;
}
