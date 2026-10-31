using System;
using Terraria.GameContent.ItemDropRules;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static partial class NPCLoader
{
    public static void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.ModifyNPCLoot))
        {
            try { own!.ModifyNPCLoot(npcLoot); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.ModifyNPCLoot", e); }
        }
        foreach (GlobalNPC global in Globals.Templates)
        {
            if (!Has(global, NPCMethod.ModifyNPCLoot)) continue;
            try
            {
                if (!global.AppliesToEntity(npc, false)) continue;
                GlobalNPC?[] slots = Slots(npc);
                GlobalNPC instance = global.StaticIndex < slots.Length ? slots[global.StaticIndex] ?? global : global;
                instance.ModifyNPCLoot(npc, npcLoot);
            }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.ModifyNPCLoot", e); }
        }
    }

    public static void ModifyGlobalLoot(GlobalLoot globalLoot)
    {
        foreach (GlobalNPC global in Globals.Templates)
        {
            if (!Has(global, NPCMethod.ModifyGlobalLoot)) continue;
            try { global.ModifyGlobalLoot(globalLoot); }
            catch (Exception e) { ItemHooks.Report($"{global.FullName}.ModifyGlobalLoot", e); }
        }
    }

    public static bool PreKill(NPC npc)
    {
        bool allowed = true;
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.PreKill)) continue;
            try { allowed &= global!.PreKill(npc); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.PreKill", e); }
        }
        if (!allowed) return false;
        ModNPC? own = InstanceOf(npc);
        if (!Has(own, NPCMethod.PreKill)) return true;
        try { return own!.PreKill(); }
        catch (Exception e) { ItemHooks.Report($"{own!.FullName}.PreKill", e); return true; }
    }

    public static void OnKill(NPC npc)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.OnKill))
        {
            try { own!.OnKill(); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.OnKill", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.OnKill)) continue;
            try { global!.OnKill(npc); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.OnKill", e); }
        }
    }

    public static void BossLoot(NPC npc, ref int potionType, ref int potionStack, ref int heartStack)
    {
        ModNPC? own = InstanceOf(npc);
        if (!Has(own, NPCMethod.BossLoot)) return;
        try { own!.BossLoot(ref potionType, ref potionStack, ref heartStack); }
        catch (Exception e) { ItemHooks.Report($"{own!.FullName}.BossLoot", e); }
    }

    public static void BossLoot(NPC npc, ref int potionType)
    {
        ModNPC? own = InstanceOf(npc);
        if (!Has(own, NPCMethod.BossLoot)) return;
        try { own!.BossLoot(ref potionType); }
        catch (Exception e) { ItemHooks.Report($"{own!.FullName}.BossLoot", e); }
    }
}
