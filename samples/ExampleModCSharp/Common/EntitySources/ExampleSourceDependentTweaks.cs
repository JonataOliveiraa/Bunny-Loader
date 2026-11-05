using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Common.EntitySources
{
    
    public sealed class ExampleSourceDependentProjectileTweaks : GlobalProjectile
    {
        
        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) {
            return entity.type is ProjectileID.BulletDeadeye;
        }

        public override void OnSpawn(Projectile projectile, IEntitySource source) {
            
            if (EntitySource_Parent.Is(source) && NPC.Is(new EntitySource_Parent(source.Pointer).Entity) && new NPC(new EntitySource_Parent(source.Pointer).Entity.Pointer).type == NPCID.TacticalSkeleton) {
                projectile.damage /= 2;
            }
        }
    }

    public sealed class ExampleSourceDependentItemTweaks : GlobalItem
    {
        public override void OnSpawn(WorldItem item, IEntitySource source) {
            
            if (EntitySource_ShakeTree.Is(source)) {
                NPC.NewNPC(source, (int)item.position.X, (int)item.position.Y, NPCID.BlueSlime);
            }
        }
    }

}
