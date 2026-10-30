using System;
using BunnyLoader;
using Terraria.DataStructures;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static unsafe partial class NPCLoader
{
    internal static GlobalNPC?[] Slots(NPC npc) => Globals.Slots(npc) ?? Array.Empty<GlobalNPC?>();

    static bool Has(ModNPC? npc, NPCMethod method) => npc != null && (npc.Hooks & method) != 0;
    static bool Has(GlobalNPC? global, NPCMethod method) => global != null && (global.Hooks & method) != 0;

    public static void NPCAI(NPC npc)
    {
        ModNPC? own = InstanceOf(npc);
        GlobalNPC?[] globals = Slots(npc);
        if (PreAI(npc, own, globals))
        {
            int type = npc.type;
            var previous = NPCEventHooks.Borrowing;
            if (own?.AIType is > 0)
            {
                NPCEventHooks.Borrowing = new() { Pointer = npc.Pointer, Instance = own };
                npc.type = own.AIType;
            }
            try { npc.VanillaAI(); }
            finally
            {
                npc.type = type;
                NPCEventHooks.Borrowing = previous;
            }
            AI(npc, own, globals);
        }
        PostAI(npc, own, globals);
    }

    public static bool PreAI(NPC npc) => PreAI(npc, InstanceOf(npc), Slots(npc));

    static bool PreAI(NPC npc, ModNPC? own, GlobalNPC?[] globals)
    {
        bool allowed = true;
        foreach (GlobalNPC? global in globals)
        {
            if (!Has(global, NPCMethod.PreAI)) continue;
            try { allowed &= global!.PreAI(npc); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.PreAI", e); }
        }
        if (allowed && Has(own, NPCMethod.PreAI))
        {
            try { allowed = own!.PreAI(); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.PreAI", e); }
        }
        return allowed;
    }

    public static void AI(NPC npc) => AI(npc, InstanceOf(npc), Slots(npc));

    static void AI(NPC npc, ModNPC? own, GlobalNPC?[] globals)
    {
        if (Has(own, NPCMethod.AI))
        {
            try { own!.AI(); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.AI", e); }
        }
        foreach (GlobalNPC? global in globals)
        {
            if (!Has(global, NPCMethod.AI)) continue;
            try { global!.AI(npc); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.AI", e); }
        }
    }

    public static void PostAI(NPC npc) => PostAI(npc, InstanceOf(npc), Slots(npc));

    static void PostAI(NPC npc, ModNPC? own, GlobalNPC?[] globals)
    {
        if (Has(own, NPCMethod.PostAI))
        {
            try { own!.PostAI(); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.PostAI", e); }
        }
        foreach (GlobalNPC? global in globals)
        {
            if (!Has(global, NPCMethod.PostAI)) continue;
            try { global!.PostAI(npc); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.PostAI", e); }
        }
    }

    public static void FindFrame(NPC npc, int frameHeight)
    {
        ModNPC? own = InstanceOf(npc);
        var previous = NPCEventHooks.Borrowing;
        NPCEventHooks.Borrowing = new() { Pointer = npc.Pointer, Instance = own };
        try { Host.Native()->NPCOriginal(npc.Pointer, 1, own?.AnimationType ?? 0); }
        finally { NPCEventHooks.Borrowing = previous; }
        if (Has(own, NPCMethod.FindFrame))
        {
            try { own!.FindFrame(frameHeight); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.FindFrame", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.FindFrame)) continue;
            try { global!.FindFrame(npc, frameHeight); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.FindFrame", e); }
        }
    }

    public static void ResetEffects(NPC npc)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.ResetEffects))
        {
            try { own!.ResetEffects(); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.ResetEffects", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.ResetEffects)) continue;
            try { global!.ResetEffects(npc); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.ResetEffects", e); }
        }
    }

    public static bool CheckActive(NPC npc)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.CheckActive))
        {
            try { if (!own!.CheckActive()) return false; }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.CheckActive", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.CheckActive)) continue;
            try { if (!global!.CheckActive(npc)) return false; }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.CheckActive", e); }
        }
        return true;
    }

    public static bool CheckDead(NPC npc)
    {
        bool allowed = true;
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.CheckDead))
        {
            try { allowed = own!.CheckDead(); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.CheckDead", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.CheckDead)) continue;
            try { allowed &= global!.CheckDead(npc); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.CheckDead", e); }
        }
        return allowed;
    }

    public static void HitEffect(NPC npc, in NPC.HitInfo hit)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.HitEffect))
        {
            try { own!.HitEffect(hit); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.HitEffect", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.HitEffect)) continue;
            try { global!.HitEffect(npc, hit); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.HitEffect", e); }
        }
    }

    public static void UpdateLifeRegen(NPC npc, ref int damage)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.UpdateLifeRegen))
        {
            try { own!.UpdateLifeRegen(ref damage); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.UpdateLifeRegen", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.UpdateLifeRegen)) continue;
            try { global!.UpdateLifeRegen(npc, ref damage); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.UpdateLifeRegen", e); }
        }
    }

    public static void ApplyDifficultyAndPlayerScaling(NPC npc, int numPlayers, float balance, float bossAdjustment)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.ApplyDifficultyAndPlayerScaling))
        {
            try { own!.ApplyDifficultyAndPlayerScaling(numPlayers, balance, bossAdjustment); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.ApplyDifficultyAndPlayerScaling", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.ApplyDifficultyAndPlayerScaling)) continue;
            try { global!.ApplyDifficultyAndPlayerScaling(npc, numPlayers, balance, bossAdjustment); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.ApplyDifficultyAndPlayerScaling", e); }
        }
    }

    internal static void OnSpawn(NPC npc, IEntitySource source)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.OnSpawn))
        {
            try { own!.OnSpawn(source); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.OnSpawn", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.OnSpawn)) continue;
            try { global!.OnSpawn(npc, source); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.OnSpawn", e); }
        }
    }
}
