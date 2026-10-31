using System;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static partial class NPCLoader
{
    internal const NPCMethod PlayerContactMethods = NPCMethod.CanHitPlayer | NPCMethod.ModifyHitPlayer | NPCMethod.OnHitPlayer;

    internal const NPCMethod NPCContactMethods = NPCMethod.CanHitNPC | NPCMethod.CanBeHitByNPC | NPCMethod.ModifyHitNPC | NPCMethod.OnHitNPC;

    public static bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot)
    {
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.CanHitPlayer)) continue;
            try
            {
                if (!global!.CanHitPlayer(npc, target, ref cooldownSlot)) return false;
            }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.CanHitPlayer", e); }
        }
        ModNPC? own = InstanceOf(npc);
        if (!Has(own, NPCMethod.CanHitPlayer)) return true;
        try { return own!.CanHitPlayer(target, ref cooldownSlot); }
        catch (Exception e)
        {
            ItemHooks.Report($"{own!.FullName}.CanHitPlayer", e);
            return true;
        }
    }

    public static void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.ModifyHitPlayer))
        {
            try { own!.ModifyHitPlayer(target, ref modifiers); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.ModifyHitPlayer", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.ModifyHitPlayer)) continue;
            try { global!.ModifyHitPlayer(npc, target, ref modifiers); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.ModifyHitPlayer", e); }
        }
    }

    public static void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.OnHitPlayer))
        {
            try { own!.OnHitPlayer(target, hurtInfo); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.OnHitPlayer", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.OnHitPlayer)) continue;
            try { global!.OnHitPlayer(npc, target, hurtInfo); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.OnHitPlayer", e); }
        }
    }

    public static bool CanHitNPC(NPC npc, NPC target)
    {
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.CanHitNPC)) continue;
            try
            {
                if (!global!.CanHitNPC(npc, target)) return false;
            }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.CanHitNPC", e); }
        }
        foreach (GlobalNPC? global in Slots(target))
        {
            if (!Has(global, NPCMethod.CanBeHitByNPC)) continue;
            try
            {
                if (!global!.CanBeHitByNPC(target, npc)) return false;
            }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.CanBeHitByNPC", e); }
        }
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.CanHitNPC))
        {
            try
            {
                if (!own!.CanHitNPC(target)) return false;
            }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.CanHitNPC", e); }
        }
        ModNPC? victim = InstanceOf(target);
        if (!Has(victim, NPCMethod.CanBeHitByNPC)) return true;
        try { return victim!.CanBeHitByNPC(npc); }
        catch (Exception e)
        {
            ItemHooks.Report($"{victim!.FullName}.CanBeHitByNPC", e);
            return true;
        }
    }

    public static void ModifyHitNPC(NPC npc, NPC target, ref NPC.HitModifiers modifiers)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.ModifyHitNPC))
        {
            try { own!.ModifyHitNPC(target, ref modifiers); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.ModifyHitNPC", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.ModifyHitNPC)) continue;
            try { global!.ModifyHitNPC(npc, target, ref modifiers); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.ModifyHitNPC", e); }
        }
    }

    public static void OnHitNPC(NPC npc, NPC target, in NPC.HitInfo hit)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.OnHitNPC))
        {
            try { own!.OnHitNPC(target, hit); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.OnHitNPC", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.OnHitNPC)) continue;
            try { global!.OnHitNPC(npc, target, hit); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.OnHitNPC", e); }
        }
    }
}
