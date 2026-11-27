/* FNA - XNA4 Reimplementation for Desktop Platforms
 * Copyright 2009-2024 Ethan Lee and the MonoGame Team
 *
 * Released under the Microsoft Public License.
 * See LICENSE for details.
 */

#nullable disable

using System;

namespace Microsoft.Xna.Framework
{
    internal static class FnaMath
    {
        const float MachineEpsilonFloat = 1.1920929E-07f;

        internal static bool WithinEpsilon(float floatA, float floatB) => Math.Abs(floatA - floatB) < MachineEpsilonFloat;

        internal static float Dot(Vector3 a, Vector3 b) => (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);

        internal static void Dot(ref Vector3 a, ref Vector3 b, out float result) => result = (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);

        internal static void Cross(ref Vector3 a, ref Vector3 b, out Vector3 result)
        {
            float x = (a.Y * b.Z) - (b.Y * a.Z);
            float y = -((a.X * b.Z) - (b.X * a.Z));
            float z = (a.X * b.Y) - (b.X * a.Y);
            result = new Vector3(x, y, z);
        }

        internal static float LengthSquared(Vector3 v) => (v.X * v.X) + (v.Y * v.Y) + (v.Z * v.Z);

        internal static float Length(Vector3 v) => (float)Math.Sqrt(LengthSquared(v));

        internal static float DistanceSquared(Vector3 a, Vector3 b) => LengthSquared(a - b);

        internal static void DistanceSquared(ref Vector3 a, ref Vector3 b, out float result) => result = LengthSquared(a - b);

        internal static float Distance(Vector3 a, Vector3 b) => (float)Math.Sqrt(LengthSquared(a - b));

        internal static Vector3 Subtract(Vector3 a, Vector3 b) => a - b;

        internal static void Multiply(ref Vector3 value, float scaleFactor, out Vector3 result) => result = value * scaleFactor;

        internal static Vector3 Transform(Vector3 position, Matrix matrix) => new(
            (position.X * matrix.M11) + (position.Y * matrix.M21) + (position.Z * matrix.M31) + matrix.M41,
            (position.X * matrix.M12) + (position.Y * matrix.M22) + (position.Z * matrix.M32) + matrix.M42,
            (position.X * matrix.M13) + (position.Y * matrix.M23) + (position.Z * matrix.M33) + matrix.M43);
    }
}
