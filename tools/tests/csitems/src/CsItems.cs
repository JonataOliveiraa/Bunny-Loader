using System;
using BunnyLoader;
using CsItems.Content.Items;
using Terraria;
using Terraria.ID;

namespace CsItems;

// Teste do loader de ModItem do C# (BunnyLoader/Loaders/ItemLoader.cs).
// Load: os tipos ja sairam. Checks.Run: o jogo ja conhece os itens (chamado
// do SetStaticDefaults do ultimo item registrado, na thread do jogo).
public sealed class CsItemsMod : Mod
{
    public override void Load()
    {
        int example = ModContent.ItemType<ExampleItem>(), sword = ModContent.ItemType<CsSword>();
        Checks.Report("load types", example >= ItemID.Count && sword >= ItemID.Count && example != sword,
            $"example={example} sword={sword}");
        Checks.Report("load lookup", ModContent.GetModItem(example) is ExampleItem, "");
    }
}

public static class Checks
{
    public static void Run()
    {
        int example = ModContent.ItemType<ExampleItem>();
        Item item = Item.New();
        item.SetDefaults(example, default);
        Checks.Report("example defaults",
            item.type == example && item.width == 20 && item.height == 20 && item.maxStack == Item.CommonMaxStack &&
            item.value == 100,
            $"type={item.type} size={item.width}x{item.height} maxStack={item.maxStack} value={item.value}");
        Checks.Report("example name", Lang.GetItemNameValue(example) == "Example Item", $"name={Lang.GetItemNameValue(example)}");

        int sword = ModContent.ItemType<CsSword>();
        Item blade = Item.New();
        blade.SetDefaults(sword, default);
        Checks.Report("sword defaults", blade.type == sword && blade.damage == 50 && blade.useTime == 20 && blade.melee,
            $"type={blade.type} damage={blade.damage} useTime={blade.useTime} melee={blade.melee}");
        Checks.Report("sword name", Lang.GetItemNameValue(sword) == "Espada C#", $"name={Lang.GetItemNameValue(sword)}");

        string? gem = Lang.GetItemNameValue(ModContent.ItemType<CsGem>());
        Checks.Report("localized name", gem == "Gema C#" || gem == "C# Gem", $"name={gem}");

        Item vanilla = Item.New();
        vanilla.SetDefaults(ItemID.Minishark, default);
        Checks.Report("vanilla untouched", vanilla.type == ItemID.Minishark && vanilla.useTime > 0,
            $"type={vanilla.type} useTime={vanilla.useTime}");
        Log.Info("csitems END");
    }

    public static void Report(string name, bool ok, string detail) =>
        Log.Info($"csitems {name}: {(ok ? "ok" : "FAIL")} {detail}");
}
