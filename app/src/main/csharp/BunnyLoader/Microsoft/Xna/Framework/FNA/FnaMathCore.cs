/* FNA - XNA4 Reimplementation for Desktop Platforms
 * Copyright 2009-2024 Ethan Lee and the MonoGame Team
 *
 * Released under the Microsoft Public License.
 * See LICENSE for details.
 */

/* Derived from code by the Mono.Xna Team (Copyright 2006).
 * Released under the MIT License. See monoxna.LICENSE for details.
 */

#nullable disable

using System;

namespace Microsoft.Xna.Framework;

internal static class FnaMathCore
{
    internal static Vector2 Vector2Transform(Vector2 position, Matrix matrix)
    {
        return new Vector2(
        (position.X * matrix.M11) + (position.Y * matrix.M21) + matrix.M41,
        (position.X * matrix.M12) + (position.Y * matrix.M22) + matrix.M42
        );
    }

    internal static void Vector2Transform(ref Vector2 position, ref Matrix matrix, out Vector2 result)
    {
        float x = (position.X * matrix.M11) + (position.Y * matrix.M21) + matrix.M41;
        float y = (position.X * matrix.M12) + (position.Y * matrix.M22) + matrix.M42;
        result.X = x;
        result.Y = y;
    }

    internal static Vector2 Vector2Transform(Vector2 value, Quaternion rotation)
    {
        Vector2Transform(ref value, ref rotation, out value);
        return value;
    }

    internal static void Vector2Transform(ref Vector2 value, ref Quaternion rotation, out Vector2 result)
    {
        float x = 2 * -(rotation.Z * value.Y);
        float y = 2 * (rotation.Z * value.X);
        float z = 2 * (rotation.X * value.Y - rotation.Y * value.X);
        result.X = value.X + x * rotation.W + (rotation.Y * z - rotation.Z * y);
        result.Y = value.Y + y * rotation.W + (rotation.Z * x - rotation.X * z);
    }

    internal static Vector2 Vector2TransformNormal(Vector2 normal, Matrix matrix)
    {
        return new Vector2(
        (normal.X * matrix.M11) + (normal.Y * matrix.M21),
        (normal.X * matrix.M12) + (normal.Y * matrix.M22)
        );
    }

    internal static void Vector2TransformNormal(ref Vector2 normal, ref Matrix matrix, out Vector2 result)
    {
        float x = (normal.X * matrix.M11) + (normal.Y * matrix.M21);
        float y = (normal.X * matrix.M12) + (normal.Y * matrix.M22);
        result.X = x;
        result.Y = y;
    }

    internal static Vector2 Vector2Reflect(Vector2 vector, Vector2 normal)
    {
        Vector2 result;
        float val = 2.0f * ((vector.X * normal.X) + (vector.Y * normal.Y));
        result.X = vector.X - (normal.X * val);
        result.Y = vector.Y - (normal.Y * val);
        return result;
    }

    internal static void Vector2Reflect(ref Vector2 vector, ref Vector2 normal, out Vector2 result)
    {
        float val = 2.0f * ((vector.X * normal.X) + (vector.Y * normal.Y));
        result.X = vector.X - (normal.X * val);
        result.Y = vector.Y - (normal.Y * val);
    }

    internal static Vector2 Vector2Normalize(Vector2 value)
    {
        float val = 1.0f / (float) Math.Sqrt((value.X * value.X) + (value.Y * value.Y));
        value.X *= val;
        value.Y *= val;
        return value;
    }

    internal static void Vector2Normalize(ref Vector2 value, out Vector2 result)
    {
        float val = 1.0f / (float) Math.Sqrt((value.X * value.X) + (value.Y * value.Y));
        result.X = value.X * val;
        result.Y = value.Y * val;
    }

    internal static Vector2 Vector2SmoothStep(Vector2 value1, Vector2 value2, float amount)
    {
        return new Vector2(
        MathHelperSmoothStep(value1.X, value2.X, amount),
        MathHelperSmoothStep(value1.Y, value2.Y, amount)
        );
    }

    internal static void Vector2SmoothStep(ref Vector2 value1, ref Vector2 value2, float amount, out Vector2 result)
    {
        result.X = MathHelperSmoothStep(value1.X, value2.X, amount);
        result.Y = MathHelperSmoothStep(value1.Y, value2.Y, amount);
    }

    internal static Vector2 Vector2Barycentric(Vector2 value1, Vector2 value2, Vector2 value3, float amount1, float amount2)
    {
        return new Vector2(
        MathHelperBarycentric(value1.X, value2.X, value3.X, amount1, amount2),
        MathHelperBarycentric(value1.Y, value2.Y, value3.Y, amount1, amount2)
        );
    }

    internal static void Vector2Barycentric(ref Vector2 value1, ref Vector2 value2, ref Vector2 value3, float amount1, float amount2, out Vector2 result)
    {
        result.X = MathHelperBarycentric(value1.X, value2.X, value3.X, amount1, amount2);
        result.Y = MathHelperBarycentric(value1.Y, value2.Y, value3.Y, amount1, amount2);
    }

    internal static Vector2 Vector2Clamp(Vector2 value1, Vector2 min, Vector2 max)
    {
        return new Vector2(
        MathHelperClamp(value1.X, min.X, max.X),
        MathHelperClamp(value1.Y, min.Y, max.Y)
        );
    }

    internal static void Vector2Clamp(ref Vector2 value1, ref Vector2 min, ref Vector2 max, out Vector2 result)
    {
        result.X = MathHelperClamp(value1.X, min.X, max.X);
        result.Y = MathHelperClamp(value1.Y, min.Y, max.Y);
    }

    internal static Vector2 Vector2Lerp(Vector2 value1, Vector2 value2, float amount)
    {
        return new Vector2(
        MathHelperLerp(value1.X, value2.X, amount),
        MathHelperLerp(value1.Y, value2.Y, amount)
        );
    }

    internal static void Vector2Lerp(ref Vector2 value1, ref Vector2 value2, float amount, out Vector2 result)
    {
        result.X = MathHelperLerp(value1.X, value2.X, amount);
        result.Y = MathHelperLerp(value1.Y, value2.Y, amount);
    }

    internal static Vector2 Vector2Max(Vector2 value1, Vector2 value2)
    {
        return new Vector2(
        value1.X > value2.X ? value1.X : value2.X,
        value1.Y > value2.Y ? value1.Y : value2.Y
        );
    }

    internal static void Vector2Max(ref Vector2 value1, ref Vector2 value2, out Vector2 result)
    {
        result.X = value1.X > value2.X ? value1.X : value2.X;
        result.Y = value1.Y > value2.Y ? value1.Y : value2.Y;
    }

    internal static Vector2 Vector2Min(Vector2 value1, Vector2 value2)
    {
        return new Vector2(
        value1.X < value2.X ? value1.X : value2.X,
        value1.Y < value2.Y ? value1.Y : value2.Y
        );
    }

    internal static void Vector2Min(ref Vector2 value1, ref Vector2 value2, out Vector2 result)
    {
        result.X = value1.X < value2.X ? value1.X : value2.X;
        result.Y = value1.Y < value2.Y ? value1.Y : value2.Y;
    }

    internal static float Vector2Distance(Vector2 value1, Vector2 value2)
    {
        float v1 = value1.X - value2.X, v2 = value1.Y - value2.Y;
        return (float) Math.Sqrt((v1 * v1) + (v2 * v2));
    }

    internal static void Vector2Distance(ref Vector2 value1, ref Vector2 value2, out float result)
    {
        float v1 = value1.X - value2.X, v2 = value1.Y - value2.Y;
        result = (float) Math.Sqrt((v1 * v1) + (v2 * v2));
    }

    internal static float Vector2DistanceSquared(Vector2 value1, Vector2 value2)
    {
        float v1 = value1.X - value2.X, v2 = value1.Y - value2.Y;
        return (v1 * v1) + (v2 * v2);
    }

    internal static void Vector2DistanceSquared(ref Vector2 value1, ref Vector2 value2, out float result)
    {
        float v1 = value1.X - value2.X, v2 = value1.Y - value2.Y;
        result = (v1 * v1) + (v2 * v2);
    }

    internal static float Vector2Dot(Vector2 value1, Vector2 value2)
    {
        return (value1.X * value2.X) + (value1.Y * value2.Y);
    }

    internal static void Vector2Dot(ref Vector2 value1, ref Vector2 value2, out float result)
    {
        result = (value1.X * value2.X) + (value1.Y * value2.Y);
    }

    internal static Vector3 Vector3Transform(Vector3 position, Matrix matrix)
    {
        Vector3Transform(ref position, ref matrix, out position);
        return position;
    }

    internal static void Vector3Transform(ref Vector3 position, ref Matrix matrix, out Vector3 result)
    {
        float x = (
        (position.X * matrix.M11) +
        (position.Y * matrix.M21) +
        (position.Z * matrix.M31) +
        matrix.M41
        );
        float y = (
        (position.X * matrix.M12) +
        (position.Y * matrix.M22) +
        (position.Z * matrix.M32) +
        matrix.M42
        );
        float z = (
        (position.X * matrix.M13) +
        (position.Y * matrix.M23) +
        (position.Z * matrix.M33) +
        matrix.M43
        );
        result.X = x;
        result.Y = y;
        result.Z = z;
    }

    internal static Vector3 Vector3Transform(Vector3 value, Quaternion rotation)
    {
        Vector3 result;
        Vector3Transform(ref value, ref rotation, out result);
        return result;
    }

    internal static void Vector3Transform(ref Vector3 value, ref Quaternion rotation, out Vector3 result)
    {
        float x = 2 * (rotation.Y * value.Z - rotation.Z * value.Y);
        float y = 2 * (rotation.Z * value.X - rotation.X * value.Z);
        float z = 2 * (rotation.X * value.Y - rotation.Y * value.X);
        result.X = value.X + x * rotation.W + (rotation.Y * z - rotation.Z * y);
        result.Y = value.Y + y * rotation.W + (rotation.Z * x - rotation.X * z);
        result.Z = value.Z + z * rotation.W + (rotation.X * y - rotation.Y * x);
    }

