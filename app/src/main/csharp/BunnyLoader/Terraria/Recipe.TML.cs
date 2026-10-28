using System;
using System.Collections.Generic;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Exceptions;

namespace Terraria;

public readonly partial struct Recipe
{
    public static class IngredientQuantityRules
    {
        public static IngredientQuantityCallback Alchemy = (Recipe recipe, int type, ref int amount, bool isDecrafting) =>
        {
            if (!Main.LocalPlayer.alchemyTable && !isDecrafting) return;
            for (int i = amount; i > 0; i--)
            {
                if (Main.rand.Next(3) == 0) amount--;
            }
        };
    }

    public delegate void OnCraftCallback(Recipe recipe, Item item, List<Item> consumedItems, Item destinationStack);

    [Obsolete("Replaced by IngredientQuantityCallback due to not accounting for shimmer decrafting")]
    public delegate void ConsumeItemCallback(Recipe recipe, int type, ref int amount);

    public delegate void IngredientQuantityCallback(Recipe recipe, int type, ref int amount, bool isDecrafting);

    RecipeLoader.Extra Data => RecipeLoader.DataOf(this);

    public Mod? Mod => Data.Mod;

    public List<Condition> Conditions => Data.Conditions;

    public List<Condition> DecraftConditions => Data.DecraftConditions;

    public int RecipeIndex => Data.Index;

    public bool Disabled => Data.Disabled;

    public bool DecraftDisabled => notDecraftable;

    public static Recipe Create(int result, int amount = 1)
    {
        Mod mod = RecipeLoader.Setup("A Recipe can only be created inside recipe related methods");
        Recipe recipe = RecipeLoader.NewRecipe(mod);
        recipe.createItem.SetDefaults(result, default);
        recipe.createItem.stack = amount;
        return recipe;
    }

    public Recipe AddIngredient(int itemID, int stack = 1)
    {
        var items = requiredItem;
        int slot = 0;
        while (slot < items.Length && items[slot].type > 0) slot++;
        if (slot >= items.Length) throw new RecipeException($"A recipe takes at most {items.Length} ingredients; item {itemID} does not fit.");
        Item item = items[slot];
        item.SetDefaults(itemID, default);
        item.stack = stack;
        return this;
    }

    public Recipe AddIngredient(Mod mod, string itemName, int stack = 1)
    {
        mod ??= Mod!;
        if (!ModContent.TryFind(mod.Name, itemName, out ModItem item))
            throw new RecipeException($"The item {itemName} does not exist in the mod {mod.Name}.\r\nIf you are trying to use a vanilla item, try removing the first argument.");
        return AddIngredient(item, stack);
    }

    public Recipe AddIngredient(ModItem item, int stack = 1) => AddIngredient(item.Type, stack);

    public Recipe AddIngredient<T>(int stack = 1) where T : ModItem => AddIngredient(ModContent.ItemType<T>(), stack);

    public Recipe AddRecipeGroup(string name, int stack = 1)
    {
        if (RecipeLoader.GroupByKey(name) is not RecipeGroup group) throw new RecipeException($"A recipe group with the name {name} does not exist.");
        return AddRecipeGroup(group, stack);
    }

    public Recipe AddRecipeGroup(int recipeGroupId, int stack = 1)
    {
        if (RecipeLoader.GroupById(recipeGroupId) is not RecipeGroup group) throw new RecipeException($"A recipe group with the ID {recipeGroupId} does not exist.");
        return AddRecipeGroup(group, stack);
    }

    public Recipe AddRecipeGroup(RecipeGroup recipeGroup, int stack = 1)
    {
        AddIngredient(recipeGroup.GetPlaceholderItemType(), stack);
        RequireGroup(recipeGroup);
        return this;
    }

    public Recipe AddTile(int tileID)
    {
        if (tileID < 0) throw new RecipeException($"No tile has ID '{tileID}'.");
        requiredTile = tileID;
        return this;
    }

    public Recipe AddCondition(LocalizedText description, Func<bool> condition) => AddCondition(new Condition(description, condition));

    public Recipe AddCondition(params Condition[] conditions) => AddCondition((IEnumerable<Condition>)conditions);

    public Recipe AddCondition(Condition condition)
    {
        Conditions.Add(condition);
        RecipeLoader.WantFilter();
        return this;
    }

    public Recipe AddCondition(IEnumerable<Condition> conditions)
    {
        Conditions.AddRange(conditions);
        RecipeLoader.WantFilter();
        return this;
    }

    public Recipe AddDecraftCondition(LocalizedText description, Func<bool> condition) => AddDecraftCondition(new Condition(description, condition));

    public Recipe AddDecraftCondition(params Condition[] conditions) => AddDecraftCondition((IEnumerable<Condition>)conditions);

    public Recipe AddDecraftCondition(Condition condition)
    {
        DecraftConditions.Add(condition);
        return this;
    }

    public Recipe AddDecraftCondition(IEnumerable<Condition> conditions)
    {
        DecraftConditions.AddRange(conditions);
        return this;
    }

    public Recipe ApplyConditionsAsDecraftConditions()
    {
        foreach (Condition condition in Conditions)
        {
            if (!DecraftConditions.Contains(condition)) DecraftConditions.Add(condition);
        }
        return this;
    }

    public Recipe DisableDecraft()
    {
        RecipeLoader.Setup("A Recipe can only be disabled inside Recipe related methods.");
        notDecraftable = true;
        return this;
    }

    public Recipe AddOnCraftCallback(OnCraftCallback callback)
    {
        Data.OnCraft += callback;
        RecipeLoader.WantCraft();
        return this;
    }

    [Obsolete("Replaced by AddConsumeIngredientCallback due to not accounting for shimmer decrafting")]
    public Recipe AddConsumeItemCallback(ConsumeItemCallback callback) =>
        AddConsumeIngredientCallback((Recipe recipe, int type, ref int amount, bool isDecrafting) => callback(recipe, type, ref amount));

    public Recipe AddConsumeIngredientCallback(IngredientQuantityCallback callback)
    {
        Data.ConsumeIngredient += callback;
        RecipeLoader.WantConsume();
        return this;
    }

    public Recipe Clone()
    {
        Mod mod = RecipeLoader.Setup("A Recipe can only be cloned inside recipe related methods");
        Recipe clone = RecipeLoader.NewRecipe(mod);
        clone.createItem.SetDefaults(createItem.type, default);
        clone.createItem.stack = createItem.stack;
        var from = requiredItem;
        for (int i = 0; i < from.Length && from[i].type > 0; i++) clone.AddIngredient(from[i].type, from[i].stack);
        var groups = acceptedGroups;
        var cloneGroups = clone.acceptedGroups;
        for (int i = 0; i < groups.Length && i < cloneGroups.Length; i++) cloneGroups[i] = groups[i];
        clone.requiredTile = requiredTile;
        clone.notDecraftable = notDecraftable;
        clone.crimson = crimson;
        clone.corruption = corruption;
        clone.needHoney = needHoney;
        clone.needWater = needWater;
        clone.needLava = needLava;
        clone.needSnowBiome = needSnowBiome;
        clone.needTorchGodsFavor = needTorchGodsFavor;
        clone.needGraveyardBiome = needGraveyardBiome;
        clone.needMechdusa = needMechdusa;
        RecipeLoader.Extra data = Data, cloneData = clone.Data;
        cloneData.OnCraft = data.OnCraft;
        cloneData.ConsumeIngredient = data.ConsumeIngredient;
        foreach (Condition condition in data.Conditions) clone.AddCondition(condition);
        foreach (Condition condition in data.DecraftConditions) clone.AddDecraftCondition(condition);
        return clone;
    }

    public Recipe Register()
    {
        if (createItem.IsNull || createItem.type == 0) throw new RecipeException("A recipe without any result has been added.");
        if (RecipeIndex >= 0) throw new RecipeException("There was an attempt to register an already registered recipe.");
        RecipeLoader.Register(this);
        return this;
    }

    public bool HasResult(int itemID) => createItem.type == itemID;

    public bool HasResult(ModItem item) => HasResult(item.Type);

    public bool HasResult<T>() where T : ModItem => HasResult(ModContent.ItemType<T>());

    public bool HasIngredient(int itemID) => IngredientSlot(itemID) >= 0;

    public bool HasIngredient(ModItem item) => HasIngredient(item.Type);

    public bool HasIngredient<T>() where T : ModItem => HasIngredient(ModContent.ItemType<T>());

    public bool HasRecipeGroup(int id)
    {
        var groups = acceptedGroups;
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] == id) return true;
        }
        return false;
    }

    public bool HasRecipeGroup(string name) => RecipeLoader.GroupByKey(name) is RecipeGroup group && HasRecipeGroup(group.RegisteredId);

    public bool HasRecipeGroup(RecipeGroup group) => HasRecipeGroup(group.RegisteredId);

    public bool HasTile(int tileID) => requiredTile == tileID;

    public bool HasCondition(Condition condition) => Conditions.Contains(condition);

    public bool HasShimmerCondition(Condition condition) => DecraftConditions.Contains(condition);

    public bool TryGetResult(int itemID, out Item result)
    {
        result = createItem;
        return createItem.type == itemID;
    }

    public bool TryGetResult(ModItem item, out Item result) => TryGetResult(item.Type, out result);

    public bool TryGetResult<T>(out Item result) where T : ModItem => TryGetResult(ModContent.ItemType<T>(), out result);

    public bool TryGetIngredient(int itemID, out Item ingredient)
    {
        int slot = IngredientSlot(itemID);
        ingredient = slot >= 0 ? requiredItem[slot] : default;
        return slot >= 0;
    }

    public bool TryGetIngredient(ModItem item, out Item ingredient) => TryGetIngredient(item.Type, out ingredient);

    public bool TryGetIngredient<T>(out Item ingredient) where T : ModItem => TryGetIngredient(ModContent.ItemType<T>(), out ingredient);

    public bool RemoveIngredient(Item item)
    {
        var items = requiredItem;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != item || item.type <= 0) continue;
            RemoveIngredientAt(i);
            return true;
        }
        return false;
    }

    public bool RemoveIngredient(int itemID)
    {
        bool removed = false;
        int slot;
        while ((slot = IngredientSlot(itemID)) >= 0)
        {
            RemoveIngredientAt(slot);
            removed = true;
        }
        return removed;
    }

    public bool RemoveTile(int tileID)
    {
        if (requiredTile != tileID) return false;
        requiredTile = -1;
        return true;
    }

    public bool RemoveRecipeGroup(int groupID)
    {
        var groups = acceptedGroups;
        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] != groupID) continue;
            for (int j = i; j < groups.Length - 1; j++) groups[j] = groups[j + 1];
            groups[groups.Length - 1] = -1;
            return true;
        }
        return false;
    }

    public bool RemoveCondition(Condition condition) => Conditions.Remove(condition);

    public bool RemoveShimmerCondition(Condition condition) => DecraftConditions.Remove(condition);

    public void DisableRecipe()
    {
        RecipeLoader.Setup("A Recipe can only be disabled inside recipe related methods");
        Data.Disabled = true;
        RecipeLoader.WantFilter();
    }

    public void ReplaceResult(int itemID, int stack = 1)
    {
        createItem.SetDefaults(itemID, default);
        createItem.stack = stack;
    }

    public void ReplaceResult(ModItem item, int stack = 1) => ReplaceResult(item.Type, stack);

    public void ReplaceResult<T>(int stack = 1) where T : ModItem => ReplaceResult(ModContent.ItemType<T>(), stack);

    int IngredientSlot(int itemID)
    {
        var items = requiredItem;
        for (int i = 0; i < items.Length; i++)
        {
            int type = items[i].type;
            if (type <= 0) return -1;
            if (type == itemID) return i;
        }
        return -1;
    }

    void RemoveIngredientAt(int slot)
    {
        var items = requiredItem;
        Item removed = items[slot];
        for (int i = slot; i < items.Length - 1; i++) items[i] = items[i + 1];
        removed.SetDefaults(0, default);
        removed.stack = 0;
        items[items.Length - 1] = removed;
    }
}
