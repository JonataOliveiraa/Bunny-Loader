using CsDamage.Content.DamageClasses;
using Terraria;
using Terraria.ModLoader;

namespace CsDamage.Content.Items;

public class TestBonusCharm : ModItem
{
    public override void SetDefaults()
    {
        Item.width = 24;
        Item.height = 28;
        Item.accessory = true;
    }

    public override void UpdateAccessory(Player player, bool hideVisual)
    {
        player.GetDamage(DamageClass.Generic) += 0.25f;
        player.GetDamage(DamageClass.Generic) *= 1.12f;
        player.GetDamage(DamageClass.Generic).Base += 4;
        player.GetDamage(DamageClass.Generic).Flat += 5;
        player.GetDamage(DamageClass.Ranged) += 0.2f;
        player.GetCritChance(DamageClass.Melee) += 10;
        player.GetAttackSpeed(DamageClass.Ranged) += 0.15f;
        player.GetArmorPenetration(DamageClass.Magic) += 5;
        player.GetKnockback<TestDamageClass>() += 1f;
        player.GetCritChance<TestDamageClass>() += 10;
        player.GetAttackSpeed<TestDamageClass>() += 0.5f;
    }
}
