using Terraria;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items;

// O ExampleItem do tModLoader. Nome e tooltip vem de Localization/<idioma>.json;
// a textura e o ExampleItem.png ao lado. A receita (10 Blocos de Terra numa
// Bancada) chega com o loader de receitas.
public class ExampleItem : ModItem
{
    public override void SetDefaults()
    {
        Item.width = 20;
        Item.height = 20;

        Item.maxStack = Item.CommonMaxStack;
        Item.value = Item.buyPrice(silver: 1);
    }
}
