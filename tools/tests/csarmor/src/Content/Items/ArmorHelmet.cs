using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsArmor.Content.Items;

[AutoloadEquip(EquipType.Head)]
public class ArmorHelmet : ModItem
{
    public const string Bonus = "csarmor bonus";

    public static int ColorCalls;

    public override void SetStaticDefaults() => ArmorIDs.Head.Sets.DrawHatHair[Item.headSlot] = true;

    public override void SetDefaults()
    {
        Item.width = 18;
        Item.height = 18;
        Item.defense = 2;
    }

    public override bool IsArmorSet(Item head, Item body, Item legs) =>
        body.type == ModContent.ItemType<ArmorBreastplate>() && legs.type == ModContent.ItemType<ArmorLeggings>();

    public override void UpdateArmorSet(Player player) => player.setBonus = Bonus;

    public override void DrawArmorColor(Player drawPlayer, float shadow, ref Color color, ref int glowMask, ref Color glowMaskColor)
    {
        ColorCalls++;
        glowMask = 1;
        glowMaskColor = new Color(255, 0, 0, 255);
    }
}
