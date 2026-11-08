using System.Collections.Generic;

namespace Terraria;

public readonly partial struct Main
{
    [System.ThreadStatic] static Player currentPlayerOverride;

    public static Player CurrentPlayer => currentPlayerOverride.IsNull ? LocalPlayer : currentPlayerOverride;

    internal static Player CurrentPlayerOverride
    {
        get => currentPlayerOverride;
        set => currentPlayerOverride = value;
    }

    public static IEnumerable<WorldItem> ActiveItems
    {
        get
        {
            var items = item;
            for (int i = 0; i < maxItems && i < items.Length; i++)
            {
                WorldItem current = items[i];
                if (current.IsNull) continue;
                Item inner = current.inner;
                if (!inner.IsNull && inner.type != 0) yield return current;
            }
        }
    }

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
