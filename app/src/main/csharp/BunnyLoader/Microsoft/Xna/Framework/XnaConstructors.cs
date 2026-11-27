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

        public static Vector2 operator *(Vector2 a, Vector2 b) => new(a.X * b.X, a.Y * b.Y);

        public static Vector2 operator /(Vector2 a, Vector2 b) => new(a.X / b.X, a.Y / b.Y);

        public static Vector2 operator -(Vector2 a) => new(-a.X, -a.Y);

        public readonly float LengthValue => MathF.Sqrt(X * X + Y * Y);

        public static Vector2 Zero => default;

        public static Vector2 One => new(1f, 1f);

        public static Vector2 UnitX => new(1f, 0f);

        public static Vector2 UnitY => new(0f, 1f);

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

        public Vector3(float value) : this(value, value, value) { }

        public Vector3(Vector2 value, float z) : this(value.X, value.Y, z) { }

        public static Vector3 Zero => default;

        public static Vector3 One => new(1f, 1f, 1f);

        public static Vector3 UnitX => new(1f, 0f, 0f);

        public static Vector3 UnitY => new(0f, 1f, 0f);

        public static Vector3 UnitZ => new(0f, 0f, 1f);

        public static Vector3 Up => new(0f, 1f, 0f);

        public static Vector3 Down => new(0f, -1f, 0f);

        public static Vector3 Right => new(1f, 0f, 0f);

        public static Vector3 Left => new(-1f, 0f, 0f);

        public static Vector3 Forward => new(0f, 0f, -1f);

        public static Vector3 Backward => new(0f, 0f, 1f);

        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static Vector3 operator -(Vector3 a) => new(-a.X, -a.Y, -a.Z);

        public static Vector3 operator *(Vector3 a, Vector3 b) => new(a.X * b.X, a.Y * b.Y, a.Z * b.Z);

        public static Vector3 operator *(Vector3 a, float s) => new(a.X * s, a.Y * s, a.Z * s);

        public static Vector3 operator *(float s, Vector3 a) => new(a.X * s, a.Y * s, a.Z * s);

        public static Vector3 operator /(Vector3 a, Vector3 b) => new(a.X / b.X, a.Y / b.Y, a.Z / b.Z);

        public static Vector3 operator /(Vector3 a, float s) => new(a.X / s, a.Y / s, a.Z / s);

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

        public Vector4(float value) : this(value, value, value, value) { }

        public Vector4(Vector2 value, float z, float w) : this(value.X, value.Y, z, w) { }

        public Vector4(Vector3 value, float w) : this(value.X, value.Y, value.Z, w) { }

        public static Vector4 Zero => default;

        public static Vector4 One => new(1f, 1f, 1f, 1f);

        public static Vector4 UnitX => new(1f, 0f, 0f, 0f);

        public static Vector4 UnitY => new(0f, 1f, 0f, 0f);

        public static Vector4 UnitZ => new(0f, 0f, 1f, 0f);

        public static Vector4 UnitW => new(0f, 0f, 0f, 1f);

        public static Vector4 operator +(Vector4 a, Vector4 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z, a.W + b.W);

        public static Vector4 operator -(Vector4 a, Vector4 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z, a.W - b.W);

        public static Vector4 operator -(Vector4 a) => new(-a.X, -a.Y, -a.Z, -a.W);

        public static Vector4 operator *(Vector4 a, Vector4 b) => new(a.X * b.X, a.Y * b.Y, a.Z * b.Z, a.W * b.W);

        public static Vector4 operator *(Vector4 a, float s) => new(a.X * s, a.Y * s, a.Z * s, a.W * s);

        public static Vector4 operator *(float s, Vector4 a) => new(a.X * s, a.Y * s, a.Z * s, a.W * s);

        public static Vector4 operator /(Vector4 a, Vector4 b) => new(a.X / b.X, a.Y / b.Y, a.Z / b.Z, a.W / b.W);

        public static Vector4 operator /(Vector4 a, float s) => new(a.X / s, a.Y / s, a.Z / s, a.W / s);

        public override readonly string ToString() => $"{{X:{X} Y:{Y} Z:{Z} W:{W}}}";
    }

    public partial struct Quaternion
    {
        public Quaternion(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public Quaternion(Vector3 vectorPart, float scalarPart) : this(vectorPart.X, vectorPart.Y, vectorPart.Z, scalarPart) { }

        public static Quaternion Identity => new(0f, 0f, 0f, 1f);

        public static Quaternion operator +(Quaternion a, Quaternion b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z, a.W + b.W);

        public static Quaternion operator -(Quaternion a, Quaternion b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z, a.W - b.W);

        public static Quaternion operator -(Quaternion a) => new(-a.X, -a.Y, -a.Z, -a.W);

        public static Quaternion operator *(Quaternion a, float s) => new(a.X * s, a.Y * s, a.Z * s, a.W * s);

        public static Quaternion operator *(Quaternion a, Quaternion b)
        {
            float crossX = (a.Y * b.Z) - (a.Z * b.Y);
            float crossY = (a.Z * b.X) - (a.X * b.Z);
            float crossZ = (a.X * b.Y) - (a.Y * b.X);
            float dot = (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);

            return new(
                (a.X * b.W) + (b.X * a.W) + crossX,
                (a.Y * b.W) + (b.Y * a.W) + crossY,
                (a.Z * b.W) + (b.Z * a.W) + crossZ,
                (a.W * b.W) - dot);
        }

        public static Quaternion operator /(Quaternion a, Quaternion b)
        {
            float inverse = 1f / ((b.X * b.X) + (b.Y * b.Y) + (b.Z * b.Z) + (b.W * b.W));
            return a * new Quaternion(-b.X * inverse, -b.Y * inverse, -b.Z * inverse, b.W * inverse);
        }

        public override readonly string ToString() => $"{{X:{X} Y:{Y} Z:{Z} W:{W}}}";
    }

    public partial struct Plane
    {
        public Plane(Vector3 normal, float d)
        {
            Normal = normal;
            D = d;
        }

        public Plane(float a, float b, float c, float d) : this(new Vector3(a, b, c), d) { }

        public Plane(Vector4 value) : this(new Vector3(value.X, value.Y, value.Z), value.W) { }

        public Plane(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a;
            Vector3 ac = c - a;

            Vector3 cross = new(
                (ab.Y * ac.Z) - (ab.Z * ac.Y),
                (ab.Z * ac.X) - (ab.X * ac.Z),
                (ab.X * ac.Y) - (ab.Y * ac.X));

            float factor = 1f / MathF.Sqrt((cross.X * cross.X) + (cross.Y * cross.Y) + (cross.Z * cross.Z));
            Normal = cross * factor;
            D = -((Normal.X * a.X) + (Normal.Y * a.Y) + (Normal.Z * a.Z));
        }

        public override readonly string ToString() => $"{{Normal:{Normal} D:{D}}}";
    }

    public partial struct Point
    {
        public Point(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static Point Zero => default;

        public static Point operator +(Point a, Point b) => new(a.X + b.X, a.Y + b.Y);

        public static Point operator -(Point a, Point b) => new(a.X - b.X, a.Y - b.Y);

        public static Point operator *(Point a, Point b) => new(a.X * b.X, a.Y * b.Y);

        public static Point operator /(Point a, Point b) => new(a.X / b.X, a.Y / b.Y);

        public override readonly string ToString() => $"{{X:{X} Y:{Y}}}";
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

        public static Rectangle Empty => default;

        public override readonly string ToString() => $"{{X:{X} Y:{Y} Width:{Width} Height:{Height}}}";
    }

    public readonly partial struct GameTime
    {
        public GameTime() : this(New().Pointer) { }

        public GameTime(TimeSpan totalGameTime, TimeSpan elapsedGameTime)
            : this(New(totalGameTime, elapsedGameTime, totalGameTime, elapsedGameTime).Pointer) { }

        public GameTime(TimeSpan totalRealTime, TimeSpan elapsedRealTime, bool isRunningSlowly)
            : this(New(totalRealTime, elapsedRealTime, totalRealTime, elapsedRealTime, isRunningSlowly).Pointer) { }
    }
}

namespace Microsoft.Xna.Framework.Graphics
{
    [System.ComponentModel.TypeConverter(typeof(Microsoft.Xna.Framework.Design.ColorConverter))]
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

        public Color(Vector3 color) : this(color.X, color.Y, color.Z, 1f) { }

        public Color(Vector4 color) : this(color.X, color.Y, color.Z, color.W) { }

        public static Color FromNonPremultiplied(Vector4 vector) =>
            new(vector.X * vector.W, vector.Y * vector.W, vector.Z * vector.W, vector.W);

        public static Color FromNonPremultiplied(int r, int g, int b, int a) =>
            new(r * a / 255, g * a / 255, b * a / 255, a);

        public static Color operator *(Color value, float scale) =>
            new((int)(value.R * scale), (int)(value.G * scale), (int)(value.B * scale), (int)(value.A * scale));

        public static Color operator *(float scale, Color value) => value * scale;

        public uint PackedValue
        {
            readonly get => R | ((uint)G << 8) | ((uint)B << 16) | ((uint)A << 24);
            set
            {
                R = (byte)value;
                G = (byte)(value >> 8);
                B = (byte)(value >> 16);
                A = (byte)(value >> 24);
            }
        }

        public static Color Multiply(Color value, float scale) => value * scale;

        public static Color Lerp(Color value1, Color value2, float amount)
        {
            amount = Math.Clamp(amount, 0f, 1f);
            return new(
                (int)(value1.R + (value2.R - value1.R) * amount),
                (int)(value1.G + (value2.G - value1.G) * amount),
                (int)(value1.B + (value2.B - value1.B) * amount),
                (int)(value1.A + (value2.A - value1.A) * amount));
        }

        public override readonly string ToString() => $"{{R:{R} G:{G} B:{B} A:{A}}}";
    }

    public partial struct Viewport
    {
        public Viewport(int x, int y, int width, int height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
            minDepth = 0f;
            maxDepth = 1f;
        }

        public Viewport(Rectangle bounds) : this(bounds.X, bounds.Y, bounds.Width, bounds.Height) { }

        public override readonly string ToString() =>
            $"{{X:{x} Y:{y} Width:{width} Height:{height} MinDepth:{minDepth} MaxDepth:{maxDepth}}}";
    }
}

