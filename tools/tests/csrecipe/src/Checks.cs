using System;
using System.Collections.Generic;
using CsRecipe.Content;
using CsRecipe.Content.Items;
using log4net;
using BunnyLoader.Interop;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Exceptions;

namespace CsRecipe;

public static class Checks
{
    static int pass, fail, frames, dirtSlot = -1;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsRecipeMod>().Logger;

    public static void Reset()
    {
        frames = pass = fail = 0;
        done = false;
    }

    public static void Frame(Player p)
    {
        if (done || ++frames != 120) return;
        done = true;
        try { Run(p); }
        catch (Exception e) { Report("run", false, e.ToString()); }
        finally { Cleanup(p); }
        Log.Info($"csrecipe END pass={pass} fail={fail}");
    }

    static void Run(Player p)
    {
        int thing = ModContent.ItemType<Thing>(), bit = ModContent.ItemType<Bit>();
        Report("ModSystem runs SetStaticDefaults and PostSetupContent", TestRecipes.StaticDefaults && TestRecipes.PostSetup, "");

        Recipe basic = TestRecipes.Basic;
        Report("Recipe.Create and Register put the recipe in Main.recipe", basic.RecipeIndex >= 0 && Main.recipe[basic.RecipeIndex] == basic &&
            basic.createItem.type == thing && basic.createItem.stack == 7 && basic.Mod == ModContent.GetInstance<CsRecipeMod>(),
            $"index={basic.RecipeIndex} result={basic.createItem.type}x{basic.createItem.stack} total={Recipe.numRecipes}");
        Report("PostAddRecipes edits an ingredient", basic.TryGetIngredient(ItemID.DirtBlock, out Item dirt) && dirt.stack == 2, $"stack={dirt.stack}");

        Recipe chain = TestRecipes.Chain;
        var types = new List<int>();
        for (int i = 0; i < chain.requiredItem.Length && chain.requiredItem[i].type > 0; i++) types.Add(chain.requiredItem[i].type);
        Report("CreateRecipe chains ingredients, groups, tile and conditions",
            types.Count == 9 && types[0] == ItemID.StoneBlock && types[2] == bit && types[3] == bit && chain.HasRecipeGroup(RecipeGroups.Wood) &&
            chain.HasRecipeGroup(RecipeGroups.IronBar) && chain.HasRecipeGroup("CsRecipe:Bit") && chain.HasTile(TileID.WorkBenches) &&
            chain.Conditions.Count == 4 && chain.TryGetIngredient(ItemID.Acorn, out Item acorn) && acorn.stack == 10,
            $"ingredients={string.Join(",", types)} conditions={chain.Conditions.Count} tile={chain.requiredTile}");

        Report("RecipeGroup.Register merges into a vanilla group by key, and keys are kept",
            TestRecipes.SilverGroup == RecipeGroups.SilverBar && RecipeGroups.SilverBar.Contains(bit) && RecipeGroups.SilverBar.Key == "SilverBar" &&
            TestRecipes.ModGroup.Key == "CsRecipe:Bit" && RecipeGroups.Bugs.Contains(bit) && TestRecipes.ModGroup.RegisteredId >= 0,
            $"silver={TestRecipes.SilverGroup.RegisteredId}/{RecipeGroups.SilverBar.RegisteredId} mod={TestRecipes.ModGroup.RegisteredId}");

        Recipe origin = TestRecipes.Base, clone = TestRecipes.Cloned;
        Report("Clone copies, and the clone's changes stay in it",
            clone.RecipeIndex > origin.RecipeIndex && clone.HasIngredient(ItemID.Wood) && clone.HasIngredient(ItemID.SilverCoin) &&
            !clone.HasIngredient(ItemID.CopperCoin) && origin.HasIngredient(ItemID.CopperCoin) && clone.Conditions.Count == 1 &&
            clone.HasCondition(Condition.TimeDay) && origin.Conditions.Count == 2 && clone.HasTile(TileID.Anvils) && origin.requiredTile == -1,
            $"clone={clone.RecipeIndex} origin={origin.RecipeIndex} conditions={clone.Conditions.Count}/{origin.Conditions.Count}");

        bool threw = false;
        try { Recipe.Create(ItemID.Torch); }
        catch (RecipeException) { threw = true; }
        Report("Recipe.Create outside the recipe methods throws RecipeException", threw, "");

        Report("Condition without a translation reads its name", Condition.NearWater.Description.Value == "Near Water", $"'{Condition.NearWater.Description.Value}'");

        dirtSlot = GiveDirt(p, 10);
        TestRecipes.Switch = false;
        HashSet<int> off = Available();
        TestRecipes.Switch = true;
        HashSet<int> on = Available();
        Report("the crafting menu takes the recipes the player can make", dirtSlot >= 0 && off.Contains(basic.RecipeIndex), $"slot={dirtSlot} available={off.Count}");
        Report("a condition takes a recipe out of the menu", !off.Contains(TestRecipes.Gated.RecipeIndex) && on.Contains(TestRecipes.Gated.RecipeIndex) &&
            !RecipeLoader.RecipeAvailable(TestRecipes.Disabled), $"off={off.Contains(TestRecipes.Gated.RecipeIndex)} on={on.Contains(TestRecipes.Gated.RecipeIndex)}");
        Report("DisableRecipe keeps a recipe out of the menu", !on.Contains(TestRecipes.Disabled.RecipeIndex) && TestRecipes.Disabled.Disabled, "");
        if (TestRecipes.Fillers > 0)
            Report("a recipe past Recipe.maxRecipes reaches the menu", TestRecipes.Beyond.RecipeIndex >= Recipe.maxRecipes && on.Contains(TestRecipes.Beyond.RecipeIndex),
                $"index={TestRecipes.Beyond.RecipeIndex} max={Recipe.maxRecipes} fillers={TestRecipes.Fillers}");
        else
            Log.Info($"csrecipe: no recipe past Recipe.maxRecipes (total {Recipe.numRecipes}, max {Recipe.maxRecipes})");

        Recipe keep = TestRecipes.KeepChain;
        int chainDiscount = keep.GetIngredientCraftingDiscount(p, Recipe.RequiredItemEntry.New(ItemID.Chain, 1));
        int stoneDiscount = keep.GetIngredientCraftingDiscount(p, Recipe.RequiredItemEntry.New(ItemID.StoneBlock, 10));
        Report("AddConsumeIngredientCallback decides how much is used", chainDiscount == 1 && stoneDiscount == 0, $"chain={chainDiscount} stone={stoneDiscount}");

        Thing.Created = CreatedCounter.Created = TestRecipes.Crafts = 0;
        Item result = Item.New();
        result.SetDefaults(thing, default);
        result.stack = 2;
        Main.CraftItem_GrantItem(TestRecipes.Crafted, result, false);
        Report("AddOnCraftCallback, ModItem.OnCreated and GlobalItem.OnCreated run on craft",
            TestRecipes.Crafts == 1 && Thing.Created == 1 && CreatedCounter.Created == 1,
            $"callback={TestRecipes.Crafts} modItem={Thing.Created} global={CreatedCounter.Created}");

        bool named = TestRecipes.Named.ProcessGroupsForText(ItemID.Gel, out GameString text);
        Report("a group registered with a name function shows that name", named && text.ToString() == "Named group", $"'{text.ToString()}'");
    }

    static HashSet<int> Available()
    {
        Recipe.FindRecipes(false);
        var set = new HashSet<int>();
        var available = Main.availableRecipe;
        for (int k = 0; k < Main.numAvailableRecipes; k++) set.Add(available[k]);
        return set;
    }

    static int GiveDirt(Player p, int stack)
    {
        for (int k = 0; k < 50; k++)
        {
            Item item = p.inventory[k];
            if (item.type > 0 && item.stack > 0) continue;
            item.SetDefaults(ItemID.DirtBlock, default);
            item.stack = stack;
            return k;
        }
        return -1;
    }

    static void Cleanup(Player p)
    {
        try
        {
            if (dirtSlot >= 0 && p.inventory[dirtSlot].type == ItemID.DirtBlock) p.inventory[dirtSlot].TurnToAir();
            int thing = ModContent.ItemType<Thing>();
            if (Main.mouseItem.type == thing) Main.mouseItem.TurnToAir();
            for (int k = 0; k < 50; k++)
            {
                if (p.inventory[k].type == thing) p.inventory[k].TurnToAir();
            }
        }
        catch (Exception e) { Report("cleanup", false, e.Message); }
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csrecipe {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
