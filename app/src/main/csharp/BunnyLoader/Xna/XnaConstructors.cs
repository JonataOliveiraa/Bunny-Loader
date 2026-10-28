using System;

namespace Microsoft.Xna.Framework
{
    public partial struct Vector2
    {
        public Vector2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public Vector2(float value) : this(value, value) { }

        public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);

        public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);

        public static Vector2 operator *(Vector2 a, float s) => new(a.X * s, a.Y * s);

        public static Vector2 operator *(float s, Vector2 a) => new(a.X * s, a.Y * s);

        public static Vector2 operator /(Vector2 a, float s) => new(a.X / s, a.Y / s);

        public readonly float LengthValue => MathF.Sqrt(X * X + Y * Y);

        public override readonly string ToString() => $"{{X:{X} Y:{Y}}}";
    }

    public partial struct Vector3
    {
        public Vector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override readonly string ToString() => $"{{X:{X} Y:{Y} Z:{Z}}}";
    }

    public partial struct Vector4
    {
        public Vector4(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }
    }

    public partial struct Point
    {
        public Point(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    public partial struct Rectangle
    {
        public Rectangle(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public override readonly string ToString() => $"{{X:{X} Y:{Y} Width:{Width} Height:{Height}}}";
    }
}

namespace Microsoft.Xna.Framework.Graphics
{
    public partial struct Color
    {
        public Color(int r, int g, int b) : this(r, g, b, 255) { }

        public Color(int r, int g, int b, int a)
        {
            R = (byte)Math.Clamp(r, 0, 255);
            G = (byte)Math.Clamp(g, 0, 255);
            B = (byte)Math.Clamp(b, 0, 255);
            A = (byte)Math.Clamp(a, 0, 255);
        }

        public Color(float r, float g, float b) : this(r, g, b, 1f) { }

        public Color(float r, float g, float b, float a)
            : this((int)(r * 255f + 0.5f), (int)(g * 255f + 0.5f), (int)(b * 255f + 0.5f), (int)(a * 255f + 0.5f)) { }

        public override readonly string ToString() => $"{{R:{R} G:{G} B:{B} A:{A}}}";
    }
}
