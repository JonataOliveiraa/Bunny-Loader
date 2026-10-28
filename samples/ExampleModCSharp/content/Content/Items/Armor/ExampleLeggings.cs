using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Armor;

[AutoloadEquip(EquipType.Legs)]
public class ExampleLeggings : ModItem
{
    public static readonly int MoveSpeedBonus = 5;

    public override void SetDefaults()
    {
        Item.width = 18;
        Item.height = 18;
        Item.value = Item.sellPrice(gold: 1);
        Item.rare = 2;
        Item.defense = 5;
    }

    public override void UpdateEquip(Player player)
    {
        player.moveSpeed += MoveSpeedBonus / 100f;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>()
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}
