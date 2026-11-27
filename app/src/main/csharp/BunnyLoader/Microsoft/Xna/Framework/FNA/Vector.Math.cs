/* FNA - XNA4 Reimplementation for Desktop Platforms
 * Copyright 2009-2024 Ethan Lee and the MonoGame Team
 *
 * Released under the Microsoft Public License.
 * See LICENSE for details.
 */

/* Derived from code by the Mono.Xna Team (Copyright 2006).
 * Released under the MIT License. See monoxna.LICENSE for details.
 */

using System;

namespace Microsoft.Xna.Framework;

public partial struct Vector2
{
    public static Vector2 Add(Vector2 value1, Vector2 value2) => new(value1.X + value2.X, value1.Y + value2.Y);

    public static Vector2 Subtract(Vector2 value1, Vector2 value2) => new(value1.X - value2.X, value1.Y - value2.Y);

    public static Vector2 Multiply(Vector2 value1, Vector2 value2) => new(value1.X * value2.X, value1.Y * value2.Y);

    public static Vector2 Multiply(Vector2 value1, float scaleFactor) => new(value1.X * scaleFactor, value1.Y * scaleFactor);

    public static Vector2 Divide(Vector2 value1, Vector2 value2) => new(value1.X / value2.X, value1.Y / value2.Y);

    public static Vector2 Divide(Vector2 value1, float divider)
    {
        float factor = 1f / divider;
        return new(value1.X * factor, value1.Y * factor);
    }

    public static Vector2 Negate(Vector2 value) => new(-value.X, -value.Y);

    public static float Dot(Vector2 value1, Vector2 value2) => (value1.X * value2.X) + (value1.Y * value2.Y);

    public static float Distance(Vector2 value1, Vector2 value2) => FnaMathCore.Vector2Distance(value1, value2);

    public static float DistanceSquared(Vector2 value1, Vector2 value2) => FnaMathCore.Vector2DistanceSquared(value1, value2);

    public readonly float Length() => MathF.Sqrt((X * X) + (Y * Y));

    public readonly float LengthSquared() => (X * X) + (Y * Y);

    public void Normalize() => this = FnaMathCore.Vector2Normalize(this);

    public static Vector2 Normalize(Vector2 value) => FnaMathCore.Vector2Normalize(value);

    public static Vector2 Lerp(Vector2 value1, Vector2 value2, float amount) => FnaMathCore.Vector2Lerp(value1, value2, amount);

    public static Vector2 SmoothStep(Vector2 value1, Vector2 value2, float amount) => FnaMathCore.Vector2SmoothStep(value1, value2, amount);

    public static Vector2 Barycentric(Vector2 value1, Vector2 value2, Vector2 value3, float amount1, float amount2) =>
        FnaMathCore.Vector2Barycentric(value1, value2, value3, amount1, amount2);

    public static Vector2 Clamp(Vector2 value1, Vector2 min, Vector2 max) => FnaMathCore.Vector2Clamp(value1, min, max);

    public static Vector2 Max(Vector2 value1, Vector2 value2) => FnaMathCore.Vector2Max(value1, value2);

    public static Vector2 Min(Vector2 value1, Vector2 value2) => FnaMathCore.Vector2Min(value1, value2);

    public static Vector2 Reflect(Vector2 vector, Vector2 normal) => FnaMathCore.Vector2Reflect(vector, normal);

    public static Vector2 Transform(Vector2 position, Matrix matrix) => FnaMathCore.Vector2Transform(position, matrix);

    public static Vector2 Transform(Vector2 value, Quaternion rotation) => FnaMathCore.Vector2Transform(value, rotation);

    public static Vector2 TransformNormal(Vector2 normal, Matrix matrix) => FnaMathCore.Vector2TransformNormal(normal, matrix);
}

public partial struct Vector3
{
    public static Vector3 Add(Vector3 value1, Vector3 value2) => new(value1.X + value2.X, value1.Y + value2.Y, value1.Z + value2.Z);

    public static Vector3 Subtract(Vector3 value1, Vector3 value2) => new(value1.X - value2.X, value1.Y - value2.Y, value1.Z - value2.Z);

    public static Vector3 Multiply(Vector3 value1, Vector3 value2) => new(value1.X * value2.X, value1.Y * value2.Y, value1.Z * value2.Z);

    public static Vector3 Multiply(Vector3 value1, float scaleFactor) => new(value1.X * scaleFactor, value1.Y * scaleFactor, value1.Z * scaleFactor);

    public static Vector3 Divide(Vector3 value1, Vector3 value2) => new(value1.X / value2.X, value1.Y / value2.Y, value1.Z / value2.Z);

    public static Vector3 Divide(Vector3 value1, float value2)
    {
        float factor = 1f / value2;
        return new(value1.X * factor, value1.Y * factor, value1.Z * factor);
    }

    public static Vector3 Negate(Vector3 value) => new(-value.X, -value.Y, -value.Z);

    public static float Dot(Vector3 vector1, Vector3 vector2) => (vector1.X * vector2.X) + (vector1.Y * vector2.Y) + (vector1.Z * vector2.Z);

