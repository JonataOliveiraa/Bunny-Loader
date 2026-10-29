namespace Terraria.ModLoader;

public struct StatModifier
{
    public static readonly StatModifier Default = new();

    public float Base = 0f;

    public float Additive { get; private set; } = 1f;

    public float Multiplicative { get; private set; } = 1f;

    public float Flat = 0f;

    public StatModifier() { }

    public StatModifier(float additive, float multiplicative, float flat = 0f, float @base = 0f)
    {
        Additive = additive;
        Multiplicative = multiplicative;
        Flat = flat;
        Base = @base;
    }

    public override bool Equals(object? obj) => obj is StatModifier m && this == m;

    public override int GetHashCode()
    {
        int hashCode = 1713062080;
        hashCode = hashCode * -1521134295 + Additive.GetHashCode();
        hashCode = hashCode * -1521134295 + Multiplicative.GetHashCode();
        hashCode = hashCode * -1521134295 + Flat.GetHashCode();
        hashCode = hashCode * -1521134295 + Base.GetHashCode();
        return hashCode;
    }

    public static StatModifier operator +(StatModifier m, float add) => new(m.Additive + add, m.Multiplicative, m.Flat, m.Base);

    public static StatModifier operator -(StatModifier m, float sub) => new(m.Additive - sub, m.Multiplicative, m.Flat, m.Base);

    public static StatModifier operator *(StatModifier m, float mul) => new(m.Additive, m.Multiplicative * mul, m.Flat, m.Base);

    public static StatModifier operator /(StatModifier m, float div) => new(m.Additive, m.Multiplicative / div, m.Flat, m.Base);

    public static StatModifier operator +(float add, StatModifier m) => m + add;

    public static StatModifier operator *(float mul, StatModifier m) => m * mul;

    public static bool operator ==(StatModifier m1, StatModifier m2) =>
        m1.Additive == m2.Additive && m1.Multiplicative == m2.Multiplicative && m1.Flat == m2.Flat && m1.Base == m2.Base;

    public static bool operator !=(StatModifier m1, StatModifier m2) => !(m1 == m2);

    public float ApplyTo(float baseValue) => (baseValue + Base) * Additive * Multiplicative + Flat;

    public StatModifier CombineWith(StatModifier m) => new(Additive + m.Additive - 1, Multiplicative * m.Multiplicative, Flat + m.Flat, Base + m.Base);

    public StatModifier Scale(float scale) => new(1 + (Additive - 1) * scale, 1 + (Multiplicative - 1) * scale, Flat * scale, Base * scale);

    public float Undo(float currentValue) => ((currentValue - Flat) / (Multiplicative * Additive)) - Base;
}
