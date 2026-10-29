using Terraria.ID;
using Terraria.ModLoader;

namespace Terraria;

public readonly partial struct Player
{
    public bool HasBuff(int type) => FindBuffIndex(type) != -1;

    public string setBonus
    {
        get => Terraria.ModLoader.Core.ItemEquipHooks.GetSetBonus(this);
        set => Terraria.ModLoader.Core.ItemEquipHooks.SetSetBonus(this, value);
    }

    public ref StatModifier GetDamage<T>() where T : DamageClass => ref GetDamage(ModContent.GetInstance<T>());

    public ref StatModifier GetDamage(DamageClass damageClass) => ref DamageClassLoader.Stats(this, damageClass).damage;

    public ref float GetCritChance<T>() where T : DamageClass => ref GetCritChance(ModContent.GetInstance<T>());

    public ref float GetCritChance(DamageClass damageClass) => ref DamageClassLoader.Stats(this, damageClass).critChance;

    public ref float GetAttackSpeed<T>() where T : DamageClass => ref GetAttackSpeed(ModContent.GetInstance<T>());

    public ref float GetAttackSpeed(DamageClass damageClass) => ref DamageClassLoader.Stats(this, damageClass).attackSpeed;

    public ref float GetArmorPenetration<T>() where T : DamageClass => ref GetArmorPenetration(ModContent.GetInstance<T>());

    public ref float GetArmorPenetration(DamageClass damageClass) => ref DamageClassLoader.Stats(this, damageClass).armorPen;

    public ref StatModifier GetKnockback<T>() where T : DamageClass => ref GetKnockback(ModContent.GetInstance<T>());

    public ref StatModifier GetKnockback(DamageClass damageClass) => ref DamageClassLoader.Stats(this, damageClass).knockback;

    public StatModifier GetTotalDamage<T>() where T : DamageClass => GetTotalDamage(ModContent.GetInstance<T>());

    public StatModifier GetTotalDamage(DamageClass damageClass) => DamageClassLoader.Total(this, damageClass).Damage;

    public float GetTotalCritChance<T>() where T : DamageClass => GetTotalCritChance(ModContent.GetInstance<T>());

    public float GetTotalCritChance(DamageClass damageClass) => DamageClassLoader.Total(this, damageClass).Crit;

    public float GetTotalAttackSpeed<T>() where T : DamageClass => GetTotalAttackSpeed(ModContent.GetInstance<T>());

    public float GetTotalAttackSpeed(DamageClass damageClass) => DamageClassLoader.Total(this, damageClass).Speed;

    public float GetTotalArmorPenetration<T>() where T : DamageClass => GetTotalArmorPenetration(ModContent.GetInstance<T>());

    public float GetTotalArmorPenetration(DamageClass damageClass) => DamageClassLoader.Total(this, damageClass).Pen;

    public StatModifier GetTotalKnockback<T>() where T : DamageClass => GetTotalKnockback(ModContent.GetInstance<T>());

    public StatModifier GetTotalKnockback(DamageClass damageClass) => DamageClassLoader.Total(this, damageClass).Knockback;

    public float GetWeaponAttackSpeed(Item sItem)
    {
        float speed = GetTotalAttackSpeed(sItem.DamageType);
        var multiplier = ItemID.Sets.BonusMeleeSpeedMultiplier;
        int type = sItem.type;
        float bonus = !multiplier.IsNull && (uint)type < (uint)multiplier.Length ? multiplier[type] : 1f;
        return 1 + (speed - 1) * bonus;
    }

    public int GetWeaponArmorPenetration(Item sItem) => (int)(sItem.armorPenetration + GetTotalArmorPenetration(sItem.DamageType));
}
