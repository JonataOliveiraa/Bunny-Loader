using System;
using Microsoft.Xna.Framework;

namespace Terraria;

public static partial class Utils
{
    public static Rectangle CornerRectangle(Point start, Point end) => new(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Abs(start.X - end.X), Math.Abs(start.Y - end.Y));
}
