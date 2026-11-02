using BunnyLoader.Interop;
using Microsoft.Xna.Framework;

namespace Terraria;

public readonly partial struct Collision
{
    public static void LaserScan(Vector2 samplingPoint, Vector2 directionUnit, float samplingWidth, float maxDistance, float[] samples)
    {
        GameArray<float> buffer = GameArray<float>.New(new GameClass("System", "Single"), samples.Length);
        LaserScan(samplingPoint, directionUnit, samplingWidth, maxDistance, buffer);
        buffer.AsSpan().CopyTo(samples);
    }
}
