using Terraria.Localization;

namespace Terraria.ModLoader;

public class MeleeDamageClass : DamageClass
{
    internal override int VanillaTip => 2;

    public override LocalizedText DisplayName => Lang.tip[2];
}
