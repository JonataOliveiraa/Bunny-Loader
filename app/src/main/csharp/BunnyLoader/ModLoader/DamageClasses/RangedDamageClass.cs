using Terraria.Localization;

namespace Terraria.ModLoader;

public class RangedDamageClass : DamageClass
{
    internal override int VanillaTip => 3;

    public override LocalizedText DisplayName => Lang.tip[3];
}
