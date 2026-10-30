using Terraria;
using Terraria.ModLoader;

namespace CsNpc.Content;

public sealed class LateGlobalNpc : GlobalNPC
{
    public int SeenDamage;

    public override bool InstancePerEntity => true;

    public override bool AppliesToEntity(NPC entity, bool lateInstantiation) =>
        lateInstantiation && entity.type == ModContent.NPCType<TestNpc>() && entity.damage == 43;

    public override void SetDefaults(NPC entity)
    {
        SeenDamage = entity.damage;
        entity.defense += 2;
    }
}
