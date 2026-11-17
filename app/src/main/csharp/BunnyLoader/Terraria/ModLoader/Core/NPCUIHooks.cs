using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;

namespace Terraria.ModLoader.Core;

internal static unsafe class NPCUIHooks
{
    const NPCHook Hover = NPCHook.ModifyHoverBoundingBox | NPCHook.PreHoverInteract;
    const int PartyHat = 0, ChatBubble = 1;

    [ThreadStatic] static NPC adjusting;
    static readonly List<(NPC Npc, Vector2 Position, Rectangle Frame, bool Active, bool Town)> hidden = new();
    static readonly NPCGates.TypeSet HoverTypes = new(type => NPCGates.Wants(type, Hover));
    static readonly NPCGates.TypeSet ChatTypes = new(type => NPCGates.Wants(type, NPCMethod.TownChat));
    static readonly NPCGates.TypeSet BubbleTypes = new(type => NPCGates.Wants(type, NPCHook.ChatBubblePosition));
    static (IntPtr Raw, IntPtr Asset) partyHat;
    static readonly List<NPC> talkers = new(), lent = new();
    static uint talkersAt = uint.MaxValue;
    static int* talkGate;

    internal static void Install(NPCHook flags)
    {
        if ((flags & NPCHook.PartyHatPosition) != 0)
            ItemHooks.Once("NPCPartyHatPosition", () =>
            {
                NPCGates.Type<On_Main.hook_DrawNPCExtras>(1, NPCHook.PartyHatPosition);
                On_Main.DrawNPCExtras += DrawNPCExtras;
            });
        if ((flags & NPCHook.ChatBubblePosition) != 0) ItemHooks.Once("NPCChatBubblePosition", () => On_Main.DrawNPCChatBubble += DrawNPCChatBubble);
        if ((flags & Hover) != 0) WantHover();
    }

    internal static void WantHover() => ItemHooks.Once("NPCHover", () => On_Main.HoverOverNPCs += HoverOverNPCs);

    internal static void WantChat()
    {
        WantHover();
        ItemHooks.Once("NPCTalkCursor", () =>
        {
            talkGate = NPCGates.Flag<On_GUIQuickActionButton.hook_GetMouseNPC>();
            NPCGates.Flag<On_GUIQuickActionButton.hook_GetMouseNPCAtPosition>(talkGate);
            On_GUIQuickActionButton.GetMouseNPC += GetMouseNPC;
            On_GUIQuickActionButton.GetMouseNPCAtPosition += GetMouseNPCAtPosition;
        });
    }

    internal static void Spawned(NPC npc)
    {
        if (talkGate != null && ChatTypes.Contains(npc.type)) *talkGate = 1;
    }

    static bool TalksAsTown(NPC npc) =>
        npc.active && !npc.townNPC && !ChatListed(npc.type) && ChatTypes.Contains(npc.type) && !SpecialBox(npc.type) &&
        NPCLoader.HasHooks(npc, NPCMethod.TownChat) && NPCLoader.CanChat(npc) == true;

    static int LendTalkers()
    {
        int mark = lent.Count;
        try
        {
            uint now = Main.GameUpdateCount;
            if (now != talkersAt && mark == 0 && hidden.Count == 0)
            {
                talkersAt = now;
                talkers.Clear();
                for (int n = 0; n < 200; n++)
                {
                    NPC npc = Main.npc[n];
                    if (TalksAsTown(npc)) talkers.Add(npc);
                }
                if (talkers.Count == 0 && talkGate != null) *talkGate = 0;
            }
            foreach (NPC npc in talkers)
            {
                if (!npc.active || npc.townNPC) continue;
                npc.townNPC = true;
                lent.Add(npc);
            }
        }
        catch (Exception e) { ItemHooks.Report("NPC talk cursor", e); }
        return mark;
    }

    static void ReturnTalkers(int mark)
    {
        for (int k = lent.Count - 1; k >= mark; k--)
        {
            lent[k].townNPC = false;
            lent.RemoveAt(k);
        }
    }

    static NPC GetMouseNPC(On_GUIQuickActionButton.orig_GetMouseNPC orig, out bool allowTileInteraction)
    {
        int mark = LendTalkers();
        try { return orig(out allowTileInteraction); }
        finally { ReturnTalkers(mark); }
    }

    static NPC GetMouseNPCAtPosition(On_GUIQuickActionButton.orig_GetMouseNPCAtPosition orig, Vector2 mouseWorldPosition, out bool allowTileInteraction)
    {
        int mark = LendTalkers();
        try { return orig(mouseWorldPosition, out allowTileInteraction); }
        finally { ReturnTalkers(mark); }
    }

