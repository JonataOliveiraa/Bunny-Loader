using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsEquip.Content.Items;

[AutoloadEquip(EquipType.Wings)]
public class TestWings : ModItem
{
    public static int Vertical, Horizontal, SlotInStatic = -1;

    public override void SetStaticDefaults()
    {
        SlotInStatic = Item.wingSlot;
        ArmorIDs.Wing.Sets.Stats[Item.wingSlot] = new WingStats(180, 9f, 2.5f);
    }

    public override void SetDefaults()
    {
        Item.width = 22;
        Item.height = 20;
        Item.accessory = true;
    }

    public override void VerticalWingSpeeds(Player player, ref float ascentWhenFalling, ref float ascentWhenRising,
                                            ref float maxCanAscendMultiplier, ref float maxAscentMultiplier, ref float constantAscend)
    {
        Vertical++;
        ascentWhenFalling = 0.85f;
        ascentWhenRising = 0.15f;
        maxCanAscendMultiplier = 1f;
        maxAscentMultiplier = 3f;
        constantAscend = 0.135f;
    }

    public override void HorizontalWingSpeeds(Player player, ref float speed, ref float acceleration)
    {
        Horizontal++;
        speed = 9f;
    }
}
