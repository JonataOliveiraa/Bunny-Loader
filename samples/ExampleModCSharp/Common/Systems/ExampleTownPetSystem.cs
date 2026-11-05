using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ExampleMod.Common.Systems
{
    public class ExampleTownPetSystem : ModSystem
    {
        public static bool boughtExampleTownPet = false;

        public override void ClearWorld() {
            boughtExampleTownPet = false;
        }

        public override void SaveWorldData(TagCompound tag) {
            if (boughtExampleTownPet) {
                tag["boughtExampleTownPet"] = true;
            }
        }

        public override void LoadWorldData(TagCompound tag) {
            boughtExampleTownPet = tag.ContainsKey("boughtExampleTownPet");
        }

        public override void NetSend(BinaryWriter writer) {
            writer.WriteFlags(boughtExampleTownPet);
        }

        public override void NetReceive(BinaryReader reader) {
            reader.ReadFlags(out boughtExampleTownPet);
        }
    }
}
