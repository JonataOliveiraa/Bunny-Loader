using Terraria;
using Terraria.ModLoader;

namespace CsDamageClass.Content.DamageClasses;

public sealed class TestClass : DamageClass
{
    public static int StaticDefaults;

    public override void SetStaticDefaults() => StaticDefaults++;

    public override bool GetEffectInheritance(DamageClass damageClass) => damageClass == Magic;

    public override void SetDefaultStats(Player player)
    {
        player.GetCritChance<TestClass>() += 4;
        player.GetArmorPenetration<TestClass>() += 10;
    }

    public override bool ShowStatTooltipLine(Player player, string lineName) => lineName != "Speed";
}
