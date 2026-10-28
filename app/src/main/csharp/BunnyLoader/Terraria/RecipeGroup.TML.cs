using System;
using BunnyLoader.Interop;
using Terraria.ModLoader;

namespace Terraria;

public readonly partial struct RecipeGroup
{
    public string Key => RecipeLoader.KeyOf(this);

    public RecipeGroup Add(int itemID) => Add(itemID, default);

    public static RecipeGroup Register(string key, string groupDescriptorKey, params int[] validItems)
    {
        if (RecipeLoader.GroupByKey(key) is RecipeGroup existing) return RecipeLoader.Merge(existing, validItems);
        RecipeGroup group = New(groupDescriptorKey, ItemArray(validItems)).Register();
        RecipeLoader.Named(key, group, null);
        return group;
    }

    public static RecipeGroup Register(string key, Func<string> getName, params int[] validItems)
    {
        if (RecipeLoader.GroupByKey(key) is RecipeGroup existing) return RecipeLoader.Merge(existing, validItems);
        RecipeGroup group = New(key, ItemArray(validItems)).Register();
        RecipeLoader.Named(key, group, getName);
        return group;
    }

    static GameArray<int> ItemArray(int[] validItems)
    {
        var items = GameArray<int>.New(new GameClass("System", "Int32"), validItems.Length);
        for (int i = 0; i < validItems.Length; i++) items[i] = validItems[i];
        return items;
    }
}
