using System.IO;

namespace Terraria.ModLoader.IO;

public static class ItemIO
{
    public static void Send(Item item, BinaryWriter writer, bool writeStack = false, bool writeFavorite = false)
    {
        writer.Write((short)item.type);
        writer.Write((byte)item.prefix);
        if (writeStack) writer.Write(item.stack);
        if (writeFavorite) writer.Write(item.favorited);
    }

    public static void Receive(Item item, BinaryReader reader, bool readStack = false, bool readFavorite = false)
    {
        item.SetDefaults(reader.ReadInt16(), default);
        item.Prefix(reader.ReadByte());
        if (readStack) item.stack = reader.ReadInt32();
        if (readFavorite) item.favorited = reader.ReadBoolean();
    }

    public static Item Receive(BinaryReader reader, bool readStack = false, bool readFavorite = false)
    {
        Item item = Item.New();
        _ = new BunnyLoader.Interop.GameHandle(item.Pointer);
        Receive(item, reader, readStack, readFavorite);
        return item;
    }
}