    static IntPtr TextureOf(GameObjectRef asset) => asset.Pointer == IntPtr.Zero ? IntPtr.Zero : Host.Native()->TextureAssetValue(asset.Pointer);

    static IntPtr PartyHatAsset()
    {
        GameArray<GameObject> extra = TextureAssets.Extra.Raw;
        IntPtr raw = extra.Length > 72 ? extra[72].Pointer : IntPtr.Zero;
        var cached = partyHat;
        if (raw != IntPtr.Zero && cached.Raw == raw) return cached.Asset;
        IntPtr asset = TextureAssets.Extra[72]?.Pointer ?? IntPtr.Zero;
        if (raw != IntPtr.Zero) partyHat = (raw, asset);
        return asset;
    }

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
        if (beforeDraw || !NPCLoader.HasHooks(n, NPCHook.PartyHatPosition))
        {
            orig(self, n, beforeDraw, addHeight, addY, npcColor, halfSize, npcSpriteEffect, screenPosition);
            return;
        }
        bool armed = Arm(TextureOf(new GameObjectRef(PartyHatAsset())), PartyHat, n);
        try { orig(self, n, beforeDraw, addHeight, addY, npcColor, halfSize, npcSpriteEffect, screenPosition); }
        finally
        {
            if (armed) Disarm();
        }
    }

    static void DrawNPCChatBubble(On_Main.orig_DrawNPCChatBubble orig, int i)
    {
        bool armed = (uint)i < 200u && BubbleTypes.Contains(Main.npc[i].type) && NPCLoader.HasHooks(Main.npc[i], NPCHook.ChatBubblePosition) &&
            Arm(TextureOf(new GameObjectRef(TextureAssets.Chat?.Pointer ?? IntPtr.Zero)), ChatBubble, Main.npc[i]);
        try { orig(i); }
        finally
        {
            if (armed) Disarm();
        }
    }

    static bool SpecialBox(int type) => type >= 87 && type <= 92 || type == 677 || type == 695 || type == 696;

    static bool ChatListed(int type) => type is 105 or 106 or 123 or 354 or 376 or 453 or 579 or 589;

    static void HoverOverNPCs(On_Main.orig_HoverOverNPCs orig, Main self, Rectangle mouseRectangle)
    {
        hidden.Clear();
        try
        {
            for (int n = 0; n < 200; n++)
            {
                NPC npc = Main.npc[n];
                if (!npc.active) continue;
                int type = npc.type;
                bool hover = HoverTypes.Contains(type) && NPCLoader.HasHooks(npc, Hover);
                bool chat = ChatTypes.Contains(type) && !SpecialBox(type) && NPCLoader.HasHooks(npc, NPCMethod.TownChat);
                if (!hover && !chat) continue;
                Vector2 bottom = npc.position + npc.netOffset + new Vector2(npc.width / 2f, npc.height);
                Rectangle frame = npc.frame;
                var vanilla = new Rectangle((int)bottom.X - frame.Width / 2, (int)bottom.Y - frame.Height, frame.Width, frame.Height);
                Rectangle box = vanilla;
                if (hover && !SpecialBox(type)) NPCLoader.ModifyHoverBoundingBox(npc, ref box);
                bool intersects = mouseRectangle.Intersects(box);
                bool considered = intersects || Main.SmartInteractShowingGenuine && Main.SmartInteractNPC == n;
                bool town = npc.townNPC;
                if (hover && considered && !NPCLoader.PreHoverInteract(npc, intersects))
                {
                    hidden.Add((npc, npc.position, frame, true, town));
                    npc.active = false;
                    continue;
                }
                bool moved = box.X != vanilla.X || box.Y != vanilla.Y || box.Width != vanilla.Width || box.Height != vanilla.Height;
                bool listed = ChatListed(type);
                bool vanillaChat = town || listed;
                bool canChat = chat && considered ? NPCLoader.CanChat(npc) ?? vanillaChat : vanillaChat;
                bool lend = canChat != vanillaChat && (canChat || !listed);
                if (!moved && !lend) continue;
                hidden.Add((npc, npc.position, frame, true, town));
                if (lend) npc.townNPC = canChat;
                if (!moved) continue;
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
                var (npc, position, frame, active, town) = hidden[k];
                npc.position = position;
                npc.frame = frame;
                npc.active = active;
                npc.townNPC = town;
            }
            hidden.Clear();
        }
    }
}