    internal static Vector3 Vector3TransformNormal(Vector3 normal, Matrix matrix)
    {
        Vector3TransformNormal(ref normal, ref matrix, out normal);
        return normal;
    }

    internal static void Vector3TransformNormal(ref Vector3 normal, ref Matrix matrix, out Vector3 result)
    {
        float x = (normal.X * matrix.M11) + (normal.Y * matrix.M21) + (normal.Z * matrix.M31);
        float y = (normal.X * matrix.M12) + (normal.Y * matrix.M22) + (normal.Z * matrix.M32);
        float z = (normal.X * matrix.M13) + (normal.Y * matrix.M23) + (normal.Z * matrix.M33);
        result.X = x;
        result.Y = y;
        result.Z = z;
    }

    internal static Vector3 Vector3Reflect(Vector3 vector, Vector3 normal)
    {
        Vector3 reflectedVector;
        float dotProduct = ((vector.X * normal.X) + (vector.Y * normal.Y)) +
        (vector.Z * normal.Z);
        reflectedVector.X = vector.X - (2.0f * normal.X) * dotProduct;
        reflectedVector.Y = vector.Y - (2.0f * normal.Y) * dotProduct;
        reflectedVector.Z = vector.Z - (2.0f * normal.Z) * dotProduct;
        return reflectedVector;
    }

    internal static void Vector3Reflect(ref Vector3 vector, ref Vector3 normal, out Vector3 result)
    {
        float dotProduct = ((vector.X * normal.X) + (vector.Y * normal.Y)) +
        (vector.Z * normal.Z);
        result.X = vector.X - (2.0f * normal.X) * dotProduct;
        result.Y = vector.Y - (2.0f * normal.Y) * dotProduct;
        result.Z = vector.Z - (2.0f * normal.Z) * dotProduct;
    }

    internal static Vector3 Vector3Normalize(Vector3 value)
    {
        float factor = 1.0f / (float) Math.Sqrt(
        (value.X * value.X) +
        (value.Y * value.Y) +
        (value.Z * value.Z)
        );
        return new Vector3(
        value.X * factor,
        value.Y * factor,
        value.Z * factor
        );
    }

    internal static void Vector3Normalize(ref Vector3 value, out Vector3 result)
    {
        float factor = 1.0f / (float) Math.Sqrt(
        (value.X * value.X) +
        (value.Y * value.Y) +
        (value.Z * value.Z)
        );
        result.X = value.X * factor;
        result.Y = value.Y * factor;
        result.Z = value.Z * factor;
    }

    internal static Vector3 Vector3SmoothStep(Vector3 value1, Vector3 value2, float amount)
    {
        return new Vector3(
        MathHelperSmoothStep(value1.X, value2.X, amount),
        MathHelperSmoothStep(value1.Y, value2.Y, amount),
        MathHelperSmoothStep(value1.Z, value2.Z, amount)
        );
    }

    internal static void Vector3SmoothStep(ref Vector3 value1, ref Vector3 value2, float amount, out Vector3 result)
    {
        result.X = MathHelperSmoothStep(value1.X, value2.X, amount);
        result.Y = MathHelperSmoothStep(value1.Y, value2.Y, amount);
        result.Z = MathHelperSmoothStep(value1.Z, value2.Z, amount);
    }

    internal static Vector3 Vector3Cross(Vector3 vector1, Vector3 vector2)
    {
        Vector3Cross(ref vector1, ref vector2, out vector1);
        return vector1;
    }

    internal static void Vector3Cross(ref Vector3 vector1, ref Vector3 vector2, out Vector3 result)
    {
        float x = vector1.Y * vector2.Z - vector2.Y * vector1.Z;
        float y = -(vector1.X * vector2.Z - vector2.X * vector1.Z);
        float z = vector1.X * vector2.Y - vector2.X * vector1.Y;
        result.X = x;
        result.Y = y;
        result.Z = z;
    }

    internal static Vector3 Vector3Lerp(Vector3 value1, Vector3 value2, float amount)
    {
        return new Vector3(
        MathHelperLerp(value1.X, value2.X, amount),
        MathHelperLerp(value1.Y, value2.Y, amount),
        MathHelperLerp(value1.Z, value2.Z, amount)
        );
    }

    internal static void Vector3Lerp(ref Vector3 value1, ref Vector3 value2, float amount, out Vector3 result)
    {
        result.X = MathHelperLerp(value1.X, value2.X, amount);
        result.Y = MathHelperLerp(value1.Y, value2.Y, amount);
        result.Z = MathHelperLerp(value1.Z, value2.Z, amount);
    }

    internal static Vector3 Vector3Max(Vector3 value1, Vector3 value2)
    {
        return new Vector3(
        MathHelperMax(value1.X, value2.X),
        MathHelperMax(value1.Y, value2.Y),
        MathHelperMax(value1.Z, value2.Z)
        );
    }

    internal static void Vector3Max(ref Vector3 value1, ref Vector3 value2, out Vector3 result)
    {
        result.X = MathHelperMax(value1.X, value2.X);
        result.Y = MathHelperMax(value1.Y, value2.Y);
        result.Z = MathHelperMax(value1.Z, value2.Z);
    }

    internal static Vector3 Vector3Min(Vector3 value1, Vector3 value2)
    {
        return new Vector3(
        MathHelperMin(value1.X, value2.X),
        MathHelperMin(value1.Y, value2.Y),
        MathHelperMin(value1.Z, value2.Z)
        );
    }

    internal static void Vector3Min(ref Vector3 value1, ref Vector3 value2, out Vector3 result)
    {
        result.X = MathHelperMin(value1.X, value2.X);
        result.Y = MathHelperMin(value1.Y, value2.Y);
        result.Z = MathHelperMin(value1.Z, value2.Z);
    }

    internal static float Vector3Distance(Vector3 vector1, Vector3 vector2)
    {
        float result;
        Vector3DistanceSquared(ref vector1, ref vector2, out result);
        return (float) Math.Sqrt(result);
    }

    internal static void Vector3Distance(ref Vector3 value1, ref Vector3 value2, out float result)
    {
        Vector3DistanceSquared(ref value1, ref value2, out result);
        result = (float) Math.Sqrt(result);
    }

    internal static float Vector3DistanceSquared(Vector3 value1, Vector3 value2)
    {
        return (
        (value1.X - value2.X) * (value1.X - value2.X) +
        (value1.Y - value2.Y) * (value1.Y - value2.Y) +
        (value1.Z - value2.Z) * (value1.Z - value2.Z)
        );
    }

    internal static void Vector3DistanceSquared(ref Vector3 value1, ref Vector3 value2, out float result)
    {
        result = (
        (value1.X - value2.X) * (value1.X - value2.X) +
        (value1.Y - value2.Y) * (value1.Y - value2.Y) +
        (value1.Z - value2.Z) * (value1.Z - value2.Z)
        );
    }

    internal static float Vector3Dot(Vector3 vector1, Vector3 vector2)
    {
        return vector1.X * vector2.X + vector1.Y * vector2.Y + vector1.Z * vector2.Z;
    }

    internal static void Vector3Dot(ref Vector3 vector1, ref Vector3 vector2, out float result)
    {
        result = (
        (vector1.X * vector2.X) +
        (vector1.Y * vector2.Y) +
        (vector1.Z * vector2.Z)
        );
    }

    internal static Vector4 Vector4Transform(Vector2 position, Matrix matrix)
    {
        Vector4 result;
        Vector4Transform(ref position, ref matrix, out result);
        return result;
    }

    internal static Vector4 Vector4Transform(Vector3 position, Matrix matrix)
    {
        Vector4 result;
        Vector4Transform(ref position, ref matrix, out result);
        return result;
    }

    internal static Vector4 Vector4Transform(Vector4 vector, Matrix matrix)
    {
        Vector4Transform(ref vector, ref matrix, out vector);
        return vector;
    }

    internal static void Vector4Transform(ref Vector2 position, ref Matrix matrix, out Vector4 result)
    {
        result = new Vector4(
        (position.X * matrix.M11) + (position.Y * matrix.M21) + matrix.M41,
        (position.X * matrix.M12) + (position.Y * matrix.M22) + matrix.M42,
        (position.X * matrix.M13) + (position.Y * matrix.M23) + matrix.M43,
        (position.X * matrix.M14) + (position.Y * matrix.M24) + matrix.M44
        );
    }

    internal static void Vector4Transform(ref Vector3 position, ref Matrix matrix, out Vector4 result)
    {
        float x = (
        (position.X * matrix.M11) +
        (position.Y * matrix.M21) +
        (position.Z * matrix.M31) +
        matrix.M41
        );
        float y = (
        (position.X * matrix.M12) +
        (position.Y * matrix.M22) +
        (position.Z * matrix.M32) +
        matrix.M42
        );
        float z = (
        (position.X * matrix.M13) +
        (position.Y * matrix.M23) +
        (position.Z * matrix.M33) +
        matrix.M43
        );
        float w = (
        (position.X * matrix.M14) +
        (position.Y * matrix.M24) +
        (position.Z * matrix.M34) +
        matrix.M44
        );
        result.X = x;
        result.Y = y;
        result.Z = z;
        result.W = w;
    }

    internal static void Vector4Transform(ref Vector4 vector, ref Matrix matrix, out Vector4 result)
    {
        float x = (
        (vector.X * matrix.M11) +
        (vector.Y * matrix.M21) +
        (vector.Z * matrix.M31) +
        (vector.W * matrix.M41)
        );
        float y = (
        (vector.X * matrix.M12) +
        (vector.Y * matrix.M22) +
        (vector.Z * matrix.M32) +
        (vector.W * matrix.M42)
        );
        float z = (
        (vector.X * matrix.M13) +
        (vector.Y * matrix.M23) +
        (vector.Z * matrix.M33) +
        (vector.W * matrix.M43)
        );
        float w = (
        (vector.X * matrix.M14) +
        (vector.Y * matrix.M24) +
        (vector.Z * matrix.M34) +
        (vector.W * matrix.M44)
        );
        result.X = x;
        result.Y = y;
        result.Z = z;
        result.W = w;
    }

