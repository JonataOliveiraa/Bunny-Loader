using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent.UI;
using Terraria.ModLoader.Core;

namespace Terraria.ModLoader;

public static partial class NPCLoader
{
    public static int? PickEmote(NPC npc, Player closestPlayer, List<int> emoteList, WorldUIAnchor otherAnchor)
    {
        int? result = null;
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.PickEmote))
        {
            try { result = own!.PickEmote(closestPlayer, emoteList, otherAnchor); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.PickEmote", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.PickEmote)) continue;
            try
            {
                if (global!.PickEmote(npc, closestPlayer, emoteList, otherAnchor) is int emote) result = emote;
            }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.PickEmote", e); }
        }
        return result;
    }

    public static void EmoteBubblePosition(NPC npc, ref Vector2 position, ref SpriteEffects spriteEffects)
    {
        ModNPC? own = InstanceOf(npc);
        if (Has(own, NPCMethod.EmoteBubblePosition))
        {
            try { own!.EmoteBubblePosition(ref position, ref spriteEffects); }
            catch (Exception e) { ItemHooks.Report($"{own!.FullName}.EmoteBubblePosition", e); }
        }
        foreach (GlobalNPC? global in Slots(npc))
        {
            if (!Has(global, NPCMethod.EmoteBubblePosition)) continue;
            try { global!.EmoteBubblePosition(npc, ref position, ref spriteEffects); }
            catch (Exception e) { ItemHooks.Report($"{global!.FullName}.EmoteBubblePosition", e); }
        }
    }
}
