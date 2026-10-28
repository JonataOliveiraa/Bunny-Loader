using Terraria.ModLoader;

namespace CsLoc.Content.Items;

public class LocGem : ModItem
{
    public override void SetStaticDefaults() => Checks.OnGameThread();

    public override void SetDefaults()
    {
        Item.width = 26;
        Item.height = 26;
    }
}
