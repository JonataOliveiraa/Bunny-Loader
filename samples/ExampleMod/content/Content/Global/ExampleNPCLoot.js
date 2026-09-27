const { ItemID, NPCID } = Terraria.ID;
const { ItemDropRule } = Terraria.GameContent.ItemDropRules;

export class ExampleNPCLoot extends GlobalNPC {
    ModifyNPCLoot(npc, npcLoot) {
        if (npc.type === NPCID.Demolitionist) {
            npcLoot.Add(ItemDropRule.Common(ItemID.Dynamite, 10, 1, 3));
        }

        if (npc.type === NPCID.FireImp) {
            npcLoot.RemoveWhere((rule) => 'itemId' in rule && rule.itemId === ItemID.ObsidianRose);
        }
    }
}
