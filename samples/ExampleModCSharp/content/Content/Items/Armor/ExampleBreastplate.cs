using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Items.Armor;

[AutoloadEquip(EquipType.Body)]
public class ExampleBreastplate : ModItem
{
    public static readonly int MaxManaIncrease = 20;
    public static readonly int MaxMinionIncrease = 1;

    public override void SetDefaults()
    {
        Item.width = 18;
        Item.height = 18;
        Item.value = Item.sellPrice(gold: 1);
        Item.rare = 2;
        Item.defense = 6;
    }

    public override void UpdateEquip(Player player)
    {
        player.buffImmune[BuffID.OnFire] = true;
        player.statManaMax2 += MaxManaIncrease;
        player.maxMinions += MaxMinionIncrease;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>()
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}
