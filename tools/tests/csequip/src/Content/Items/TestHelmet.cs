using Terraria;
using Terraria.ModLoader;

namespace CsEquip.Content.Items;

[AutoloadEquip(EquipType.Head)]
public class TestHelmet : ModItem
{
    public static int SetUpdates, VanityUpdates;

    public override void SetDefaults()
    {
        Item.width = 18;
        Item.height = 18;
        Item.defense = 5;
    }

    public override bool IsArmorSet(Item head, Item body, Item legs) =>
        body.type == ModContent.ItemType<TestBreastplate>() && legs.type == ModContent.ItemType<TestLeggings>();

    public override void UpdateArmorSet(Player player) => SetUpdates++;

    public override void UpdateVanitySet(Player player) => VanityUpdates++;
}