    internal static Vector4 Vector4Transform(Vector2 value, Quaternion rotation)
    {
        Vector4 result;
        Vector4Transform(ref value, ref rotation, out result);
        return result;
    }

    internal static Vector4 Vector4Transform(Vector3 value, Quaternion rotation)
    {
        Vector4 result;
        Vector4Transform(ref value, ref rotation, out result);
        return result;
    }

    internal static Vector4 Vector4Transform(Vector4 value, Quaternion rotation)
    {
        Vector4 result;
        Vector4Transform(ref value, ref rotation, out result);
        return result;
    }

    internal static void Vector4Transform(ref Vector2 value, ref Quaternion rotation, out Vector4 result)
    {
        double xx = rotation.X + rotation.X;
        double yy = rotation.Y + rotation.Y;
        double zz = rotation.Z + rotation.Z;
        double wxx = rotation.W * xx;
        double wyy = rotation.W * yy;
        double wzz = rotation.W * zz;
        double xxx = rotation.X * xx;
        double xyy = rotation.X * yy;
        double xzz = rotation.X * zz;
        double yyy = rotation.Y * yy;
        double yzz = rotation.Y * zz;
        double zzz = rotation.Z * zz;
        result.X = (float) (
        (double) value.X * (1.0 - yyy - zzz) +
        (double) value.Y * (xyy - wzz)
        );
        result.Y = (float) (
        (double) value.X * (xyy + wzz) +
        (double) value.Y * (1.0 - xxx - zzz)
        );
        result.Z = (float) (
        (double) value.X * (xzz - wyy) +
        (double) value.Y * (yzz + wxx)
        );
        result.W = 1.0f;
    }

    internal static void Vector4Transform(ref Vector3 value, ref Quaternion rotation, out Vector4 result)
    {
        double xx = rotation.X + rotation.X;
        double yy = rotation.Y + rotation.Y;
        double zz = rotation.Z + rotation.Z;
        double wxx = rotation.W * xx;
        double wyy = rotation.W * yy;
        double wzz = rotation.W * zz;
        double xxx = rotation.X * xx;
        double xyy = rotation.X * yy;
        double xzz = rotation.X * zz;
        double yyy = rotation.Y * yy;
        double yzz = rotation.Y * zz;
        double zzz = rotation.Z * zz;
        result.X = (float) (
        (double) value.X * (1.0 - yyy - zzz) +
        (double) value.Y * (xyy - wzz) +
        (double) value.Z * (xzz + wyy)
        );
        result.Y = (float) (
        (double) value.X * (xyy + wzz) +
        (double) value.Y * (1.0 - xxx - zzz) +
        (double) value.Z * (yzz - wxx)
        );
        result.Z = (float) (
        (double) value.X * (xzz - wyy) +
        (double) value.Y * (yzz + wxx) +
        (double) value.Z * (1.0 - xxx - yyy)
        );
        result.W = 1.0f;
    }

    internal static void Vector4Transform(ref Vector4 value, ref Quaternion rotation, out Vector4 result)
    {
        double xx = rotation.X + rotation.X;
        double yy = rotation.Y + rotation.Y;
        double zz = rotation.Z + rotation.Z;
        double wxx = rotation.W * xx;
        double wyy = rotation.W * yy;
        double wzz = rotation.W * zz;
        double xxx = rotation.X * xx;
        double xyy = rotation.X * yy;
        double xzz = rotation.X * zz;
        double yyy = rotation.Y * yy;
        double yzz = rotation.Y * zz;
        double zzz = rotation.Z * zz;
        result.X = (float) (
        (double) value.X * (1.0 - yyy - zzz) +
        (double) value.Y * (xyy - wzz) +
        (double) value.Z * (xzz + wyy)
        );
        result.Y = (float) (
        (double) value.X * (xyy + wzz) +
        (double) value.Y * (1.0 - xxx - zzz) +
        (double) value.Z * (yzz - wxx)
        );
        result.Z = (float) (
        (double) value.X * (xzz - wyy) +
        (double) value.Y * (yzz + wxx) +
        (double) value.Z * (1.0 - xxx - yyy)
        );
        result.W = value.W;
    }

    internal static Vector4 Vector4Normalize(Vector4 vector)
    {
        float factor = 1.0f / (float) Math.Sqrt(
        (vector.X * vector.X) +
        (vector.Y * vector.Y) +
        (vector.Z * vector.Z) +
        (vector.W * vector.W)
        );
        return new Vector4(
        vector.X * factor,
        vector.Y * factor,
        vector.Z * factor,
        vector.W * factor
        );
    }

    internal static void Vector4Normalize(ref Vector4 vector, out Vector4 result)
    {
        float factor = 1.0f / (float) Math.Sqrt(
        (vector.X * vector.X) +
        (vector.Y * vector.Y) +
        (vector.Z * vector.Z) +
        (vector.W * vector.W)
        );
        result.X = vector.X * factor;
        result.Y = vector.Y * factor;
        result.Z = vector.Z * factor;
        result.W = vector.W * factor;
    }

    internal static Vector4 Vector4Max(Vector4 value1, Vector4 value2)
    {
        return new Vector4(
        MathHelperMax(value1.X, value2.X),
        MathHelperMax(value1.Y, value2.Y),
        MathHelperMax(value1.Z, value2.Z),
        MathHelperMax(value1.W, value2.W)
        );
    }

    internal static void Vector4Max(ref Vector4 value1, ref Vector4 value2, out Vector4 result)
    {
        result.X = MathHelperMax(value1.X, value2.X);
        result.Y = MathHelperMax(value1.Y, value2.Y);
        result.Z = MathHelperMax(value1.Z, value2.Z);
        result.W = MathHelperMax(value1.W, value2.W);
    }

    internal static Vector4 Vector4Min(Vector4 value1, Vector4 value2)
    {
        return new Vector4(
        MathHelperMin(value1.X, value2.X),
        MathHelperMin(value1.Y, value2.Y),
        MathHelperMin(value1.Z, value2.Z),
        MathHelperMin(value1.W, value2.W)
        );
    }

    internal static void Vector4Min(ref Vector4 value1, ref Vector4 value2, out Vector4 result)
    {
        result.X = MathHelperMin(value1.X, value2.X);
        result.Y = MathHelperMin(value1.Y, value2.Y);
        result.Z = MathHelperMin(value1.Z, value2.Z);
        result.W = MathHelperMin(value1.W, value2.W);
    }

    internal static float Vector4Distance(Vector4 value1, Vector4 value2)
    {
        return (float) Math.Sqrt(Vector4DistanceSquared(value1, value2));
    }

    internal static void Vector4Distance(ref Vector4 value1, ref Vector4 value2, out float result)
    {
        result = (float) Math.Sqrt(Vector4DistanceSquared(value1, value2));
    }

    internal static float Vector4DistanceSquared(Vector4 value1, Vector4 value2)
    {
        return (
        (value1.W - value2.W) * (value1.W - value2.W) +
        (value1.X - value2.X) * (value1.X - value2.X) +
        (value1.Y - value2.Y) * (value1.Y - value2.Y) +
        (value1.Z - value2.Z) * (value1.Z - value2.Z)
        );
    }

    internal static void Vector4DistanceSquared(ref Vector4 value1, ref Vector4 value2, out float result)
    {
        result = (
        (value1.W - value2.W) * (value1.W - value2.W) +
        (value1.X - value2.X) * (value1.X - value2.X) +
        (value1.Y - value2.Y) * (value1.Y - value2.Y) +
        (value1.Z - value2.Z) * (value1.Z - value2.Z)
        );
    }

    internal static float Vector4Dot(Vector4 vector1, Vector4 vector2)
    {
        return (
        vector1.X * vector2.X +
        vector1.Y * vector2.Y +
        vector1.Z * vector2.Z +
        vector1.W * vector2.W
        );
    }

    internal static void Vector4Dot(ref Vector4 vector1, ref Vector4 vector2, out float result)
    {
        result = (
        (vector1.X * vector2.X) +
        (vector1.Y * vector2.Y) +
        (vector1.Z * vector2.Z) +
        (vector1.W * vector2.W)
        );
    }

    internal static Matrix MatrixCreateShadow(Vector3 lightDirection, Plane plane)
    {
        Matrix result;
        MatrixCreateShadow(ref lightDirection, ref plane, out result);
        return result;
    }

    internal static void MatrixCreateShadow(ref Vector3 lightDirection, ref Plane plane, out Matrix result)
    {
        float dot = (
        (plane.Normal.X * lightDirection.X) +
        (plane.Normal.Y * lightDirection.Y) +
        (plane.Normal.Z * lightDirection.Z)
        );
        float x = -plane.Normal.X;
        float y = -plane.Normal.Y;
        float z = -plane.Normal.Z;
        float d = -plane.D;
        result.M11 = (x * lightDirection.X) + dot;
        result.M12 = x * lightDirection.Y;
        result.M13 = x * lightDirection.Z;
        result.M14 = 0;
        result.M21 = y * lightDirection.X;
        result.M22 = (y * lightDirection.Y) + dot;
        result.M23 = y * lightDirection.Z;
        result.M24 = 0;
        result.M31 = z * lightDirection.X;
        result.M32 = z * lightDirection.Y;
        result.M33 = (z * lightDirection.Z) + dot;
        result.M34 = 0;
        result.M41 = d * lightDirection.X;
        result.M42 = d * lightDirection.Y;
        result.M43 = d * lightDirection.Z;
        result.M44 = dot;
    }

    internal static Matrix MatrixCreateReflection(Plane value)
    {
        Matrix result;
        MatrixCreateReflection(ref value, out result);
        return result;
    }

