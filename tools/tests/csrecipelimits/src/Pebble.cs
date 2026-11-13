using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace RecipeLimitsTest;

public class Pebble : ModItem
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.StoneBlock}";

    public override void SetDefaults()
    {
        Item.width = 12;
        Item.height = 12;
        Item.maxStack = 999;
    }
}
