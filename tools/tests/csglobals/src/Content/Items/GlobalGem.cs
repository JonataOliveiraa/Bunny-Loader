using System.Collections.Generic;
using CsGlobals.Common.GlobalItems;
using Terraria.ModLoader;

namespace CsGlobals.Content.Items;

public class GlobalGem : ModItem
{
    public static readonly List<string> Order = new();

    public static bool SawGemGlobal;

    public override void SetDefaults()
    {
        Order.Add("GlobalGem.SetDefaults");
        SawGemGlobal = Item.TryGetGlobalItem(out GemGlobal _);
        Item.width = 26;
        Item.height = 26;
    }
}
