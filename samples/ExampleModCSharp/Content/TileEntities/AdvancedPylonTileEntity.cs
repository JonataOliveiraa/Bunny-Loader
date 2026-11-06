using ExampleMod.Content.Tiles;
using System.IO;
using Terraria;
using Terraria.GameContent.Tile_Entities;
using Terraria.ID;
using Terraria.ModLoader.Default;
using Terraria.ModLoader.IO;

namespace ExampleMod.Content.TileEntities
{
    public class AdvancedPylonTileEntity : TEModdedPylon
    {
        public bool isActive;

        public override void NetSend(BinaryWriter writer) {
            writer.WriteFlags(isActive);
        }

        public override void NetReceive(BinaryReader reader) {
            reader.ReadFlags(out isActive);
        }

        public override void Update() {
            if (!Main.rand.NextBool(180)) {
                return;
            }

            isActive = !isActive;
            if (Main.netMode == NetmodeID.Server) {
                NetMessage.SendData(MessageID.TileEntitySharing, number: ID);
            }
        }
    }
}
