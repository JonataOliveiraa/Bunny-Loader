using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Accessories;

[AutoloadEquip(EquipType.Shield)]
public class ExampleShield : ModItem
{
    public override void SetDefaults()
    {
        Item.width = 24;
        Item.height = 28;
        Item.value = Item.buyPrice(10);
        Item.rare = 2;
        Item.accessory = true;

        Item.defense = 1000;
        Item.lifeRegen = 10;
    }

    public override void UpdateAccessory(Player player, bool hideVisual)
    {
        player.meleeDamage += 1f;
        player.rangedDamage += 1f;
        player.magicDamage += 1f;
        player.minionDamage += 1f;
        player.endurance = 1f - (0.1f * (1f - player.endurance));
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>()
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}
