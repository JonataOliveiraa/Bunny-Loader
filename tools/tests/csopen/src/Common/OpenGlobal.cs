using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsOpen.Common;

public class OpenGlobal : GlobalItem
{
    public static int CrateRightClicks, AcornRightClicks;

    public override bool CanRightClick(Item item) => item.type == ItemID.Acorn;

    public override void RightClick(Item item, Player player)
    {
        if (item.type == ItemID.WoodenCrate) CrateRightClicks++;
        if (item.type == ItemID.Acorn) AcornRightClicks++;
    }
}
