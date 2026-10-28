using System.Collections.Generic;
using System;
using BunnyLoader.Interop;
using BunnyLoader;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader.Core;
using Terraria.ModLoader.Exceptions;

namespace Terraria.ModLoader;

public static class RecipeLoader
{
    internal sealed class Extra
    {
        public Mod? Mod;
        public readonly List<Condition> Conditions = new();
        public readonly List<Condition> DecraftConditions = new();
        public Recipe.OnCraftCallback? OnCraft;
        public Recipe.IngredientQuantityCallback? ConsumeIngredient;
        public bool Disabled;
        public int Index = -1;
        public GameHandle? Pin;
    }

    static readonly object Gate = new();

    static readonly Dictionary<nint, Extra> Extras = new();

    static readonly Dictionary<string, RecipeGroup> GroupsByKey = new();

    static readonly Dictionary<nint, string> GroupKeys = new();

    static readonly Dictionary<int, Func<string>> GroupNames = new();

    static readonly HashSet<nint> GroupsToSort = new();

    static bool vanillaGroups, filtering, beyond;

    static int added;

    internal static bool setupRecipes;

    internal static Mod? CurrentMod { get; private set; }

    static readonly (string Name, Func<RecipeGroup> Get)[] VanillaGroups =
    {
        ("Birds", () => RecipeGroups.Birds), ("Scorpions", () => RecipeGroups.Scorpions), ("Bugs", () => RecipeGroups.Bugs),
        ("Ducks", () => RecipeGroups.Ducks), ("Squirrels", () => RecipeGroups.Squirrels), ("Butterflies", () => RecipeGroups.Butterflies),
        ("Fireflies", () => RecipeGroups.Fireflies), ("Snails", () => RecipeGroups.Snails), ("FishForDinner", () => RecipeGroups.FishForDinner),
        ("GoldenCritter", () => RecipeGroups.GoldenCritter), ("Dragonflies", () => RecipeGroups.Dragonflies), ("Turtles", () => RecipeGroups.Turtles),
        ("Fruit", () => RecipeGroups.Fruit), ("Balloons", () => RecipeGroups.Balloons), ("Wood", () => RecipeGroups.Wood),
        ("Sand", () => RecipeGroups.Sand), ("IronBar", () => RecipeGroups.IronBar), ("Fragment", () => RecipeGroups.Fragment),
        ("PressurePlate", () => RecipeGroups.PressurePlate), ("Macaws", () => RecipeGroups.Macaws), ("Cockatiels", () => RecipeGroups.Cockatiels),
        ("CloudBalloons", () => RecipeGroups.CloudBalloons), ("BlizzardBalloons", () => RecipeGroups.BlizzardBalloons),
        ("SandstormBalloons", () => RecipeGroups.SandstormBalloons), ("CritterGuides", () => RecipeGroups.CritterGuides),
        ("NatureGuides", () => RecipeGroups.NatureGuides), ("Seashells", () => RecipeGroups.Seashells), ("Stone", () => RecipeGroups.Stone),
        ("CobaltBar", () => RecipeGroups.CobaltBar), ("MythrilBar", () => RecipeGroups.MythrilBar), ("AdamantiteBar", () => RecipeGroups.AdamantiteBar),
        ("GemCritter", () => RecipeGroups.GemCritter), ("MagicMirror", () => RecipeGroups.MagicMirror), ("Jellyfish", () => RecipeGroups.Jellyfish),
        ("SilverBar", () => RecipeGroups.SilverBar), ("GoldBar", () => RecipeGroups.GoldBar),
    };

    public static bool RecipeAvailable(Recipe recipe)
    {
        Extra? extra = Peek(recipe);
        if (extra == null) return true;
        if (extra.Disabled) return false;
        foreach (Condition condition in extra.Conditions)
        {
            bool met;
            try { met = condition.IsMet(); }
            catch (Exception e)
            {
                ItemHooks.Report($"Condition {condition.Description.Key}", e);
                met = false;
            }
            if (!met) return false;
        }
        return true;
    }

    internal static Extra DataOf(Recipe recipe)
    {
        lock (Gate)
        {
            if (!Extras.TryGetValue(recipe.Pointer, out Extra? extra)) Extras[recipe.Pointer] = extra = new Extra();
            return extra;
        }
    }

    static Extra? Peek(Recipe recipe)
    {
        if (recipe.IsNull) return null;
        lock (Gate) return Extras.TryGetValue(recipe.Pointer, out Extra? extra) ? extra : null;
    }

    internal static Mod Setup(string message)
    {
        if (!setupRecipes || CurrentMod == null) throw new RecipeException(message);
        return CurrentMod;
    }

