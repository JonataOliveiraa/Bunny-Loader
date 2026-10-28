using Terraria.Localization;

namespace Terraria.ModLoader;

public class MagicDamageClass : DamageClass
{
    internal override int VanillaTip => 4;

    public override LocalizedText DisplayName => Lang.tip[4];
}
