using Terraria;
using Terraria.ModLoader;

namespace CsArmor.Content.Items;

public class ArmorRing : ModItem
{
    public static int LastSlot = -1;

    public override void SetDefaults()
    {
        Item.width = 20;
        Item.height = 20;
        Item.accessory = true;
    }

    public override bool CanEquipAccessory(Player player, int slot, bool modded)
    {
        LastSlot = slot;
        return slot != 3;
    }
}
