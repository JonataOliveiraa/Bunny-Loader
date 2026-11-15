using System;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Graphics.Light;

namespace Terraria.ModLoader.Core;

internal static unsafe class NPCDrawHooks
{
    [ThreadStatic] static (IntPtr Pointer, Color Light)? drawLight;
    static int* tinting;
    static bool queueCounts;
    static readonly NPCGates.TypeSet DrawBehindTypes = new(type => NPCGates.Wants(type, NPCMethod.DrawBehind));

    internal static void Install(NPCMethod methods)
    {
        if ((methods & NPCLoader.DrawMethods) != 0)
            ItemHooks.Once("NPCDraw", () =>
            {
                On_Main.DrawNPCDirect_SpriteBatch_NPC_bool_Vector2 += DrawNPCDirect;
                On_Main.DrawNPCDirect_SpriteBatch_NPC_bool_Vector2_LightMap_refRectangle += DrawNPCDirectLit;
                tinting = NPCGates.Flag<On_NPC.hook_GetNPCColorTintedByBuffs>();
                On_NPC.GetNPCColorTintedByBuffs += TintedByBuffs;
            });
        if ((methods & NPCMethod.GetAlpha) != 0)
            ItemHooks.Once("NPCAlpha", () =>
            {
                NPCGates.Type<On_NPC.hook_GetAlpha>(0, NPCMethod.GetAlpha);
                On_NPC.GetAlpha += GetAlpha;
            });
        if ((methods & NPCMethod.DrawBehind) != 0) ItemHooks.Once("NPCDrawBehind", () => On_Main.CacheNPCDraws += CacheNPCDraws);
        if ((methods & NPCMethod.BossHeadSlot) != 0)
            ItemHooks.Once("NPCBossHeadSlot", () =>
            {
                NPCGates.Type<On_NPC.hook_GetBossHeadTextureIndex>(0, NPCMethod.BossHeadSlot);
                On_NPC.GetBossHeadTextureIndex += BossHeadSlot;
            });
        if ((methods & NPCMethod.BossHeadRotation) != 0)
            ItemHooks.Once("NPCBossHeadRotation", () =>
            {
                NPCGates.Type<On_NPC.hook_GetBossHeadRotation>(0, NPCMethod.BossHeadRotation);
                On_NPC.GetBossHeadRotation += BossHeadRotation;
            });
        if ((methods & NPCMethod.BossHeadSpriteEffects) != 0)
            ItemHooks.Once("NPCBossHeadSpriteEffects", () =>
            {
                NPCGates.Type<On_NPC.hook_GetBossHeadSpriteEffects>(0, NPCMethod.BossHeadSpriteEffects);
                On_NPC.GetBossHeadSpriteEffects += BossHeadSpriteEffects;
            });
    }

    struct Drawing
    {
        public Color Light;
        public bool Own, Tinted, ModDrew;
        public int Queued;
        public (IntPtr Pointer, Color Light)? Outer;
    }

    static void DrawNPCDirect(On_Main.orig_DrawNPCDirect_SpriteBatch_NPC_bool_Vector2 orig, Main self, SpriteBatch mySpriteBatch, NPC rCurrentNPC,
        bool behindTiles, Vector2 screenPos)
    {
        if (!Resolve(rCurrentNPC, out ModNPC? own, out GlobalNPC?[] globals))
        {
            orig(self, mySpriteBatch, rCurrentNPC, behindTiles, screenPos);
            return;
        }
        bool drawn = Before(rCurrentNPC, own, globals, mySpriteBatch, screenPos, out Drawing drawing);
        if (drawn)
        {
            try { orig(self, mySpriteBatch, rCurrentNPC, behindTiles, screenPos); }
            finally { Restore(in drawing); }
        }
        After(rCurrentNPC, mySpriteBatch, screenPos, in drawing, drawn);
    }

