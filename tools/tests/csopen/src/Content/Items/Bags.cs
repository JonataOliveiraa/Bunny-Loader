using Terraria;
using Terraria.ModLoader;

namespace CsOpen.Content.Items;

public abstract class TestBag : ModItem
{
    public static int RightClicks;

    public override void SetDefaults()
    {
        Item.width = 24;
        Item.height = 24;
        Item.maxStack = 99;
    }

    public override bool CanRightClick() => true;

    public override void RightClick(Player player) => RightClicks++;
}

public class Bag : TestBag
{
}

public class KeepBag : TestBag
{
    public override bool ConsumeItem(Player player) => false;
}

public class LockedBag : TestBag
{
    public static bool Closed;

    public override bool CanRightClick() => !Closed;
}
