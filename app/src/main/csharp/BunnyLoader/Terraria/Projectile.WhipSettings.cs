using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria.ModLoader.Core;

namespace Terraria;

public struct WhipSettings
{
    public int Segments;
    public float RangeMultiplier;
}

public readonly partial struct Projectile
{
    public ref WhipSettings WhipSettings => ref WhipHooks.Of(this).Value;

    public static void FillWhipControlPoints(Projectile proj, List<Vector2> controlPoints, Player owner = default, bool getActualCollisionPoints = false, int frameOffset = 0) =>
        WhipHooks.Fill(proj, controlPoints, owner, getActualCollisionPoints, frameOffset);
}
