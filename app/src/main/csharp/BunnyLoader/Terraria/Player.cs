using Terraria.ModLoader;

namespace Terraria;

public readonly partial struct Player
{
    public bool HasBuff(int type) => FindBuffIndex(type) != -1;

    public ref StatModifier GetDamage(DamageClass damageClass) => ref DamageClassLoader.Stats(this, damageClass).Damage;

    public ref StatModifier GetDamage<T>() where T : DamageClass => ref GetDamage(ModContent.GetInstance<T>());

    public ref float GetCritChance(DamageClass damageClass) => ref DamageClassLoader.Stats(this, damageClass).Crit;

    public ref float GetCritChance<T>() where T : DamageClass => ref GetCritChance(ModContent.GetInstance<T>());

    public ref float GetAttackSpeed(DamageClass damageClass) => ref DamageClassLoader.Stats(this, damageClass).Speed;

    public ref float GetAttackSpeed<T>() where T : DamageClass => ref GetAttackSpeed(ModContent.GetInstance<T>());

    public ref float GetArmorPenetration(DamageClass damageClass) => ref DamageClassLoader.Stats(this, damageClass).ArmorPen;

    public ref float GetArmorPenetration<T>() where T : DamageClass => ref GetArmorPenetration(ModContent.GetInstance<T>());

    public ref StatModifier GetKnockback(DamageClass damageClass) => ref DamageClassLoader.Stats(this, damageClass).Knockback;

    public ref StatModifier GetKnockback<T>() where T : DamageClass => ref GetKnockback(ModContent.GetInstance<T>());

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

    public float GetWeaponAttackSpeed(Item sItem) => DamageClassLoader.WeaponAttackSpeed(this, sItem);

    public int GetWeaponArmorPenetration(Item sItem) => DamageClassLoader.WeaponArmorPenetration(this, sItem);
}
