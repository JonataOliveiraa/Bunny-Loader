using Terraria.DataStructures;
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
        public readonly List<Mod> Editors = new();
        public readonly List<Condition> Conditions = new();
        public readonly List<Condition> DecraftConditions = new();
        public Recipe.OnCraftCallback? OnCraft;
        public Recipe.IngredientQuantityCallback? ConsumeIngredient;
        public bool Disabled;
        public int Index = -1;
        public GameHandle? Pin;
        public Recipe OrderTarget;
        public bool OrderAfter;
    }

    static readonly object Gate = new();

    static readonly Dictionary<nint, Extra> Extras = new();

    static readonly Dictionary<string, RecipeGroup> GroupsByKey = new();

    static readonly Dictionary<nint, string> GroupKeys = new();

    static readonly Dictionary<int, Func<string>> GroupNames = new();

    static readonly HashSet<nint> GroupsToSort = new();

    static bool vanillaGroups, filtering, beyond, ordering, edited;

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
        CheckBeforeRegister(recipe);
        Extra extra = DataOf(recipe);
        int index = Recipe.numRecipes;
        var recipes = Main.recipe;
        bool grew = index >= recipes.Length;
        if (grew)
        {
            var grown = GameArray<Recipe>.New(Recipe.Class, index + 1);
            for (int i = 0; i < recipes.Length; i++) grown[i] = recipes[i];
            Main.recipe = grown;
        }
        Recipe.currentRecipe = recipe;
        try { Recipe.AddRecipe(); }
        catch
        {
            if (grew) Main.recipe = recipes;
            throw;
        }
        extra.Index = index;
        extra.Pin?.Dispose();
        extra.Pin = null;
        added++;
        if (index >= Recipe.maxRecipes) WantBeyond();
    }

    internal static void Edited(Recipe recipe)
    {
        if (!setupRecipes) return;
        edited = true;
        Mod? mod = CurrentMod;
        if (mod == null) return;
        Extra extra = DataOf(recipe);
        if (extra.Mod != mod && !extra.Editors.Contains(mod)) extra.Editors.Add(mod);
    }

    static void CheckBeforeRegister(Recipe recipe)
    {
        var items = recipe.requiredItem;
        if (items.IsNull) throw new RecipeException("A recipe without an ingredient array has been added.");
        int end = -1;
        for (int j = 0; j < items.Length; j++)
        {
            if (items[j].IsNull) throw new RecipeException($"A recipe with nothing in ingredient slot {j} has been added; set ingredients with Recipe.AddIngredient.");
            if (end < 0 && items[j].type <= 0) end = j;
        }
        if (end < 0) throw new RecipeException($"A recipe with {items.Length} ingredients has been added; the game needs the last slot empty, so it takes at most {items.Length - 1}.");
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
        if (added == 0 && GroupsToSort.Count == 0 && !ordering && !edited) return;
        if (ordering) Step("recipe order", OrderRecipes);
        foreach (nint pointer in GroupsToSort) Step("recipe groups", () => new RecipeGroup(pointer).SortDecraftingEntries());
        Step("recipe ingredients", CheckIngredients);
        Step("recipe quick lookups", Recipe.CreateRequiredItemQuickLookups);
        Step("recipe materials", UpdateWhichItemsAreMaterials);
        Step("recipe material fields", Recipe.UpdateMaterialFieldForAllRecipes);
        Step("crafted items", Recipe.UpdateWhichItemsAreCrafted);
        Step("shimmer recipes", ShimmerTransforms.UpdateRecipeSets);
        Step("item samples after recipes", ContentSamples.FixItemsAfterRecipesAreAdded);
        Log.Info($"recipes: {added} from C# mods (total {Recipe.numRecipes})");
    }

    static void CheckIngredients()
    {
        Item empty = Recipe.defaultRecipeItem;
        if (!empty.IsNull && empty.type != 0)
        {
            Log.Error($"recipes: a mod turned the empty ingredient slot that all the game's recipes share into {ItemName(empty.type)}, so every recipe gained that ingredient. It was emptied again; mods should add ingredients with Recipe.AddIngredient.");
            empty.TurnToAir();
        }
        var recipes = Main.recipe;
        for (int i = 0, count = Math.Min(Recipe.numRecipes, recipes.Length); i < count; i++)
        {
            Recipe recipe = recipes[i];
            if (recipe.IsNull) throw new InvalidOperationException($"Main.recipe[{i}] is null below Recipe.numRecipes ({Recipe.numRecipes})");
            if (recipe.createItem.IsNull)
            {
                recipe.createItem = Item.New();
                Disable(recipe, i, "has no result item");
            }
            var items = recipe.requiredItem;
            if (items.IsNull) throw new InvalidOperationException($"{Describe(recipe, i)} has no ingredient array");
            int end = -1;
            for (int j = 0; j < items.Length; j++)
            {
                if (items[j].IsNull)
                {
                    items[j] = Item.New();
                    Disable(recipe, i, $"had nothing in ingredient slot {j}");
                }
                if (end < 0 && items[j].type <= 0) end = j;
            }
            if (end >= 0) continue;
            items[items.Length - 1] = Item.New();
            Disable(recipe, i, $"had {items.Length} ingredients and no empty slot after the last one, which makes the game read past the end");
        }
    }

    static void Disable(Recipe recipe, int index, string problem)
    {
        Log.Error($"recipes: {Describe(recipe, index)} {problem}. The recipe was turned off.");
        DataOf(recipe).Disabled = true;
        WantFilter();
    }

    static string Describe(Recipe recipe, int index)
    {
        Extra? extra = Peek(recipe);
        string result = recipe.createItem.IsNull ? "nothing" : ItemName(recipe.createItem.type);
        string origin = extra?.Mod is Mod mod ? $"added by {mod.Name}" : "from the game";
        if (extra != null && extra.Editors.Count > 0) origin += $", changed by {string.Join(", ", extra.Editors.ConvertAll(m => m.Name))}";
        return $"recipe #{index} for {result} ({origin})";
    }

    static string ItemName(int type)
    {
        try { return ItemLoader.GetItem(type)?.FullName ?? ItemID.Search.GetName(type) ?? $"item {type}"; }
        catch (Exception) { return $"item {type}"; }
    }

    internal static void WantOrder() => ordering = true;

    internal static int IndexOf(Recipe recipe)
    {
        if (recipe.IsNull) return -1;
        Extra? extra = Peek(recipe);
        if (extra != null && extra.Index >= 0) return extra.Index;
        var recipes = Main.recipe;
        for (int i = 0, count = Recipe.numRecipes; i < count && i < recipes.Length; i++)
        {
            if (recipes[i] == recipe) return i;
        }
        return -1;
    }

    internal static Recipe FirstRecipeFor(int itemId)
    {
        var recipes = Main.recipe;
        for (int i = 0, count = Recipe.numRecipes; i < count && i < recipes.Length; i++)
        {
            Recipe recipe = recipes[i];
            if (!recipe.IsNull && recipe.createItem.type == itemId) return recipe;
        }
        return default;
    }

    internal static Recipe OrderTargetOf(Recipe recipe) => Peek(recipe)?.OrderTarget ?? default;

    static void OrderRecipes()
    {
        var recipes = Main.recipe;
        int count = Math.Min(Recipe.numRecipes, recipes.Length);
        var sortBefore = new Dictionary<nint, List<Recipe>>();
        var sortAfter = new Dictionary<nint, List<Recipe>>();
        var baseOrder = new List<Recipe>(count);
        for (int k = 0; k < count; k++)
        {
            Recipe r = recipes[k];
            Extra? extra = Peek(r);
            if (extra == null || extra.OrderTarget.IsNull)
            {
                baseOrder.Add(r);
                continue;
            }
            var lists = extra.OrderAfter ? sortAfter : sortBefore;
            if (!lists.TryGetValue(extra.OrderTarget.Pointer, out List<Recipe>? list)) lists[extra.OrderTarget.Pointer] = list = new List<Recipe>();
            list.Add(r);
        }
        if (sortBefore.Count == 0 && sortAfter.Count == 0) return;
        var sorted = new List<Recipe>(count);
        void Sort(Recipe r)
        {
            if (sortBefore.TryGetValue(r.Pointer, out List<Recipe>? before))
                foreach (Recipe c in before) Sort(c);
            sorted.Add(r);
            if (sortAfter.TryGetValue(r.Pointer, out List<Recipe>? after))
                foreach (Recipe c in after) Sort(c);
        }
        foreach (Recipe r in baseOrder) Sort(r);
        if (sorted.Count != count) throw new InvalidOperationException($"recipe ordering lost recipes ({sorted.Count} of {count}): a recipe is sorted next to one that is not registered");
        for (int k = 0; k < count; k++)
        {
            recipes[k] = sorted[k];
            Extra? extra = Peek(sorted[k]);
            if (extra != null && extra.Index >= 0) extra.Index = k;
        }
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
        Item item = GUIInstance.Active.GUICraftGuide.guideItem;
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

    static void UpdateWhichItemsAreMaterials()
    {
        var materials = ItemID.Sets.IsAMaterial;
        var recipes = Main.recipe;
        var groups = new Dictionary<int, RecipeGroup>();
        for (int i = 0, count = Math.Min(Recipe.numRecipes, recipes.Length); i < count; i++)
        {
            var lookup = recipes[i].requiredItemQuickLookup;
            for (int j = 0; j < lookup.Length; j++)
            {
                Recipe.RequiredItemEntry entry = lookup[j];
                if (entry.itemIdOrRecipeGroup == 0) break;
                if (entry.IsRecipeGroup)
                {
                    RecipeGroup group = entry.RecipeGroup;
                    if (!group.IsNull) groups[group.RegisteredId] = group;
                }
                else if ((uint)entry.itemIdOrRecipeGroup < (uint)materials.Length) materials[entry.itemIdOrRecipeGroup] = true;
            }
        }
        foreach (RecipeGroup group in groups.Values)
        {
            var items = new GameList(group.Items);
            for (int k = 0; k < items.Count; k++)
            {
                int type = items.Get<int>(k);
                if ((uint)type < (uint)materials.Length) materials[type] = true;
            }
        }
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
            if (ItemHooks.ModItemOf(result) is ModItem modItem)
            {
                try { modItem.OnCraft(recipe); }
                catch (Exception e) { ItemHooks.Report($"{modItem.FullName}.OnCraft", e); }
            }
            ItemLoader.OnCreated(result, new RecipeItemCreationContext(recipe, consumed, destination));
        }
        catch (Exception e) { ItemHooks.Report("Recipe OnCraft", e); }
        orig(recipe, result, quickCraft);
    }
}
