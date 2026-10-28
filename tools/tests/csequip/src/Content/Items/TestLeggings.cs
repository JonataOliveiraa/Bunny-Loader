using Terraria;
using Terraria.ModLoader;

namespace CsEquip.Content.Items;

[AutoloadEquip(EquipType.Legs)]
public class TestLeggings : ModItem
{
    public override void SetDefaults()
    {
        Item.width = 18;
        Item.height = 18;
        Item.defense = 5;
    }
}
