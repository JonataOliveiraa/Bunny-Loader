using Terraria;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Armor;

[AutoloadEquip(EquipType.Head)]
public class ExampleHelmet : ModItem
{
    public static readonly int AdditiveGenericDamageBonus = 20;

    public override void SetDefaults()
    {
        Item.width = 18;
        Item.height = 18;
        Item.value = Item.sellPrice(gold: 1);
        Item.rare = 2;
        Item.defense = 5;
    }

    public override bool IsArmorSet(Item head, Item body, Item legs) =>
        body.type == ModContent.ItemType<ExampleBreastplate>() && legs.type == ModContent.ItemType<ExampleLeggings>();

    public override void UpdateArmorSet(Player player)
    {
        float bonus = AdditiveGenericDamageBonus / 100f;
        player.meleeDamage += bonus;
        player.rangedDamage += bonus;
        player.magicDamage += bonus;
        player.minionDamage += bonus;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>()
            .AddTile(Terraria.ID.TileID.WorkBenches)
            .Register();
    }
}
