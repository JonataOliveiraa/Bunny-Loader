using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Terraria;

public readonly partial struct Dust
{
    public static int NewDust(Vector2 Position, int Width, int Height, int Type, float SpeedX = 0f, float SpeedY = 0f, int Alpha = 0) =>
        NewDust(Position, Width, Height, Type, SpeedX, SpeedY, Alpha, default(Color), 1f);

    public static Dust NewDustDirect(Vector2 Position, int Width, int Height, int Type, float SpeedX = 0f, float SpeedY = 0f, int Alpha = 0) =>
        NewDustDirect(Position, Width, Height, Type, SpeedX, SpeedY, Alpha, default(Color), 1f);
}
