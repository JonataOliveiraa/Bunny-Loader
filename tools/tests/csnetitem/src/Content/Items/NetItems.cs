using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CsNetItem.Content.Items;

public class Tagged : ModItem
{
    public int Value;

    public override void SetDefaults()
    {
        Item.width = 20;
        Item.height = 20;
        Item.maxStack = 1;
    }

    public override void NetSend(BinaryWriter writer) => writer.Write(Value);

    public override void NetReceive(BinaryReader reader) => Value = reader.ReadInt32();
}

public class Noted : ModItem
{
    public string Note = "";

    public override void SetDefaults()
    {
        Item.width = 20;
        Item.height = 20;
        Item.maxStack = 1;
    }

    public override void SaveData(TagCompound tag)
    {
        if (Note.Length > 0) tag["note"] = Note;
    }

    public override void LoadData(TagCompound tag) => Note = tag.GetString("note");
}
