using Terraria.ModLoader;

namespace CsLoad.Content.Items;

[Autoload(false)]
public class ManualItem : ModItem
{
    public override void SetDefaults()
    {
        Item.width = 20;
        Item.height = 20;
    }
}
