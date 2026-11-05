using ExampleMod.Content.Projectiles;
using Terraria;
using Terraria.ModLoader;

namespace ExampleMod.Common.GlobalNPCs
{
    internal class DamageOverTimeGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;
        public bool exampleJavelinDebuff;

        public override void ResetEffects(NPC npc) {
            exampleJavelinDebuff = false;
        }

        public override void UpdateLifeRegen(NPC npc, ref int damage) {
            if (exampleJavelinDebuff) {
                if (npc.lifeRegen > 0) {
                    npc.lifeRegen = 0;
                }
                
                int exampleJavelinCount = 0;
                foreach (var p in Main.ActiveProjectiles) {
                    if (p.type == ModContent.ProjectileType<ExampleJavelinProjectile>() && p.ai[0] == 1f && p.ai[1] == npc.whoAmI) {
                        exampleJavelinCount++;
                    }
                }

                npc.lifeRegen -= exampleJavelinCount * 2 * 3;
                if (damage < exampleJavelinCount * 3) {
                    damage = exampleJavelinCount * 3;
                }
            }
        }

    }
}
