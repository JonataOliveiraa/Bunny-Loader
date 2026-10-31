using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static partial class NPCLoader
{
    public static void TownNPCAttackStrength(NPC npc, ref int damage, ref float knockback)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.TownAttack))
        {
            try { own!.TownNPCAttackStrength(ref damage, ref knockback); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.TownNPCAttackStrength", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.TownAttack)) continue;
            try { global!.TownNPCAttackStrength(npc, ref damage, ref knockback); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.TownNPCAttackStrength", e); }
        }
    }

    public static void TownNPCAttackCooldown(NPC npc, ref int cooldown, ref int randExtraCooldown)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.TownAttack))
        {
            try { own!.TownNPCAttackCooldown(ref cooldown, ref randExtraCooldown); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.TownNPCAttackCooldown", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.TownAttack)) continue;
            try { global!.TownNPCAttackCooldown(npc, ref cooldown, ref randExtraCooldown); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.TownNPCAttackCooldown", e); }
        }
    }

    public static void TownNPCAttackProj(NPC npc, ref int projType, ref int attackDelay)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.TownAttack))
        {
            try { own!.TownNPCAttackProj(ref projType, ref attackDelay); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.TownNPCAttackProj", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.TownAttack)) continue;
            try { global!.TownNPCAttackProj(npc, ref projType, ref attackDelay); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.TownNPCAttackProj", e); }
        }
    }

    public static void TownNPCAttackProjSpeed(NPC npc, ref float multiplier, ref float gravityCorrection, ref float randomOffset)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.TownAttack))
        {
            try { own!.TownNPCAttackProjSpeed(ref multiplier, ref gravityCorrection, ref randomOffset); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.TownNPCAttackProjSpeed", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.TownAttack)) continue;
            try { global!.TownNPCAttackProjSpeed(npc, ref multiplier, ref gravityCorrection, ref randomOffset); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.TownNPCAttackProjSpeed", e); }
        }
    }

    public static void TownNPCAttackShoot(NPC npc, ref bool inBetweenShots)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.TownAttack))
        {
            try { own!.TownNPCAttackShoot(ref inBetweenShots); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.TownNPCAttackShoot", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.TownAttack)) continue;
            try { global!.TownNPCAttackShoot(npc, ref inBetweenShots); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.TownNPCAttackShoot", e); }
        }
    }

    public static void TownNPCAttackMagic(NPC npc, ref float auraLightMultiplier)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.TownAttack))
        {
            try { own!.TownNPCAttackMagic(ref auraLightMultiplier); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.TownNPCAttackMagic", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.TownAttack)) continue;
            try { global!.TownNPCAttackMagic(npc, ref auraLightMultiplier); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.TownNPCAttackMagic", e); }
        }
    }

    public static void TownNPCAttackSwing(NPC npc, ref int itemWidth, ref int itemHeight)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.TownAttack))
        {
            try { own!.TownNPCAttackSwing(ref itemWidth, ref itemHeight); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.TownNPCAttackSwing", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.TownAttack)) continue;
            try { global!.TownNPCAttackSwing(npc, ref itemWidth, ref itemHeight); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.TownNPCAttackSwing", e); }
        }
    }

    public static void DrawTownAttackGun(NPC npc, ref Texture2D item, ref Rectangle itemFrame, ref float scale, ref int horizontalHoldoutOffset)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.TownAttackDraw))
        {
            try { own!.DrawTownAttackGun(ref item, ref itemFrame, ref scale, ref horizontalHoldoutOffset); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.DrawTownAttackGun", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.TownAttackDraw)) continue;
            try { global!.DrawTownAttackGun(npc, ref item, ref itemFrame, ref scale, ref horizontalHoldoutOffset); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.DrawTownAttackGun", e); }
        }
    }

    public static void DrawTownAttackSwing(NPC npc, ref Texture2D item, ref Rectangle itemFrame, ref int itemSize, ref float scale, ref Vector2 offset)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.TownAttackDraw))
        {
            try { own!.DrawTownAttackSwing(ref item, ref itemFrame, ref itemSize, ref scale, ref offset); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.DrawTownAttackSwing", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.TownAttackDraw)) continue;
            try { global!.DrawTownAttackSwing(npc, ref item, ref itemFrame, ref itemSize, ref scale, ref offset); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.DrawTownAttackSwing", e); }
        }
    }

}
