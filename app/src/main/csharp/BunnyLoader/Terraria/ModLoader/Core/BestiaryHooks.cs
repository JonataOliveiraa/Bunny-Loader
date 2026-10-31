using System;
using System.IO;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.GameContent.Bestiary;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

internal static class BestiaryHooks
{
    internal static void ContentReady()
    {
        BestiaryDatabase database = Main.BestiaryDB;
        if (database.IsNull) return;
        int added = 0;
        for (int type = NPCID.Count; type < NPCLoader.NPCCount; type++)
        {
            if (NPCLoader.GetNPC(type) is not ModNPC own) continue;
            try
            {
                if (Register(database, own)) added++;
            }
            catch (Exception e) { ItemHooks.Report($"{own.FullName} bestiary entry", e); }
        }
        bool globals = NPCLoader.AnyGlobal(NPCMethod.SetBestiary);
        bool loot = NPCLoader.AnyGlobal(NPCMethod.ModifyNPCLoot) || NPCLoader.AnyGlobal(NPCMethod.ModifyGlobalLoot);
        if (globals || loot)
        {
            for (int type = 1; type < NPCID.Count; type++)
            {
                try { Vanilla(database, type, globals, loot); }
                catch (Exception e) { ItemHooks.Report($"bestiary entry of NPC {type}", e); }
            }
        }
        if (added == 0 && !globals && !loot) return;
        ContentSamples.CreateBestiarySortingIds(database);
        Log.Info($"bestiary: {added} mod NPC entr{(added == 1 ? "y" : "ies")}");
    }

    static NPC Sample(int type) =>
        new GameDictionary(ContentSamples.NpcsByNetId).TryGetValue(type, out IntPtr npc) ? new NPC(npc) : default;

    static BestiaryEntry Find(BestiaryDatabase database, int type)
    {
        BestiaryEntry entry = database.FindEntryByNPCID(type);
        return !entry.IsNull && IndexOf(database, entry) >= 0 ? entry : default;
    }

    static int IndexOf(BestiaryDatabase database, BestiaryEntry entry)
    {
        var entries = new GameList(database._entries);
        for (int i = 0; i < entries.Count; i++)
            if (entries[i] == entry.Pointer) return i;
        return -1;
    }

    static bool Register(BestiaryDatabase database, ModNPC own)
    {
        int type = own.Type;
        BestiaryEntry generic = Find(database, type);
        if (!generic.IsNull) new GameList(database._entries).RemoveAt(IndexOf(database, generic));
        if (NPCID.Sets.NPCBestiaryDrawOffset.TryGetValue(type, out var modifiers) && modifiers.Hide) return false;
        ContentSamples.NpcBestiaryRarityStars.Reapply(type);
        NPC sample = Sample(type);
        BestiaryEntry entry;
        if (!sample.IsNull && sample.townNPC) entry = BestiaryEntry.TownNPC(type);
        else if (NPCID.Sets.CountsAsCritter[type]) entry = BestiaryEntry.Critter(type);
        else entry = BestiaryEntry.Enemy(type);
        NPCLoader.SetBestiary(sample, own, database, entry);
        Icon(own, entry);
        database.Register(entry);
        database.ExtractDropsForNPC(Main.ItemDropsDB, type);
        return true;
    }

    static void Icon(ModNPC own, BestiaryEntry entry)
    {
        string? path = NPCID.Sets.NPCBestiaryDrawOffset.ModTexture(own.Type);
        if (path == null) return;
        string file;
        try { file = ItemLoader.ResolveTexture(own, path); }
        catch (FileNotFoundException)
        {
            Log.Warn($"{own.FullName}: CustomTexturePath '{path}' is not in the mod");
            return;
        }
        var asset = new Asset<Texture2D>(path, file);
        _ = asset.Value;
        if (asset.GameAsset == IntPtr.Zero || entry.Icon.IsNull) return;
        new UnlockableNPCEntryIcon(entry.Icon.Pointer)._customTexture = new GameObject(asset.GameAsset);
    }

    static void Vanilla(BestiaryDatabase database, int type, bool globals, bool loot)
    {
        BestiaryEntry entry = Find(database, type);
        if (entry.IsNull) return;
        NPC sample = Sample(type);
        if (sample.IsNull) return;
        if (globals) NPCLoader.SetBestiary(sample, null, database, entry);
        if (!loot) return;
        entry.Info.RemoveAll(static element => ItemDropBestiaryInfoElement.Is(element));
        database.ExtractDropsForNPC(Main.ItemDropsDB, type);
    }
}
