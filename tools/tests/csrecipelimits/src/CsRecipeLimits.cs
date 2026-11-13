using System;
using log4net;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace RecipeLimitsTest;

public sealed class CsRecipeLimitsMod : Mod
{
    static int frames, pass, fail;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsRecipeLimitsMod>().Logger;

    public override void Load() => On_Player.Update += Update;

    static void Update(On_Player.orig_Update orig, Player self, int i)
    {
        orig(self, i);
        if (Main.gameMenu)
        {
            frames = pass = fail = 0;
            done = false;
            return;
        }
        if (i != Main.myPlayer || done || ++frames != 120) return;
        done = true;
        try { Run(); }
        catch (Exception e) { Report("run", false, e.ToString()); }
        Log.Info($"csrecipelimits END pass={pass} fail={fail}");
    }

    static void Run()
    {
        Recipe r14 = LimitRecipes.Largest, full = LimitRecipes.Full, holed = LimitRecipes.Holed;
        Recipe sword = LimitRecipes.Sword, bow = LimitRecipes.Bow;
        int pebble = ModContent.ItemType<Pebble>();

        Report("a recipe takes 14 ingredients", r14.RecipeIndex >= 0 && LimitRecipes.Count(r14) == 14 && !r14.Disabled,
            $"index={r14.RecipeIndex} count={LimitRecipes.Count(r14)} disabled={r14.Disabled}");
        Report("the 15th ingredient throws RecipeException", LimitRecipes.ThrewAt15 && LimitRecipes.ThrowMessage.Contains("14"),
            $"'{LimitRecipes.ThrowMessage}'");
        int nulls = 0;
        var all = Main.recipe;
        for (int k = 0; k < all.Length; k++)
        {
            if (all[k].IsNull) nulls++;
        }
        Report("Register refuses a recipe with no empty slot or a null slot, and the recipe list stays whole",
            LimitRecipes.RefusedFull && LimitRecipes.RefusedNull && LimitRecipes.CountAfterRefusals == LimitRecipes.CountBeforeRefusals && nulls == 0,
            $"full={LimitRecipes.RefusedFull} null={LimitRecipes.RefusedNull} count={LimitRecipes.CountBeforeRefusals}->{LimitRecipes.CountAfterRefusals} length={LimitRecipes.LengthAfterRefusals} nulls={nulls}");
        Report("the shimmer recipe sets cover the whole recipe list", ShimmerTransforms.RecipeSets.PostSkeletron.Length == all.Length,
            $"sets={ShimmerTransforms.RecipeSets.PostSkeletron.Length} recipes={all.Length}");
        Report("a recipe filled to 15 after Register is turned off with an empty last slot",
            full.Disabled && !full.requiredItem[14].IsNull && full.requiredItem[14].type == 0 && LimitRecipes.Count(full) == 14,
            $"disabled={full.Disabled} last={full.requiredItem[14].type} count={LimitRecipes.Count(full)}");
        Report("a null slot set after Register is filled and the recipe turned off", holed.Disabled && !holed.requiredItem[3].IsNull,
            $"disabled={holed.Disabled} null={holed.requiredItem[3].IsNull}");
        Report("AddIngredient on a game recipe changes only that recipe",
            !sword.IsNull && sword.HasIngredient(ItemID.Gel) && sword.HasIngredient(ItemID.Wood) && LimitRecipes.SharedAfterAdd == 0 && !sword.Disabled,
            $"gel={sword.HasIngredient(ItemID.Gel)} wood={sword.HasIngredient(ItemID.Wood)} shared={LimitRecipes.SharedAfterAdd}");
        Report("a game recipe edited past the limit is turned off", !bow.IsNull && bow.Disabled && bow.requiredItem[14].type == 0,
            $"added={LimitRecipes.BowAdded} disabled={bow.Disabled} last={bow.requiredItem[14].type}");
        Report("the shared empty slot is emptied again", Recipe.defaultRecipeItem.type == 0 && LimitRecipes.Count(Main.recipe[0]) == LimitRecipes.FirstCount,
            $"shared={Recipe.defaultRecipeItem.type} first={LimitRecipes.Count(Main.recipe[0])}/{LimitRecipes.FirstCount} sharedSlot={LimitRecipes.SharedSlot}");
        Report("the material pass reaches the last recipe", ItemID.Sets.IsAMaterial[pebble] && r14.requiredItem[13].type == pebble && r14.requiredItem[13].material,
            $"set={ItemID.Sets.IsAMaterial[pebble]} field={r14.requiredItem[13].material}");

        Recipe.FindRecipes(false);
        bool listed = false;
        var available = Main.availableRecipe;
        for (int k = 0; k < Main.numAvailableRecipes; k++)
        {
            if (available[k] == full.RecipeIndex || available[k] == holed.RecipeIndex) listed = true;
        }
        Report("the crafting menu builds without the turned-off recipes", !listed, $"available={Main.numAvailableRecipes}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csrecipelimits {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
