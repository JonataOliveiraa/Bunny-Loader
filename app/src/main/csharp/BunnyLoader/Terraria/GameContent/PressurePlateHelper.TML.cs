using BunnyLoader.Interop;
using Microsoft.Xna.Framework;

namespace Terraria.GameContent;

public readonly unsafe partial struct PressurePlateHelper
{
    public static bool IsPressed(Point location) => new GameDictionary(PressurePlatesPressed_).ContainsKey(location);
}
