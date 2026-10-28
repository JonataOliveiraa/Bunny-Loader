using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Accessories;

[AutoloadEquip(EquipType.Wings)]
public class ExampleWings : ModItem
{
    public override void SetStaticDefaults()
    {
        ArmorIDs.Wing.Sets.Stats[Item.wingSlot] = new WingStats(180, 9f, 2.5f);
    }

    public override void SetDefaults()
    {
        Item.width = 22;
        Item.height = 20;
        Item.value = 10000;
        Item.rare = 2;
        Item.accessory = true;
    }

    public override void VerticalWingSpeeds(Player player, ref float ascentWhenFalling, ref float ascentWhenRising,
                                            ref float maxCanAscendMultiplier, ref float maxAscentMultiplier, ref float constantAscend)
    {
        ascentWhenFalling = 0.85f;
        ascentWhenRising = 0.15f;
        maxCanAscendMultiplier = 1f;
        maxAscentMultiplier = 3f;
        constantAscend = 0.135f;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>()
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}