    static void DrawNPCDirectLit(On_Main.orig_DrawNPCDirect_SpriteBatch_NPC_bool_Vector2_LightMap_refRectangle orig, Main self, SpriteBatch mySpriteBatch,
        NPC rCurrentNPC, bool behindTiles, Vector2 screenPos, LightMap lightMap, ref Rectangle lightRegion)
    {
        if (!Resolve(rCurrentNPC, out ModNPC? own, out GlobalNPC?[] globals))
        {
            orig(self, mySpriteBatch, rCurrentNPC, behindTiles, screenPos, lightMap, ref lightRegion);
            return;
        }
        bool drawn = Before(rCurrentNPC, own, globals, mySpriteBatch, screenPos, out Drawing drawing);
        if (drawn)
        {
            try { orig(self, mySpriteBatch, rCurrentNPC, behindTiles, screenPos, lightMap, ref lightRegion); }
            finally { Restore(in drawing); }
        }
        After(rCurrentNPC, mySpriteBatch, screenPos, in drawing, drawn);
    }

    static bool Resolve(NPC npc, out ModNPC? own, out GlobalNPC?[] globals)
    {
        globals = NPCLoader.Slots(npc);
        ModNPC? template = NPCEventHooks.Borrowed(npc) ?? NPCLoader.GetNPC(npc.type);
        bool ownDraws = template != null && (template.Hooks & NPCLoader.DrawMethods) != 0;
        own = ownDraws ? NPCLoader.InstanceOf(npc) : null;
        return ownDraws || NPCLoader.Any(globals, NPCLoader.DrawMethods);
    }

    static bool Before(NPC npc, ModNPC? own, GlobalNPC?[] globals, SpriteBatch batch, Vector2 screenPos, out Drawing drawing)
    {
        Vector2 center = npc.Center;
        Color light = float.IsFinite(center.X) && float.IsFinite(center.Y) ? Lighting.GetColor((int)(center.X / 16f), (int)(center.Y / 16f)) : Color.White;
        Color original = light;
        NPCLoader.DrawEffects(npc, own, globals, ref light);
        drawing = new Drawing
        {
            Light = light,
            Own = (own != null && (own.Hooks & NPCLoader.OwnDrawMethods) != 0) || NPCLoader.Any(globals, NPCLoader.OwnDrawMethods),
            Outer = drawLight,
        };
        if (drawing.Own)
        {
            if (!queueCounts || Queued(batch) != 0) DrawBatches.Restart(batch);
            drawing.Queued = Queued(batch);
        }
        bool draw = NPCLoader.PreDraw(npc, own, globals, batch, screenPos, light);
        if (drawing.Own)
        {
            int queued = Queued(batch);
            drawing.ModDrew = queued != drawing.Queued;
            drawing.Queued = queued;
        }
        if (!draw) return false;
        if (light.Equals(original)) drawLight = null;
        else
        {
            drawLight = (npc.Pointer, light);
            drawing.Tinted = tinting != null;
            if (drawing.Tinted) Interlocked.Increment(ref *tinting);
        }
        return true;
    }

    static void Restore(in Drawing drawing)
    {
        drawLight = drawing.Outer;
        if (drawing.Tinted) Interlocked.Decrement(ref *tinting);
    }

    static void After(NPC npc, SpriteBatch batch, Vector2 screenPos, in Drawing drawing, bool drawn)
    {
        int queued = drawing.Own ? Queued(batch) : -1;
        if (drawing.Own && drawn && drawing.Queued >= 0 && queued > drawing.Queued) queueCounts = true;
        NPCLoader.PostDraw(npc, batch, screenPos, drawing.Light);
        if (drawing.Own && (!queueCounts || drawing.ModDrew || queued < 0 || Queued(batch) != queued)) DrawBatches.Restart(batch);
    }

    static int Queued(SpriteBatch batch)
    {
        if (batch.IsNull) return -1;
        SpriteBatcher batcher = batch._batcher;
        return batcher.IsNull ? -1 : batcher._batchItemCount;
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
            if (npc.active && DrawBehindTypes.Contains(npc.type) && NPCLoader.HasHooks(npc, NPCMethod.DrawBehind)) NPCLoader.DrawBehind(npc, i);
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
