using Terraria;
using Terraria.ModLoader;

namespace CsItems.Content.Items;

// O ExampleItem do ExampleMod do tModLoader (sem receita e pesquisa, que ainda nao tem loader).
public class ExampleItem : ModItem
{
    public override void SetStaticDefaults()
    {
        Checks.Report("static defaults", Type == ModContent.ItemType<ExampleItem>(), $"type={Type}");
        Checks.Run();
    }

    public override void SetDefaults()
    {
        Item.width = 20;
        Item.height = 20;
        Item.maxStack = Item.CommonMaxStack;
        Item.value = Item.buyPrice(silver: 1);
    }
}
