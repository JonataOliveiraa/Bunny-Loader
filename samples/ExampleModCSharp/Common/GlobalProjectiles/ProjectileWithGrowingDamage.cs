using ExampleMod.Common.Configs;
using ExampleMod.Common.GlobalItems;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace ExampleMod.Common.GlobalProjectiles
{
    public class ProjectileWithGrowingDamage : GlobalProjectile
    {
        private WeaponWithGrowingDamage sourceGlobalItem;

        public override bool InstancePerEntity => true;

        public override bool IsLoadingEnabled(Mod mod) {
            
            return ModContent.GetInstance<ExampleModConfig>().WeaponWithGrowingDamageToggle;
        }

        public override void OnSpawn(Projectile projectile, IEntitySource source) {

            if (EntitySource_ItemUse.Is(source)) {
                var itemSource = new EntitySource_ItemUse(source.Pointer);
                itemSource.Item.TryGetGlobalItem(out sourceGlobalItem);
            }
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone) {
            if (sourceGlobalItem == null) {
                return;
            }

            int owner = projectile.owner;
            if (owner < 0 || owner >= Main.player.Length) {
                return;
            }

            Player player = Main.player[owner];
            sourceGlobalItem.OnHitNPCGeneral(player, target, hit, projectile: projectile);
        }
    }
}
