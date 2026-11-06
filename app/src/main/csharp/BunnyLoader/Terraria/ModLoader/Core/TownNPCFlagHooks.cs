using System.Collections.Generic;
using Terraria.ID;

namespace Terraria.ModLoader.Core;

internal static class TownNPCFlagHooks
{
    static readonly List<int> hidden = new();

    internal static void WantActsLike()
    {
        for (int type = NPCID.Count; type < NPCID.Sets.ActsLikeTownNPC.Length; type++)
        {
            if (!NPCID.Sets.ActsLikeTownNPC[type])
                continue;
            ItemHooks.Once("TownActsLike", () =>
            {
                On_NPC.get_isLikeATownNPC += IsLikeATownNPC;
                On_NPC.get_CanBeTalkedTo += CanBeTalkedTo;
                On_NPC.get_CanTalk += CanTalk;
                On_NPC.TrySyncingUniqueTownNPCData += TrySyncing;
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

    static bool Staying(NPC npc) => npc.type >= NPCID.Count && npc.ModNPC?.TownNPCStayingHomeless == true;

    static bool IsLikeATownNPC(On_NPC.orig_get_isLikeATownNPC orig, NPC self) => orig(self) || Acts(self);

    static bool CanBeTalkedTo(On_NPC.orig_get_CanBeTalkedTo orig, NPC self) => orig(self) || (Acts(self) && self.aiStyle == 7 && self.velocity.Y == 0f);

    static bool CanTalk(On_NPC.orig_get_CanTalk orig, NPC self) => orig(self) || (Acts(self) && self.aiStyle == 7 && self.velocity.Y == 0f && !NPCID.Sets.IsTownPet[self.type]);

    static void TrySyncing(On_NPC.orig_TrySyncingUniqueTownNPCData orig, NPC self, int npcIndex)
    {
        if (self.townNPC || !Acts(self))
        {
            orig(self, npcIndex);
            return;
        }
        self.townNPC = true;
        orig(self, npcIndex);
        self.townNPC = false;
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
