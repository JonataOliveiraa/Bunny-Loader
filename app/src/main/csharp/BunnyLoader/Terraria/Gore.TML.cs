using Microsoft.Xna.Framework;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;

namespace Terraria;

public readonly partial struct Gore
{
    public ModGore? ModGore => GoreLoader.GetModGore(type);

    public static int NewGore(IEntitySource source, Vector2 Position, Vector2 Velocity, int Type, float Scale = 1f)
    {
        GoreHooks.Source = source;
        try { return NewGore(Position, Velocity, Type, Scale); }
        finally { GoreHooks.Source = default; }
    }

    public static Gore NewGoreDirect(IEntitySource source, Vector2 Position, Vector2 Velocity, int Type, float Scale = 1f)
    {
        GoreHooks.Source = source;
        try { return NewGoreDirect(Position, Velocity, Type, Scale); }
        finally { GoreHooks.Source = default; }
    }

    public static Gore NewGorePerfect(IEntitySource source, Vector2 Position, Vector2 Velocity, int Type, float Scale = 1f)
    {
        GoreHooks.Source = source;
        try { return NewGorePerfect(Position, Velocity, Type, Scale); }
        finally { GoreHooks.Source = default; }
    }
}