    internal static void MatrixCreateReflection(ref Plane value, out Matrix result)
    {
        Plane plane;
        PlaneNormalize(ref value, out plane);
        float x = plane.Normal.X;
        float y = plane.Normal.Y;
        float z = plane.Normal.Z;
        float num3 = -2f * x;
        float num2 = -2f * y;
        float num = -2f * z;
        result.M11 = (num3 * x) + 1f;
        result.M12 = num2 * x;
        result.M13 = num * x;
        result.M14 = 0;
        result.M21 = num3 * y;
        result.M22 = (num2 * y) + 1;
        result.M23 = num * y;
        result.M24 = 0;
        result.M31 = num3 * z;
        result.M32 = num2 * z;
        result.M33 = (num * z) + 1;
        result.M34 = 0;
        result.M41 = num3 * plane.D;
        result.M42 = num2 * plane.D;
        result.M43 = num * plane.D;
        result.M44 = 1;
    }

    internal static Matrix MatrixCreateFromYawPitchRoll(float yaw, float pitch, float roll)
    {
        Matrix matrix;
        MatrixCreateFromYawPitchRoll(yaw, pitch, roll, out matrix);
        return matrix;
    }

    internal static void MatrixCreateFromYawPitchRoll(float yaw, float pitch, float roll, out Matrix result)
    {
        Quaternion quaternion;
        QuaternionCreateFromYawPitchRoll(yaw, pitch, roll, out quaternion);
        MatrixCreateFromQuaternion(ref quaternion, out result);
    }

    internal static Matrix MatrixCreateFromAxisAngle(Vector3 axis, float angle)
    {
        Matrix result;
        MatrixCreateFromAxisAngle(ref axis, angle, out result);
        return result;
    }

    internal static void MatrixCreateFromAxisAngle(ref Vector3 axis, float angle, out Matrix result)
    {
        float x = axis.X;
        float y = axis.Y;
        float z = axis.Z;
        float num2 = (float) Math.Sin((double) angle);
        float num = (float) Math.Cos((double) angle);
        float num11 = x * x;
        float num10 = y * y;
        float num9 = z * z;
        float num8 = x * y;
        float num7 = x * z;
        float num6 = y * z;
        result.M11 = num11 + (num * (1f - num11));
        result.M12 = (num8 - (num * num8)) + (num2 * z);
        result.M13 = (num7 - (num * num7)) - (num2 * y);
        result.M14 = 0;
        result.M21 = (num8 - (num * num8)) - (num2 * z);
        result.M22 = num10 + (num * (1f - num10));
        result.M23 = (num6 - (num * num6)) + (num2 * x);
        result.M24 = 0;
        result.M31 = (num7 - (num * num7)) + (num2 * y);
        result.M32 = (num6 - (num * num6)) - (num2 * x);
        result.M33 = num9 + (num * (1f - num9));
        result.M34 = 0;
        result.M41 = 0;
        result.M42 = 0;
        result.M43 = 0;
        result.M44 = 1;
    }

    internal static Matrix MatrixCreateFromQuaternion(Quaternion quaternion)
    {
        Matrix result;
        MatrixCreateFromQuaternion(ref quaternion, out result);
        return result;
    }

    internal static void MatrixCreateFromQuaternion(ref Quaternion quaternion, out Matrix result)
    {
        float num9 = quaternion.X * quaternion.X;
        float num8 = quaternion.Y * quaternion.Y;
        float num7 = quaternion.Z * quaternion.Z;
        float num6 = quaternion.X * quaternion.Y;
        float num5 = quaternion.Z * quaternion.W;
        float num4 = quaternion.Z * quaternion.X;
        float num3 = quaternion.Y * quaternion.W;
        float num2 = quaternion.Y * quaternion.Z;
        float num = quaternion.X * quaternion.W;
        result.M11 = 1f - (2f * (num8 + num7));
        result.M12 = 2f * (num6 + num5);
        result.M13 = 2f * (num4 - num3);
        result.M14 = 0f;
        result.M21 = 2f * (num6 - num5);
        result.M22 = 1f - (2f * (num7 + num9));
        result.M23 = 2f * (num2 + num);
        result.M24 = 0f;
        result.M31 = 2f * (num4 + num3);
        result.M32 = 2f * (num2 - num);
        result.M33 = 1f - (2f * (num8 + num9));
        result.M34 = 0f;
        result.M41 = 0f;
        result.M42 = 0f;
        result.M43 = 0f;
        result.M44 = 1f;
    }

    internal static Matrix MatrixCreatePerspective(float width, float height, float nearPlaneDistance, float farPlaneDistance)
    {
        Matrix matrix;
        MatrixCreatePerspective(width, height, nearPlaneDistance, farPlaneDistance, out matrix);
        return matrix;
    }

    internal static void MatrixCreatePerspective(float width, float height, float nearPlaneDistance, float farPlaneDistance, out Matrix result)
    {
        if (nearPlaneDistance <= 0f)
        {
        throw new ArgumentException("nearPlaneDistance <= 0");
        }
        if (farPlaneDistance <= 0f)
        {
        throw new ArgumentException("farPlaneDistance <= 0");
        }
        if (nearPlaneDistance >= farPlaneDistance)
        {
        throw new ArgumentException("nearPlaneDistance >= farPlaneDistance");
        }
        result.M11 = (2f * nearPlaneDistance) / width;
        result.M12 = result.M13 = result.M14 = 0f;
        result.M22 = (2f * nearPlaneDistance) / height;
        result.M21 = result.M23 = result.M24 = 0f;
        result.M33 = farPlaneDistance / (nearPlaneDistance - farPlaneDistance);
        result.M31 = result.M32 = 0f;
        result.M34 = -1f;
        result.M41 = result.M42 = result.M44 = 0f;
        result.M43 = (
        (nearPlaneDistance * farPlaneDistance) /
        (nearPlaneDistance - farPlaneDistance)
        );
    }

    internal static Matrix MatrixCreatePerspectiveFieldOfView(float fieldOfView, float aspectRatio, float nearPlaneDistance, float farPlaneDistance)
    {
        Matrix result;
        MatrixCreatePerspectiveFieldOfView(
        fieldOfView,
        aspectRatio,
        nearPlaneDistance,
        farPlaneDistance,
        out result
        );
        return result;
    }

    internal static void MatrixCreatePerspectiveFieldOfView(float fieldOfView, float aspectRatio, float nearPlaneDistance, float farPlaneDistance, out Matrix result)
    {
        if ((fieldOfView <= 0f) || (fieldOfView >= 3.141593f))
        {
        throw new ArgumentException("fieldOfView <= 0 or >= PI");
        }
        if (nearPlaneDistance <= 0f)
        {
        throw new ArgumentException("nearPlaneDistance <= 0");
        }
        if (farPlaneDistance <= 0f)
        {
        throw new ArgumentException("farPlaneDistance <= 0");
        }
        if (nearPlaneDistance >= farPlaneDistance)
        {
        throw new ArgumentException("nearPlaneDistance >= farPlaneDistance");
        }
        float num = 1f / ((float) Math.Tan((double) (fieldOfView * 0.5f)));
        float num9 = num / aspectRatio;
        result.M11 = num9;
        result.M12 = result.M13 = result.M14 = 0;
        result.M22 = num;
        result.M21 = result.M23 = result.M24 = 0;
        result.M31 = result.M32 = 0f;
        result.M33 = farPlaneDistance / (nearPlaneDistance - farPlaneDistance);
        result.M34 = -1;
        result.M41 = result.M42 = result.M44 = 0;
        result.M43 = (
        (nearPlaneDistance * farPlaneDistance) /
        (nearPlaneDistance - farPlaneDistance)
        );
    }

    internal static Matrix MatrixCreatePerspectiveOffCenter(float left, float right, float bottom, float top, float nearPlaneDistance, float farPlaneDistance)
    {
        Matrix result;
        MatrixCreatePerspectiveOffCenter(
        left,
        right,
        bottom,
        top,
        nearPlaneDistance,
        farPlaneDistance,
        out result
        );
        return result;
    }

    internal static void MatrixCreatePerspectiveOffCenter(float left, float right, float bottom, float top, float nearPlaneDistance, float farPlaneDistance, out Matrix result)
    {
        if (nearPlaneDistance <= 0f)
        {
        throw new ArgumentException("nearPlaneDistance <= 0");
        }
        if (farPlaneDistance <= 0f)
        {
        throw new ArgumentException("farPlaneDistance <= 0");
        }
        if (nearPlaneDistance >= farPlaneDistance)
        {
        throw new ArgumentException("nearPlaneDistance >= farPlaneDistance");
        }
        result.M11 = (2f * nearPlaneDistance) / (right - left);
        result.M12 = result.M13 = result.M14 = 0;
        result.M22 = (2f * nearPlaneDistance) / (top - bottom);
        result.M21 = result.M23 = result.M24 = 0;
        result.M31 = (left + right) / (right - left);
        result.M32 = (top + bottom) / (top - bottom);
        result.M33 = farPlaneDistance / (nearPlaneDistance - farPlaneDistance);
        result.M34 = -1;
        result.M43 = (
        (nearPlaneDistance * farPlaneDistance) /
        (nearPlaneDistance - farPlaneDistance)
        );
        result.M41 = result.M42 = result.M44 = 0;
    }

    internal static Matrix MatrixCreateOrthographic(float width, float height, float zNearPlane, float zFarPlane)
    {
        Matrix matrix;
        MatrixCreateOrthographic(width, height, zNearPlane, zFarPlane, out matrix);
        return matrix;
    }

    internal static void MatrixCreateOrthographic(float width, float height, float zNearPlane, float zFarPlane, out Matrix result)
    {
        result.M11 = 2f / width;
        result.M12 = result.M13 = result.M14 = 0f;
        result.M22 = 2f / height;
        result.M21 = result.M23 = result.M24 = 0f;
        result.M33 = 1f / (zNearPlane - zFarPlane);
        result.M31 = result.M32 = result.M34 = 0f;
        result.M41 = result.M42 = 0f;
        result.M43 = zNearPlane / (zNearPlane - zFarPlane);
        result.M44 = 1f;
    }

