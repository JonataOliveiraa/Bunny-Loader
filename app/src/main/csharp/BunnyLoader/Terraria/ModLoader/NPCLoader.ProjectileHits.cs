using System;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static partial class NPCLoader
{
    internal const NPCMethod ProjectileHitMethods = NPCMethod.CanBeHitByProjectile | NPCMethod.ModifyIncomingHit |
        NPCMethod.ModifyHitByProjectile | NPCMethod.OnHitByProjectile;

    public static bool? CanBeHitByProjectile(NPC npc, Projectile projectile)
    {
        bool? result = null;
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.CanBeHitByProjectile)) continue;
            try
            {
                bool? next = global!.CanBeHitByProjectile(npc, projectile);
                if (next == false) return false;
                result ??= next;
            }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.CanBeHitByProjectile", e); }
        }
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.CanBeHitByProjectile))
        {
            try
            {
                bool? next = own!.CanBeHitByProjectile(projectile);
                if (next == false) return false;
                result ??= next;
            }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.CanBeHitByProjectile", e); }
        }
        return result;
    }

    public static void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.ModifyHitByProjectile))
        {
            try { own!.ModifyHitByProjectile(projectile, ref modifiers); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.ModifyHitByProjectile", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.ModifyHitByProjectile)) continue;
            try { global!.ModifyHitByProjectile(npc, projectile, ref modifiers); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.ModifyHitByProjectile", e); }
        }
    }

    public static void OnHitByProjectile(NPC npc, Projectile projectile, in NPC.HitInfo hit, int damageDone)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.OnHitByProjectile))
        {
            try { own!.OnHitByProjectile(projectile, hit, damageDone); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.OnHitByProjectile", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.OnHitByProjectile)) continue;
            try { global!.OnHitByProjectile(npc, projectile, hit, damageDone); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.OnHitByProjectile", e); }
        }
    }
}
