using Terraria.ModLoader;

namespace CsLoad.Content.Items;

// Fora do autoload: o Mod.Load o registra com AddContent.
[Autoload(false)]
public class ManualItem : ModItem
{
    public override void SetDefaults()
    {
        Item.width = 20;
        Item.height = 20;
    }
}
