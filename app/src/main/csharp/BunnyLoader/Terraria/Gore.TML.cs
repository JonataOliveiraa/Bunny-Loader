using Microsoft.Xna.Framework;
using Terraria.DataStructures;

namespace Terraria;

public readonly partial struct Gore
{
    public static int NewGore(IEntitySource source, Vector2 Position, Vector2 Velocity, int Type, float Scale = 1f) =>
        NewGore(Position, Velocity, Type, Scale);

    public static Gore NewGoreDirect(IEntitySource source, Vector2 Position, Vector2 Velocity, int Type, float Scale = 1f) =>
        NewGoreDirect(Position, Velocity, Type, Scale);

    public static Gore NewGorePerfect(IEntitySource source, Vector2 Position, Vector2 Velocity, int Type, float Scale = 1f) =>
        NewGorePerfect(Position, Velocity, Type, Scale);
}
