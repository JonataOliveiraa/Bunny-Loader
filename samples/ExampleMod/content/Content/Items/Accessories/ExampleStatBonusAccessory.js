import { ExampleDamageClass } from '../../DamageClasses/ExampleDamageClass.js';

// Os bônus por classe de dano: dano de todas (aditivo, multiplicativo, base e
// fixo), crítico do corpo a corpo, velocidade à distância, penetração da
// magia e repulsão da classe de exemplo.
const AdditiveDamageBonus = 25;
const MultiplicativeDamageBonus = 12;
const BaseDamageBonus = 4;
const FlatDamageBonus = 5;
const MeleeCritBonus = 10;
const RangedAttackSpeedBonus = 15;
const MagicArmorPenetration = 5;
const ExampleKnockback = 100;

export class ExampleStatBonusAccessory extends ModItem {
    SetDefaults() {
        this.Item.width = 40;
        this.Item.height = 40;
        this.Item.accessory = true;
    }

    UpdateAccessory(item, player, vanity, hideVisual) {
        player.GetDamage(DamageClass.Generic).Additive += AdditiveDamageBonus / 100;
        player.GetDamage(DamageClass.Generic).Multiplicative *= 1 + MultiplicativeDamageBonus / 100;
        player.GetDamage(DamageClass.Generic).Base += BaseDamageBonus;
        player.GetDamage(DamageClass.Generic).Flat += FlatDamageBonus;
        player.GetCritChance(DamageClass.Melee).value += MeleeCritBonus;
        player.GetAttackSpeed(DamageClass.Ranged).value += RangedAttackSpeedBonus / 100;
        player.GetArmorPenetration(DamageClass.Magic).value += MagicArmorPenetration;
        player.GetKnockback(ExampleDamageClass).Additive += ExampleKnockback / 100;
    }
}
