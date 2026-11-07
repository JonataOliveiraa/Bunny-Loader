using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ApiGapsTest.Items;

[AutoloadEquip(EquipType.Head)]
public sealed class GapHelmet : ModItem
{
    internal static int updates;

    public readonly int Roll = Main.rand.Next(10);
    public readonly bool Reversed = Main.ReversedUpDownArmorSetBonuses;
    public readonly Color Disco = Main.DiscoColor;

    public override void SetStaticDefaults()
    {
        ItemID.Sets.FoodParticleColors[Type] = new Color[3] { new(255, 0, 0), new(0, 255, 0), new(0, 0, 255) };
    }

    public override void SetDefaults()
    {
        Item.width = 18;
        Item.height = 18;
        Item.defense = 1;
    }

    public override void Update(WorldItem item, ref float gravity, ref float maxFallSpeed) => updates++;
}
