using System;
using BunnyLoader.Interop;
using CsNpcBestiary.Content;
using log4net;
using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsNpcBestiary;

public static class Checks
{
    static int pass, fail, frames;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsNpcBestiaryMod>().Logger;

    public static void Reset()
    {
        pass = fail = frames = 0;
        done = false;
    }

    public static void Frame()
    {
        if (done || ++frames != 30) return;
        try { Run(); }
        catch (Exception e) { Report("run", false, e.ToString()); }
        done = true;
        Log.Info($"csnpcbestiary END pass={pass} fail={fail}");
    }

    static int Entries(BestiaryDatabase database, int type)
    {
        var entries = new GameList(database._entries);
        int count = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = new BestiaryEntry(entries[i]);
            if (UnlockableNPCEntryIcon.Is(entry.Icon) && new UnlockableNPCEntryIcon(entry.Icon.Pointer)._npcNetId == type) count++;
        }
        return count;
    }

    static bool Has(BestiaryEntry entry, IntPtr element)
    {
        foreach (IBestiaryInfoElement info in entry.Info)
            if (info.Pointer == element) return true;
        return false;
    }

    static int Drops(BestiaryEntry entry, int item)
    {
        int count = 0;
        foreach (IBestiaryInfoElement info in entry.Info)
            if (ItemDropBestiaryInfoElement.Is(info) && new ItemDropBestiaryInfoElement(info.Pointer)._droprateInfo.itemId == item) count++;
        return count;
    }

    static void Run()
    {
        BestiaryDatabase database = Main.BestiaryDB;
        int beast = ModContent.NPCType<BeastNpc>(), hidden = ModContent.NPCType<HiddenNpc>();
        BestiaryEntry entry = database.FindEntryByNPCID(beast);
        int count = Entries(database, beast);
        Report("a mod NPC gets one bestiary entry and its SetBestiary runs once", count == 1 && BeastNpc.Calls == 1, $"entries={count} calls={BeastNpc.Calls}");
        Report("SetBestiary elements reach the entry",
            Has(entry, BeastNpc.Flavor) && Has(entry, BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface.Pointer), $"info={entry.Info.Count}");
        Report("the mod NPC drops are listed in its entry", Drops(entry, ItemID.Gel) == 1, $"gel={Drops(entry, ItemID.Gel)}");
        int stars = ContentSamples.NpcBestiaryRarityStars.TryGetValue(beast, out int value) ? value : -1;
        Report("NpcBestiaryRarityStars keeps the mod value", stars == 4, $"stars={stars}");
        bool modifiers = NPCID.Sets.NPCBestiaryDrawOffset.TryGetValue(beast, out var drawModifiers);
        Report("NPCBestiaryDrawOffset stores the draw modifiers",
            modifiers && drawModifiers.Velocity == 1f && drawModifiers.Direction.HasValue && drawModifiers.Direction.Value == 1 && drawModifiers.Scale == 1f &&
            drawModifiers.CustomTexturePath == "CsNpcBestiary/Content/BeastIcon",
            $"found={modifiers} velocity={drawModifiers.Velocity} direction={drawModifiers.Direction} scale={drawModifiers.Scale} texture={(string?)drawModifiers.CustomTexturePath}");
        bool icon = UnlockableNPCEntryIcon.Is(entry.Icon) && !new UnlockableNPCEntryIcon(entry.Icon.Pointer)._customTexture.IsNull;
        Report("a mod CustomTexturePath becomes the entry icon", icon, $"icon={icon}");
        int hiddenCount = Entries(database, hidden);
        Report("Hide keeps the NPC out of the bestiary", hiddenCount == 0 && HiddenNpc.Calls == 0, $"entries={hiddenCount} calls={HiddenNpc.Calls}");
        BestiaryEntry bunny = database.FindEntryByNPCID(NPCID.Bunny);
        Report("GlobalNPC.SetBestiary reaches a vanilla entry", BunnyGlobal.Calls >= 1 && Has(bunny, BunnyGlobal.Flavor), $"calls={BunnyGlobal.Calls}");
        Report("a global loot change refreshes the vanilla entry drops", Drops(bunny, ItemID.Diamond) == 1, $"diamond={Drops(bunny, ItemID.Diamond)}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csnpcbestiary {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
