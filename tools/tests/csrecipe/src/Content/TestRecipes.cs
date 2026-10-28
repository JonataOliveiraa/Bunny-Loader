using CsRecipe.Content.Items;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CsRecipe.Content;

public class TestRecipes : ModSystem
{
    public static RecipeGroup ModGroup, SilverGroup, NamedGroup;

    public static Recipe Basic, Chain, Gated, Disabled, Base, Cloned, KeepChain, Crafted, Named, Beyond;

    public static bool Switch, StaticDefaults, PostSetup;

    public static int Fillers, Crafts;

    public override void SetStaticDefaults() => StaticDefaults = true;

    public override void PostSetupContent() => PostSetup = true;

    public override void AddRecipeGroups()
    {
        ModGroup = RecipeGroup.Register("CsRecipe:Bit", "ItemName.DirtBlock", ModContent.ItemType<Bit>(), ItemID.DirtBlock);
        SilverGroup = RecipeGroup.Register(nameof(ItemID.SilverBar), "ItemName.SilverBar", ItemID.SilverBar, ItemID.TungstenBar, ModContent.ItemType<Bit>());
        NamedGroup = RecipeGroup.Register("CsRecipe:Named", () => "Named group", ItemID.Gel, ModContent.ItemType<Bit>());
        RecipeGroups.Bugs.Add(ModContent.ItemType<Bit>());
    }

    public override void AddRecipes()
    {
        Basic = Recipe.Create(ModContent.ItemType<Thing>(), 7);
        Basic.AddIngredient(ItemID.DirtBlock);
        Basic.Register();

        var thing = ModContent.GetInstance<Thing>();
        Chain = thing.CreateRecipe()
            .AddIngredient(ItemID.StoneBlock)
            .AddIngredient(ItemID.Acorn, 10)
            .AddIngredient<Bit>()
            .AddIngredient(Mod, "Bit")
            .AddRecipeGroup(RecipeGroups.Wood)
            .AddRecipeGroup(RecipeGroups.IronBar, 2)
            .AddRecipeGroup(ModGroup, 2)
            .AddRecipeGroup("Wood")
            .AddRecipeGroup("CsRecipe:Bit", 2)
            .AddTile(TileID.WorkBenches)
            .AddCondition(Condition.InDesert)
            .AddCondition(Condition.NearWater)
            .AddCondition(Condition.TimeNight)
            .AddCondition(Language.GetOrRegister("Mods.CsRecipe.Conditions.LowHealth"), () => Main.LocalPlayer.statLife < Main.LocalPlayer.statLifeMax / 2)
            .Register();

        Gated = Recipe.Create(ItemID.Torch, 3)
            .AddIngredient(ItemID.DirtBlock)
            .AddCondition(new Condition("Mods.CsRecipe.Conditions.Switch", () => Switch))
            .Register();

        Disabled = Recipe.Create(ItemID.Torch, 5).AddIngredient(ItemID.DirtBlock).Register();
        Disabled.DisableRecipe();

        Base = Recipe.Create(ModContent.ItemType<Thing>(), 10);
        Base.AddIngredient(ItemID.Wood, 10)
            .AddIngredient(ItemID.CopperCoin)
            .AddCondition(Condition.InBeach)
            .AddCondition(Condition.TimeDay)
            .Register();
        Cloned = Base.Clone()
            .AddIngredient(ItemID.SilverCoin)
            .AddTile(TileID.Anvils);
        Cloned.RemoveIngredient(ItemID.CopperCoin);
        Cloned.RemoveCondition(Condition.InBeach);
        Cloned.Register();

        KeepChain = Recipe.Create(ItemID.AlphabetStatueJ)
            .AddIngredient(ItemID.StoneBlock, 10)
            .AddIngredient(ItemID.Chain)
            .AddConsumeIngredientCallback(DontConsumeChain)
            .AddTile(TileID.HeavyWorkBench)
            .Register();

        Crafted = Recipe.Create(ModContent.ItemType<Thing>(), 2)
            .AddIngredient(ItemID.Gel, 3)
            .AddOnCraftCallback((recipe, item, consumed, destination) => Crafts++)
            .Register();

        Named = Recipe.Create(ModContent.ItemType<Thing>(), 3).AddRecipeGroup("CsRecipe:Named").Register();

        int need = Recipe.maxRecipes - Recipe.numRecipes + 2;
        if (need > 1 && need < 2000)
        {
            for (int i = 0; i < need - 1; i++)
            {
                Recipe.Create(ItemID.DirtBlock).AddIngredient(ItemID.LunarBar, 999).Register();
                Fillers++;
            }
            Beyond = Recipe.Create(ItemID.Gel, 2).AddIngredient(ItemID.DirtBlock, 2).Register();
        }
    }

    public override void PostAddRecipes()
    {
        if (Basic.TryGetIngredient(ItemID.DirtBlock, out Item ingredient)) ingredient.stack *= 2;
    }

    static void DontConsumeChain(Recipe recipe, int type, ref int amount, bool isDecrafting)
    {
        if (type == ItemID.Chain) amount = 0;
    }
}