    internal static Recipe NewRecipe(Mod mod)
    {
        Recipe recipe = Recipe.New();
        Extra extra = DataOf(recipe);
        extra.Mod = mod;
        extra.Pin = new GameHandle(recipe.Pointer);
        return recipe;
    }

    internal static void Register(Recipe recipe)
    {
        Extra extra = DataOf(recipe);
        int index = Recipe.numRecipes;
        var recipes = Main.recipe;
        if (index >= recipes.Length)
        {
            var grown = GameArray<Recipe>.New(Recipe.Class, index + 1);
            for (int i = 0; i < recipes.Length; i++) grown[i] = recipes[i];
            Main.recipe = grown;
        }
        Recipe.currentRecipe = recipe;
        Recipe.AddRecipe();
        extra.Index = index;
        extra.Pin?.Dispose();
        extra.Pin = null;
        added++;
        if (index >= Recipe.maxRecipes) WantBeyond();
    }

    internal static void BeginSetup() => setupRecipes = true;

    internal static void SetCurrentMod(Mod? mod) => CurrentMod = mod;

    internal static void EndSetup()
    {
        setupRecipes = false;
        CurrentMod = null;
        Finish();
    }

    static void Finish()
    {
        if (added == 0 && GroupsToSort.Count == 0) return;
        foreach (nint pointer in GroupsToSort) Step("recipe groups", () => new RecipeGroup(pointer).SortDecraftingEntries());
        Step("recipes", Recipe.CreateRequiredItemQuickLookups);
        Step("recipes", Recipe.UpdateMaterialFieldForAllRecipes);
        Step("recipes", Recipe.UpdateWhichItemsAreMaterials);
        Step("recipes", Recipe.UpdateWhichItemsAreCrafted);
        Step("recipes", ShimmerTransforms.UpdateRecipeSets);
        Step("recipes", ContentSamples.FixItemsAfterRecipesAreAdded);
        Log.Info($"recipes: {added} from C# mods (total {Recipe.numRecipes})");
    }

    static void Step(string what, Action step)
    {
        try { step(); }
        catch (Exception e) { Log.Error($"{what}: {e}"); }
    }

    internal static RecipeGroup? GroupByKey(string key)
    {
        EnsureVanillaGroups();
        lock (Gate) return GroupsByKey.TryGetValue(key, out RecipeGroup group) ? group : null;
    }

    internal static RecipeGroup? GroupById(int id)
    {
        EnsureVanillaGroups();
        lock (Gate)
        {
            foreach (RecipeGroup group in GroupsByKey.Values)
            {
                if (group.RegisteredId == id) return group;
            }
        }
        return null;
    }

    internal static string KeyOf(RecipeGroup group)
    {
        EnsureVanillaGroups();
        lock (Gate) return GroupKeys.TryGetValue(group.Pointer, out string? key) ? key : "";
    }

    internal static void Named(string key, RecipeGroup group, Func<string>? getName)
    {
        lock (Gate)
        {
            GroupsByKey[key] = group;
            GroupKeys[group.Pointer] = key;
            GroupsToSort.Add(group.Pointer);
            if (getName != null) GroupNames[group.RegisteredId] = getName;
        }
        if (getName != null) ItemHooks.Once("RecipeGroupNames", () => On_Recipe.ProcessGroupsForText += ProcessGroupsForText);
    }

    internal static RecipeGroup Merge(RecipeGroup group, int[] validItems)
    {
        foreach (int item in validItems)
        {
            if (!group.Contains(item)) group.Add(item);
        }
        lock (Gate) GroupsToSort.Add(group.Pointer);
        return group;
    }

    static void EnsureVanillaGroups()
    {
        lock (Gate)
        {
            if (vanillaGroups) return;
            vanillaGroups = true;
            foreach ((string name, Func<RecipeGroup> get) in VanillaGroups)
            {
                try
                {
                    RecipeGroup group = get();
                    if (group.IsNull || group.RegisteredId < 0) continue;
                    GroupsByKey.TryAdd(name, group);
                    GroupKeys.TryAdd(group.Pointer, name);
                }
                catch (Exception e) { Log.Warn($"recipe groups: RecipeGroups.{name} could not be read: {e.Message}"); }
            }
        }
    }

    static bool ProcessGroupsForText(On_Recipe.orig_ProcessGroupsForText orig, Recipe self, int type, out GameString theText)
    {
        bool found = orig(self, type, out theText);
        if (!found) return false;
        try
        {
            var groups = self.acceptedGroups;
            for (int i = 0; i < groups.Length; i++)
            {
                Func<string>? getName;
                lock (Gate) GroupNames.TryGetValue(groups[i], out getName);
                if (getName == null || GroupById(groups[i]) is not RecipeGroup group || !group.Contains(type)) continue;
                theText = GameString.From(getName());
                break;
            }
        }
        catch (Exception e) { ItemHooks.Report("RecipeGroup names", e); }
        return true;
    }

