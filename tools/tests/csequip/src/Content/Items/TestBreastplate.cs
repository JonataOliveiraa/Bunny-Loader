using Terraria;
using Terraria.ModLoader;

namespace CsEquip.Content.Items;

[AutoloadEquip(EquipType.Body)]
public class TestBreastplate : ModItem
{
    public static int Updates;

    public override void SetDefaults()
    {
        Item.width = 18;
        Item.height = 18;
        Item.defense = 6;
    }

    public override void UpdateEquip(Player player)
    {
        Updates++;
        player.statManaMax2 += 20;
    }
}
