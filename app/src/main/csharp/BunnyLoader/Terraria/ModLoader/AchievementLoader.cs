using System.Collections.Generic;
using BunnyLoader.Interop;
using Terraria.Achievements;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static class AchievementLoader
{
    static readonly object Gate = new();
    static readonly List<ModAchievement> achievements = new();
    static readonly Dictionary<string, ModAchievement> byName = new();

    public static int AchievementCount
    {
        get
        {
            lock (Gate) return achievements.Count;
        }
    }

    internal static void Add(ModAchievement achievement)
    {
        lock (Gate) achievements.Add(achievement);
    }

    internal static void Setup(ModAchievement achievement)
    {
        AchievementManager manager = Main.Achievements;
        manager.Register(achievement.Achievement);
        manager.RegisterIconIndex(achievement.Achievement.Name, achievement.Index);
        lock (Gate) byName[achievement.FullName] = achievement;
        AchievementHooks.Install();
    }

    internal static ModAchievement? Of(Achievement achievement)
    {
        if (achievement.IsNull) return null;
        string? name = achievement.Name;
        if (name == null) return null;
        lock (Gate) return byName.GetValueOrDefault(name);
    }

    internal static void FinishSetup()
    {
        lock (Gate)
            if (byName.Count == 0) return;
        AchievementManager manager = Main.Achievements;
        var kept = new List<(Achievement Achievement, int Count)>();
        var list = new GameList(manager.CreateAchievementsList());
        for (int i = 0; i < list.Count; i++)
        {
            Achievement achievement = list.Get<Achievement>(i);
            if (Of(achievement) == null) kept.Add((achievement, achievement._completedCount));
        }
        try
        {
            manager.Load(manager._savePath, manager._isCloudSave);
        }
        finally
        {
            foreach ((Achievement achievement, int count) in kept)
                achievement._completedCount = count;
        }
    }
}
