using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Accessories;

[AutoloadEquip(EquipType.Beard)]
public class ExampleBeard : ModItem
{
    public override void SetStaticDefaults()
    {
        ArmorIDs.Beard.Sets.UseHairColor[Item.beardSlot] = true;
    }

    public override void SetDefaults()
    {
        Item.width = 18;
        Item.height = 14;
        Item.maxStack = 1;
        Item.color = Main.LocalPlayer.hairColor;
        Item.value = Item.sellPrice(0, 1);
        Item.accessory = true;
        Item.vanity = true;
    }
}
