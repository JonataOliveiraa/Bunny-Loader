using BunnyLoader.Interop;

namespace Terraria;

public static class GameListExtensions
{
    public static void Add(this GameObject list, int value) => new GameList(list).Add<int>(value);
}
