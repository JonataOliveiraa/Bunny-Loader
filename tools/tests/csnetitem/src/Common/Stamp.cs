using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace CsNetItem.Common;

public class Stamp : GlobalItem
{
    public int Mark;

    public override bool InstancePerEntity => true;

    public override void NetSend(Item item, BinaryWriter writer) => writer.Write(Mark);

    public override void NetReceive(Item item, BinaryReader reader) => Mark = reader.ReadInt32();
}
