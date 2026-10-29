using ExampleMod.Content.DamageClasses;
using Terraria;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Accessories;

public class ExampleStatBonusAccessory : ModItem
{
    public static readonly int AdditiveDamageBonus = 25;
    public static readonly int MultiplicativeDamageBonus = 12;
    public static readonly int BaseDamageBonus = 4;
    public static readonly int FlatDamageBonus = 5;
    public static readonly int MeleeCritBonus = 10;
    public static readonly int RangedAttackSpeedBonus = 15;
    public static readonly int MagicArmorPenetration = 5;
    public static readonly int ExampleKnockback = 100;

    public override void SetDefaults()
    {
        Item.width = 40;
        Item.height = 40;
        Item.accessory = true;
    }

    public override void UpdateAccessory(Player player, bool hideVisual)
    {
        player.GetDamage(DamageClass.Generic) += AdditiveDamageBonus / 100f;
        player.GetDamage(DamageClass.Generic) *= 1 + MultiplicativeDamageBonus / 100f;
        player.GetDamage(DamageClass.Generic).Base += BaseDamageBonus;
        player.GetDamage(DamageClass.Generic).Flat += FlatDamageBonus;

        player.GetCritChance(DamageClass.Melee) += MeleeCritBonus;

        player.GetAttackSpeed(DamageClass.Ranged) += RangedAttackSpeedBonus / 100f;

        player.GetArmorPenetration(DamageClass.Magic) += MagicArmorPenetration;

        player.GetKnockback<ExampleDamageClass>() += ExampleKnockback / 100f;
    }
}
