using System;
using Microsoft.Xna.Framework.Graphics;

namespace CsTexData;

public static class Pattern
{
    public const int Width = 4, Height = 3;

    public const string Png =
        "iVBORw0KGgoAAAANSUhEUgAAAAQAAAADCAYAAAC09K7GAAAAL0lEQVR42hXIIQEAIAADwZfEQxJmYZDEQ86NcfIAZ3KyWbmMgBpqqKEfbrjhhkceolEdZaXZ9V8AAAAASUVORK5CYII=";

    public static Color At(int x, int y) => new(x * 80, y * 124, 248 - x * 80, 255);

    public static Color[] Pixels()
    {
        var pixels = new Color[Width * Height];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                pixels[y * Width + x] = At(x, y);
        return pixels;
    }

    public static byte[] PngBytes() => Convert.FromBase64String(Png);

    public static string? Compare(Color[] got, Color[] expected)
    {
        if (got.Length != expected.Length) return $"{got.Length} pixels, expected {expected.Length}";
        for (int i = 0; i < got.Length; i++)
        {
            if (got[i] != expected[i])
                return $"pixel {i % Width},{i / Width} is {Text(got[i])}, expected {Text(expected[i])}";
        }
        return null;
    }

    public static string Text(Color c) => $"{c.R},{c.G},{c.B},{c.A}";
}
