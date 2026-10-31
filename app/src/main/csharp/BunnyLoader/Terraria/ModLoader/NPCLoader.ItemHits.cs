using System;
using Microsoft.Xna.Framework;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static partial class NPCLoader
{
    internal const NPCMethod ItemHitMethods = NPCMethod.CanBeHitByItem | NPCMethod.CanCollideWithPlayerMeleeAttack |
        NPCMethod.ModifyIncomingHit | NPCMethod.ModifyHitByItem | NPCMethod.OnHitByItem;

    internal static bool HasHooks(NPC npc, NPCMethod methods)
    {
        if (Has(NPCEventHooks.Borrowed(npc) ?? GetNPC(npc.type), methods)) return true;
        foreach (GlobalNPC? global in Slots(npc))
            if (Has(global, methods)) return true;
        return false;
    }

    public static bool? CanBeHitByItem(NPC npc, Player player, Item item)
    {
        bool? result = null;
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.CanBeHitByItem)) continue;
            try
            {
                bool? next = global!.CanBeHitByItem(npc, player, item);
                if (next == false) return false;
                result ??= next;
            }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.CanBeHitByItem", e); }
        }
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.CanBeHitByItem))
        {
            try
            {
                bool? next = own!.CanBeHitByItem(player, item);
                if (next == false) return false;
                result ??= next;
            }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.CanBeHitByItem", e); }
        }
        return result;
    }

    public static bool? CanCollideWithPlayerMeleeAttack(NPC npc, Player player, Item item, Rectangle meleeAttackHitbox)
    {
        bool? result = null;
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.CanCollideWithPlayerMeleeAttack)) continue;
            try
            {
                bool? next = global!.CanCollideWithPlayerMeleeAttack(npc, player, item, meleeAttackHitbox);
                if (next == false) return false;
                result ??= next;
            }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.CanCollideWithPlayerMeleeAttack", e); }
        }
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.CanCollideWithPlayerMeleeAttack))
        {
            try
            {
                bool? next = own!.CanCollideWithPlayerMeleeAttack(player, item, meleeAttackHitbox);
                if (next == false) return false;
                result ??= next;
            }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.CanCollideWithPlayerMeleeAttack", e); }
        }
        return result;
    }

    public static void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.ModifyIncomingHit))
        {
            try { own!.ModifyIncomingHit(ref modifiers); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.ModifyIncomingHit", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.ModifyIncomingHit)) continue;
            try { global!.ModifyIncomingHit(npc, ref modifiers); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.ModifyIncomingHit", e); }
        }
    }

    public static void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.ModifyHitByItem))
        {
            try { own!.ModifyHitByItem(player, item, ref modifiers); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.ModifyHitByItem", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.ModifyHitByItem)) continue;
            try { global!.ModifyHitByItem(npc, player, item, ref modifiers); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.ModifyHitByItem", e); }
        }
    }

    public static void OnHitByItem(NPC npc, Player player, Item item, in NPC.HitInfo hit, int damageDone)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.OnHitByItem))
        {
            try { own!.OnHitByItem(player, item, hit, damageDone); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.OnHitByItem", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.OnHitByItem)) continue;
            try { global!.OnHitByItem(npc, player, item, hit, damageDone); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.OnHitByItem", e); }
        }
    }
}