    internal static Matrix MatrixCreateOrthographicOffCenter(float left, float right, float bottom, float top, float zNearPlane, float zFarPlane)
    {
        Matrix matrix;
        MatrixCreateOrthographicOffCenter(
        left,
        right,
        bottom,
        top,
        zNearPlane,
        zFarPlane,
        out matrix
        );
        return matrix;
    }

    internal static void MatrixCreateOrthographicOffCenter(float left, float right, float bottom, float top, float zNearPlane, float zFarPlane, out Matrix result)
    {
        result.M11 = (float) (2.0 / ((double) right - (double) left));
        result.M12 = 0.0f;
        result.M13 = 0.0f;
        result.M14 = 0.0f;
        result.M21 = 0.0f;
        result.M22 = (float) (2.0 / ((double) top - (double) bottom));
        result.M23 = 0.0f;
        result.M24 = 0.0f;
        result.M31 = 0.0f;
        result.M32 = 0.0f;
        result.M33 = (float) (1.0 / ((double) zNearPlane - (double) zFarPlane));
        result.M34 = 0.0f;
        result.M41 = (float) (
        ((double) left + (double) right) /
        ((double) left - (double) right)
        );
        result.M42 = (float) (
        ((double) top + (double) bottom) /
        ((double) bottom - (double) top)
        );
        result.M43 = (float) (
        (double) zNearPlane /
        ((double) zNearPlane - (double) zFarPlane)
        );
        result.M44 = 1.0f;
    }

    internal static Matrix MatrixCreateRotationX(float radians)
    {
        Matrix result;
        MatrixCreateRotationX(radians, out result);
        return result;
    }

    internal static void MatrixCreateRotationX(float radians, out Matrix result)
    {
        result = Matrix.Identity;
        float val1 = (float) Math.Cos(radians);
        float val2 = (float) Math.Sin(radians);
        result.M22 = val1;
        result.M23 = val2;
        result.M32 = -val2;
        result.M33 = val1;
    }

    internal static Matrix MatrixCreateRotationY(float radians)
    {
        Matrix result;
        MatrixCreateRotationY(radians, out result);
        return result;
    }

    internal static void MatrixCreateRotationY(float radians, out Matrix result)
    {
        result = Matrix.Identity;
        float val1 = (float) Math.Cos(radians);
        float val2 = (float) Math.Sin(radians);
        result.M11 = val1;
        result.M13 = -val2;
        result.M31 = val2;
        result.M33 = val1;
    }

    internal static Matrix MatrixCreateRotationZ(float radians)
    {
        Matrix result;
        MatrixCreateRotationZ(radians, out result);
        return result;
    }

    internal static void MatrixCreateRotationZ(float radians, out Matrix result)
    {
        result = Matrix.Identity;
        float val1 = (float) Math.Cos(radians);
        float val2 = (float) Math.Sin(radians);
        result.M11 = val1;
        result.M12 = val2;
        result.M21 = -val2;
        result.M22 = val1;
    }

    internal static Matrix MatrixInvert(Matrix matrix)
    {
        MatrixInvert(ref matrix, out matrix);
        return matrix;
    }

    internal static void MatrixInvert(ref Matrix matrix, out Matrix result)
    {
        float num1 = matrix.M11;
        float num2 = matrix.M12;
        float num3 = matrix.M13;
        float num4 = matrix.M14;
        float num5 = matrix.M21;
        float num6 = matrix.M22;
        float num7 = matrix.M23;
        float num8 = matrix.M24;
        float num9 = matrix.M31;
        float num10 = matrix.M32;
        float num11 = matrix.M33;
        float num12 = matrix.M34;
        float num13 = matrix.M41;
        float num14 = matrix.M42;
        float num15 = matrix.M43;
        float num16 = matrix.M44;
        float num17 = (float) (
        (double) num11 * (double) num16 -
        (double) num12 * (double) num15
        );
        float num18 = (float) (
        (double) num10 * (double) num16 -
        (double) num12 * (double) num14
        );
        float num19 = (float) (
        (double) num10 * (double) num15 -
        (double) num11 * (double) num14
        );
        float num20 = (float) (
        (double) num9 * (double) num16 -
        (double) num12 * (double) num13
        );
        float num21 = (float) (
        (double) num9 * (double) num15 -
        (double) num11 * (double) num13
        );
        float num22 = (float) (
        (double) num9 * (double) num14 -
        (double) num10 * (double) num13
        );
        float num23 = (float) (
        (double) num6 * (double) num17 -
        (double) num7 * (double) num18 +
        (double) num8 * (double) num19
        );
        float num24 = (float) -(
        (double) num5 * (double) num17 -
        (double) num7 * (double) num20 +
        (double) num8 * (double) num21
        );
        float num25 = (float) (
        (double) num5 * (double) num18 -
        (double) num6 * (double) num20 +
        (double) num8 * (double) num22
        );
        float num26 = (float) -(
        (double) num5 * (double) num19 -
        (double) num6 * (double) num21 +
        (double) num7 * (double) num22
        );
        float num27 = (float) (
        1.0 / (
        (double) num1 * (double) num23 +
        (double) num2 * (double) num24 +
        (double) num3 * (double) num25 +
        (double) num4 * (double) num26
        )
        );
        result.M11 = num23 * num27;
        result.M21 = num24 * num27;
        result.M31 = num25 * num27;
        result.M41 = num26 * num27;
        result.M12 = (float) (
        -(
        (double) num2 * (double) num17 -
        (double) num3 * (double) num18 +
        (double) num4 * (double) num19
        ) * num27
        );
        result.M22 = (float) (
        (
        (double) num1 * (double) num17 -
        (double) num3 * (double) num20 +
        (double) num4 * (double) num21
        ) * num27
        );
        result.M32 = (float) (
        -(
        (double) num1 * (double) num18 -
        (double) num2 * (double) num20 +
        (double) num4 * (double) num22
        ) * num27
        );
        result.M42 = (float) (
        (
        (double) num1 * (double) num19 -
        (double) num2 * (double) num21 +
        (double) num3 * (double) num22
        ) * num27
        );
        float num28 = (float) (
        (double) num7 * (double) num16 -
        (double) num8 * (double) num15
        );
        float num29 = (float) (
        (double) num6 * (double) num16 -
        (double) num8 * (double) num14
        );
        float num30 = (float) (
        (double) num6 * (double) num15 -
        (double) num7 * (double) num14
        );
        float num31 = (float) (
        (double) num5 * (double) num16 -
        (double) num8 * (double) num13
        );
        float num32 = (float) (
        (double) num5 * (double) num15 -
        (double) num7 * (double) num13
        );
        float num33 = (float) (
        (double) num5 * (double) num14 -
        (double) num6 * (double) num13
        );
        result.M13 = (float) (
        (
        (double) num2 * (double) num28 -
        (double) num3 * (double) num29 +
        (double) num4 * (double) num30
        ) * num27
        );
        result.M23 = (float) (
        -(
        (double) num1 * (double) num28 -
        (double) num3 * (double) num31 +
        (double) num4 * (double) num32
        ) * num27
        );
        result.M33 = (float) (
        (
        (double) num1 * (double) num29 -
        (double) num2 * (double) num31 +
        (double) num4 * (double) num33
        ) * num27
        );
        result.M43 = (float) (
        -(
        (double) num1 * (double) num30 -
        (double) num2 * (double) num32 +
        (double) num3 * (double) num33
        ) * num27
        );
        float num34 = (float) (
        (double) num7 * (double) num12 -
        (double) num8 * (double) num11
        );
        float num35 = (float) (
        (double) num6 * (double) num12 -
        (double) num8 * (double) num10
        );
        float num36 = (float) (
        (double) num6 * (double) num11 -
        (double) num7 * (double) num10
        );
        float num37 = (float) (
        (double) num5 * (double) num12 -
        (double) num8 * (double) num9);
        float num38 = (float) (
        (double) num5 * (double) num11 -
        (double) num7 * (double) num9
        );
        float num39 = (float) (
        (double) num5 * (double) num10 -
        (double) num6 * (double) num9
        );
        result.M14 = (float) (
        -(
        (double) num2 * (double) num34 -
        (double) num3 * (double) num35 +
        (double) num4 * (double) num36
        ) * num27
        );
        result.M24 = (float) (
        (
        (double) num1 * (double) num34 -
        (double) num3 * (double) num37 +
        (double) num4 * (double) num38
        ) * num27
        );
        result.M34 = (float) (
        -(
        (double) num1 * (double) num35 -
        (double) num2 * (double) num37 +
        (double) num4 * (double) num39
        ) * num27
        );
        result.M44 = (float) (
        (
        (double) num1 * (double) num36 -
        (double) num2 * (double) num38 +
        (double) num3 * (double) num39
        ) * num27
        );
    }

    internal static Matrix MatrixLerp(Matrix matrix1, Matrix matrix2, float amount)
    {
        matrix1.M11 = matrix1.M11 + ((matrix2.M11 - matrix1.M11) * amount);
        matrix1.M12 = matrix1.M12 + ((matrix2.M12 - matrix1.M12) * amount);
        matrix1.M13 = matrix1.M13 + ((matrix2.M13 - matrix1.M13) * amount);
        matrix1.M14 = matrix1.M14 + ((matrix2.M14 - matrix1.M14) * amount);
        matrix1.M21 = matrix1.M21 + ((matrix2.M21 - matrix1.M21) * amount);
        matrix1.M22 = matrix1.M22 + ((matrix2.M22 - matrix1.M22) * amount);
        matrix1.M23 = matrix1.M23 + ((matrix2.M23 - matrix1.M23) * amount);
        matrix1.M24 = matrix1.M24 + ((matrix2.M24 - matrix1.M24) * amount);
        matrix1.M31 = matrix1.M31 + ((matrix2.M31 - matrix1.M31) * amount);
        matrix1.M32 = matrix1.M32 + ((matrix2.M32 - matrix1.M32) * amount);
        matrix1.M33 = matrix1.M33 + ((matrix2.M33 - matrix1.M33) * amount);
        matrix1.M34 = matrix1.M34 + ((matrix2.M34 - matrix1.M34) * amount);
        matrix1.M41 = matrix1.M41 + ((matrix2.M41 - matrix1.M41) * amount);
        matrix1.M42 = matrix1.M42 + ((matrix2.M42 - matrix1.M42) * amount);
        matrix1.M43 = matrix1.M43 + ((matrix2.M43 - matrix1.M43) * amount);
        matrix1.M44 = matrix1.M44 + ((matrix2.M44 - matrix1.M44) * amount);
        return matrix1;
    }

