using Microsoft.Xna.Framework;

namespace Terraria.UI;

public struct CalculatedStyle
{
    public float X;
    public float Y;
    public float Width;
    public float Height;

    public CalculatedStyle(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public readonly Rectangle ToRectangle() => new((int)X, (int)Y, (int)Width, (int)Height);

    public readonly Vector2 Position() => new(X, Y);

    public readonly Vector2 Center() => new(X + Width * 0.5f, Y + Height * 0.5f);

    public readonly bool Contains(Vector2 point) => point.X >= X && point.X < X + Width && point.Y >= Y && point.Y < Y + Height;
}
