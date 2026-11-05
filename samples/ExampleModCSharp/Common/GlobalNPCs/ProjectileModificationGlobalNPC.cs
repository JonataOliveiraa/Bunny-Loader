using Terraria.ModLoader;

namespace ExampleMod.Common.GlobalNPCs
{
    
    public class ProjectileModificationGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;
        public int timesHitByModifiedProjectiles;
    }
}
