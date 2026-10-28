using Terraria.ModLoader;

namespace CsUse.Content.Items;

public class PlainItem : ModItem
{
    public override void SetDefaults()
    {
        Item.width = 40;
        Item.height = 40;
        Item.useStyle = 1;
        Item.useTime = 20;
        Item.useAnimation = 20;
    }
}
