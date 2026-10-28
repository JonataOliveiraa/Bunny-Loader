using Terraria.Localization;

namespace Terraria.ModLoader;

public class GenericDamageClass : DamageClass
{
    internal override int VanillaTip => 55;

    public override LocalizedText DisplayName => Lang.tip[55];

    public override StatInheritanceData GetModifierInheritance(DamageClass damageClass) => StatInheritanceData.None;
}
