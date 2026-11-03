using ExampleMod.Content.Items;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ExampleMod.Content.Pets.ExamplePet;

public class ExamplePetItem : ModItem
{
    public override void SetDefaults()
    {
        Item.CloneDefaults(ItemID.ZephyrFish);

        Item.shoot = ModContent.ProjectileType<ExamplePetProjectile>();
        Item.buffType = ModContent.BuffType<ExamplePetBuff>();
    }

    public override bool? UseItem(Player player)
    {
        if (player.whoAmI == Main.myPlayer)
        {
            player.AddBuff(Item.buffType, 3600);
        }

        return true;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient<ExampleItem>()
            .AddTile<Tiles.Furniture.ExampleWorkbench>()
            .Register();
    }
}
