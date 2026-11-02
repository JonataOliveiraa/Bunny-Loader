using System;
using System.Collections.Generic;
using BunnyLoader.Interop;
using log4net;
using Terraria;
using Terraria.Achievements;
using Terraria.GameContent.Achievements;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsAchievement.Content;

public sealed class Probe
{
    public static readonly List<string> Completed = new();
    public static int Kills;
    public static int Pickups;
}

public sealed class FlagAchievement : ModAchievement
{
    public override int Index => 0;

    public override void SetStaticDefaults() => AddCondition();

    public override void OnCompleted(Achievement achievement) => Probe.Completed.Add(Name);
}

public sealed class CountAchievement : ModAchievement
{
    public CustomIntCondition Condition;

    public override bool Hidden => Condition.Value == 0;

    public override void SetStaticDefaults()
    {
        Achievement.SetCategory(AchievementCategory.Collector);
        Condition = AddIntCondition(5);
    }

    public override void OnCompleted(Achievement achievement) => Probe.Completed.Add(Name);
}

public sealed class KillAchievement : ModAchievement
{
    public override int Index => 1;

    public override void SetStaticDefaults() => AddNPCKilledCondition(NPCID.BlueSlime);

    public override void OnCompleted(Achievement achievement) => Probe.Completed.Add(Name);
}

public sealed class MultiAchievement : ModAchievement
{
    public override void SetStaticDefaults()
    {
        AddItemPickupCondition("Gel", ItemID.Gel);
        AddItemCraftCondition("Torch", ItemID.Torch);
    }

    public override void OnCompleted(Achievement achievement) => Probe.Completed.Add(Name);
}

public sealed class CsAchievementMod : Mod
{
    static int frames, pass, fail;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsAchievementMod>().Logger;

