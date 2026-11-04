using Microsoft.Xna.Framework;

namespace Terraria;

public static partial class UtilsExtensions
{
    public static Microsoft.Xna.Framework.Graphics.Color MultiplyRGBA(this Microsoft.Xna.Framework.Graphics.Color first, Microsoft.Xna.Framework.Graphics.Color second) => Utils.MultiplyRGBA(first, second);

    public static Vector2 MoveTowards(this Vector2 value, Vector2 target, float amount) => Utils.MoveTowards(value, target, amount);
    public static Vector2 DirectionTo(this Vector2 value, Vector2 target) => Utils.DirectionTo(value, target);
    public static Vector2 DirectionFrom(this Vector2 value, Vector2 target) => Utils.DirectionFrom(value, target);
    public static Vector2 ClosestPointInRect(this Rectangle rectangle, Vector2 point) => Utils.ClosestPointInRect(rectangle, point);
    public static Point ToPoint(this Vector2 value) => Utils.ToPoint(value);
    public static float AngleTowards(this float value, float target, float amount) => Utils.AngleTowards(value, target, amount);
    public static int ToInt(this bool value) => Utils.ToInt(value);
}