    internal static void MatrixLerp(ref Matrix matrix1, ref Matrix matrix2, float amount, out Matrix result)
    {
        result.M11 = matrix1.M11 + ((matrix2.M11 - matrix1.M11) * amount);
        result.M12 = matrix1.M12 + ((matrix2.M12 - matrix1.M12) * amount);
        result.M13 = matrix1.M13 + ((matrix2.M13 - matrix1.M13) * amount);
        result.M14 = matrix1.M14 + ((matrix2.M14 - matrix1.M14) * amount);
        result.M21 = matrix1.M21 + ((matrix2.M21 - matrix1.M21) * amount);
        result.M22 = matrix1.M22 + ((matrix2.M22 - matrix1.M22) * amount);
        result.M23 = matrix1.M23 + ((matrix2.M23 - matrix1.M23) * amount);
        result.M24 = matrix1.M24 + ((matrix2.M24 - matrix1.M24) * amount);
        result.M31 = matrix1.M31 + ((matrix2.M31 - matrix1.M31) * amount);
        result.M32 = matrix1.M32 + ((matrix2.M32 - matrix1.M32) * amount);
        result.M33 = matrix1.M33 + ((matrix2.M33 - matrix1.M33) * amount);
        result.M34 = matrix1.M34 + ((matrix2.M34 - matrix1.M34) * amount);
        result.M41 = matrix1.M41 + ((matrix2.M41 - matrix1.M41) * amount);
        result.M42 = matrix1.M42 + ((matrix2.M42 - matrix1.M42) * amount);
        result.M43 = matrix1.M43 + ((matrix2.M43 - matrix1.M43) * amount);
        result.M44 = matrix1.M44 + ((matrix2.M44 - matrix1.M44) * amount);
    }

    internal static Matrix MatrixTransform(Matrix value, Quaternion rotation)
    {
        Matrix result;
        MatrixTransform(ref value, ref rotation, out result);
        return result;
    }

    internal static void MatrixTransform(ref Matrix value, ref Quaternion rotation, out Matrix result)
    {
        Matrix rotMatrix = MatrixCreateFromQuaternion(rotation);
        MatrixMultiply(ref value, ref rotMatrix, out result);
    }

    internal static Matrix MatrixTranspose(Matrix matrix)
    {
        Matrix ret;
        MatrixTranspose(ref matrix, out ret);
        return ret;
    }

    internal static void MatrixTranspose(ref Matrix matrix, out Matrix result)
    {
        Matrix ret;
        ret.M11 = matrix.M11;
        ret.M12 = matrix.M21;
        ret.M13 = matrix.M31;
        ret.M14 = matrix.M41;
        ret.M21 = matrix.M12;
        ret.M22 = matrix.M22;
        ret.M23 = matrix.M32;
        ret.M24 = matrix.M42;
        ret.M31 = matrix.M13;
        ret.M32 = matrix.M23;
        ret.M33 = matrix.M33;
        ret.M34 = matrix.M43;
        ret.M41 = matrix.M14;
        ret.M42 = matrix.M24;
        ret.M43 = matrix.M34;
        ret.M44 = matrix.M44;
        result = ret;
    }

    internal static Matrix MatrixMultiply(Matrix matrix1, Matrix matrix2)
    {
        float m11 = (
        (matrix1.M11 * matrix2.M11) +
        (matrix1.M12 * matrix2.M21) +
        (matrix1.M13 * matrix2.M31) +
        (matrix1.M14 * matrix2.M41)
        );
        float m12 = (
        (matrix1.M11 * matrix2.M12) +
        (matrix1.M12 * matrix2.M22) +
        (matrix1.M13 * matrix2.M32) +
        (matrix1.M14 * matrix2.M42)
        );
        float m13 = (
        (matrix1.M11 * matrix2.M13) +
        (matrix1.M12 * matrix2.M23) +
        (matrix1.M13 * matrix2.M33) +
        (matrix1.M14 * matrix2.M43)
        );
        float m14 = (
        (matrix1.M11 * matrix2.M14) +
        (matrix1.M12 * matrix2.M24) +
        (matrix1.M13 * matrix2.M34) +
        (matrix1.M14 * matrix2.M44)
        );
        float m21 = (
        (matrix1.M21 * matrix2.M11) +
        (matrix1.M22 * matrix2.M21) +
        (matrix1.M23 * matrix2.M31) +
        (matrix1.M24 * matrix2.M41)
        );
        float m22 = (
        (matrix1.M21 * matrix2.M12) +
        (matrix1.M22 * matrix2.M22) +
        (matrix1.M23 * matrix2.M32) +
        (matrix1.M24 * matrix2.M42)
        );
        float m23 = (
        (matrix1.M21 * matrix2.M13) +
        (matrix1.M22 * matrix2.M23) +
        (matrix1.M23 * matrix2.M33) +
        (matrix1.M24 * matrix2.M43)
        );
        float m24 = (
        (matrix1.M21 * matrix2.M14) +
        (matrix1.M22 * matrix2.M24) +
        (matrix1.M23 * matrix2.M34) +
        (matrix1.M24 * matrix2.M44)
        );
        float m31 = (
        (matrix1.M31 * matrix2.M11) +
        (matrix1.M32 * matrix2.M21) +
        (matrix1.M33 * matrix2.M31) +
        (matrix1.M34 * matrix2.M41)
        );
        float m32 = (
        (matrix1.M31 * matrix2.M12) +
        (matrix1.M32 * matrix2.M22) +
        (matrix1.M33 * matrix2.M32) +
        (matrix1.M34 * matrix2.M42)
        );
        float m33 = (
        (matrix1.M31 * matrix2.M13) +
        (matrix1.M32 * matrix2.M23) +
        (matrix1.M33 * matrix2.M33) +
        (matrix1.M34 * matrix2.M43)
        );
        float m34 = (
        (matrix1.M31 * matrix2.M14) +
        (matrix1.M32 * matrix2.M24) +
        (matrix1.M33 * matrix2.M34) +
        (matrix1.M34 * matrix2.M44)
        );
        float m41 = (
        (matrix1.M41 * matrix2.M11) +
        (matrix1.M42 * matrix2.M21) +
        (matrix1.M43 * matrix2.M31) +
        (matrix1.M44 * matrix2.M41)
        );
        float m42 = (
        (matrix1.M41 * matrix2.M12) +
        (matrix1.M42 * matrix2.M22) +
        (matrix1.M43 * matrix2.M32) +
        (matrix1.M44 * matrix2.M42)
        );
        float m43 = (
        (matrix1.M41 * matrix2.M13) +
        (matrix1.M42 * matrix2.M23) +
        (matrix1.M43 * matrix2.M33) +
        (matrix1.M44 * matrix2.M43)
        );
        float m44 = (
        (matrix1.M41 * matrix2.M14) +
        (matrix1.M42 * matrix2.M24) +
        (matrix1.M43 * matrix2.M34) +
        (matrix1.M44 * matrix2.M44)
        );
        matrix1.M11 = m11;
        matrix1.M12 = m12;
        matrix1.M13 = m13;
        matrix1.M14 = m14;
        matrix1.M21 = m21;
        matrix1.M22 = m22;
        matrix1.M23 = m23;
        matrix1.M24 = m24;
        matrix1.M31 = m31;
        matrix1.M32 = m32;
        matrix1.M33 = m33;
        matrix1.M34 = m34;
        matrix1.M41 = m41;
        matrix1.M42 = m42;
        matrix1.M43 = m43;
        matrix1.M44 = m44;
        return matrix1;
    }

