using System.Collections.Generic;

namespace Terraria;

public readonly partial struct Main
{
    public static IEnumerable<Player> ActivePlayers
    {
        get
        {
            var players = player;
            for (int i = 0; i < maxPlayers && i < players.Length; i++)
            {
                Player current = players[i];
                if (!current.IsNull && current.active) yield return current;
            }
        }
    }
}
