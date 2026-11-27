using Microsoft.Xna.Framework;

namespace Terraria.DataStructures;

public partial struct Point16
{
    public Point16(short x, short y)
    {
        X = x;
        Y = y;
    }

    public Point16(Point point) : this(point.X, point.Y) { }

    public Point16(int size) : this(size, size) { }

    public Point16(short size) : this(size, size) { }

    public static Point16 operator +(Point16 first, Point16 second) => new(first.X + second.X, first.Y + second.Y);

    public static Point16 operator -(Point16 first, Point16 second) => new(first.X - second.X, first.Y - second.Y);

    public static Point16 operator *(Point16 first, Point16 second) => new(first.X * second.X, first.Y * second.Y);

    public static Point16 operator /(Point16 first, Point16 second) => new(first.X / second.X, first.Y / second.Y);

    public static Point16 operator %(Point16 first, int num) => new(first.X % num, first.Y % num);

    public static implicit operator Point(Point16 point) => new(point.X, point.Y);

    public override readonly string ToString() => $"{{{X}, {Y}}}";

    public void Deconstruct(out int x, out int y)
    {
        x = X;
        y = Y;
    }
}