    internal static void MatrixMultiply(ref Matrix matrix1, ref Matrix matrix2, out Matrix result)
    {
        float m11 = (
        (matrix1.M11 * matrix2.M11) +
        (matrix1.M12 * matrix2.M21) +
        (matrix1.M13 * matrix2.M31) +
        (matrix1.M14 * matrix2.M41)
        );
        float m12 = (
        (matrix1.M11 * matrix2.M12) +
        (matrix1.M12 * matrix2.M22) +
        (matrix1.M13 * matrix2.M32) +
        (matrix1.M14 * matrix2.M42)
        );
        float m13 = (
        (matrix1.M11 * matrix2.M13) +
        (matrix1.M12 * matrix2.M23) +
        (matrix1.M13 * matrix2.M33) +
        (matrix1.M14 * matrix2.M43)
        );
        float m14 = (
        (matrix1.M11 * matrix2.M14) +
        (matrix1.M12 * matrix2.M24) +
        (matrix1.M13 * matrix2.M34) +
        (matrix1.M14 * matrix2.M44)
        );
        float m21 = (
        (matrix1.M21 * matrix2.M11) +
        (matrix1.M22 * matrix2.M21) +
        (matrix1.M23 * matrix2.M31) +
        (matrix1.M24 * matrix2.M41)
        );
        float m22 = (
        (matrix1.M21 * matrix2.M12) +
        (matrix1.M22 * matrix2.M22) +
        (matrix1.M23 * matrix2.M32) +
        (matrix1.M24 * matrix2.M42)
        );
        float m23 = (
        (matrix1.M21 * matrix2.M13) +
        (matrix1.M22 * matrix2.M23) +
        (matrix1.M23 * matrix2.M33) +
        (matrix1.M24 * matrix2.M43)
        );
        float m24 = (
        (matrix1.M21 * matrix2.M14) +
        (matrix1.M22 * matrix2.M24) +
        (matrix1.M23 * matrix2.M34) +
        (matrix1.M24 * matrix2.M44)
        );
        float m31 = (
        (matrix1.M31 * matrix2.M11) +
        (matrix1.M32 * matrix2.M21) +
        (matrix1.M33 * matrix2.M31) +
        (matrix1.M34 * matrix2.M41)
        );
        float m32 = (
        (matrix1.M31 * matrix2.M12) +
        (matrix1.M32 * matrix2.M22) +
        (matrix1.M33 * matrix2.M32) +
        (matrix1.M34 * matrix2.M42)
        );
        float m33 = (
        (matrix1.M31 * matrix2.M13) +
        (matrix1.M32 * matrix2.M23) +
        (matrix1.M33 * matrix2.M33) +
        (matrix1.M34 * matrix2.M43)
        );
        float m34 = (
        (matrix1.M31 * matrix2.M14) +
        (matrix1.M32 * matrix2.M24) +
        (matrix1.M33 * matrix2.M34) +
        (matrix1.M34 * matrix2.M44)
        );
        float m41 = (
        (matrix1.M41 * matrix2.M11) +
        (matrix1.M42 * matrix2.M21) +
        (matrix1.M43 * matrix2.M31) +
        (matrix1.M44 * matrix2.M41)
        );
        float m42 = (
        (matrix1.M41 * matrix2.M12) +
        (matrix1.M42 * matrix2.M22) +
        (matrix1.M43 * matrix2.M32) +
        (matrix1.M44 * matrix2.M42)
        );
        float m43 = (
        (matrix1.M41 * matrix2.M13) +
        (matrix1.M42 * matrix2.M23) +
        (matrix1.M43 * matrix2.M33) +
        (matrix1.M44 * matrix2.M43)
        );
        float m44 = (
        (matrix1.M41 * matrix2.M14) +
        (matrix1.M42 * matrix2.M24) +
        (matrix1.M43 * matrix2.M34) +
        (matrix1.M44 * matrix2.M44)
        );
        result.M11 = m11;
        result.M12 = m12;
        result.M13 = m13;
        result.M14 = m14;
        result.M21 = m21;
        result.M22 = m22;
        result.M23 = m23;
        result.M24 = m24;
        result.M31 = m31;
        result.M32 = m32;
        result.M33 = m33;
        result.M34 = m34;
        result.M41 = m41;
        result.M42 = m42;
        result.M43 = m43;
        result.M44 = m44;
    }

    internal static Matrix MatrixMultiply(Matrix matrix1, float scaleFactor)
    {
        matrix1.M11 *= scaleFactor;
        matrix1.M12 *= scaleFactor;
        matrix1.M13 *= scaleFactor;
        matrix1.M14 *= scaleFactor;
        matrix1.M21 *= scaleFactor;
        matrix1.M22 *= scaleFactor;
        matrix1.M23 *= scaleFactor;
        matrix1.M24 *= scaleFactor;
        matrix1.M31 *= scaleFactor;
        matrix1.M32 *= scaleFactor;
        matrix1.M33 *= scaleFactor;
        matrix1.M34 *= scaleFactor;
        matrix1.M41 *= scaleFactor;
        matrix1.M42 *= scaleFactor;
        matrix1.M43 *= scaleFactor;
        matrix1.M44 *= scaleFactor;
        return matrix1;
    }

    internal static void MatrixMultiply(ref Matrix matrix1, float scaleFactor, out Matrix result)
    {
        result.M11 = matrix1.M11 * scaleFactor;
        result.M12 = matrix1.M12 * scaleFactor;
        result.M13 = matrix1.M13 * scaleFactor;
        result.M14 = matrix1.M14 * scaleFactor;
        result.M21 = matrix1.M21 * scaleFactor;
        result.M22 = matrix1.M22 * scaleFactor;
        result.M23 = matrix1.M23 * scaleFactor;
        result.M24 = matrix1.M24 * scaleFactor;
        result.M31 = matrix1.M31 * scaleFactor;
        result.M32 = matrix1.M32 * scaleFactor;
        result.M33 = matrix1.M33 * scaleFactor;
        result.M34 = matrix1.M34 * scaleFactor;
        result.M41 = matrix1.M41 * scaleFactor;
        result.M42 = matrix1.M42 * scaleFactor;
        result.M43 = matrix1.M43 * scaleFactor;
        result.M44 = matrix1.M44 * scaleFactor;
    }

    internal static Quaternion QuaternionCreateFromYawPitchRoll(float yaw, float pitch, float roll)
    {
        Quaternion quaternion;
        QuaternionCreateFromYawPitchRoll(yaw, pitch, roll, out quaternion);
        return quaternion;
    }

    internal static void QuaternionCreateFromYawPitchRoll(float yaw, float pitch, float roll, out Quaternion result)
    {
        float halfRoll = roll * 0.5f;
        float sinRoll = (float) Math.Sin(halfRoll);
        float cosRoll = (float) Math.Cos(halfRoll);
        float halfPitch = pitch * 0.5f;
        float sinPitch = (float) Math.Sin(halfPitch);
        float cosPitch = (float) Math.Cos(halfPitch);
        float halfYaw = yaw * 0.5f;
        float sinYaw = (float) Math.Sin(halfYaw);
        float cosYaw = (float) Math.Cos(halfYaw);
        result.X = ((cosYaw * sinPitch) * cosRoll) + ((sinYaw * cosPitch) * sinRoll);
        result.Y = ((sinYaw * cosPitch) * cosRoll) - ((cosYaw * sinPitch) * sinRoll);
        result.Z = ((cosYaw * cosPitch) * sinRoll) - ((sinYaw * sinPitch) * cosRoll);
        result.W = ((cosYaw * cosPitch) * cosRoll) + ((sinYaw * sinPitch) * sinRoll);
    }

    internal static Quaternion QuaternionCreateFromAxisAngle(Vector3 axis, float angle)
    {
        Quaternion quaternion;
        QuaternionCreateFromAxisAngle(ref axis, angle, out quaternion);
        return quaternion;
    }

    internal static void QuaternionCreateFromAxisAngle(ref Vector3 axis, float angle, out Quaternion result)
    {
        float half = angle * 0.5f;
        float sin = (float) Math.Sin((double) half);
        float cos = (float) Math.Cos((double) half);
        result.X = axis.X * sin;
        result.Y = axis.Y * sin;
        result.Z = axis.Z * sin;
        result.W = cos;
    }

    internal static Quaternion QuaternionCreateFromRotationMatrix(Matrix matrix)
    {
        Quaternion quaternion;
        QuaternionCreateFromRotationMatrix(ref matrix, out quaternion);
        return quaternion;
    }

    internal static void QuaternionCreateFromRotationMatrix(ref Matrix matrix, out Quaternion result)
    {
        float sqrt;
        float half;
        float scale = matrix.M11 + matrix.M22 + matrix.M33;
        if (scale > 0.0f)
        {
        sqrt = (float) Math.Sqrt(scale + 1.0f);
        result.W = sqrt * 0.5f;
        sqrt = 0.5f / sqrt;
        result.X = (matrix.M23 - matrix.M32) * sqrt;
        result.Y = (matrix.M31 - matrix.M13) * sqrt;
        result.Z = (matrix.M12 - matrix.M21) * sqrt;
        }
        else if ((matrix.M11 >= matrix.M22) && (matrix.M11 >= matrix.M33))
        {
        sqrt = (float) Math.Sqrt(1.0f + matrix.M11 - matrix.M22 - matrix.M33);
        half = 0.5f / sqrt;
        result.X = 0.5f * sqrt;
        result.Y = (matrix.M12 + matrix.M21) * half;
        result.Z = (matrix.M13 + matrix.M31) * half;
        result.W = (matrix.M23 - matrix.M32) * half;
        }
        else if (matrix.M22 > matrix.M33)
        {
        sqrt = (float) Math.Sqrt(1.0f + matrix.M22 - matrix.M11 - matrix.M33);
        half = 0.5f/sqrt;
        result.X = (matrix.M21 + matrix.M12)*half;
        result.Y = 0.5f*sqrt;
        result.Z = (matrix.M32 + matrix.M23)*half;
        result.W = (matrix.M31 - matrix.M13)*half;
        }
        else
        {
        sqrt = (float) Math.Sqrt(1.0f + matrix.M33 - matrix.M11 - matrix.M22);
        half = 0.5f / sqrt;
        result.X = (matrix.M31 + matrix.M13) * half;
        result.Y = (matrix.M32 + matrix.M23) * half;
        result.Z = 0.5f * sqrt;
        result.W = (matrix.M12 - matrix.M21) * half;
        }
    }

    internal static Quaternion QuaternionInverse(Quaternion quaternion)
    {
        Quaternion inverse;
        QuaternionInverse(ref quaternion, out inverse);
        return inverse;
    }

    internal static void QuaternionInverse(ref Quaternion quaternion, out Quaternion result)
    {
        float num2 = (
        (quaternion.X * quaternion.X) +
        (quaternion.Y * quaternion.Y) +
        (quaternion.Z * quaternion.Z) +
        (quaternion.W * quaternion.W)
        );
        float num = 1f / num2;
        result.X = -quaternion.X * num;
        result.Y = -quaternion.Y * num;
        result.Z = -quaternion.Z * num;
        result.W = quaternion.W * num;
    }

    internal static Quaternion QuaternionConcatenate(Quaternion value1, Quaternion value2)
    {
        Quaternion quaternion;
        QuaternionConcatenate(ref value1, ref value2, out quaternion);
        return quaternion;
    }

