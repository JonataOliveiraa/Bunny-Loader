using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsHeal.Common;

public class ManaGlobal : GlobalItem
{
    public override void GetHealMana(Item item, Player player, bool quickHeal, ref int healValue)
    {
        if (item.type == ItemID.LesserManaPotion) healValue *= 2;
    }
}