    public override void Load()
    {
        On_Player.Update += Update;
        AchievementsHelper.OnNPCKilled += (player, npcId) => { if (npcId == NPCID.BlueSlime) Probe.Kills++; };
        AchievementsHelper.OnItemPickup += (player, itemId, count) => { if (itemId == ItemID.Gel) Probe.Pickups += count; };
    }

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer || done || ++frames < 30)
            return;
        done = true;
        try { Run(); }
        catch (Exception e) { Report("run", false, e.ToString()); }
        Log.Info($"csachievement END pass={pass} fail={fail}");
    }

    static Achievement Get<T>() where T : ModAchievement => ModContent.GetInstance<T>().Achievement;

    static void Run()
    {
        AchievementManager manager = Main.Achievements;
        Achievement flag = Get<FlagAchievement>(), count = Get<CountAchievement>(), kill = Get<KillAchievement>(), multi = Get<MultiAchievement>();
        Log.Info($"csachievement persisted={flag.IsCompleted && multi.IsCompleted}");
        foreach (string name in new[] { flag.Name!, count.Name!, kill.Name!, multi.Name! })
            manager.Clear(name);

        Report("the achievements are in the game's manager under Mod/Class", manager.GetAchievement("CsAchievement/FlagAchievement") == flag && !flag.IsNull, flag.Name ?? "null");
        Report("FriendlyName and Description come from Localization", flag.FriendlyName.Value == "Flag Test" && flag.Description.Value == "Complete the flag.",
            $"{flag.FriendlyName.Value} | {flag.Description.Value}");
        Report("the name falls back to the pretty class name", count.FriendlyName.Value == "Count Achievement", count.FriendlyName.Value ?? "null");
        Report("the category defaults to Slayer and SetCategory changes it", flag.Category == AchievementCategory.Slayer && count.Category == AchievementCategory.Collector,
            $"{flag.Category} {count.Category}");
        Report("the icon index is registered", manager.GetIconIndex(flag.Name) == 0 && manager.GetIconIndex(kill.Name) == 1, $"{manager.GetIconIndex(flag.Name)} {manager.GetIconIndex(kill.Name)}");
        Report("AutoStaticDefaults gives the int condition and many conditions a tracker, and the flag none",
            count.HasTracker && multi.HasTracker && !flag.HasTracker, $"{count.HasTracker} {multi.HasTracker} {flag.HasTracker}");
        Report("a key names the condition", !multi.GetCondition("Gel").IsNull && !multi.GetCondition("Torch").IsNull, "");

        int vanillaBroken = 0;
        var list = new GameList(manager.CreateAchievementsList());
        for (int i = 0; i < list.Count; i++)
        {
            Achievement a = list.Get<Achievement>(i);
            if (a.Name != null && !a.Name.Contains('/') && a._completedCount > 0 && !a.IsCompleted && a._completedCount > ConditionsOf(a)) vanillaBroken++;
        }
        Report("reading the save again keeps the vanilla counts", vanillaBroken == 0, $"broken={vanillaBroken}");

        flag.GetCondition("Condition").Complete();
        Report("completing the flag completes the achievement and runs OnCompleted once", flag.IsCompleted && Probe.Completed.FindAll(n => n == "FlagAchievement").Count == 1,
            string.Join(",", Probe.Completed));

        ModAchievement countMod = ModContent.GetInstance<CountAchievement>();
        bool hiddenBefore = countMod.Hidden;
        CountAchievement countInstance = ModContent.GetInstance<CountAchievement>();
        countInstance.Condition.Value += 3;
        bool partial = !count.IsCompleted && !countMod.Hidden;
        countInstance.Condition.Value += 2;
        Report("the int condition completes at its max and Hidden follows the value", hiddenBefore && partial && count.IsCompleted && Probe.Completed.Contains("CountAchievement"),
            $"hidden={hiddenBefore} partial={partial} done={count.IsCompleted}");

        Player player = Main.player[Main.myPlayer];
        AchievementsHelper.NotifyNPCKilledDirect(player, NPCID.BlueSlime);
        Report("the NPC killed condition listens to the game and AchievementsHelper.OnNPCKilled fires", kill.IsCompleted && Probe.Kills == 1 && Probe.Completed.Contains("KillAchievement"),
            $"done={kill.IsCompleted} kills={Probe.Kills}");

        Item gel = Item.New();
        gel.SetDefaults(ItemID.Gel, default);
        gel.stack = 3;
        AchievementsHelper.NotifyItemPickup(player, gel);
        Report("one of two conditions leaves the achievement open; OnItemPickup fires with the stack", !multi.IsCompleted && multi.GetCondition("Gel").IsCompleted && Probe.Pickups == 3,
            $"done={multi.IsCompleted} gel={multi.GetCondition("Gel").IsCompleted} pickups={Probe.Pickups}");
        multi.GetCondition("Torch").Complete();
        Report("the second condition completes it", multi.IsCompleted && Probe.Completed.Contains("MultiAchievement"), $"done={multi.IsCompleted}");
        manager.Save();

        manager.Clear(count.Name);
        manager.Clear(kill.Name);
        GUIAchievementsMenu menu = GUIInstance.Active.GUIAchievementsMenu;
        menu.Open(0, -1);
        var sorted = new GameList(menu.SortedAchievementsData);
        var found = new List<string>();
        for (int k = 0; !sorted.IsNull && k < sorted.Count; k++)
        {
            string? name = sorted.Get<Achievement>(k).Name;
            if (name != null && name.Contains('/')) found.Add($"{k}:{name}");
        }
        Report("the achievements menu lists the mod achievements, the open ones last", found.Count == 4 && found[^1].EndsWith("Achievement") &&
            !found[0].Contains("Count") && !found[0].Contains("Kill"), string.Join(" ", found));
    }

    static int ConditionsOf(Achievement a)
    {
        var conditions = new GameDictionary(a._conditions);
        return conditions.IsNull ? 0 : conditions.Count;
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok)
            pass++;
        else
            fail++;
        Log.Info($"csachievement {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