    internal static void QuaternionConcatenate(ref Quaternion value1, ref Quaternion value2, out Quaternion result)
    {
        float x1 = value1.X;
        float y1 = value1.Y;
        float z1 = value1.Z;
        float w1 = value1.W;
        float x2 = value2.X;
        float y2 = value2.Y;
        float z2 = value2.Z;
        float w2 = value2.W;
        result.X = ((x2 * w1) + (x1 * w2)) + ((y2 * z1) - (z2 * y1));
        result.Y = ((y2 * w1) + (y1 * w2)) + ((z2 * x1) - (x2 * z1));
        result.Z = ((z2 * w1) + (z1 * w2)) + ((x2 * y1) - (y2 * x1));
        result.W = (w2 * w1) - (((x2 * x1) + (y2 * y1)) + (z2 * z1));
    }

    internal static Quaternion QuaternionDivide(Quaternion quaternion1, Quaternion quaternion2)
    {
        Quaternion quaternion;
        QuaternionDivide(ref quaternion1, ref quaternion2, out quaternion);
        return quaternion;
    }

    internal static void QuaternionDivide(ref Quaternion quaternion1, ref Quaternion quaternion2, out Quaternion result)
    {
        float x = quaternion1.X;
        float y = quaternion1.Y;
        float z = quaternion1.Z;
        float w = quaternion1.W;
        float num14 = (
        (quaternion2.X * quaternion2.X) +
        (quaternion2.Y * quaternion2.Y) +
        (quaternion2.Z * quaternion2.Z) +
        (quaternion2.W * quaternion2.W)
        );
        float num5 = 1f / num14;
        float num4 = -quaternion2.X * num5;
        float num3 = -quaternion2.Y * num5;
        float num2 = -quaternion2.Z * num5;
        float num = quaternion2.W * num5;
        float num13 = (y * num2) - (z * num3);
        float num12 = (z * num4) - (x * num2);
        float num11 = (x * num3) - (y * num4);
        float num10 = ((x * num4) + (y * num3)) + (z * num2);
        result.X = ((x * num) + (num4 * w)) + num13;
        result.Y = ((y * num) + (num3 * w)) + num12;
        result.Z = ((z * num) + (num2 * w)) + num11;
        result.W = (w * num) - num10;
    }

    internal static Quaternion QuaternionMultiply(Quaternion quaternion1, Quaternion quaternion2)
    {
        Quaternion quaternion;
        QuaternionMultiply(ref quaternion1, ref quaternion2, out quaternion);
        return quaternion;
    }

    internal static Quaternion QuaternionMultiply(Quaternion quaternion1, float scaleFactor)
    {
        Quaternion quaternion;
        QuaternionMultiply(ref quaternion1, scaleFactor, out quaternion);
        return quaternion;
    }

    internal static void QuaternionMultiply(ref Quaternion quaternion1, ref Quaternion quaternion2, out Quaternion result)
    {
        float x = quaternion1.X;
        float y = quaternion1.Y;
        float z = quaternion1.Z;
        float w = quaternion1.W;
        float num4 = quaternion2.X;
        float num3 = quaternion2.Y;
        float num2 = quaternion2.Z;
        float num = quaternion2.W;
        float num12 = (y * num2) - (z * num3);
        float num11 = (z * num4) - (x * num2);
        float num10 = (x * num3) - (y * num4);
        float num9 = ((x * num4) + (y * num3)) + (z * num2);
        result.X = ((x * num) + (num4 * w)) + num12;
        result.Y = ((y * num) + (num3 * w)) + num11;
        result.Z = ((z * num) + (num2 * w)) + num10;
        result.W = (w * num) - num9;
    }

    internal static void QuaternionMultiply(ref Quaternion quaternion1, float scaleFactor, out Quaternion result)
    {
        result.X = quaternion1.X * scaleFactor;
        result.Y = quaternion1.Y * scaleFactor;
        result.Z = quaternion1.Z * scaleFactor;
        result.W = quaternion1.W * scaleFactor;
    }

    internal static Quaternion QuaternionNormalize(Quaternion quaternion)
    {
        Quaternion quaternion2;
        QuaternionNormalize(ref quaternion, out quaternion2);
        return quaternion2;
    }

    internal static void QuaternionNormalize(ref Quaternion quaternion, out Quaternion result)
    {
        float num = 1.0f / ((float) Math.Sqrt(
        (quaternion.X * quaternion.X) +
        (quaternion.Y * quaternion.Y) +
        (quaternion.Z * quaternion.Z) +
        (quaternion.W * quaternion.W)
        ));
        result.X = quaternion.X * num;
        result.Y = quaternion.Y * num;
        result.Z = quaternion.Z * num;
        result.W = quaternion.W * num;
    }

    internal static Quaternion QuaternionConjugate(Quaternion value)
    {
        return new Quaternion(-value.X, -value.Y, -value.Z, value.W);
    }

    internal static void QuaternionConjugate(ref Quaternion value, out Quaternion result)
    {
        result.X = -value.X;
        result.Y = -value.Y;
        result.Z = -value.Z;
        result.W = value.W;
    }

    internal static float QuaternionDot(Quaternion quaternion1, Quaternion quaternion2)
    {
        return (
        (quaternion1.X * quaternion2.X) +
        (quaternion1.Y * quaternion2.Y) +
        (quaternion1.Z * quaternion2.Z) +
        (quaternion1.W * quaternion2.W)
        );
    }

    internal static void QuaternionDot(ref Quaternion quaternion1, ref Quaternion quaternion2, out float result)
    {
        result = (
        (quaternion1.X * quaternion2.X) +
        (quaternion1.Y * quaternion2.Y) +
        (quaternion1.Z * quaternion2.Z) +
        (quaternion1.W * quaternion2.W)
        );
    }

    internal static Plane PlaneNormalize(Plane value)
    {
        Plane ret;
        PlaneNormalize(ref value, out ret);
        return ret;
    }

    internal static void PlaneNormalize(ref Plane value, out Plane result)
    {
        float length = value.Normal.Length();
        float factor = 1.0f / length;
        result.Normal = value.Normal * factor;
        result.D = value.D * factor;
    }

    internal static Plane PlaneTransform(Plane plane, Matrix matrix)
    {
        Plane result;
        PlaneTransform(ref plane, ref matrix, out result);
        return result;
    }

    internal static void PlaneTransform(ref Plane plane, ref Matrix matrix, out Plane result)
    {
        Matrix transformedMatrix;
        MatrixInvert(ref matrix, out transformedMatrix);
        MatrixTranspose(
        ref transformedMatrix,
        out transformedMatrix
        );
        Vector4 vector = new Vector4(plane.Normal, plane.D);
        Vector4 transformedVector;
        Vector4Transform(
        ref vector,
        ref transformedMatrix,
        out transformedVector
        );
        result = new Plane(transformedVector);
    }

    internal static Plane PlaneTransform(Plane plane, Quaternion rotation)
    {
        Plane result;
        PlaneTransform(ref plane, ref rotation, out result);
        return result;
    }

    internal static void PlaneTransform(ref Plane plane, ref Quaternion rotation, out Plane result)
    {
        Vector3Transform(
        ref plane.Normal,
        ref rotation,
        out result.Normal
        );
        result.D = plane.D;
    }

    internal static float MathHelperMax(float value1, float value2)
    {
        return value1 > value2 ? value1 : value2;
    }

    internal static float MathHelperMin(float value1, float value2)
    {
        return value1 < value2 ? value1 : value2;
    }

    internal static float MathHelperHermite(float value1, float tangent1, float value2, float tangent2, float amount)
    {
        double v1 = value1, v2 = value2, t1 = tangent1, t2 = tangent2, s = amount;
        double result;
        double sCubed = s * s * s;
        double sSquared = s * s;
        if (MathHelperWithinEpsilon(amount, 0f))
        {
        result = value1;
        }
        else if (MathHelperWithinEpsilon(amount, 1f))
        {
        result = value2;
        }
        else
        {
        result = (
        ((2 * v1 - 2 * v2 + t2 + t1) * sCubed) +
        ((3 * v2 - 3 * v1 - 2 * t1 - t2) * sSquared) +
        (t1 * s) +
        v1
        );
        }
        return (float) result;
    }

    internal static float MathHelperCatmullRom(float value1, float value2, float value3, float value4, float amount)
    {
        double amountSquared = amount * amount;
        double amountCubed = amountSquared * amount;
        return (float) (
        0.5 *
        (
        ((2.0 * value2 + (value3 - value1) * amount) +
        ((2.0 * value1 - 5.0 * value2 + 4.0 * value3 - value4) * amountSquared) +
        (3.0 * value2 - value1 - 3.0 * value3 + value4) * amountCubed)
        )
        );
    }

    internal static float MathHelperBarycentric(float value1, float value2, float value3, float amount1, float amount2)
    {
        return value1 + (value2 - value1) * amount1 + (value3 - value1) * amount2;
    }

    internal static float MathHelperClamp(float value, float min, float max)
    {
        value = (value > max) ? max : value;
        value = (value < min) ? min : value;
        return value;
    }

    internal static int MathHelperClamp(int value, int min, int max)
    {
        value = (value > max) ? max : value;
        value = (value < min) ? min : value;
        return value;
    }

    internal static float MathHelperLerp(float value1, float value2, float amount)
    {
        return value1 + (value2 - value1) * amount;
    }

    internal static float MathHelperSmoothStep(float value1, float value2, float amount)
    {
        float result = MathHelperClamp(amount, 0f, 1f);
        result = MathHelperHermite(value1, 0f, value2, 0f, result);
        return result;
    }

    internal static bool MathHelperWithinEpsilon(float floatA, float floatB)
    {
        return Math.Abs(floatA - floatB) < 1.1920929E-07f;
    }

    internal static float MathHelperWrapAngle(float angle)
    {
        if ((angle > -MathHelper.Pi) && (angle <= MathHelper.Pi))
        {
        return angle;
        }
        angle %= MathHelper.TwoPi;
        if (angle <= -MathHelper.Pi)
        {
        return angle + MathHelper.TwoPi;
        }
        if (angle > MathHelper.Pi)
        {
        return angle - MathHelper.TwoPi;
        }
        return angle;
    }
}
