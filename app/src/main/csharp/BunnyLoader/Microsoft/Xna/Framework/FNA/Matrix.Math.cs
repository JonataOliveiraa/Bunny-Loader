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

public partial struct Matrix
{
    public Vector3 Backward
    {
        readonly get => new(M31, M32, M33);
        set
        {
            M31 = value.X;
            M32 = value.Y;
            M33 = value.Z;
        }
    }

    public Vector3 Forward
    {
        readonly get => new(-M31, -M32, -M33);
        set
        {
            M31 = -value.X;
            M32 = -value.Y;
            M33 = -value.Z;
        }
    }

    public Vector3 Up
    {
        readonly get => new(M21, M22, M23);
        set
        {
            M21 = value.X;
            M22 = value.Y;
            M23 = value.Z;
        }
    }

    public Vector3 Down
    {
        readonly get => new(-M21, -M22, -M23);
        set
        {
            M21 = -value.X;
            M22 = -value.Y;
            M23 = -value.Z;
        }
    }

    public Vector3 Right
    {
        readonly get => new(M11, M12, M13);
        set
        {
            M11 = value.X;
            M12 = value.Y;
            M13 = value.Z;
        }
    }

    public Vector3 Left
    {
        readonly get => new(-M11, -M12, -M13);
        set
        {
            M11 = -value.X;
            M12 = -value.Y;
            M13 = -value.Z;
        }
    }

    public Vector3 Translation
    {
        readonly get => new(M41, M42, M43);
        set
        {
            M41 = value.X;
            M42 = value.Y;
            M43 = value.Z;
        }
    }

    public readonly float Determinant()
    {
        float num18 = (M33 * M44) - (M34 * M43);
        float num17 = (M32 * M44) - (M34 * M42);
        float num16 = (M32 * M43) - (M33 * M42);
        float num15 = (M31 * M44) - (M34 * M41);
        float num14 = (M31 * M43) - (M33 * M41);
        float num13 = (M31 * M42) - (M32 * M41);

        return (
            (M11 * (((M22 * num18) - (M23 * num17)) + (M24 * num16))) -
            (M12 * (((M21 * num18) - (M23 * num15)) + (M24 * num14))) +
            (M13 * (((M21 * num17) - (M22 * num15)) + (M24 * num13))) -
            (M14 * (((M21 * num16) - (M22 * num14)) + (M23 * num13)))
        );
    }

    public static Matrix Add(Matrix matrix1, Matrix matrix2) => matrix1 + matrix2;

    public static Matrix Subtract(Matrix matrix1, Matrix matrix2) => matrix1 - matrix2;

    public static Matrix Multiply(Matrix matrix1, Matrix matrix2) => matrix1 * matrix2;

    public static Matrix Multiply(Matrix matrix1, float factor) => matrix1 * factor;

    public static Matrix Divide(Matrix matrix1, Matrix matrix2) => matrix1 / matrix2;

    public static Matrix Divide(Matrix matrix1, float divider) => matrix1 / divider;

    public static Matrix Negate(Matrix matrix) => -matrix;

    public static Matrix Transpose(Matrix matrix) => FnaMathCore.MatrixTranspose(matrix);

    public static Matrix Invert(Matrix matrix) => FnaMathCore.MatrixInvert(matrix);

    public static Matrix Lerp(Matrix matrix1, Matrix matrix2, float amount) => FnaMathCore.MatrixLerp(matrix1, matrix2, amount);

    public static Matrix Transform(Matrix value, Quaternion rotation) => FnaMathCore.MatrixTransform(value, rotation);

    public static Matrix CreateTranslation(float xPosition, float yPosition, float zPosition) =>
        new(1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f, 0f, xPosition, yPosition, zPosition, 1f);

    public static Matrix CreateTranslation(Vector3 position) => CreateTranslation(position.X, position.Y, position.Z);

    public static Matrix CreateScale(float xScale, float yScale, float zScale) =>
        new(xScale, 0f, 0f, 0f, 0f, yScale, 0f, 0f, 0f, 0f, zScale, 0f, 0f, 0f, 0f, 1f);

    public static Matrix CreateScale(float scale) => CreateScale(scale, scale, scale);

    public static Matrix CreateScale(Vector3 scales) => CreateScale(scales.X, scales.Y, scales.Z);

    public static Matrix CreateRotationX(float radians) => FnaMathCore.MatrixCreateRotationX(radians);

    public static Matrix CreateRotationY(float radians) => FnaMathCore.MatrixCreateRotationY(radians);

    public static Matrix CreateRotationZ(float radians) => FnaMathCore.MatrixCreateRotationZ(radians);

