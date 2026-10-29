namespace Terraria.ModLoader;

public class DamageClassData
{
    public StatModifier damage = StatModifier.Default;
    public float critChance = 0f;
    public float attackSpeed = 1f;
    public float armorPen = 0f;
    public StatModifier knockback = StatModifier.Default;

    internal void Reset()
    {
        damage = StatModifier.Default;
        critChance = 0f;
        attackSpeed = 1f;
        armorPen = 0f;
        knockback = StatModifier.Default;
    }

    internal void CopyFrom(DamageClassData other)
    {
        damage = other.damage;
        critChance = other.critChance;
        attackSpeed = other.attackSpeed;
        armorPen = other.armorPen;
        knockback = other.knockback;
    }
}