    public static Vector3 Cross(Vector3 vector1, Vector3 vector2) => FnaMathCore.Vector3Cross(vector1, vector2);

    public static float Distance(Vector3 value1, Vector3 value2) => FnaMathCore.Vector3Distance(value1, value2);

    public static float DistanceSquared(Vector3 value1, Vector3 value2) => FnaMathCore.Vector3DistanceSquared(value1, value2);

    public readonly float Length() => MathF.Sqrt((X * X) + (Y * Y) + (Z * Z));

    public readonly float LengthSquared() => (X * X) + (Y * Y) + (Z * Z);

    public void Normalize() => this = FnaMathCore.Vector3Normalize(this);

    public static Vector3 Normalize(Vector3 value) => FnaMathCore.Vector3Normalize(value);

    public static Vector3 Lerp(Vector3 value1, Vector3 value2, float amount) => FnaMathCore.Vector3Lerp(value1, value2, amount);

    public static Vector3 SmoothStep(Vector3 value1, Vector3 value2, float amount) => FnaMathCore.Vector3SmoothStep(value1, value2, amount);

    public static Vector3 Max(Vector3 value1, Vector3 value2) => FnaMathCore.Vector3Max(value1, value2);

    public static Vector3 Min(Vector3 value1, Vector3 value2) => FnaMathCore.Vector3Min(value1, value2);

    public static Vector3 Reflect(Vector3 vector, Vector3 normal) => FnaMathCore.Vector3Reflect(vector, normal);

    public static Vector3 Transform(Vector3 position, Matrix matrix) => FnaMathCore.Vector3Transform(position, matrix);

    public static Vector3 Transform(Vector3 value, Quaternion rotation) => FnaMathCore.Vector3Transform(value, rotation);

    public static Vector3 TransformNormal(Vector3 normal, Matrix matrix) => FnaMathCore.Vector3TransformNormal(normal, matrix);
}

public partial struct Vector4
{
    public static Vector4 Add(Vector4 value1, Vector4 value2) =>
        new(value1.X + value2.X, value1.Y + value2.Y, value1.Z + value2.Z, value1.W + value2.W);

    public static Vector4 Subtract(Vector4 value1, Vector4 value2) =>
        new(value1.X - value2.X, value1.Y - value2.Y, value1.Z - value2.Z, value1.W - value2.W);

    public static Vector4 Multiply(Vector4 value1, Vector4 value2) =>
        new(value1.X * value2.X, value1.Y * value2.Y, value1.Z * value2.Z, value1.W * value2.W);

    public static Vector4 Multiply(Vector4 value1, float scaleFactor) =>
        new(value1.X * scaleFactor, value1.Y * scaleFactor, value1.Z * scaleFactor, value1.W * scaleFactor);

    public static Vector4 Divide(Vector4 value1, Vector4 value2) =>
        new(value1.X / value2.X, value1.Y / value2.Y, value1.Z / value2.Z, value1.W / value2.W);

    public static Vector4 Divide(Vector4 value1, float divider)
    {
        float factor = 1f / divider;
        return new(value1.X * factor, value1.Y * factor, value1.Z * factor, value1.W * factor);
    }

    public static Vector4 Negate(Vector4 value) => new(-value.X, -value.Y, -value.Z, -value.W);

    public static float Dot(Vector4 vector1, Vector4 vector2) => FnaMathCore.Vector4Dot(vector1, vector2);

    public static float Distance(Vector4 value1, Vector4 value2) => FnaMathCore.Vector4Distance(value1, value2);

    public static float DistanceSquared(Vector4 value1, Vector4 value2) => FnaMathCore.Vector4DistanceSquared(value1, value2);

    public readonly float Length() => MathF.Sqrt((X * X) + (Y * Y) + (Z * Z) + (W * W));

    public readonly float LengthSquared() => (X * X) + (Y * Y) + (Z * Z) + (W * W);

    public void Normalize() => this = FnaMathCore.Vector4Normalize(this);

    public static Vector4 Normalize(Vector4 vector) => FnaMathCore.Vector4Normalize(vector);

    public static Vector4 Max(Vector4 value1, Vector4 value2) => FnaMathCore.Vector4Max(value1, value2);

    public static Vector4 Min(Vector4 value1, Vector4 value2) => FnaMathCore.Vector4Min(value1, value2);

    public static Vector4 Transform(Vector2 position, Matrix matrix) => FnaMathCore.Vector4Transform(position, matrix);

    public static Vector4 Transform(Vector3 position, Matrix matrix) => FnaMathCore.Vector4Transform(position, matrix);

    public static Vector4 Transform(Vector4 vector, Matrix matrix) => FnaMathCore.Vector4Transform(vector, matrix);

    public static Vector4 Transform(Vector2 value, Quaternion rotation) => FnaMathCore.Vector4Transform(value, rotation);

    public static Vector4 Transform(Vector3 value, Quaternion rotation) => FnaMathCore.Vector4Transform(value, rotation);

    public static Vector4 Transform(Vector4 value, Quaternion rotation) => FnaMathCore.Vector4Transform(value, rotation);
}
