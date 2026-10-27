using BunnyLoader;
using BunnyLoader.Loaders;
using Terraria.ModLoader;

namespace Terraria;

// Os membros de DamageClass que o tModLoader poe no Player. Os bonus voltam ao
// padrao a cada quadro (ResetEffects, mais o SetDefaultStats de cada classe):
// ponha-os onde o tModLoader poe (UpdateEquip, UpdateAccessory, buffs...).
public readonly unsafe partial struct Player
{
    /// <summary>O dano da classe, por ref: player.GetDamage(DamageClass.Generic) += 0.1f;</summary>
    public ref StatModifier GetDamage(DamageClass damageClass) => ref DamageClassLoader.Stats(this, damageClass).Damage;

    public ref StatModifier GetDamage<T>() where T : DamageClass => ref GetDamage(ModContent.GetInstance<T>());

    /// <summary>O critico da classe, em pontos percentuais, por ref.</summary>
    public ref float GetCritChance(DamageClass damageClass) => ref DamageClassLoader.Stats(this, damageClass).Crit;

    public ref float GetCritChance<T>() where T : DamageClass => ref GetCritChance(ModContent.GetInstance<T>());

    /// <summary>A velocidade de ataque da classe (1 = normal), por ref.</summary>
    public ref float GetAttackSpeed(DamageClass damageClass) => ref DamageClassLoader.Stats(this, damageClass).Speed;

    public ref float GetAttackSpeed<T>() where T : DamageClass => ref GetAttackSpeed(ModContent.GetInstance<T>());

    /// <summary>A penetracao de armadura da classe, por ref. O do jogo, GetArmorPenetration(bool), segue igual.</summary>
    public ref float GetArmorPenetration(DamageClass damageClass) => ref DamageClassLoader.Stats(this, damageClass).ArmorPen;

    public ref float GetArmorPenetration<T>() where T : DamageClass => ref GetArmorPenetration(ModContent.GetInstance<T>());

    /// <summary>A repulsao da classe, por ref.</summary>
    public ref StatModifier GetKnockback(DamageClass damageClass) => ref DamageClassLoader.Stats(this, damageClass).Knockback;

    public ref StatModifier GetKnockback<T>() where T : DamageClass => ref GetKnockback(ModContent.GetInstance<T>());

    /// <summary>O dano total: o da classe mais o que ela herda das outras.</summary>
    public StatModifier GetTotalDamage(DamageClass damageClass) => DamageClassLoader.Total(this, damageClass).Damage;

    public StatModifier GetTotalDamage<T>() where T : DamageClass => GetTotalDamage(ModContent.GetInstance<T>());

    public float GetTotalCritChance(DamageClass damageClass) => DamageClassLoader.Total(this, damageClass).Crit;

    public float GetTotalCritChance<T>() where T : DamageClass => GetTotalCritChance(ModContent.GetInstance<T>());

    public float GetTotalAttackSpeed(DamageClass damageClass) => DamageClassLoader.Total(this, damageClass).Speed;

    public float GetTotalAttackSpeed<T>() where T : DamageClass => GetTotalAttackSpeed(ModContent.GetInstance<T>());

    public float GetTotalArmorPenetration(DamageClass damageClass) => DamageClassLoader.Total(this, damageClass).ArmorPen;

    public float GetTotalArmorPenetration<T>() where T : DamageClass => GetTotalArmorPenetration(ModContent.GetInstance<T>());

    public StatModifier GetTotalKnockback(DamageClass damageClass) => DamageClassLoader.Total(this, damageClass).Knockback;

    public StatModifier GetTotalKnockback<T>() where T : DamageClass => GetTotalKnockback(ModContent.GetInstance<T>());

    /// <summary>A velocidade total da classe do item, com o BonusMeleeSpeedMultiplier dele.</summary>
    public float GetWeaponAttackSpeed(Item item) => DamageClassLoader.WeaponAttackSpeed(this, item);

    /// <summary>A penetracao do item mais a total da classe dele.</summary>
    public int GetWeaponArmorPenetration(Item item) => DamageClassLoader.WeaponArmorPenetration(this, item);
}
