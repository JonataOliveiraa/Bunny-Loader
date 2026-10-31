using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsArmor.Content.Items;

[AutoloadEquip(EquipType.Body)]
public class ArmorBreastplate : ModItem
{
    public static int ArmCalls;

    public override void SetStaticDefaults()
    {
        ArmorIDs.Body.Sets.HidesTopSkin[Item.bodySlot] = true;
        ArmorIDs.Body.Sets.HidesHands[Item.bodySlot] = false;
    }

    public override void SetDefaults()
    {
        Item.width = 18;
        Item.height = 18;
        Item.defense = 3;
    }

    public override void ArmorArmGlowMask(Player drawPlayer, float shadow, ref int glowMask, ref Color color)
    {
        ArmCalls++;
        glowMask = 2;
    }
}
