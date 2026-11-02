using ExampleMod.Common;
using ExampleMod.Content.Items;
using ExampleMod.Content.Items.Weapons.Melee;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ExampleMod.Content;

public class ExampleRecipes : ModSystem
{
    public static RecipeGroup ExampleRecipeGroup;
    public static RecipeGroup SilverBarRecipeGroup;

    public override void AddRecipeGroups()
    {
        ExampleRecipeGroup = RecipeGroup.Register(
            "ExampleMod:ExampleItem",
            ModContent.GetInstance<ExampleItem>().DisplayName.Key,
            ModContent.ItemType<ExampleItem>()
        );

        SilverBarRecipeGroup = RecipeGroup.Register(
            nameof(ItemID.SilverBar),
            "ItemName.SilverBar",
            ItemID.SilverBar, ItemID.TungstenBar
        );
    }

    public override void AddRecipes()
    {
        Recipe recipe = Recipe.Create(ModContent.ItemType<ExampleItem>(), 999);
        recipe.AddIngredient(ItemID.StoneBlock);
        recipe.Register();

        var resultItem = ModContent.GetInstance<ExampleItem>();

        resultItem.CreateRecipe()
            .AddIngredient(ItemID.StoneBlock)
            .AddIngredient(ItemID.Acorn, 10)
            .AddIngredient(resultItem)
            .AddIngredient<ExampleMeleeWeapon>()
            .AddIngredient(Mod, "ExampleMeleeWeapon")
            .AddRecipeGroup(RecipeGroups.Wood)
            .AddRecipeGroup(RecipeGroups.IronBar, 2)
            .AddRecipeGroup(ExampleRecipeGroup, 2)
            .AddRecipeGroup("Wood")
            .AddRecipeGroup("ExampleMod:ExampleItem", 2)
            .AddTile(TileID.WorkBenches)
            .AddCondition(Condition.InDesert)
            .AddCondition(Condition.NearWater)
            .AddCondition(Condition.TimeNight)
            .AddCondition(Language.GetOrRegister("Mods.ExampleMod.Conditions.LowHealth"), () => Main.LocalPlayer.statLife < Main.LocalPlayer.statLifeMax / 2)
            .Register();

        Recipe baseRecipe = Recipe.Create(ModContent.ItemType<ExampleItem>(), 10);
        baseRecipe.AddIngredient(ItemID.Wood, 10)
            .AddIngredient(ItemID.CopperCoin)
            .AddCondition(Condition.InBeach)
            .AddCondition(Condition.TimeDay)
            .Register();

        Recipe clonedRecipe = baseRecipe.Clone()
            .AddIngredient(ItemID.SilverCoin)
            .AddTile(TileID.Anvils);

        clonedRecipe.RemoveIngredient(ItemID.CopperCoin);
        clonedRecipe.RemoveCondition(Condition.InBeach);

        clonedRecipe.Register();

        Recipe.Create(ItemID.AlphabetStatueJ)
            .AddIngredient(ItemID.StoneBlock, 10)
            .AddIngredient(ItemID.Chain)
            .AddConsumeIngredientCallback(ExampleRecipeCallbacks.DontConsumeChain)
            .AddTile(TileID.HeavyWorkBench)
            .Register();

        Recipe.Create(ItemID.AlphabetStatueZ)
            .AddIngredient(ItemID.StoneBlock, 10)
            .AddIngredient(ItemID.Chain)
            .AddOnCraftCallback(ExampleRecipeCallbacks.RandomlySpawnConfetti)
            .AddTile(TileID.HeavyWorkBench)
            .Register();
    }

    public override void PostAddRecipes()
    {
        for (int i = 0; i < Recipe.numRecipes; i++)
        {
            Recipe recipe = Main.recipe[i];

            if (recipe.TryGetIngredient(ItemID.Wood, out Item ingredient))
            {
                ingredient.stack *= 2;
            }
        }
    }
}