    public static Matrix CreateFromAxisAngle(Vector3 axis, float angle) => FnaMathCore.MatrixCreateFromAxisAngle(axis, angle);

    public static Matrix CreateFromQuaternion(Quaternion quaternion) => FnaMathCore.MatrixCreateFromQuaternion(quaternion);

    public static Matrix CreateFromYawPitchRoll(float yaw, float pitch, float roll) => FnaMathCore.MatrixCreateFromYawPitchRoll(yaw, pitch, roll);

    public static Matrix CreateOrthographic(float width, float height, float zNearPlane, float zFarPlane) =>
        FnaMathCore.MatrixCreateOrthographic(width, height, zNearPlane, zFarPlane);

    public static Matrix CreateOrthographicOffCenter(float left, float right, float bottom, float top, float zNearPlane, float zFarPlane) =>
        FnaMathCore.MatrixCreateOrthographicOffCenter(left, right, bottom, top, zNearPlane, zFarPlane);

    public static Matrix CreatePerspective(float width, float height, float nearPlaneDistance, float farPlaneDistance) =>
        FnaMathCore.MatrixCreatePerspective(width, height, nearPlaneDistance, farPlaneDistance);

    public static Matrix CreatePerspectiveFieldOfView(float fieldOfView, float aspectRatio, float nearPlaneDistance, float farPlaneDistance) =>
        FnaMathCore.MatrixCreatePerspectiveFieldOfView(fieldOfView, aspectRatio, nearPlaneDistance, farPlaneDistance);

    public static Matrix CreatePerspectiveOffCenter(float left, float right, float bottom, float top, float nearPlaneDistance, float farPlaneDistance) =>
        FnaMathCore.MatrixCreatePerspectiveOffCenter(left, right, bottom, top, nearPlaneDistance, farPlaneDistance);

    public static Matrix CreateShadow(Vector3 lightDirection, Plane plane) => FnaMathCore.MatrixCreateShadow(lightDirection, plane);

    public static Matrix CreateReflection(Plane value) => FnaMathCore.MatrixCreateReflection(value);
}

public partial struct Quaternion
{
    public static Quaternion Add(Quaternion quaternion1, Quaternion quaternion2) => quaternion1 + quaternion2;

    public static Quaternion Subtract(Quaternion quaternion1, Quaternion quaternion2) => quaternion1 - quaternion2;

    public static Quaternion Multiply(Quaternion quaternion1, Quaternion quaternion2) => FnaMathCore.QuaternionMultiply(quaternion1, quaternion2);

    public static Quaternion Multiply(Quaternion quaternion1, float scaleFactor) => FnaMathCore.QuaternionMultiply(quaternion1, scaleFactor);

    public static Quaternion Divide(Quaternion quaternion1, Quaternion quaternion2) => FnaMathCore.QuaternionDivide(quaternion1, quaternion2);

    public static Quaternion Negate(Quaternion quaternion) => -quaternion;

    public static float Dot(Quaternion quaternion1, Quaternion quaternion2) => FnaMathCore.QuaternionDot(quaternion1, quaternion2);

    public static Quaternion Concatenate(Quaternion value1, Quaternion value2) => FnaMathCore.QuaternionConcatenate(value1, value2);

    public void Conjugate()
    {
        X = -X;
        Y = -Y;
        Z = -Z;
    }

    public static Quaternion Conjugate(Quaternion value) => FnaMathCore.QuaternionConjugate(value);

    public static Quaternion Inverse(Quaternion quaternion) => FnaMathCore.QuaternionInverse(quaternion);

    public readonly float Length() => MathF.Sqrt((X * X) + (Y * Y) + (Z * Z) + (W * W));

    public readonly float LengthSquared() => (X * X) + (Y * Y) + (Z * Z) + (W * W);

    public void Normalize() => this = FnaMathCore.QuaternionNormalize(this);

    public static Quaternion Normalize(Quaternion quaternion) => FnaMathCore.QuaternionNormalize(quaternion);

    public static Quaternion CreateFromAxisAngle(Vector3 axis, float angle) => FnaMathCore.QuaternionCreateFromAxisAngle(axis, angle);

    public static Quaternion CreateFromYawPitchRoll(float yaw, float pitch, float roll) => FnaMathCore.QuaternionCreateFromYawPitchRoll(yaw, pitch, roll);

    public static Quaternion CreateFromRotationMatrix(Matrix matrix) => FnaMathCore.QuaternionCreateFromRotationMatrix(matrix);
}

