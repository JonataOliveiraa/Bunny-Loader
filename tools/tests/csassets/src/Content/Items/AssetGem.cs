using Terraria.ModLoader;

namespace CsAssets.Content.Items;

public class AssetGem : ModItem
{
    public override void SetStaticDefaults() => Checks.OnGameThread();

    public override void SetDefaults()
    {
        Item.width = 26;
        Item.height = 26;
    }
}
