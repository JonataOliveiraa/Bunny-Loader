using Terraria.Localization;

namespace Terraria.ModLoader;

public class ThrowingDamageClass : DamageClass
{
    internal override int VanillaTip => 58;

    public override LocalizedText DisplayName => Lang.tip[58];

    public override bool GetPrefixInheritance(DamageClass damageClass) => damageClass == Ranged;
}
