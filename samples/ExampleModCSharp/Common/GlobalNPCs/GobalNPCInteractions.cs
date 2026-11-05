using System.Linq;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Common.GlobalNPCs;

public class GlobalNPCInteractions : GlobalNPC
{

    public override bool AppliesToEntity(NPC npc, bool lateInstantiation) {
        return npc.type == NPCID.Guide;
    }

    public override void RegisterChatButtons(NPC npc, NPCInteractionList interactions) {

        interactions.InsertBefore(NPCInteractions.Shop("Terraria/BestiaryGirl/Shop", "Shop"), NPCInteractionDatabase.CloseButton);

        NPCInteraction guideTipNPCInteraction = interactions.Interactions.OfType<NPCInteractions.Actions.GuideTip>().FirstOrDefault();
        interactions.Disable(guideTipNPCInteraction); 
    }

    public override bool PreChatButtonClicked(NPC npc, NPCInteraction interaction) {
        
        if (interaction is NPCInteractions.Actions.CloseChat) {
            Main.npcChatText = "You can't close my chat window!";
            return false;
        }

        return base.PreChatButtonClicked(npc, interaction);
    }

    public override void OnChatButtonClicked(NPC npc, NPCInteraction interaction) {
        
        if (interaction is NPCInteractions.Actions.GuideReverseCrafting) {
            
            Main.NewText($"<{npc.FullName}> Simply place a material in the slot and I'll tell you everything you can craft with it!");
        }
    }
}
