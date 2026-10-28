using Terraria.ModLoader;

namespace CsInstances.Content.Items;

public class CounterItem : ModItem
{
    public int Count;

    public bool RanSetDefaults;

    public override void SetStaticDefaults() => Checks.Run();

    public override void SetDefaults()
    {
        RanSetDefaults = true;
        Item.width = 26;
        Item.height = 26;
    }
}
