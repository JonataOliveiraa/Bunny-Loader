using System;

namespace Microsoft.Xna.Framework;

public partial struct Matrix : IEquatable<Matrix>
{
    public Matrix(float m11, float m12, float m13, float m14, float m21, float m22, float m23, float m24,
        float m31, float m32, float m33, float m34, float m41, float m42, float m43, float m44)
    {
        M11 = m11; M12 = m12; M13 = m13; M14 = m14;
        M21 = m21; M22 = m22; M23 = m23; M24 = m24;
        M31 = m31; M32 = m32; M33 = m33; M34 = m34;
        M41 = m41; M42 = m42; M43 = m43; M44 = m44;
    }

    public static Matrix operator *(in Matrix a, in Matrix b) => new(
        a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31 + a.M14 * b.M41,
        a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32 + a.M14 * b.M42,
        a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33 + a.M14 * b.M43,
        a.M11 * b.M14 + a.M12 * b.M24 + a.M13 * b.M34 + a.M14 * b.M44,
        a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31 + a.M24 * b.M41,
        a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32 + a.M24 * b.M42,
        a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33 + a.M24 * b.M43,
        a.M21 * b.M14 + a.M22 * b.M24 + a.M23 * b.M34 + a.M24 * b.M44,
        a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31 + a.M34 * b.M41,
        a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32 + a.M34 * b.M42,
        a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33 + a.M34 * b.M43,
        a.M31 * b.M14 + a.M32 * b.M24 + a.M33 * b.M34 + a.M34 * b.M44,
        a.M41 * b.M11 + a.M42 * b.M21 + a.M43 * b.M31 + a.M44 * b.M41,
        a.M41 * b.M12 + a.M42 * b.M22 + a.M43 * b.M32 + a.M44 * b.M42,
        a.M41 * b.M13 + a.M42 * b.M23 + a.M43 * b.M33 + a.M44 * b.M43,
        a.M41 * b.M14 + a.M42 * b.M24 + a.M43 * b.M34 + a.M44 * b.M44);

    public static Matrix operator *(Matrix m, float f) => new(
        m.M11 * f, m.M12 * f, m.M13 * f, m.M14 * f, m.M21 * f, m.M22 * f, m.M23 * f, m.M24 * f,
        m.M31 * f, m.M32 * f, m.M33 * f, m.M34 * f, m.M41 * f, m.M42 * f, m.M43 * f, m.M44 * f);

    public static Matrix operator /(Matrix m, float f) => m * (1f / f);

    public static Matrix operator /(Matrix a, Matrix b) => new(
        a.M11 / b.M11, a.M12 / b.M12, a.M13 / b.M13, a.M14 / b.M14, a.M21 / b.M21, a.M22 / b.M22, a.M23 / b.M23, a.M24 / b.M24,
        a.M31 / b.M31, a.M32 / b.M32, a.M33 / b.M33, a.M34 / b.M34, a.M41 / b.M41, a.M42 / b.M42, a.M43 / b.M43, a.M44 / b.M44);

    public static Matrix operator +(in Matrix a, in Matrix b) => new(
        a.M11 + b.M11, a.M12 + b.M12, a.M13 + b.M13, a.M14 + b.M14, a.M21 + b.M21, a.M22 + b.M22, a.M23 + b.M23, a.M24 + b.M24,
        a.M31 + b.M31, a.M32 + b.M32, a.M33 + b.M33, a.M34 + b.M34, a.M41 + b.M41, a.M42 + b.M42, a.M43 + b.M43, a.M44 + b.M44);

    public static Matrix operator -(Matrix a, Matrix b) => a + -b;

    public static Matrix operator -(Matrix m) => m * -1f;

    public static bool operator ==(Matrix a, Matrix b) => a.Equals(b);

    public static bool operator !=(Matrix a, Matrix b) => !a.Equals(b);

    public readonly bool Equals(Matrix other) =>
        M11 == other.M11 && M12 == other.M12 && M13 == other.M13 && M14 == other.M14 &&
        M21 == other.M21 && M22 == other.M22 && M23 == other.M23 && M24 == other.M24 &&
        M31 == other.M31 && M32 == other.M32 && M33 == other.M33 && M34 == other.M34 &&
        M41 == other.M41 && M42 == other.M42 && M43 == other.M43 && M44 == other.M44;

    public override readonly bool Equals(object? obj) => obj is Matrix m && Equals(m);

    public override readonly int GetHashCode() => HashCode.Combine(
        HashCode.Combine(M11, M12, M13, M14, M21, M22, M23, M24),
        HashCode.Combine(M31, M32, M33, M34, M41, M42, M43, M44));

    public override readonly string ToString() =>
        $"{{M11:{M11} M12:{M12} M13:{M13} M14:{M14}}} {{M21:{M21} M22:{M22} M23:{M23} M24:{M24}}} " +
        $"{{M31:{M31} M32:{M32} M33:{M33} M34:{M34}}} {{M41:{M41} M42:{M42} M43:{M43} M44:{M44}}}";
}
