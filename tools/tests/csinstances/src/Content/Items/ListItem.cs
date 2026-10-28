using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CsInstances.Content.Items;

public class ListItem : ModItem
{
    public List<int> Values = new();

    public override ModItem Clone(Item newEntity)
    {
        var copy = (ListItem)base.Clone(newEntity);
        copy.Values = new List<int>(Values);
        return copy;
    }

    public override void SetDefaults()
    {
        Item.width = 26;
        Item.height = 26;
    }
}
