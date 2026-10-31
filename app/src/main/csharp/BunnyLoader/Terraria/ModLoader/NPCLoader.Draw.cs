using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static partial class NPCLoader
{
    internal const NPCMethod DrawMethods = NPCMethod.DrawEffects | NPCMethod.PreDraw | NPCMethod.PostDraw;

    internal const NPCMethod OwnDrawMethods = NPCMethod.PreDraw | NPCMethod.PostDraw;

    public static void BossHeadSlot(NPC npc, ref int index)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.BossHeadSlot))
        {
            try { own!.BossHeadSlot(ref index); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.BossHeadSlot", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.BossHeadSlot)) continue;
            try { global!.BossHeadSlot(npc, ref index); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.BossHeadSlot", e); }
        }
    }

    public static void BossHeadRotation(NPC npc, ref float rotation)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.BossHeadRotation))
        {
            try { own!.BossHeadRotation(ref rotation); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.BossHeadRotation", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.BossHeadRotation)) continue;
            try { global!.BossHeadRotation(npc, ref rotation); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.BossHeadRotation", e); }
        }
    }

    public static void BossHeadSpriteEffects(NPC npc, ref SpriteEffects spriteEffects)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.BossHeadSpriteEffects))
        {
            try { own!.BossHeadSpriteEffects(ref spriteEffects); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.BossHeadSpriteEffects", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.BossHeadSpriteEffects)) continue;
            try { global!.BossHeadSpriteEffects(npc, ref spriteEffects); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.BossHeadSpriteEffects", e); }
        }
    }

    public static Color? GetAlpha(NPC npc, Color lightColor)
    {
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.GetAlpha)) continue;
            try
            {
                if (global!.GetAlpha(npc, lightColor) is Color color) return color;
            }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.GetAlpha", e); }
        }
        ModNPC? own = InstanceOf(npc);
        if (!Has(own, NPCMethod.GetAlpha)) return null;
        try { return own!.GetAlpha(lightColor); }
        catch (Exception e)
        {
            ItemHooks.Report($"{own!.FullName}.GetAlpha", e);
            return null;
        }
    }

    public static void DrawEffects(NPC npc, ref Color drawColor)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.DrawEffects))
        {
            try { own!.DrawEffects(ref drawColor); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.DrawEffects", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.DrawEffects)) continue;
            try { global!.DrawEffects(npc, ref drawColor); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.DrawEffects", e); }
        }
    }

    public static bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        bool result = true;
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.PreDraw)) continue;
            try { result &= global!.PreDraw(npc, spriteBatch, screenPos, drawColor); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.PreDraw", e); }
        }
        ModNPC? own = InstanceOf(npc);
        if (!result || !Has(own, NPCMethod.PreDraw)) return result;
        try { return own!.PreDraw(spriteBatch, screenPos, drawColor); }
        catch (Exception e)
        {
            ItemHooks.Report($"{own!.FullName}.PreDraw", e);
            return true;
        }
    }

    public static void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.PostDraw))
        {
            try { own!.PostDraw(spriteBatch, screenPos, drawColor); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.PostDraw", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.PostDraw)) continue;
            try { global!.PostDraw(npc, spriteBatch, screenPos, drawColor); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.PostDraw", e); }
        }
    }

    internal static void DrawBehind(NPC npc, int index)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.DrawBehind))
        {
            try { own!.DrawBehind(index); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.DrawBehind", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.DrawBehind)) continue;
            try { global!.DrawBehind(npc, index); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.DrawBehind", e); }
        }
    }
}
