using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ExampleMod.Common.GlobalNPCs
{
    
    public class ExampleNPCNetSync : GlobalNPC
    {
        public override bool InstancePerEntity => true;
        private bool differentBehavior;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) {
            return entity.type == NPCID.Sharkron2;
        }

        public override void OnSpawn(NPC npc, IEntitySource source) {

            if (EntitySource_Parent.Is(source)
                && Projectile.Is(new EntitySource_Parent(source.Pointer).Entity)
                && new Projectile(new EntitySource_Parent(source.Pointer).Entity.Pointer).type == ProjectileID.Cthulunado
                && Main.bloodMoon) {
                differentBehavior = true;
            }
        }

        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter) {
            bitWriter.WriteBit(differentBehavior);
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader) {
            differentBehavior = bitReader.ReadBit();
        }

        public override void AI(NPC npc) {
            if (differentBehavior) {
                npc.scale *= 1.0025f;
                if (npc.scale > 3f) {
                    npc.scale = 3f;
                }
            }
        }
    }
}