public partial struct Plane
{
    public readonly float Dot(Vector4 value) => (Normal.X * value.X) + (Normal.Y * value.Y) + (Normal.Z * value.Z) + (D * value.W);

    public readonly float DotCoordinate(Vector3 value) => (Normal.X * value.X) + (Normal.Y * value.Y) + (Normal.Z * value.Z) + D;

    public readonly float DotNormal(Vector3 value) => (Normal.X * value.X) + (Normal.Y * value.Y) + (Normal.Z * value.Z);

    public void Normalize() => this = FnaMathCore.PlaneNormalize(this);

    public static Plane Normalize(Plane value) => FnaMathCore.PlaneNormalize(value);

    public static Plane Transform(Plane plane, Matrix matrix) => FnaMathCore.PlaneTransform(plane, matrix);

    public static Plane Transform(Plane plane, Quaternion rotation) => FnaMathCore.PlaneTransform(plane, rotation);
}

public static partial class MathHelper
{
    public static float Barycentric(float value1, float value2, float value3, float amount1, float amount2) =>
        value1 + (value2 - value1) * amount1 + (value3 - value1) * amount2;

    public static float CatmullRom(float value1, float value2, float value3, float value4, float amount) =>
        FnaMathCore.MathHelperCatmullRom(value1, value2, value3, value4, amount);

    public static float Clamp(float value, float min, float max)
    {
        value = (value > max) ? max : value;
        value = (value < min) ? min : value;
        return value;
    }

    public static float Distance(float value1, float value2) => Math.Abs(value1 - value2);

    public static float Hermite(float value1, float tangent1, float value2, float tangent2, float amount) =>
        FnaMathCore.MathHelperHermite(value1, tangent1, value2, tangent2, amount);

    public static float Lerp(float value1, float value2, float amount) => value1 + (value2 - value1) * amount;

    public static float Max(float value1, float value2) => value1 > value2 ? value1 : value2;

    public static float Min(float value1, float value2) => value1 < value2 ? value1 : value2;

    public static float SmoothStep(float value1, float value2, float amount) => FnaMathCore.MathHelperSmoothStep(value1, value2, amount);

    public static float ToDegrees(float radians) => (float)(radians * 57.295779513082320876798154814105);

    public static float ToRadians(float degrees) => (float)(degrees * 0.017453292519943295769236907684886);

    public static float WrapAngle(float angle) => FnaMathCore.MathHelperWrapAngle(angle);
}

public partial struct Rectangle
{
    public readonly int Left => X;

    public readonly int Right => X + Width;

    public readonly int Top => Y;

    public readonly int Bottom => Y + Height;

    public readonly Point Center => new(X + (Width / 2), Y + (Height / 2));

    public Point Location
    {
        readonly get => new(X, Y);
        set
        {
            X = value.X;
            Y = value.Y;
        }
    }

    public readonly bool IsEmpty => Width == 0 && Height == 0 && X == 0 && Y == 0;

    public readonly bool Contains(int x, int y) => X <= x && x < X + Width && Y <= y && y < Y + Height;

    public readonly bool Contains(Point value) => Contains(value.X, value.Y);

    public readonly bool Contains(Rectangle value) =>
        X <= value.X && value.X + value.Width <= X + Width && Y <= value.Y && value.Y + value.Height <= Y + Height;

    public readonly bool Intersects(Rectangle rect) =>
        rect.Left < Right && Left < rect.Right && rect.Top < Bottom && Top < rect.Bottom;

    public void Offset(int offsetX, int offsetY)
    {
        X += offsetX;
        Y += offsetY;
    }

    public void Offset(Point offset) => Offset(offset.X, offset.Y);

    public void Inflate(int horizontalValue, int verticalValue)
    {
        X -= horizontalValue;
        Y -= verticalValue;
        Width += horizontalValue * 2;
        Height += verticalValue * 2;
    }

    public static Rectangle Intersect(Rectangle value1, Rectangle value2)
    {
        if (!value1.Intersects(value2)) return default;

        int right = Math.Min(value1.X + value1.Width, value2.X + value2.Width);
        int left = Math.Max(value1.X, value2.X);
        int top = Math.Max(value1.Y, value2.Y);
        int bottom = Math.Min(value1.Y + value1.Height, value2.Y + value2.Height);
        return new(left, top, right - left, bottom - top);
    }

    public static Rectangle Union(Rectangle value1, Rectangle value2)
    {
        int x = Math.Min(value1.X, value2.X);
        int y = Math.Min(value1.Y, value2.Y);
        return new(x, y, Math.Max(value1.Right, value2.Right) - x, Math.Max(value1.Bottom, value2.Bottom) - y);
    }
}
