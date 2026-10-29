using Terraria;
using Terraria.ModLoader;

namespace CsDamage.Content.DamageClasses;

public class TestDamageClass : DamageClass
{
    public static int StaticDefaults;

    public override StatInheritanceData GetModifierInheritance(DamageClass damageClass) =>
        damageClass == Generic ? StatInheritanceData.Full : StatInheritanceData.None;

    public override bool GetEffectInheritance(DamageClass damageClass) => damageClass == Melee || damageClass == Magic;

    public override void SetStaticDefaults() => StaticDefaults++;

    public override void SetDefaultStats(Player player)
    {
        player.GetCritChance<TestDamageClass>() += 4;
        player.GetArmorPenetration<TestDamageClass>() += 10;
    }

    public override bool ShowStatTooltipLine(Player player, string lineName) => lineName != "Speed";
}
