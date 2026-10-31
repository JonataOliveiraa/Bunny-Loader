using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsNpcLoot.Content;

public class LootNpc : ModNPC
{
    public static bool Allow = true;
    public static int SetupCalls, Kills;
    public override string Texture => "Content/LootNpc";
    public override void SetDefaults()
    {
        NPC.width = NPC.height = 24;
        NPC.lifeMax = 100;
        NPC.damage = NPC.defense = NPC.aiStyle = 0;
        NPC.value = 0;
        NPC.townNPC = true;
        NPC.noGravity = NPC.noTileCollide = true;
    }

    public override void ModifyNPCLoot(NPCLoot npcLoot)
    {
        SetupCalls++;
        npcLoot.Add(ItemDropRule.Common(ItemID.Wood, 1, 2, 2));
        var condition = npcLoot.Add(ItemDropRule.ByCondition(new ToggleCondition(), ItemID.StoneBlock, 1, 3, 3));
        condition.OnSuccess(ItemDropRule.Common(ItemID.Acorn));
        condition.OnFailedConditions(ItemDropRule.Common(ItemID.DirtBlock, 1, 4, 4));
        npcLoot.Add(new CommonDrop(ItemID.Torch, 2, 1, 1, 0)).OnFailedRoll(ItemDropRule.Common(ItemID.IronBar));
        var lead = new LeadingConditionRule(new ToggleCondition());
        lead.OnSuccess(ItemDropRule.Common(ItemID.SilverBar));
        npcLoot.Add(lead);
        npcLoot.Add(new CustomRule());
    }

    public override bool PreKill() { Checks.Order.Add("PreN"); return Allow; }
    public override void OnKill() { Kills++; Checks.Order.Add("KillN"); Checks.DropsAtKill = Checks.Count(ItemID.Wood); }
}
