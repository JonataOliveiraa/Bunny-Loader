using System.IO;
using ExampleMod.Common.Players;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod;

public class ExampleMod : Mod
{
    internal enum MessageType : byte
    {
        ExampleStatIncreasePlayerSync,
    }

    public override void HandlePacket(BinaryReader reader, int whoAmI)
    {
        MessageType msgType = (MessageType)reader.ReadByte();

        switch (msgType)
        {
            case MessageType.ExampleStatIncreasePlayerSync:
                byte playerNumber = reader.ReadByte();
                ExampleStatIncreasePlayer examplePlayer = Main.player[playerNumber].GetModPlayer<ExampleStatIncreasePlayer>();
                examplePlayer.ReceivePlayerSync(reader);

                if ((Main.netMode & NetmodeID.Server) != 0)
                {
                    examplePlayer.SyncPlayer(-1, whoAmI, false);
                }
                break;
            default:
                Logger.WarnFormat("ExampleMod: Unknown Message type: {0}", msgType);
                break;
        }
    }
}
