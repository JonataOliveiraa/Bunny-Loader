using System;
using System.Linq;
using log4net;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsAngler.Content;

public sealed class Probe
{
    public static bool Available = true;
}

public sealed class QuestFish : ModItem
{
    public override void SetDefaults() => Item.DefaultToQuestFish();

    public override bool IsQuestFish() => true;

    public override bool IsAnglerQuestAvailable() => Probe.Available;

    public override void AnglerQuestChat(ref string description, ref string catchLocation)
    {
        description = "A fish for the test.";
        catchLocation = "Caught in the test.";
    }
}

public sealed class PlainFish : ModItem
{
    public override void SetDefaults() => Item.DefaultToQuestFish();
}

public sealed class AnglerGlobal : GlobalItem
{
    public override bool IsAnglerQuestAvailable(int type) => type != ItemID.Batfish;

    public override void AnglerChat(int type, ref string chat, ref string catchLocation)
    {
        if (type == ModContent.ItemType<QuestFish>())
            chat += " (global)";
    }
}

public sealed class CsAnglerMod : Mod
{
    static int frames, pass, fail;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsAnglerMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu || i != Main.myPlayer || done || ++frames < 30)
            return;
        done = true;
        int quest = Main.anglerQuest;
        bool finished = Main.anglerQuestFinished;
        try { Run(); }
        catch (Exception e) { Report("run", false, e.ToString()); }
        Main.anglerQuest = quest;
        Main.anglerQuestFinished = finished;
        Log.Info($"csangler END pass={pass} fail={fail}");
    }

    static void Run()
    {
        int fish = ModContent.ItemType<QuestFish>(), plain = ModContent.ItemType<PlainFish>();
        int[] ids = Main.anglerQuestItemNetIDs.AsSpan().ToArray();
        int index = Array.IndexOf(ids, fish);
        Report("IsQuestFish adds the item to the end of the quest list, once", index == ids.Length - 1 && ids.Count(id => id == fish) == 1 && !ids.Contains(plain),
            $"index={index} length={ids.Length}");

        Main.anglerQuestFinished = false;
        Main.anglerQuest = index;
        Main.npcChatCornerItem = 0;
        string? chat = Lang.AnglerQuestChat(false);
        Report("the Angler's text comes from AnglerQuestChat and GlobalItem.AnglerChat", chat == "A fish for the test. (global)\n\n(Caught in the test.)" && Main.npcChatCornerItem == fish,
            $"'{chat?.Replace('\n', '|')}' corner={Main.npcChatCornerItem}");
        string? turnIn = Lang.AnglerQuestChat(true);
        Report("turning the fish in keeps the game's text", turnIn != null && !turnIn.Contains("A fish for the test."), turnIn?.Replace('\n', '|') ?? "null");

        Report("AnglerChat without a text gives null and leaves the game's text", ItemLoader.AnglerChat(ItemID.Batfish) == null, "");
        bool batfish = false, own = false;
        ItemLoader.IsAnglerQuestAvailable(ItemID.Batfish, ref batfish);
        ItemLoader.IsAnglerQuestAvailable(fish, ref own);
        Report("IsAnglerQuestAvailable joins the item and the globals", batfish && !own, $"batfish={batfish} own={own}");

        Probe.Available = false;
        int hits = 0;
        for (int k = 0; k < 60; k++)
        {
            Main.anglerQuestFinished = false;
            Main.AnglerQuestSwap();
            int type = Main.anglerQuestItemNetIDs[Main.anglerQuest];
            if (type == fish || type == ItemID.Batfish) hits++;
        }
        Report("the daily swap skips the unavailable fish", hits == 0, $"hits={hits}");

        Probe.Available = true;
        int found = 0;
        for (int k = 0; k < 600 && found == 0; k++)
        {
            Main.anglerQuestFinished = false;
            Main.AnglerQuestSwap();
            if (Main.anglerQuestItemNetIDs[Main.anglerQuest] == fish) found = k + 1;
        }
        Report("an available mod fish can be the daily quest", found > 0, $"after={found}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok)
            pass++;
        else
            fail++;
        Log.Info($"csangler {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
