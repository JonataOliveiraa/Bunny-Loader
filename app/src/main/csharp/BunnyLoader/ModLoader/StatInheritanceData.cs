namespace Terraria.ModLoader;

/// <summary>
/// Quanto de cada bonus de outra classe uma classe de dano herda
/// (1 = tudo, 0 = nada), como no tModLoader.
/// </summary>
public readonly struct StatInheritanceData
{
    public static readonly StatInheritanceData Full = new(1f, 1f, 1f, 1f, 1f);

    public static readonly StatInheritanceData None = new(0f, 0f, 0f, 0f, 0f);

    public readonly float damageInheritance;
    public readonly float critChanceInheritance;
    public readonly float attackSpeedInheritance;
    public readonly float armorPenInheritance;
    public readonly float knockbackInheritance;

    public StatInheritanceData(float damageInheritance = 1f, float critChanceInheritance = 1f, float attackSpeedInheritance = 1f,
        float armorPenInheritance = 1f, float knockbackInheritance = 1f)
    {
        this.damageInheritance = damageInheritance;
        this.critChanceInheritance = critChanceInheritance;
        this.attackSpeedInheritance = attackSpeedInheritance;
        this.armorPenInheritance = armorPenInheritance;
        this.knockbackInheritance = knockbackInheritance;
    }

    internal bool IsNone =>
        damageInheritance == 0f && critChanceInheritance == 0f && attackSpeedInheritance == 0f &&
        armorPenInheritance == 0f && knockbackInheritance == 0f;
}
