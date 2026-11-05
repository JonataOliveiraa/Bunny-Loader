using ExampleMod.Common.ItemDropRules.DropConditions;
using ExampleMod.Content.Items;
using ExampleMod.Content.Items.Weapons;
using System.Linq;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Common.GlobalNPCs
{
    public class ExampleNPCLoot : GlobalNPC
    {
        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot) {
            if (!NPCID.Sets.CountsAsCritter[npc.type]) {
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<ExampleItem>(), 1));

            }

            if (npc.type == NPCID.Guide) {
                npcLoot.RemoveWhere(

                    rule => rule is ItemDropWithConditionRule drop
                        && drop.itemId == ItemID.GreenCap
                        && drop.condition is Conditions.NamedNPC npcNameCondition
                        && npcNameCondition.neededName == "Andrew"
                );

                npcLoot.Add(ItemDropRule.Common(ItemID.GreenCap, 1));
            }

            if (npc.type == NPCID.BloodNautilus) {
                foreach (var rule in npcLoot.Get()) {
                    if (rule is DropBasedOnExpertMode drop && drop.ruleForNormalMode is CommonDrop normalDropRule && normalDropRule.itemId == ItemID.SanguineStaff)
                        normalDropRule.chanceDenominator = 3;
                }
            }

            if (npc.type == NPCID.QueenBee) {
                foreach (var rule in npcLoot.Get()) {
                    if (rule is DropBasedOnExpertMode dropBasedOnExpertMode && dropBasedOnExpertMode.ruleForNormalMode is OneFromOptionsNotScaledWithLuckDropRule oneFromOptionsDrop && oneFromOptionsDrop.dropIds.Contains(ItemID.BeeGun)) {
                        var original = oneFromOptionsDrop.dropIds.ToList();
                        original.Add(ModContent.ItemType<Content.Items.Accessories.WaspNest>());
                        oneFromOptionsDrop.dropIds = original.ToArray();
                    }
                }
            }

            if (npc.type == NPCID.Crimera || npc.type == NPCID.Corruptor) {
                int itemType = npc.type == NPCID.Crimera ? ItemID.RottenChunk : ItemID.Vertebrae;
                npcLoot.Add(ItemDropRule.ByCondition(new ExampleDropCondition(), itemType, chanceDenominator: 3));
            }

            if (npc.aiStyle == NPCAIStyleID.Slime) {
                npcLoot.Add(ItemDropRule.ByCondition(Condition.TimeDay.ToDropCondition(ShowItemDropInUI.Always), ModContent.ItemType<ExampleSword>(), 250));
            }
        }

        public override void ModifyGlobalLoot(GlobalLoot globalLoot) {
            globalLoot.Add(ItemDropRule.ByCondition(new ExampleSoulCondition(), ModContent.ItemType<ExampleSoul>(), 5, 1, 1));
        }
    }
}
