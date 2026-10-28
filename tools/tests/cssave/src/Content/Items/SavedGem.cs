using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CsSave.Content.Items;

public class SavedGem : ModItem
{
    public static int Loads;

    public int Uses;
    public string Label = "";
    public bool Flag;
    public float Ratio;
    public List<int> Numbers = new();
    public string Inner = "";

    public override ModItem Clone(Item newEntity)
    {
        var copy = (SavedGem)base.Clone(newEntity);
        copy.Numbers = new List<int>(Numbers);
        return copy;
    }

    public override void SetDefaults()
    {
        Item.width = 26;
        Item.height = 26;
        Item.maxStack = 1;
    }

    public override void SaveData(TagCompound tag)
    {
        tag["uses"] = Uses;
        tag["label"] = Label;
        tag["flag"] = Flag;
        tag["ratio"] = Ratio;
        tag["numbers"] = Numbers;
        tag["inner"] = new TagCompound { ["deep"] = Inner };
    }

    public override void LoadData(TagCompound tag)
    {
        Loads++;
        Uses = tag.GetInt("uses");
        Label = tag.GetString("label");
        Flag = tag.GetBool("flag");
        Ratio = tag.GetFloat("ratio");
        Numbers = new List<int>(tag.GetList<int>("numbers"));
        Inner = tag.GetCompound("inner").GetString("deep");
    }
}
