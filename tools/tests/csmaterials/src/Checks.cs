using System;
using System.Collections.Generic;
using log4net;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CsMaterials;

public static class Checks
{
    static int pass, fail, frames;
    static bool done;

    static ILog Log => ModContent.GetInstance<CsMaterialsMod>().Logger;

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
        Main.mouseItem = new Item(0);
        Log.Info($"csmaterials END pass={pass} fail={fail}");
    }

    static int CountOf(Player p, int type)
    {
        int count = Main.mouseItem.type == type ? Main.mouseItem.stack : 0;
        for (int i = 0; i < 58; i++)
        {
            Item item = p.inventory[i];
            if (item.type == type) count += item.stack;
        }
        return count;
    }

    static bool Available(Recipe recipe)
    {
        Recipe.FindRecipes(false);
        var available = Main.availableRecipe;
        for (int k = 0; k < Main.numAvailableRecipes; k++)
        {
            if (available[k] == recipe.RecipeIndex) return true;
        }
        return false;
    }

    static void Run(Player p)
    {
        Recipe recipe = MaterialRecipes.TorchFromRuby;
        int rubies = CountOf(p, ItemID.Ruby);
        bool listed = Available(recipe);
        Report("the extra materials make a recipe available", rubies == 0 && listed && MaterialsPlayer.Calls >= 1,
            $"rubies={rubies} listed={listed} calls={MaterialsPlayer.Calls}");

        Main.mouseItem = new Item(0);
        int torches = CountOf(p, ItemID.Torch);
        CraftingRequests.CraftItem(recipe, 1, false);
        Item ruby = MaterialsPlayer.Bag[1];
        Report("crafting takes the material from the mod's item", ruby.stack == 3 && CountOf(p, ItemID.Torch) == torches + 3,
            $"ruby={ruby.type}x{ruby.stack} torches={torches}->{CountOf(p, ItemID.Torch)}");
        Report("the callback gets the item and its index", MaterialsPlayer.Consumed == 1 && MaterialsPlayer.LastIndex == 1 && MaterialsPlayer.LastStack == 3,
            $"calls={MaterialsPlayer.Consumed} index={MaterialsPlayer.LastIndex} stack={MaterialsPlayer.LastStack}");

        CraftingRequests.CraftItem(recipe, 1, false);
        bool emptied = ruby.stack == 1;
        bool stillListed = Available(recipe);
        Report("a used-up material leaves the recipe out", emptied && !stillListed && MaterialsPlayer.Bag[0].stack == 1,
            $"ruby={ruby.stack} listed={stillListed} gel={MaterialsPlayer.Bag[0].stack}");
    }

    static void Report(string name, bool ok, string detail)
    {
        if (ok) pass++;
        else fail++;
        Log.Info($"csmaterials {name}: {(ok ? "ok" : "FAIL")} {detail}");
    }
}
