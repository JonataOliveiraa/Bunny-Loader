using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BunnyLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;

namespace Terraria.ModLoader.Core;

internal static unsafe class NPCUIHooks
{
    const NPCHook Hover = NPCHook.ModifyHoverBoundingBox | NPCHook.PreHoverInteract;
    const int PartyHat = 0, ChatBubble = 1;

    [ThreadStatic] static NPC adjusting;
    static readonly List<(NPC Npc, Vector2 Position, Rectangle Frame, bool Active)> hidden = new();

    internal static void Install(NPCHook flags)
    {
        if ((flags & NPCHook.PartyHatPosition) != 0) ItemHooks.Once("NPCPartyHatPosition", () => On_Main.DrawNPCExtras += DrawNPCExtras);
        if ((flags & NPCHook.ChatBubblePosition) != 0) ItemHooks.Once("NPCChatBubblePosition", () => On_Main.DrawNPCChatBubble += DrawNPCChatBubble);
        if ((flags & Hover) != 0) ItemHooks.Once("NPCHover", () => On_Main.HoverOverNPCs += HoverOverNPCs);
    }

    static IntPtr TextureOf(GameObjectRef asset) => asset.Pointer == IntPtr.Zero ? IntPtr.Zero : Host.Native()->TextureAssetValue(asset.Pointer);

    readonly struct GameObjectRef
    {
        public readonly IntPtr Pointer;
        public GameObjectRef(IntPtr pointer) => Pointer = pointer;
    }

    static bool Arm(IntPtr texture, int kind, NPC npc)
    {
        if (texture == IntPtr.Zero) return false;
        adjusting = npc;
        if (Host.Native()->BeginDrawAdjust(texture, kind, &Adjust) != 0) return true;
        adjusting = default;
        return false;
    }

    static void Disarm()
    {
        Host.Native()->EndDrawAdjust();
        adjusting = default;
    }

    [UnmanagedCallersOnly]
    static void Adjust(int kind, float* position, int* effects)
    {
        NPC npc = adjusting;
        if (npc.IsNull) return;
        try
        {
            var spriteEffects = (SpriteEffects)(*effects);
            if (kind == PartyHat)
            {
                var world = new Vector2(position[0] + Main.screenPosition.X, position[1] + Main.screenPosition.Y);
                NPCLoader.PartyHatPosition(npc, ref world, ref spriteEffects);
                position[0] = world.X - Main.screenPosition.X;
                position[1] = world.Y - Main.screenPosition.Y;
            }
            else
            {
                var screen = new Vector2(position[0], position[1]);
                NPCLoader.ChatBubblePosition(npc, ref screen, ref spriteEffects);
                position[0] = screen.X;
                position[1] = screen.Y;
            }
            *effects = (int)spriteEffects;
        }
        catch (Exception e) { ItemHooks.Report("NPC draw position", e); }
    }

    static void DrawNPCExtras(On_Main.orig_DrawNPCExtras orig, Main self, NPC n, bool beforeDraw, float addHeight, float addY, Color npcColor, Vector2 halfSize,
        SpriteEffects npcSpriteEffect, Vector2 screenPosition)
    {
        bool armed = !beforeDraw && NPCLoader.HasHooks(n, NPCHook.PartyHatPosition) && Arm(TextureOf(new GameObjectRef(TextureAssets.Extra[72].Pointer)), PartyHat, n);
        try { orig(self, n, beforeDraw, addHeight, addY, npcColor, halfSize, npcSpriteEffect, screenPosition); }
        finally
        {
            if (armed) Disarm();
        }
    }

    static void DrawNPCChatBubble(On_Main.orig_DrawNPCChatBubble orig, int i)
    {
        bool armed = (uint)i < 200u && NPCLoader.HasHooks(Main.npc[i], NPCHook.ChatBubblePosition) &&
            Arm(TextureOf(new GameObjectRef(TextureAssets.Chat.Pointer)), ChatBubble, Main.npc[i]);
        try { orig(i); }
        finally
        {
            if (armed) Disarm();
        }
    }

    static bool SpecialBox(int type) => type >= 87 && type <= 92 || type == 677 || type == 695 || type == 696;

    static void HoverOverNPCs(On_Main.orig_HoverOverNPCs orig, Main self, Rectangle mouseRectangle)
    {
        hidden.Clear();
        try
        {
            for (int n = 0; n < 200; n++)
            {
                NPC npc = Main.npc[n];
                if (!npc.active || !NPCLoader.HasHooks(npc, Hover)) continue;
                Vector2 bottom = npc.position + npc.netOffset + new Vector2(npc.width / 2f, npc.height);
                Rectangle frame = npc.frame;
                var vanilla = new Rectangle((int)bottom.X - frame.Width / 2, (int)bottom.Y - frame.Height, frame.Width, frame.Height);
                Rectangle box = vanilla;
                if (!SpecialBox(npc.type)) NPCLoader.ModifyHoverBoundingBox(npc, ref box);
                bool intersects = mouseRectangle.Intersects(box);
                bool considered = intersects || Main.SmartInteractShowingGenuine && Main.SmartInteractNPC == n;
                if (considered && !NPCLoader.PreHoverInteract(npc, intersects))
                {
                    hidden.Add((npc, npc.position, frame, true));
                    npc.active = false;
                    continue;
                }
                if (box.X == vanilla.X && box.Y == vanilla.Y && box.Width == vanilla.Width && box.Height == vanilla.Height) continue;
                hidden.Add((npc, npc.position, frame, true));
                npc.frame = new Rectangle(frame.X, frame.Y, box.Width, box.Height);
                npc.position = new Vector2(box.X + box.Width / 2 - npc.width / 2f + 0.25f, box.Y + box.Height - npc.height + 0.25f) - npc.netOffset;
            }
        }
        catch (Exception e) { ItemHooks.Report("NPC hover", e); }
        try { orig(self, mouseRectangle); }
        finally
        {
            for (int k = hidden.Count - 1; k >= 0; k--)
            {
                var (npc, position, frame, active) = hidden[k];
                npc.position = position;
                npc.frame = frame;
                npc.active = active;
            }
            hidden.Clear();
        }
    }
}
