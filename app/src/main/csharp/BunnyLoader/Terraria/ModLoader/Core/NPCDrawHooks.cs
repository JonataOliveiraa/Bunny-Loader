using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Graphics.Light;

namespace Terraria.ModLoader.Core;

internal static class NPCDrawHooks
{
    [ThreadStatic] static (IntPtr Pointer, Color Light)? drawLight;

    internal static void Install(NPCMethod methods)
    {
        if ((methods & NPCLoader.DrawMethods) != 0)
            ItemHooks.Once("NPCDraw", () =>
            {
                On_Main.DrawNPCDirect_SpriteBatch_NPC_bool_Vector2 += DrawNPCDirect;
                On_Main.DrawNPCDirect_SpriteBatch_NPC_bool_Vector2_LightMap_refRectangle += DrawNPCDirectLit;
                On_NPC.GetNPCColorTintedByBuffs += TintedByBuffs;
            });
        if ((methods & NPCMethod.GetAlpha) != 0) ItemHooks.Once("NPCAlpha", () => On_NPC.GetAlpha += GetAlpha);
        if ((methods & NPCMethod.DrawBehind) != 0) ItemHooks.Once("NPCDrawBehind", () => On_Main.CacheNPCDraws += CacheNPCDraws);
        if ((methods & NPCMethod.BossHeadSlot) != 0) ItemHooks.Once("NPCBossHeadSlot", () => On_NPC.GetBossHeadTextureIndex += BossHeadSlot);
        if ((methods & NPCMethod.BossHeadRotation) != 0) ItemHooks.Once("NPCBossHeadRotation", () => On_NPC.GetBossHeadRotation += BossHeadRotation);
        if ((methods & NPCMethod.BossHeadSpriteEffects) != 0)
            ItemHooks.Once("NPCBossHeadSpriteEffects", () => On_NPC.GetBossHeadSpriteEffects += BossHeadSpriteEffects);
    }

    struct Drawing
    {
        public Color Light;
        public bool Own;
        public (IntPtr Pointer, Color Light)? Outer;
    }

    static void DrawNPCDirect(On_Main.orig_DrawNPCDirect_SpriteBatch_NPC_bool_Vector2 orig, Main self, SpriteBatch mySpriteBatch, NPC rCurrentNPC,
        bool behindTiles, Vector2 screenPos)
    {
        if (!NPCLoader.HasHooks(rCurrentNPC, NPCLoader.DrawMethods))
        {
            orig(self, mySpriteBatch, rCurrentNPC, behindTiles, screenPos);
            return;
        }
        if (Before(rCurrentNPC, mySpriteBatch, screenPos, out Drawing drawing))
        {
            try { orig(self, mySpriteBatch, rCurrentNPC, behindTiles, screenPos); }
            finally { drawLight = drawing.Outer; }
        }
        After(rCurrentNPC, mySpriteBatch, screenPos, in drawing);
    }

    static void DrawNPCDirectLit(On_Main.orig_DrawNPCDirect_SpriteBatch_NPC_bool_Vector2_LightMap_refRectangle orig, Main self, SpriteBatch mySpriteBatch,
        NPC rCurrentNPC, bool behindTiles, Vector2 screenPos, LightMap lightMap, ref Rectangle lightRegion)
    {
        if (!NPCLoader.HasHooks(rCurrentNPC, NPCLoader.DrawMethods))
        {
            orig(self, mySpriteBatch, rCurrentNPC, behindTiles, screenPos, lightMap, ref lightRegion);
            return;
        }
        if (Before(rCurrentNPC, mySpriteBatch, screenPos, out Drawing drawing))
        {
            try { orig(self, mySpriteBatch, rCurrentNPC, behindTiles, screenPos, lightMap, ref lightRegion); }
            finally { drawLight = drawing.Outer; }
        }
        After(rCurrentNPC, mySpriteBatch, screenPos, in drawing);
    }

    static bool Before(NPC npc, SpriteBatch batch, Vector2 screenPos, out Drawing drawing)
    {
        Vector2 center = npc.Center;
        Color light = float.IsFinite(center.X) && float.IsFinite(center.Y) ? Lighting.GetColor((int)(center.X / 16f), (int)(center.Y / 16f)) : Color.White;
        Color original = light;
        NPCLoader.DrawEffects(npc, ref light);
        drawing = new Drawing { Light = light, Own = NPCLoader.HasHooks(npc, NPCLoader.OwnDrawMethods), Outer = drawLight };
        if (drawing.Own) DrawBatches.Restart(batch);
        if (!NPCLoader.PreDraw(npc, batch, screenPos, light)) return false;
        drawLight = light.Equals(original) ? null : (npc.Pointer, light);
        return true;
    }

    static void After(NPC npc, SpriteBatch batch, Vector2 screenPos, in Drawing drawing)
    {
        NPCLoader.PostDraw(npc, batch, screenPos, drawing.Light);
        if (drawing.Own) DrawBatches.Restart(batch);
    }

    static Color TintedByBuffs(On_NPC.orig_GetNPCColorTintedByBuffs orig, NPC self, Color npcColor)
    {
        if (drawLight is { } light && light.Pointer == self.Pointer) npcColor = light.Light;
        return orig(self, npcColor);
    }

    static Color GetAlpha(On_NPC.orig_GetAlpha orig, NPC self, Color newColor)
    {
        if (NPCLoader.HasHooks(self, NPCMethod.GetAlpha) && NPCLoader.GetAlpha(self, self.IsABestiaryIconDummy ? Color.White : newColor) is Color color) return color;
        return orig(self, newColor);
    }

    static void CacheNPCDraws(On_Main.orig_CacheNPCDraws orig, Main self)
    {
        orig(self);
        for (int i = 0; i < 200; i++)
        {
            NPC npc = Main.npc[i];
            if (npc.active && NPCLoader.HasHooks(npc, NPCMethod.DrawBehind)) NPCLoader.DrawBehind(npc, i);
        }
    }

    static int BossHeadSlot(On_NPC.orig_GetBossHeadTextureIndex orig, NPC self)
    {
        int index = orig(self);
        if (NPCLoader.HasHooks(self, NPCMethod.BossHeadSlot)) NPCLoader.BossHeadSlot(self, ref index);
        return index;
    }

    static float BossHeadRotation(On_NPC.orig_GetBossHeadRotation orig, NPC self)
    {
        float rotation = orig(self);
        if (NPCLoader.HasHooks(self, NPCMethod.BossHeadRotation)) NPCLoader.BossHeadRotation(self, ref rotation);
        return rotation;
    }

    static SpriteEffects BossHeadSpriteEffects(On_NPC.orig_GetBossHeadSpriteEffects orig, NPC self)
    {
        SpriteEffects effects = orig(self);
        if (NPCLoader.HasHooks(self, NPCMethod.BossHeadSpriteEffects)) NPCLoader.BossHeadSpriteEffects(self, ref effects);
        return effects;
    }
}
