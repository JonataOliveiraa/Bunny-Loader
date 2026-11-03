using System;
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

    public static bool CheckAABBvLineCollision(Vector2 objectPosition, Vector2 objectDimensions, Vector2 lineStart, Vector2 lineEnd, float lineWidth, ref float collisionPoint)
    {
        float half = lineWidth * 0.5f;
        Vector2 boxPosition = lineStart;
        Vector2 boxSize = lineEnd - lineStart;
        if (boxSize.X > 0f)
        {
            boxSize.X += lineWidth;
            boxPosition.X -= half;
        }
        else
        {
            boxPosition.X += boxSize.X - half;
            boxSize.X = -boxSize.X + lineWidth;
        }
        if (boxSize.Y > 0f)
        {
            boxSize.Y += lineWidth;
            boxPosition.Y -= half;
        }
        else
        {
            boxPosition.Y += boxSize.Y - half;
            boxSize.Y = -boxSize.Y + lineWidth;
        }
        if (!(objectPosition.X + objectDimensions.X > boxPosition.X && objectPosition.X < boxPosition.X + boxSize.X &&
              objectPosition.Y + objectDimensions.Y > boxPosition.Y && objectPosition.Y < boxPosition.Y + boxSize.Y))
            return false;

        Vector2 topLeft = objectPosition - lineStart;
        Vector2 bottomRight = topLeft + objectDimensions;
        Vector2 line = lineEnd - lineStart;
        float length = line.Length();
        float angle = MathF.Atan2(line.Y, line.X);
        Vector2[] corners =
        {
            topLeft.RotatedBy(-angle),
            new Vector2(bottomRight.X, topLeft.Y).RotatedBy(-angle),
            bottomRight.RotatedBy(-angle),
            new Vector2(topLeft.X, bottomRight.Y).RotatedBy(-angle),
        };
        collisionPoint = length;
        bool hit = false;
        foreach (Vector2 corner in corners)
        {
            if (MathF.Abs(corner.Y) < half && corner.X < collisionPoint && corner.X >= 0f)
            {
                collisionPoint = corner.X;
                hit = true;
            }
        }
        Vector2 upperStart = new(0f, half), upperEnd = new(length, half);
        Vector2 lowerStart = new(0f, -half), lowerEnd = new(length, -half);
        for (int i = 0; i < corners.Length; i++)
        {
            Vector2 edge = corners[(i + 1) % corners.Length] - corners[i];
            hit |= EdgeHit(upperStart, upperEnd - upperStart, corners[i], edge, ref collisionPoint);
            hit |= EdgeHit(lowerStart, lowerEnd - lowerStart, corners[i], edge, ref collisionPoint);
        }
        return hit;
    }

    static bool EdgeHit(Vector2 start, Vector2 side, Vector2 corner, Vector2 edge, ref float collisionPoint)
    {
        float cross = side.X * edge.Y - side.Y * edge.X;
        if (cross == 0f) return false;
        Vector2 offset = corner - start;
        float t = (offset.X * edge.Y - offset.Y * edge.X) / cross;
        if (t < 0f || t > 1f) return false;
        float u = (offset.X * side.Y - offset.Y * side.X) / cross;
        if (u < 0f || u > 1f) return false;
        collisionPoint = MathF.Min(collisionPoint, start.X + t * side.X);
        return true;
    }
}
