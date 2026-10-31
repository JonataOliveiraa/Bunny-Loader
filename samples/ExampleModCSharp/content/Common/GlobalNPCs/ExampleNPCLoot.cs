using ExampleMod.Common.ItemDropRules.DropConditions;
using ExampleMod.Content.Items;
using ExampleMod.Content.Items.Weapons;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Common.GlobalNPCs;

public sealed class ExampleNPCLoot : GlobalNPC
{
    public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
    {
        if (!NPCID.Sets.CountsAsCritter[npc.type]) npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<ExampleItem>()));
        if (npc.type == NPCID.Guide)
        {
            npcLoot.RemoveWhere(rule => rule is ItemDropWithConditionRule drop && drop.itemId == ItemID.GreenCap
                && drop.condition is Conditions.NamedNPC named && named.neededName == "Andrew");
            npcLoot.Add(ItemDropRule.Common(ItemID.GreenCap));
        }
        if (npc.type == NPCID.BloodNautilus)
        {
            foreach (IItemDropRule rule in npcLoot.Get())
                if (rule is DropBasedOnExpertMode drop && drop.ruleForNormalMode is CommonDrop normal && normal.itemId == ItemID.SanguineStaff)
                    normal.chanceDenominator = 3;
        }
        if (npc.type == NPCID.Crimera || npc.type == NPCID.Corruptor)
            npcLoot.Add(ItemDropRule.ByCondition(new ExampleDropCondition(), npc.type == NPCID.Crimera ? ItemID.RottenChunk : ItemID.Vertebrae, 3));
        if (npc.aiStyle == NPCAIStyleID.Slime)
            npcLoot.Add(ItemDropRule.ByCondition(new ExampleDropCondition(), ModContent.ItemType<ExampleSword>(), 250));
    }
}
