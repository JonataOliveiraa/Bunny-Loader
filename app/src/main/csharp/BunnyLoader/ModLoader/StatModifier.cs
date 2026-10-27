using System;

namespace Terraria.ModLoader;

/// <summary>
/// Um modificador de status, como o do tModLoader:
/// (valor + Base) * Additive * Multiplicative + Flat.
/// </summary>
/// <example>
/// player.GetDamage(DamageClass.Generic) += 0.1f;   // +10% aditivo
/// player.GetDamage(DamageClass.Generic) *= 1.12f;  // x1.12 multiplicativo
/// player.GetDamage(DamageClass.Generic).Base += 4; // antes dos multiplicadores
/// player.GetDamage(DamageClass.Generic).Flat += 5; // depois deles
/// </example>
public struct StatModifier : IEquatable<StatModifier>
{
    public static readonly StatModifier Default = new(1f, 1f, 0f, 0f);

    /// <summary>Somado ao valor antes dos multiplicadores.</summary>
    public float Base;

    /// <summary>Somado ao valor depois dos multiplicadores.</summary>
    public float Flat;

    /// <summary>Os percentuais que somam entre si (1 = nada). Muda pelos operadores + e -.</summary>
    public float Additive { readonly get; private set; }

    /// <summary>Os que multiplicam entre si (1 = nada). Muda pelos operadores * e /.</summary>
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

    /// <summary>O valor com o modificador aplicado.</summary>
    public readonly float ApplyTo(float baseValue) => (baseValue + Base) * Additive * Multiplicative + Flat;

    /// <summary>Os dois juntos: os aditivos somam, os multiplicativos multiplicam.</summary>
    public readonly StatModifier CombineWith(StatModifier m) =>
        new(Additive + m.Additive - 1f, Multiplicative * m.Multiplicative, Flat + m.Flat, Base + m.Base);

    /// <summary>Uma fracao do modificador (a heranca entre classes de dano usa isto).</summary>
    public readonly StatModifier Scale(float scale) =>
        new(1f + (Additive - 1f) * scale, 1f + (Multiplicative - 1f) * scale, Flat * scale, Base * scale);

    public readonly bool Equals(StatModifier other) =>
        Additive == other.Additive && Multiplicative == other.Multiplicative && Flat == other.Flat && Base == other.Base;

    public override readonly bool Equals(object? obj) => obj is StatModifier m && Equals(m);

    public override readonly int GetHashCode() => HashCode.Combine(Additive, Multiplicative, Flat, Base);

    public override readonly string ToString() => $"(+{Additive} x{Multiplicative} base {Base} flat {Flat})";
}
