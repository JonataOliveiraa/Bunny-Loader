using System;
using Microsoft.Xna.Framework;
using Terraria.Utilities;

namespace Terraria;

public static class UtilsExtensions
{
    public static bool NextBool(this UnifiedRandom r) => r.Next(2) == 0;

    public static bool NextBool(this UnifiedRandom r, int consequent)
    {
        if (consequent < 1) throw new ArgumentOutOfRangeException(nameof(consequent), "consequent must be greater than or equal to 1.");
        return r.Next(consequent) == 0;
    }

    public static bool NextBool(this UnifiedRandom r, int numerator, int denominator)
    {
        if (numerator > denominator) throw new ArgumentOutOfRangeException(nameof(numerator), "numerator must be less than or equal to denominator.");
        return r.Next(denominator) < numerator;
    }

    public static float NextFloat(this UnifiedRandom r) => (float)r.NextDouble();

    public static float NextFloat(this UnifiedRandom r, float maxValue) => (float)r.NextDouble() * maxValue;

    public static float NextFloat(this UnifiedRandom r, float minValue, float maxValue) => (float)r.NextDouble() * (maxValue - minValue) + minValue;

    public static float ToRotation(this Vector2 v) => MathF.Atan2(v.Y, v.X);

    public static Vector2 Size(this Microsoft.Xna.Framework.Graphics.Texture2D texture) => new(texture.Width, texture.Height);

    public static Vector2 Size(this ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D> asset) => asset.Value.Size();

    public static Rectangle Frame(this Microsoft.Xna.Framework.Graphics.Texture2D tex, int horizontalFrames = 1, int verticalFrames = 1, int frameX = 0, int frameY = 0, int sizeOffsetX = 0, int sizeOffsetY = 0) =>
        Utils.Frame(tex, horizontalFrames, verticalFrames, frameX, frameY, sizeOffsetX, sizeOffsetY);

    public static Rectangle Frame(this ReLogic.Content.Asset<Microsoft.Xna.Framework.Graphics.Texture2D> tex, int horizontalFrames = 1, int verticalFrames = 1, int frameX = 0, int frameY = 0, int sizeOffsetX = 0, int sizeOffsetY = 0) =>
        tex.Value.Frame(horizontalFrames, verticalFrames, frameX, frameY, sizeOffsetX, sizeOffsetY);

    public static ReLogic.Content.Asset<T> Request<T>(this ReLogic.Content.IAssetRepository repository, string assetName, ReLogic.Content.AssetRequestMode mode = ReLogic.Content.AssetRequestMode.AsyncLoad)
        where T : struct, BunnyLoader.Interop.IGameObject =>
        Terraria.ModLoader.ModContent.Request<T>("Terraria/" + assetName.Replace('\\', '/'), mode);

    public static Vector2 RotatedBy(this Vector2 spinningpoint, double radians, Vector2 center = default)
    {
        float cos = (float)Math.Cos(radians), sin = (float)Math.Sin(radians);
        Vector2 offset = spinningpoint - center;
        return new Vector2(center.X + offset.X * cos - offset.Y * sin, center.Y + offset.X * sin + offset.Y * cos);
    }

    public static Vector2 RotatedByRandom(this Vector2 spinninpoint, double maxRadians) =>
        spinninpoint.RotatedBy(Main.rand.NextDouble() * maxRadians - Main.rand.NextDouble() * maxRadians);

    public static Vector2 SafeNormalize(this Vector2 v, Vector2 defaultValue)
    {
        if (v.X == 0 && v.Y == 0 || float.IsNaN(v.X) || float.IsNaN(v.Y)) return defaultValue;
        float length = MathF.Sqrt(v.X * v.X + v.Y * v.Y);
        return new Vector2(v.X / length, v.Y / length);
    }
}
