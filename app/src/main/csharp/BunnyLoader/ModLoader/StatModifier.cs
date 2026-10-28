using System;

namespace Terraria.ModLoader;

public struct StatModifier : IEquatable<StatModifier>
{
    public static readonly StatModifier Default = new(1f, 1f, 0f, 0f);

    public float Base;

    public float Flat;

    public float Additive { readonly get; private set; }

    public float Multiplicative { readonly get; private set; }

    public StatModifier()
    {
        Additive = 1f;
        Multiplicative = 1f;
        Flat = 0f;
        Base = 0f;
    }

    public StatModifier(float additive, float multiplicative, float flat = 0f, float @base = 0f)
    {
        Additive = additive;
        Multiplicative = multiplicative;
        Flat = flat;
        Base = @base;
    }

    public static StatModifier operator +(StatModifier m, float add) => new(m.Additive + add, m.Multiplicative, m.Flat, m.Base);

    public static StatModifier operator -(StatModifier m, float sub) => new(m.Additive - sub, m.Multiplicative, m.Flat, m.Base);

    public static StatModifier operator *(StatModifier m, float mul) => new(m.Additive, m.Multiplicative * mul, m.Flat, m.Base);

    public static StatModifier operator /(StatModifier m, float div) => new(m.Additive, m.Multiplicative / div, m.Flat, m.Base);

    public static bool operator ==(StatModifier a, StatModifier b) => a.Equals(b);

    public static bool operator !=(StatModifier a, StatModifier b) => !a.Equals(b);

    public readonly float ApplyTo(float baseValue) => (baseValue + Base) * Additive * Multiplicative + Flat;

    public readonly StatModifier CombineWith(StatModifier m) =>
        new(Additive + m.Additive - 1f, Multiplicative * m.Multiplicative, Flat + m.Flat, Base + m.Base);

    public readonly StatModifier Scale(float scale) =>
        new(1f + (Additive - 1f) * scale, 1f + (Multiplicative - 1f) * scale, Flat * scale, Base * scale);

    public readonly bool Equals(StatModifier other) =>
        Additive == other.Additive && Multiplicative == other.Multiplicative && Flat == other.Flat && Base == other.Base;

    public override readonly bool Equals(object? obj) => obj is StatModifier m && Equals(m);

    public override readonly int GetHashCode() => HashCode.Combine(Additive, Multiplicative, Flat, Base);

    public override readonly string ToString() => $"(+{Additive} x{Multiplicative} base {Base} flat {Flat})";
}
