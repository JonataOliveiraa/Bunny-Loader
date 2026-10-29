using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsArmor.Content.Items;

[AutoloadEquip(EquipType.Legs)]
public class ArmorLeggings : ModItem
{
    public override void SetStaticDefaults() => ArmorIDs.Legs.Sets.HidesBottomSkin[Item.legSlot] = true;

    public override void SetDefaults()
    {
        Item.width = 18;
        Item.height = 18;
        Item.defense = 2;
    }
}
