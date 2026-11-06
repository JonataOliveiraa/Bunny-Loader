using System.IO;

namespace Terraria.ModLoader.IO;

public static class ItemIO
{
    public static TagCompound Save(Item item)
    {
        var tag = new TagCompound();
        if (item.IsNull || item.type <= 0)
            return tag;
        if (ItemLoader.InstanceOf(item) is ModItem mod)
        {
            tag["mod"] = mod.Mod.Name;
            tag["name"] = mod.Name;
            string? data = ItemLoader.SavedText(mod);
            if (!string.IsNullOrEmpty(data)) tag["data"] = data;
        }
        else
        {
            tag["mod"] = "Terraria";
            tag["id"] = item.type;
        }
        if (item.prefix != 0)
        {
            if (PrefixLoader.GetPrefix(item.prefix) is ModPrefix prefix)
            {
                tag["modPrefixMod"] = prefix.Mod.Name;
                tag["modPrefixName"] = prefix.Name;
            }
            else tag["prefix"] = item.prefix;
        }
        if (item.stack > 1) tag["stack"] = item.stack;
        if (item.favorited) tag["fav"] = true;
        return tag;
    }

    public static Item Load(TagCompound tag)
    {
        Item item = Item.New();
        _ = new BunnyLoader.Interop.GameHandle(item.Pointer);
        Load(item, tag);
        return item;
    }

    public static void Load(Item item, TagCompound tag)
    {
        string mod = tag.ContainsKey("mod") ? tag.GetString("mod") : "";
        int type = 0;
        if (mod == "Terraria") type = tag.GetInt("id");
        else if (mod.Length > 0 && ModContent.TryFind(mod, tag.GetString("name"), out ModItem found)) type = found.Type;
        item.SetDefaults(type, default);
        if (type <= 0)
            return;
        if (tag.ContainsKey("data") && ItemLoader.InstanceOf(item) is ModItem instance) ItemLoader.LoadText(instance, tag.GetString("data"));
        if (tag.ContainsKey("modPrefixName") && ModContent.TryFind(tag.GetString("modPrefixMod"), tag.GetString("modPrefixName"), out ModPrefix prefix)) item.Prefix(prefix.Type);
        else if (tag.ContainsKey("prefix")) item.Prefix(tag.GetInt("prefix"));
        if (tag.ContainsKey("stack")) item.stack = tag.GetInt("stack");
        if (tag.ContainsKey("fav")) item.favorited = tag.GetBool("fav");
    }

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
