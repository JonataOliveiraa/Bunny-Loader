using ExampleMod.Content.NPCs;
using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ExampleMod.Common.Systems
{
    public class TownNPCRespawnSystem : ModSystem
    {
        public static bool unlockedExamplePersonSpawn = false;

        public override void ClearWorld() {
            unlockedExamplePersonSpawn = false;
        }

        public override void SaveWorldData(TagCompound tag) {
            tag[nameof(unlockedExamplePersonSpawn)] = unlockedExamplePersonSpawn;
        }

        public override void LoadWorldData(TagCompound tag) {
            unlockedExamplePersonSpawn = tag.GetBool(nameof(unlockedExamplePersonSpawn));

            unlockedExamplePersonSpawn |= NPC.AnyNPCs(ModContent.NPCType<ExamplePerson>());
        }

        public override void NetSend(BinaryWriter writer) {
            writer.WriteFlags(unlockedExamplePersonSpawn);
        }

        public override void NetReceive(BinaryReader reader) {
            reader.ReadFlags(out unlockedExamplePersonSpawn);
        }
    }
}
