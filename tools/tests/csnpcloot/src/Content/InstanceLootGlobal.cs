using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsNpcLoot.Content;

public sealed class InstanceLootGlobal : GlobalNPC
{
    int marker;
    public static bool Valid = true;
    public static int Calls;
    public override bool InstancePerEntity => true;
    public override bool AppliesToEntity(NPC npc, bool lateInstantiation) => npc.type == NPCID.BlueSlime || npc.type == ModContent.NPCType<LootNpc>();
    public override void SetDefaults(NPC npc) => marker = 42;
    public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
    {
        Calls++;
        Valid &= marker == 42 && ReferenceEquals(this, npc.GetGlobalNPC<InstanceLootGlobal>());
    }
}