    internal static void WantFilter()
    {
        filtering = true;
        ItemHooks.Once("RecipeFind", InstallFind);
    }

    static void WantBeyond()
    {
        beyond = true;
        ItemHooks.Once("RecipeFind", InstallFind);
        ItemHooks.Once("RecipeVariants", () => On_Recipe.UpdateItemVariants += UpdateItemVariants);
    }

    internal static void WantConsume() => ItemHooks.Once("RecipeConsume", () => On_Recipe.GetIngredientCraftingDiscount += GetIngredientCraftingDiscount);

    internal static void WantCraft() => ItemHooks.Once("RecipeCraft", () => On_Main.CraftItem_GrantItem += CraftItem_GrantItem);

    static void InstallFind()
    {
        On_Recipe.FindRecipes += FindRecipes;
        On_GUICraftGuidePopup.FindRecipes += FindGuideRecipes;
    }

    static void FindRecipes(On_Recipe.orig_FindRecipes orig, bool canDelayCheck)
    {
        bool beyondNow = !canDelayCheck && beyond && Recipe.numRecipes > Recipe.maxRecipes;
        if (!beyondNow && !filtering)
        {
            orig(canDelayCheck);
            return;
        }
        int oldRecipe = 0;
        try
        {
            if (beyondNow) GrowAvailable(Recipe.numRecipes);
            var available = Main.availableRecipe;
            int focus = Main.focusRecipe;
            if (!available.IsNull && focus >= 0 && focus < available.Length) oldRecipe = available[focus];
        }
        catch (Exception e) { ItemHooks.Report("recipes", e); }
        orig(canDelayCheck);
        if (beyondNow)
        {
            try { FindBeyondMax(oldRecipe); }
            catch (Exception e) { ItemHooks.Report("recipes beyond the limit", e); }
        }
        if (filtering)
        {
            try { Filter(oldRecipe); }
            catch (Exception e) { ItemHooks.Report("recipe conditions", e); }
        }
    }

    static void GrowAvailable(int total)
    {
        var available = Main.availableRecipe;
        if (!available.IsNull && available.Length < total)
        {
            var grown = GameArray<int>.New(new GameClass("System", "Int32"), total);
            for (int i = 0; i < available.Length; i++) grown[i] = available[i];
            Main.availableRecipe = grown;
        }
        var positions = Main.availableRecipeY;
        if (!positions.IsNull && positions.Length < total)
        {
            var grown = GameArray<float>.New(new GameClass("System", "Single"), total);
            for (int i = 0; i < positions.Length; i++) grown[i] = positions[i];
            Main.availableRecipeY = grown;
        }
    }

    static void FindBeyondMax(int oldRecipe)
    {
        var recipes = Main.recipe;
        int total = Math.Min(Recipe.numRecipes, recipes.Length);
        var available = Main.availableRecipe;
        var present = new HashSet<int>();
        for (int k = 0; k < Main.numAvailableRecipes; k++) present.Add(available[k]);
        Player player = Main.LocalPlayer;
        Recipe.CollectItemsToCraftWithFrom(player);
        for (int i = Recipe.maxRecipes; i < total; i++)
        {
            Recipe recipe = recipes[i];
            if (recipe.IsNull || recipe.createItem.type == 0) break;
            if (present.Contains(i)) continue;
            if (Recipe.PlayerMeetsTileRequirements(player, recipe) && Recipe.PlayerMeetsEnvironmentConditions(player, recipe) &&
                Recipe.CollectedEnoughItemsToCraft(recipe))
                Recipe.AddToAvailableRecipes(i);
        }
        Recipe.TryRefocusingRecipe(oldRecipe);
    }

    static void Filter(int oldRecipe)
    {
        var available = Main.availableRecipe;
        var recipes = Main.recipe;
        int count = Main.numAvailableRecipes;
        int kept = 0;
        for (int k = 0; k < count; k++)
        {
            int index = available[k];
            if (index >= 0 && index < recipes.Length && !RecipeAvailable(recipes[index])) continue;
            if (kept != k) available[kept] = index;
            kept++;
        }
        if (kept == count) return;
        Main.numAvailableRecipes = kept;
        Recipe.TryRefocusingRecipe(oldRecipe);
    }

    static void FindGuideRecipes(On_GUICraftGuidePopup.orig_FindRecipes orig, GUICraftGuidePopup self)
    {
        orig(self);
        try
        {
            if (beyond && Recipe.numRecipes > Recipe.maxRecipes) FindGuideBeyondMax(self);
            if (filtering) FilterGuide(self);
        }
        catch (Exception e) { ItemHooks.Report("Guide recipes", e); }
    }

