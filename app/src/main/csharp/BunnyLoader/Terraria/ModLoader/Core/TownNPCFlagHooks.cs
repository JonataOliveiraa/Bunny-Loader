using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BunnyLoader;
using BunnyLoader.Interop;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

internal static unsafe class TownNPCFlagHooks
{
    static readonly List<int> hidden = new();

    static Hook? trySyncing;

    static int needsUpdateOffset = -1;

    internal static void WantActsLike()
    {
        for (int type = NPCID.Count; type < NPCID.Sets.ActsLikeTownNPC.Length; type++)
        {
            if (!NPCID.Sets.ActsLikeTownNPC[type])
                continue;
            ItemHooks.Once("TownActsLike", () =>
            {
                NPCGates.Type<On_NPC.hook_get_isLikeATownNPC>(0, Modded);
                On_NPC.get_isLikeATownNPC += IsLikeATownNPC;
                NPCGates.Type<On_NPC.hook_get_CanBeTalkedTo>(0, Modded);
                On_NPC.get_CanBeTalkedTo += CanBeTalkedTo;
                NPCGates.Type<On_NPC.hook_get_CanTalk>(0, Modded);
                On_NPC.get_CanTalk += CanTalk;
                needsUpdateOffset = ItemHooks.FieldOffset(NPC.Class, "needsUniqueInfoUpdate");
                trySyncing = ItemHooks.Gated(NPC.Class, "void TrySyncingUniqueTownNPCData(int npcIndex)",
                    (IntPtr)(delegate* unmanaged<IntPtr, int, IntPtr, void>)&TrySyncing, 0, -1, needsUpdateOffset, 1);
            });
            return;
        }
    }

    internal static void WantNoHappiness()
    {
        bool[] set = NPCID.Sets.NoTownNPCHappiness;
        for (int type = 0; type < set.Length; type++)
        {
            if (!set[type])
                continue;
            ItemHooks.Once("TownNoHappiness", () =>
            {
                NPCGates.Type<On_ShopHelper.hook_ProcessMood>(2, NoHappiness);
                On_ShopHelper.ProcessMood += ProcessMood;
                On_GUINPCDialogue.SetupButtonText += SetupButtonText;
            });
            return;
        }
    }

    internal static void WantStayingHomeless() => ItemHooks.Once("TownStayingHomeless", () =>
    {
        On_NPC.CanShowHomelessText += CanShowHomelessText;
        On_GUINPCDialogue.get_TalkingToHomeless += TalkingToHomeless;
        On_WorldGen.UpdatePrioritizedTownNPC += UpdatePrioritized;
        On_GUIHousingMenu.RefreshNPCEntries += RefreshNPCEntries;
    });

    static bool Acts(NPC npc)
    {
        int type = npc.type;
        return type >= NPCID.Count && type < NPCID.Sets.ActsLikeTownNPC.Length && NPCID.Sets.ActsLikeTownNPC[type];
    }

    static bool Modded(int type) => type >= NPCID.Count;

    static bool NoHappiness(int type) => (uint)type < (uint)NPCID.Sets.NoTownNPCHappiness.Length && NPCID.Sets.NoTownNPCHappiness[type];

    static bool Staying(NPC npc) => npc.type >= NPCID.Count && npc.ModNPC?.TownNPCStayingHomeless == true;

    static bool IsLikeATownNPC(On_NPC.orig_get_isLikeATownNPC orig, NPC self) => orig(self) || Acts(self);

    static bool CanBeTalkedTo(On_NPC.orig_get_CanBeTalkedTo orig, NPC self) => orig(self) || (Acts(self) && self.aiStyle == 7 && self.velocity.Y == 0f);

    static void SetupButtonText(On_GUINPCDialogue.orig_SetupButtonText orig, GUINPCDialogue self, ref GameString text, ref Texture2D texture,
        ref GameString text2, ref Texture2D texture2, ref int cost, ref bool happiness)
    {
        orig(self, ref text, ref texture, ref text2, ref texture2, ref cost, ref happiness);
        NPC npc = TownNPCHooks.TalkingTo();
        if (!npc.IsNull && NoHappiness(npc.type)) happiness = false;
    }

    static void ProcessMood(On_ShopHelper.orig_ProcessMood orig, ShopHelper self, Player player, NPC npc)
    {
        if (!NoHappiness(npc.type))
        {
            orig(self, player, npc);
            return;
        }
        self._currentHappiness = "";
        self._currentPriceAdjustment = npc.loveStruck ? 0.9f : 1f;
    }

    static bool CanTalk(On_NPC.orig_get_CanTalk orig, NPC self) => orig(self) || (Acts(self) && self.aiStyle == 7 && self.velocity.Y == 0f && !NPCID.Sets.IsTownPet[self.type]);

    [UnmanagedCallersOnly]
    static void TrySyncing(IntPtr pointer, int npcIndex, IntPtr method)
    {
        Hook? hook = trySyncing;
        if (hook == null) return;
        var self = new NPC(pointer);
        bool lend = false;
        if (Main.netMode != 1 && needsUpdateOffset >= 0) *(byte*)(pointer + needsUpdateOffset) = 0;
        else
        {
            try { lend = !self.townNPC && Acts(self); }
            catch (Exception e) { ItemHooks.Report("ActsLikeTownNPC sync", e); }
        }
        if (lend) self.townNPC = true;
        try
        {
            var call = GameCall.Returning(RetKind.Int);
            call.Arg(pointer);
            call.Arg(npcIndex);
            call.Arg(method);
            ItemHooks.Original(ref call, hook, "NPC.TrySyncingUniqueTownNPCData");
        }
        finally
        {
            if (lend) self.townNPC = false;
        }
    }

    static bool CanShowHomelessText(On_NPC.orig_CanShowHomelessText orig, int npcIndex) => orig(npcIndex) && !Staying(Main.npc[npcIndex]);

    static bool TalkingToHomeless(On_GUINPCDialogue.orig_get_TalkingToHomeless orig, GUINPCDialogue self)
    {
        int talk = Main.LocalPlayer.talkNPC;
        return orig(self) && (talk < 0 || !Staying(Main.npc[talk]));
    }

    static void UpdatePrioritized(On_WorldGen.orig_UpdatePrioritizedTownNPC orig)
    {
        Hide();
        orig();
        Show();
    }

    static void RefreshNPCEntries(On_GUIHousingMenu.orig_RefreshNPCEntries orig, GUIHousingMenu self)
    {
        Hide();
        orig(self);
        Show();
    }

    static void Hide()
    {
        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC npc = Main.npc[i];
            if (npc.active && npc.townNPC && Staying(npc))
            {
                npc.townNPC = false;
                npc.active = false;
                hidden.Add(i);
            }
        }
    }

    static void Show()
    {
        foreach (int i in hidden)
        {
            NPC npc = Main.npc[i];
            npc.active = true;
            npc.townNPC = true;
        }
        hidden.Clear();
    }
}
