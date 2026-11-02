using System;
using Terraria.ModLoader.Core;

namespace Terraria.GameContent.Achievements;

public readonly partial struct AchievementsHelper
{
    internal static Action<Player, short, int>? itemPickup;
    internal static Action<short, int>? itemCraft;
    internal static Action<Player, ushort>? tileDestroyed;
    internal static Action<Player, short>? npcKilled;
    internal static Action<int>? progressionEvent;

    public static event Action<Player, short, int> OnItemPickup
    {
        add { AchievementHooks.InstallHelper(); itemPickup += value; }
        remove => itemPickup -= value;
    }

    public static event Action<short, int> OnItemCraft
    {
        add { AchievementHooks.InstallHelper(); itemCraft += value; }
        remove => itemCraft -= value;
    }

    public static event Action<Player, ushort> OnTileDestroyed
    {
        add { AchievementHooks.InstallHelper(); tileDestroyed += value; }
        remove => tileDestroyed -= value;
    }

    public static event Action<Player, short> OnNPCKilled
    {
        add { AchievementHooks.InstallHelper(); npcKilled += value; }
        remove => npcKilled -= value;
    }

    public static event Action<int> OnProgressionEvent
    {
        add { AchievementHooks.InstallHelper(); progressionEvent += value; }
        remove => progressionEvent -= value;
    }
}
