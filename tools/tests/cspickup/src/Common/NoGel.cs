using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsPickup.Common;

public class NoGel : GlobalItem
{
    public override bool CanPickup(Item item, Player player) => item.type != ItemID.Gel;
}
