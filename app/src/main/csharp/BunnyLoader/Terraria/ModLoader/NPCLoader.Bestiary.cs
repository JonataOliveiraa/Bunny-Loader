using System;
using System.Collections.Generic;
using Terraria.GameContent.Bestiary;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static partial class NPCLoader
{
    internal static bool AnyGlobal(NPCMethod method)
    {
        foreach (GlobalNPC global in Globals.Templates)
            if ((global.Hooks & method) != 0) return true;
        return false;
    }

    internal static IEnumerable<GlobalNPC> GlobalsFor(NPC npc, NPCMethod method)
    {
        GlobalNPC?[] slots = Slots(npc);
        if (slots.Length > 0)
        {
            foreach (GlobalNPC? global in slots)
                if (Has(global, method)) yield return global!;
            yield break;
        }
        foreach (GlobalNPC global in Globals.Templates)
        {
            if ((global.Hooks & method) == 0) continue;
            bool applies;
            try { applies = global.AppliesToEntity(npc, false); }
            catch (Exception e)
            {
                ItemHooks.Report($"{global.FullName}.AppliesToEntity", e);
                continue;
            }
            if (applies) yield return global;
        }
    }

    public static void SetBestiary(NPC npc, BestiaryDatabase database, BestiaryEntry bestiaryEntry) =>
        SetBestiary(npc, npc.IsNull ? null : InstanceOf(npc), database, bestiaryEntry);

    internal static void SetBestiary(NPC npc, ModNPC? own, BestiaryDatabase database, BestiaryEntry bestiaryEntry)
    {
        if (Has(own, NPCMethod.SetBestiary))
        {
            try { own!.SetBestiary(database, bestiaryEntry); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.SetBestiary", e); }
        }
        if (npc.IsNull) return;
        foreach (GlobalNPC global in GlobalsFor(npc, NPCMethod.SetBestiary))
        {
            try { global.SetBestiary(npc, database, bestiaryEntry); }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.SetBestiary", e); }
        }
    }
}
