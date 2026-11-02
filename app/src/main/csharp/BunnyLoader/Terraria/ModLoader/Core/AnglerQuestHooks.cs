using System;
using System.Collections.Generic;
using BunnyLoader;
using BunnyLoader.Interop;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

internal static class AnglerQuestHooks
{
    const int MaxSwaps = 1000;

    static bool wanted;

    public static void Install(Func<string, bool> has, bool global)
    {
        bool hooks = global
            ? has(nameof(GlobalItem.IsAnglerQuestAvailable)) || has(nameof(GlobalItem.AnglerChat))
            : has(nameof(ModItem.IsQuestFish)) || has(nameof(ModItem.IsAnglerQuestAvailable)) || has(nameof(ModItem.AnglerQuestChat));
        if (!hooks) return;
        wanted = true;
        ItemHooks.Once("AnglerQuest", () =>
        {
            On_Main.AnglerQuestSwap += AnglerQuestSwap;
            On_Lang.AnglerQuestChat += AnglerQuestChat;
        });
    }

    public static void ContentReady()
    {
        if (!wanted) return;
        GameArray<int> ids = Main.anglerQuestItemNetIDs;
        var known = new HashSet<int>();
        foreach (int id in ids) known.Add(id);
        var added = new List<int>();
        for (int type = ItemID.Count; type < ItemLoader.ItemCount; type++)
        {
            if (ItemLoader.Get(type) is not ModItem item || known.Contains(type)) continue;
            try
            {
                if (item.IsQuestFish()) added.Add(type);
            }
            catch (Exception e) { ItemHooks.Report($"{item.FullName}.IsQuestFish", e); }
        }
        if (added.Count == 0) return;
        int old = ids.Length;
        GameArray<int> grown = ids.Resized(old + added.Count);
        for (int i = 0; i < added.Count; i++) grown[old + i] = added[i];
        Main.anglerQuestItemNetIDs = grown;
    }

    static bool Unavailable(int type)
    {
        bool notAvailable = false;
        try { ItemLoader.IsAnglerQuestAvailable(type, ref notAvailable); }
        catch (Exception e) { ItemHooks.Report("IsAnglerQuestAvailable", e); }
        return notAvailable;
    }

    static void AnglerQuestSwap(On_Main.orig_AnglerQuestSwap orig)
    {
        orig();
        for (int tries = 0; tries < MaxSwaps; tries++)
        {
            GameArray<int> ids = Main.anglerQuestItemNetIDs;
            if (Main.anglerQuestFinished || Main.anglerQuest < 0 || Main.anglerQuest >= ids.Length || !Unavailable(ids[Main.anglerQuest]))
                return;
            orig();
        }
        Main.anglerQuest = 0;
        Log.Warn("AnglerQuestSwap try count limit exceeded, a mod might have bad IsAnglerQuestAvailable logic");
    }

    static string? AnglerQuestChat(On_Lang.orig_AnglerQuestChat orig, bool turnIn)
    {
        GameArray<int> ids = Main.anglerQuestItemNetIDs;
        if (turnIn || Main.anglerQuestFinished || Main.anglerQuest < 0 || Main.anglerQuest >= ids.Length)
            return orig(turnIn);
        int type = ids[Main.anglerQuest];
        string? chat = null;
        try { chat = ItemLoader.AnglerChat(type); }
        catch (Exception e) { ItemHooks.Report("AnglerChat", e); }
        if (chat == null)
            return orig(turnIn);
        Main.npcChatCornerItem = type;
        return chat;
    }
}
