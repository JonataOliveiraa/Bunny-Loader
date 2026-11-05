using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ExampleMod.Common.GlobalProjectiles
{
    
    public class ExampleProjectileNetSync : GlobalProjectile
    {
        public override bool InstancePerEntity => true;
        private bool differentBehavior;
        private float distance;

        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) {
            return entity.type == ProjectileID.SharknadoBolt;
        }

        public override void OnSpawn(Projectile projectile, IEntitySource source) {

            if (EntitySource_Parent.Is(source)
                && NPC.Is(new EntitySource_Parent(source.Pointer).Entity)
                && new NPC(new EntitySource_Parent(source.Pointer).Entity.Pointer).type == NPCID.DukeFishron
                && Main.bloodMoon) {

                differentBehavior = true;
                var npc = new NPC(new EntitySource_Parent(source.Pointer).Entity.Pointer);
                distance = projectile.Distance(Main.player[npc.target].Center);
            }
        }

        public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter binaryWriter) {
            bitWriter.WriteBit(differentBehavior);

            if (differentBehavior) {
                binaryWriter.Write(distance);
            }
        }

        public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader binaryReader) {
            differentBehavior = bitReader.ReadBit();

            if (differentBehavior) {
                distance = binaryReader.ReadSingle();
            }
        }

        public override void AI(Projectile projectile) {
            if (differentBehavior) {
                int p = Player.FindClosest(projectile.position, projectile.width, projectile.height);
                float currentDistance = p == -1 ? 0 : projectile.Distance(Main.player[p].Center);
                int dustType = DustID.GemSapphire;

                if (currentDistance < distance / 4) {
                    differentBehavior = false;
                    projectile.netUpdate = true;
                }
                
                else if (currentDistance < distance / 2) {
                    projectile.extraUpdates = 0;
                }
                
                else {
                    projectile.extraUpdates = 1;
                    dustType = DustID.GemRuby;
                }

                int d = Dust.NewDust(projectile.position, projectile.width, projectile.height, dustType, Scale: 5f);
                Main.dust[d].noGravity = true;
            }
        }
    }
}