    static void FindGuideBeyondMax(GUICraftGuidePopup popup)
    {
        Item item = Main.guideItem;
        if (item.IsNull || item.type < 1 || item.stack < 1) return;
        var recipes = Main.recipe;
        int total = Math.Min(Recipe.numRecipes, recipes.Length);
        var found = popup.availableGuideRecipe;
        if (found.Length < total)
        {
            var grown = GameArray<int>.New(new GameClass("System", "Int32"), total);
            for (int i = 0; i < found.Length; i++) grown[i] = found[i];
            popup.availableGuideRecipe = grown;
            found = grown;
        }
        var present = new HashSet<int>();
        for (int k = 0; k < popup.numAvailableGuideRecipes; k++) present.Add(found[k]);
        int type = item.type;
        for (int i = Recipe.maxRecipes; i < total; i++)
        {
            Recipe recipe = recipes[i];
            if (recipe.IsNull || recipe.createItem.type == 0) break;
            if (!present.Contains(i) && UsesItem(recipe, type)) found[popup.numAvailableGuideRecipes++] = i;
        }
    }

    static void FilterGuide(GUICraftGuidePopup popup)
    {
        var found = popup.availableGuideRecipe;
        var recipes = Main.recipe;
        int count = popup.numAvailableGuideRecipes;
        int kept = 0;
        for (int k = 0; k < count; k++)
        {
            int index = found[k];
            if (index >= 0 && index < recipes.Length && Peek(recipes[index]) is { Disabled: true }) continue;
            if (kept != k) found[kept] = index;
            kept++;
        }
        popup.numAvailableGuideRecipes = kept;
    }

    static bool UsesItem(Recipe recipe, int type)
    {
        var lookup = recipe.requiredItemQuickLookup;
        for (int j = 0; j < lookup.Length; j++)
        {
            Recipe.RequiredItemEntry entry = lookup[j];
            if (entry.itemIdOrRecipeGroup == 0) return false;
            if (entry.Matches(type)) return true;
        }
        return false;
    }

    static void UpdateItemVariants(On_Recipe.orig_UpdateItemVariants orig)
    {
        orig();
        if (Recipe.numRecipes <= Recipe.maxRecipes) return;
        try
        {
            var recipes = Main.recipe;
            int total = Math.Min(Recipe.numRecipes, recipes.Length);
            for (int i = Recipe.maxRecipes; i < total; i++)
            {
                Recipe recipe = recipes[i];
                if (recipe.IsNull) break;
                recipe.createItem.Refresh(true);
                var ingredients = recipe.requiredItem;
                for (int j = 0; j < ingredients.Length && ingredients[j].type != 0; j++) ingredients[j].Refresh(true);
            }
        }
        catch (Exception e) { ItemHooks.Report("item variants beyond the recipe limit", e); }
    }

    static int GetIngredientCraftingDiscount(On_Recipe.orig_GetIngredientCraftingDiscount orig, Recipe self, Player player, Recipe.RequiredItemEntry req)
    {
        int discount = orig(self, player, req);
        try
        {
            if (Peek(self)?.ConsumeIngredient is not Recipe.IngredientQuantityCallback callbacks) return discount;
            int stack = req.stack;
            int type = req.IsRecipeGroup ? req.RecipeGroup.GetPlaceholderItemType() : req.itemIdOrRecipeGroup;
            int amount = stack - discount;
            foreach (Recipe.IngredientQuantityCallback callback in callbacks.GetInvocationList())
            {
                try { callback(self, type, ref amount, false); }
                catch (Exception e) { ItemHooks.Report("Recipe ConsumeIngredient callback", e); }
            }
            int consumed = Math.Max(0, Math.Min(amount, Math.Max(stack, Recipe.GetAvailableItemCount(req))));
            return stack - consumed;
        }
        catch (Exception e)
        {
            ItemHooks.Report("Recipe ConsumeIngredient", e);
            return discount;
        }
    }

    static void CraftItem_GrantItem(On_Main.orig_CraftItem_GrantItem orig, Recipe recipe, Item result, bool quickCraft)
    {
        try
        {
            var consumed = new List<Item>();
            Item destination = default;
            if (Peek(recipe)?.OnCraft is Recipe.OnCraftCallback callbacks)
            {
                foreach (Recipe.OnCraftCallback callback in callbacks.GetInvocationList())
                {
                    try { callback(recipe, result, consumed, destination); }
                    catch (Exception e) { ItemHooks.Report("Recipe OnCraft callback", e); }
                }
            }
            ItemLoader.OnCreated(result, new RecipeItemCreationContext(recipe, consumed, destination));
        }
        catch (Exception e) { ItemHooks.Report("Recipe OnCraft", e); }
        orig(recipe, result, quickCraft);
    }
}
