using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsNpcLoot.Content;

public sealed class LootGlobal : GlobalNPC
{
    public static int SetupCalls, Removed;
    public static bool Allow = true;
    public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.BlueSlime || entity.type == ModContent.NPCType<LootNpc>();
    public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
    {
        SetupCalls++;
        if (npc.netID != NPCID.BlueSlime) return;
        int before = npcLoot.Get(false).Count;
        npcLoot.RemoveWhere(rule => rule is DropBasedOnExtraGel gel && gel.ruleForNormal is CommonDrop drop && drop.itemId == ItemID.Gel, false);
        Removed = before - npcLoot.Get(false).Count;
        npcLoot.Add(ItemDropRule.Common(ItemID.GoldBar));
    }
    public override void ModifyGlobalLoot(GlobalLoot globalLoot)
    {
        globalLoot.Add(ItemDropRule.ByCondition(new GlobalCondition(), ItemID.CopperBar));
    }
    public override bool PreKill(NPC npc) { Checks.Order.Add("PreG"); return Allow; }
    public override void OnKill(NPC npc) => Checks.Order.Add("KillG");
}