namespace Microsoft.Xna.Framework.Input
{
    public partial struct KeyboardState
    {
        public KeyboardState(params Keys[] keys)
        {
            if (keys == null) return;

            foreach (Keys key in keys)
            {
                uint mask = 1u << ((int)key & 0x1f);
                switch ((int)key >> 5)
                {
                    case 0: keys0 |= mask; break;
                    case 1: keys1 |= mask; break;
                    case 2: keys2 |= mask; break;
                    case 3: keys3 |= mask; break;
                    case 4: keys4 |= mask; break;
                    case 5: keys5 |= mask; break;
                    case 6: keys6 |= mask; break;
                    case 7: keys7 |= mask; break;
                }
            }
        }

        public readonly KeyState this[Keys key]
        {
            get
            {
                uint mask = 1u << ((int)key & 0x1f);
                uint word = ((int)key >> 5) switch
                {
                    0 => keys0,
                    1 => keys1,
                    2 => keys2,
                    3 => keys3,
                    4 => keys4,
                    5 => keys5,
                    6 => keys6,
                    7 => keys7,
                    _ => 0u,
                };
                return (word & mask) != 0 ? KeyState.Down : KeyState.Up;
            }
        }
    }

    public partial struct MouseState
    {
        public MouseState(
            int x, int y, int scrollWheel,
            ButtonState leftButton, ButtonState middleButton, ButtonState rightButton,
            ButtonState xButton1, ButtonState xButton2)
        {
            _x = x;
            _y = y;
            _scrollWheelValue = scrollWheel;
            _leftButton = leftButton;
            _middleButton = middleButton;
            _rightButton = rightButton;
            _xButton1 = xButton1;
            _xButton2 = xButton2;
        }

        public override readonly string ToString()
        {
            string buttons = "";
            if (_leftButton == ButtonState.Pressed) buttons = "Left";
            if (_rightButton == ButtonState.Pressed) buttons += (buttons.Length > 0 ? " " : "") + "Right";
            if (_middleButton == ButtonState.Pressed) buttons += (buttons.Length > 0 ? " " : "") + "Middle";
            if (_xButton1 == ButtonState.Pressed) buttons += (buttons.Length > 0 ? " " : "") + "XButton1";
            if (_xButton2 == ButtonState.Pressed) buttons += (buttons.Length > 0 ? " " : "") + "XButton2";
            if (buttons.Length == 0) buttons = "None";

            return $"[MouseState X={_x}, Y={_y}, Buttons={buttons}, Wheel={_scrollWheelValue}]";
        }
    }
}
