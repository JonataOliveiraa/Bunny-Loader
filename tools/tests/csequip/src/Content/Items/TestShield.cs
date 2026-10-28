using Terraria;
using Terraria.ModLoader;

namespace CsEquip.Content.Items;

[AutoloadEquip(EquipType.Shield)]
public class TestShield : ModItem
{
    public static int Updates;
    public static bool LastHidden;

    public override void SetDefaults()
    {
        Item.width = 24;
        Item.height = 28;
        Item.accessory = true;
        Item.defense = 2;
    }

    public override void UpdateAccessory(Player player, bool hideVisual)
    {
        Updates++;
        LastHidden = hideVisual;
    }
}
