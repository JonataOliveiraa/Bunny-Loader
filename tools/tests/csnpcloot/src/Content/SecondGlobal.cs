using Terraria;
using Terraria.ModLoader;

namespace CsNpcLoot.Content;

public sealed class SecondGlobal : GlobalNPC
{
    public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == ModContent.NPCType<LootNpc>();
    public override bool PreKill(NPC npc) { Checks.Order.Add("PreG2"); return true; }
}
