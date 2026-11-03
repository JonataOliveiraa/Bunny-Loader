using ExampleMod.Common.Players;
using ExampleMod.Content.Items;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Common.GlobalNPCs;

public class ExampleResourcePickupGlobalNPC : GlobalNPC
{
    public override void OnKill(NPC npc)
    {
        Player closestPlayer = Main.player[Player.FindClosest(npc.position, npc.width, npc.height)];
        ExampleResourcePlayer exampleResourcePlayer = closestPlayer.GetModPlayer<ExampleResourcePlayer>();

        if (!NPCID.Sets.NeverDropsResourcePickups[npc.type] && closestPlayer.RollLuck(6) == 0 && npc.lifeMax > 1 && npc.damage > 0 && Main.rand.NextBool(2) && exampleResourcePlayer.exampleResourceCurrent < exampleResourcePlayer.exampleResourceMax2)
        {
            Item.NewItem(npc.GetSource_Loot(), npc.getRect(), ModContent.ItemType<ExampleResourcePickup>());
        }
    }
}
