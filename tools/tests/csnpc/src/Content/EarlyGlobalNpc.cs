using Terraria;
using Terraria.ModLoader;

namespace CsNpc.Content;

public sealed class EarlyGlobalNpc : GlobalNPC
{
    public int Value = 9;
    public int DefaultsCalls;
    public bool SawModDefaults;

    public override bool InstancePerEntity => true;

    public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => !lateInstantiation && entity.type == ModContent.NPCType<TestNpc>();

    public override void SetDefaults(NPC entity)
    {
        DefaultsCalls++;
        SawModDefaults = entity.ModNPC is TestNpc && entity.damage == 40;
        entity.damage += 3;
    }
}
